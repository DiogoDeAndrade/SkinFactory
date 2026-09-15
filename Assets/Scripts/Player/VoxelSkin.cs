using UC;
using UnityEngine;

// Builds the voxel-extruded "skin" from a painting texture as a child mesh of this transform.
// Transparent pixels are empty; each opaque pixel becomes a column of voxels, eroded towards the front
// and back so the silhouette is thickest in the middle.
public class VoxelSkin : MonoBehaviour
{
    [Header("Extrusion")]
    [SerializeField, Min(1)] private int    xyGridSize = 1;         // Source pixels per voxel in X/Y
    [SerializeField, Min(1)] private int    zGridSize = 3;          // Erosion slices; the mesh is 2 * zGridSize - 1 voxels deep
    [SerializeField] private Vector3        voxelSize = new Vector3(0.03f, 0.03f, 0.03f);
    [SerializeField] private bool           cullHiddenFaces = true;

    [Header("Placement")]
    [SerializeField, Tooltip("Local position of the skin relative to this transform")]
    private Vector3                         offset = Vector3.zero;
    [SerializeField, Tooltip("Initial local rotation of the skin (euler angles)")]
    private Vector3                         rotation = Vector3.zero;
    [SerializeField, Tooltip("Continuous spin around the local Y axis, in degrees per second (0 = none)")]
    private float                           spinSpeed = 30.0f;

    [Header("Rendering")]
    [SerializeField, Tooltip("Material template; instanced with the palette texture as base map. Falls back to URP Lit.")]
    private Material                        materialTemplate;

    GameObject  skinObject;
    Mesh        mesh;
    Texture2D   paletteTexture;
    Material    material;

    public bool         hasSkin => skinObject != null;
    public GameObject   skinGameObject => skinObject;
    public Mesh         skinMesh => mesh;

    // Rebuilds the skin from the painting; a null painting just clears it
    public void Build(Texture2D painting)
    {
        Clear();
        if (painting == null) return;

        var pixels = painting.isReadable ? painting.GetPixels() : TextureUtils.ReadableCopy(painting).GetPixels();

        VoxelData voxels = VoxelBuilder.BuildFromTexture(pixels, painting.width, painting.height, xyGridSize, zGridSize, voxelSize);
        if (voxels.Voxels.Count == 0)
        {
            Debug.LogWarning("VoxelSkin: painting has no opaque pixels, nothing to extrude", this);
            return;
        }

        var (uvMap, texW, texH) = VoxelMeshBuilder.BuildUVMap(voxels.Palette);
        paletteTexture = VoxelMeshBuilder.BuildPaletteTexture(voxels.Palette, texW, texH, "Skin palette");
        mesh = VoxelMeshBuilder.BuildMeshPalette(voxels, uvMap, cullHiddenFaces);
        mesh.name = "Skin";

        material = CreateMaterial(paletteTexture);

        skinObject = new GameObject("Skin");
        skinObject.transform.SetParent(transform, false);
        skinObject.transform.localPosition = offset;
        skinObject.transform.localRotation = Quaternion.Euler(rotation);
        skinObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = skinObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    public void Clear()
    {
        if (skinObject) Destroy(skinObject);
        if (mesh) Destroy(mesh);
        if (paletteTexture) Destroy(paletteTexture);
        if (material) Destroy(material);
        skinObject = null;
        mesh = null;
        paletteTexture = null;
        material = null;
    }

    Material CreateMaterial(Texture2D palette)
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
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", palette);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", palette);
        return mat;
    }

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
