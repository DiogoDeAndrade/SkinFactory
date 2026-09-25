using System.Collections.Generic;
using UC;
using UnityEngine;

// The skin on the Skinotron, as far as the day's pipeline has got:
//  - concept drawing: the drawing as voxels, the areas its lines enclose filled in (each opaque pixel a column of
//    voxels, the outside empty)
//  - model: the polygons extruded (see ModelExtruder), textured with the drawing over paper
//  - painting: the same model, with the painting as its texture
// ShowProgress picks the stage from what the player carries; LevelManager calls it while the camera is on the skin.
// The skin is built as a child of this transform, the canvas (the drawing / painting square) centered on it.
public class SkinDisplay : MonoBehaviour
{
    [Header("Size")]
    [SerializeField, Tooltip("World size of the whole canvas the concept is drawn on")]
    private Vector2                         canvasSize = new Vector2(0.576f, 0.576f);
    [SerializeField, Min(0.001f), Tooltip("Thickness of the skin, voxels and model alike")]
    private float                           depth = 0.4f;

    [Header("Voxels")]
    [SerializeField, Min(1)] private int    xyGridSize = 1;         // Source pixels per voxel in X/Y
    [SerializeField, Min(1)] private int    zGridSize = 1;          // Erosion slices; the depth is split in 2 * zGridSize - 1 voxels
    [SerializeField] private bool           cullHiddenFaces = true;
    [SerializeField, Tooltip("Color of the areas the drawing's lines enclose")]
    private Color                           sketchFillColor = Color.white;
    [SerializeField, Min(0), Tooltip("Gaps in the drawing's lines up to about twice this many pixels wide still close an area")]
    private float                           gapClosing = 2.0f;

    [Header("Model")]
    [SerializeField, Min(0), Tooltip("Extra thickness per side for a polygon over another one (insets), so their faces don't z-fight")]
    private float                           layerOffset = 0.005f;
    [SerializeField, Tooltip("Under the drawing's lines, and wherever the painting left nothing")]
    private Color                           paperColor = new Color(0.95f, 0.94f, 0.9f, 1.0f);
    [SerializeField, Min(0), Tooltip("Pixels the painting's colors grow into the empty outside, so the model's sides get the edge colors")]
    private int                             bleedPixels = 2;

    [Header("Placement")]
    [SerializeField, Tooltip("Local position of the skin relative to this transform")]
    private Vector3                         offset = Vector3.zero;
    [SerializeField, Tooltip("Initial local rotation of the skin (euler angles)")]
    private Vector3                         rotation = Vector3.zero;
    [SerializeField, Tooltip("Continuous spin around the local Y axis, in degrees per second (0 = none)")]
    private float                           spinSpeed = 30.0f;

    [Header("Appear")]
    [SerializeField, Min(0), Tooltip("Seconds a new shape takes to grow in")]
    private float                           growTime = 0.4f;
    [SerializeField, Min(1), Tooltip("Scale of the pop when only the texture changes")]
    private float                           popScale = 1.2f;
    [SerializeField, Min(0)] private float  popTime = 0.3f;

    [Header("Rendering")]
    [SerializeField, Tooltip("Material template; instanced with the skin's texture as base map. Falls back to URP Lit.")]
    private Material                        materialTemplate;

    GameObject  skinObject;     // Spins; shows whichever mesh is current
    MeshFilter  meshFilter;
    Material    material;
    Mesh        mesh;
    Texture2D   texture;        // Voxel palette, drawing over paper, or the painting
    Vector2[][] shownModel;     // Polygons the current mesh was extruded from (null while showing voxels)

    // The furthest stage the player has reached: the model (painted or not), else the drawing, else nothing
    public void ShowProgress(Player player)
    {
        Vector2[][] model   = (player != null) ? player.modelPolygons : null;
        Texture2D drawing   = (player != null) ? player.conceptDrawing : null;
        Texture2D painting  = (player != null) ? player.painting : null;

        if (model != null) ShowModel(model, (painting != null) ? PaintingTexture(painting) : SketchTexture(drawing));
        else if (painting != null) ShowVoxels(ReadPixels32(painting), painting.width, painting.height);    // A debug painting without its model
        else if (drawing != null) ShowVoxels(FillInterior(ReadPixels32(drawing), drawing.width, drawing.height), drawing.width, drawing.height);
        else Clear();
    }

    // The model already on show only changes texture, with a pop; a new model grows in
    void ShowModel(Vector2[][] model, Texture2D tex)
    {
        if ((skinObject != null) && (model == shownModel))
        {
            SetTexture(tex);
            Pop();
            return;
        }

        SetMesh(ModelExtruder.Build(model, canvasSize, depth, layerOffset), tex);
        shownModel = model;
        GrowIn();
    }

    // Opaque pixels become voxels, transparent ones stay empty
    void ShowVoxels(Color32[] source, int w, int h)
    {
        var pixels = new Color[source.Length];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = source[i];

        var voxelSize = new Vector3(canvasSize.x * xyGridSize / w,
                                    canvasSize.y * xyGridSize / h,
                                    depth / (2 * zGridSize - 1));
        VoxelData voxels = VoxelBuilder.BuildFromTexture(pixels, w, h, xyGridSize, zGridSize, voxelSize);
        if (voxels.Voxels.Count == 0)
        {
            Debug.LogWarning("SkinDisplay: nothing opaque to build voxels from", this);
            Clear();
            return;
        }

        var (uvMap, texW, texH) = VoxelMeshBuilder.BuildUVMap(voxels.Palette);
        Texture2D palette = VoxelMeshBuilder.BuildPaletteTexture(voxels.Palette, texW, texH, "Skin palette");
        Mesh voxelMesh = VoxelMeshBuilder.BuildMeshPalette(voxels, uvMap, cullHiddenFaces);
        voxelMesh.name = "Skin voxels";

        SetMesh(voxelMesh, palette);
        shownModel = null;
        GrowIn();
    }

    public void Clear()
    {
        if (skinObject != null)
        {
            skinObject.transform.Tween().Stop("SkinAppear", Tweener.StopBehaviour.Cancel);
            Destroy(skinObject);
        }
        if (mesh) Destroy(mesh);
        if (texture) Destroy(texture);
        if (material) Destroy(material);
        skinObject = null;
        meshFilter = null;
        mesh = null;
        texture = null;
        material = null;
        shownModel = null;
    }

    #region Skin object

    // Swaps the mesh and texture on the skin object, creating it the first time
    void SetMesh(Mesh newMesh, Texture2D tex)
    {
        if (skinObject == null)
        {
            skinObject = new GameObject("Skin");
            skinObject.transform.SetParent(transform, false);
            skinObject.transform.localPosition = offset;
            skinObject.transform.localRotation = Quaternion.Euler(rotation);
            meshFilter = skinObject.AddComponent<MeshFilter>();
            material = CreateMaterial();
            skinObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        if (mesh != null) Destroy(mesh);
        mesh = newMesh;
        meshFilter.sharedMesh = mesh;
        SetTexture(tex);
    }

    void SetTexture(Texture2D tex)
    {
        if ((texture != null) && (texture != tex)) Destroy(texture);
        texture = tex;

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", tex);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", tex);
    }

    void GrowIn()
    {
        Transform t = skinObject.transform;
        t.Tween().Stop("SkinAppear", Tweener.StopBehaviour.Cancel);
        if (growTime <= 0.0f)
        {
            t.localScale = Vector3.one;
            return;
        }
        t.localScale = Vector3.one * 0.01f;
        t.LocalScaleTo(Vector3.one, growTime, "SkinAppear");
    }

    void Pop()
    {
        Transform t = skinObject.transform;
        t.Tween().Stop("SkinAppear", Tweener.StopBehaviour.Cancel);
        t.localScale = Vector3.one;
        if (popTime <= 0.0f) return;

        t.LocalScaleTo(Vector3.one * popScale, popTime * 0.5f, "SkinAppear").Done(() =>
        {
            t.LocalScaleTo(Vector3.one, popTime * 0.5f, "SkinAppear");
        });
    }

    Material CreateMaterial()
    {
        Material mat;
        if (materialTemplate != null)
        {
            mat = new Material(materialTemplate);
        }
        else
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.0f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
        }
        mat.name = "Skin material";
        return mat;
    }

    #endregion

    #region Textures

    // The drawing with every transparent pixel its lines enclose in sketchFillColor. The outside is whatever the border
    // reaches without crossing a line; for that flood the lines are thickened by gapClosing, so a slightly open outline
    // still holds, and the outside then gets back the rim the thickening took from it.
    Color32[] FillInterior(Color32[] pixels, int w, int h)
    {
        int n = w * h;
        var line = new bool[n];
        for (int i = 0; i < n; i++) line[i] = pixels[i].a > 127;

        bool[] wall = line;
        if (gapClosing > 0.0f)
        {
            float[] toLine = DistanceField.Compute(line, w, h);
            wall = new bool[n];
            for (int i = 0; i < n; i++) wall[i] = toLine[i] <= gapClosing;
        }

        // Flood from the border
        var outside = new bool[n];
        var open = new Stack<int>();
        for (int x = 0; x < w; x++)
        {
            open.Push(x);
            open.Push((h - 1) * w + x);
        }
        for (int y = 0; y < h; y++)
        {
            open.Push(y * w);
            open.Push(y * w + w - 1);
        }
        while (open.Count > 0)
        {
            int i = open.Pop();
            if (outside[i] || wall[i]) continue;
            outside[i] = true;

            int x = i % w;
            int y = i / w;
            if (x > 0) open.Push(i - 1);
            if (x < w - 1) open.Push(i + 1);
            if (y > 0) open.Push(i - w);
            if (y < h - 1) open.Push(i + w);
        }

        if (gapClosing > 0.0f)
        {
            float[] toOutside = DistanceField.Compute(outside, w, h);
            for (int i = 0; i < n; i++)
            {
                if (!line[i] && (toOutside[i] <= gapClosing)) outside[i] = true;
            }
        }

        Color32 fill = sketchFillColor;
        fill.a = 255;
        var result = (Color32[])pixels.Clone();
        for (int i = 0; i < n; i++)
        {
            if (!line[i] && !outside[i]) result[i] = fill;
        }
        return result;
    }

    // The drawing's lines over paper (plain paper without a drawing)
    Texture2D SketchTexture(Texture2D drawing)
    {
        int w = (drawing != null) ? drawing.width : 1;
        int h = (drawing != null) ? drawing.height : 1;
        Color32[] lines = (drawing != null) ? ReadPixels32(drawing) : null;
        Color32 paper = paperColor;

        var pixels = new Color32[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 c = (lines != null) ? Color32.Lerp(paper, lines[i], lines[i].a / 255.0f) : paper;
            c.a = 255;
            pixels[i] = c;
        }
        return CreateTexture(w, h, pixels, "Skin sketch");
    }

    // The painting, its colors grown bleedPixels into the empty outside and paper beyond that, all opaque
    Texture2D PaintingTexture(Texture2D painting)
    {
        int w = painting.width;
        int h = painting.height;
        Color32[] pixels = ReadPixels32(painting);

        for (int pass = 0; pass < bleedPixels; pass++)
        {
            var source = (Color32[])pixels.Clone();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (source[i].a > 0) continue;

                    if ((x > 0) && (source[i - 1].a > 0)) pixels[i] = source[i - 1];
                    else if ((x < w - 1) && (source[i + 1].a > 0)) pixels[i] = source[i + 1];
                    else if ((y > 0) && (source[i - w].a > 0)) pixels[i] = source[i - w];
                    else if ((y < h - 1) && (source[i + w].a > 0)) pixels[i] = source[i + w];
                }
            }
        }

        Color32 paper = paperColor;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0) pixels[i] = paper;
            pixels[i].a = 255;
        }
        return CreateTexture(w, h, pixels, "Skin painting");
    }

    static Color32[] ReadPixels32(Texture2D tex)
    {
        if (tex.isReadable) return tex.GetPixels32();

        var copy = TextureUtils.ReadableCopy(tex);
        var pixels = copy.GetPixels32();
        Destroy(copy);
        return pixels;
    }

    static Texture2D CreateTexture(int w, int h, Color32[] pixels, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    #endregion

    void Update()
    {
        if ((skinObject == null) || (spinSpeed == 0.0f)) return;

        skinObject.transform.Rotate(0.0f, spinSpeed * Time.deltaTime, 0.0f, Space.Self);
    }

    void OnDestroy()
    {
        Clear();
    }
}
