using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Painting station: the player fills the closed regions of the model they are carrying with palette colors.
// Left click paints the region under the cursor with the selected color, right click clears it.
// The outside region (the one containing pixel 0,0) can't be painted.
public class PaintingMG : MinigameUI
{
    [Header("Palette")]
    [SerializeField] private PaletteSO          palette;

    [Header("UI references")]
    [SerializeField] private RawImage           paintImage;         // Shows the painted regions
    [SerializeField] private PolygonGraphic     outlineGraphic;     // Draws the model outlines on top (same rect as paintImage)
    [SerializeField] private RectTransform      swatchContainer;    // Parent for the palette swatches (give it a layout group)
    [SerializeField] private Button             swatchPrefab;       // Button with an Image; one instance per palette color
    [SerializeField] private RectTransform      selectionMarker;    // Optional, parented to the selected swatch
    [SerializeField] private TextMeshProUGUI    colorCountText;     // Optional, "colors used (min N)"
    [SerializeField] private TextMeshProUGUI    regionText;         // Optional, "painted / total regions"
    [SerializeField] private Button             submitButton;       // Optional, disabled until the requirements are met

    [Header("Painting")]
    [SerializeField, Min(8)] private int        paintResolution = 64;   // Pixels; also the resolution of the exported image
    [SerializeField] private Color              unpaintedColor = new Color(0.85f, 0.85f, 0.85f, 1.0f); // Display only, never exported
    [SerializeField] private bool               requireAllRegionsPainted = true;
    [SerializeField, Range(0.0f, 0.05f)]
    [Tooltip("Regions smaller than this fraction of the canvas are slivers (overlapping or nearly touching polygons) and get merged into the neighbour they touch most")]
    private float                               minRegionArea = 0.002f;

#if UNITY_EDITOR
    [Header("Debug (editor only)")]
    [SerializeField] private bool               savePaintingOnSubmit = false;             // Writes the painting as a PNG on submit
    [SerializeField] private string             savePaintingFolder = "Assets/Art/Debug";  // Project-relative folder
#endif

    // Model being painted (owned by the Player)
    Player          player;
    Vector2[][]     model;
    List<Vector2[]> modelList = new List<Vector2[]>();

    // Region map at paint resolution
    int             res;
    int[]           regionOf;       // Per pixel: region index
    int             regionCount;    // Including the outside
    int             outsideRegion;  // Region containing pixel (0,0), never paintable (-1 if none)
    int[]           regionPaint;    // Per region: palette index, -1 = unpainted

    // Display
    Texture2D       paintTexture;
    Color32[]       paintPixels;
    List<Button>    swatches = new List<Button>();
    int             selectedColor = -1;

    // State
    bool            paintingEnabled;
    bool            paintDone;
    Canvas          canvas;

    public int  selectedColorIndex => selectedColor;
    public int  interiorRegionCount => (regionOf == null) ? 0 : regionCount - ((outsideRegion >= 0) ? 1 : 0);
    public int  minColors => (player != null && player.conceptDrawingSource != null) ? player.conceptDrawingSource.MinColors : 0;

    public int paintedRegionCount
    {
        get
        {
            if (regionPaint == null) return 0;
            int n = 0;
            for (int r = 0; r < regionCount; r++)
            {
                if ((r != outsideRegion) && (regionPaint[r] >= 0)) n++;
            }
            return n;
        }
    }

    public int colorsUsed
    {
        get
        {
            if (regionPaint == null) return 0;
            var used = new HashSet<int>();
            for (int r = 0; r < regionCount; r++)
            {
                if ((r != outsideRegion) && (regionPaint[r] >= 0)) used.Add(regionPaint[r]);
            }
            return used.Count;
        }
    }

    // Baseline of 3 stars, one more for using more colors than the concept asks for, one more for painting more
    // regions than the concept's minimum. Returned in [0,1] (stars / 5) like the other station scores.
    public int stars
    {
        get
        {
            if ((regionPaint == null) || (interiorRegionCount == 0)) return 0;

            var concept = (player != null) ? player.conceptDrawingSource : null;
            int minRegions = (concept != null) ? concept.MinClosedRegions : 0;

            int result = 3;
            if (colorsUsed > minColors) result++;
            if (paintedRegionCount > minRegions) result++;
            return result;
        }
    }

    public float score => stars / 5.0f;

    public bool canSubmit
    {
        get
        {
            if (paintDone || (regionPaint == null) || (interiorRegionCount == 0)) return false;
            if (colorsUsed < minColors) return false;
            if (requireAllRegionsPainted && (paintedRegionCount < interiorRegionCount)) return false;
            return paintedRegionCount > 0;
        }
    }

    protected override void Start()
    {
        base.Start();

        canvas = GetComponentInParent<Canvas>();
        if (player == null) player = FindAnyObjectByType<Player>();

        BuildSwatches();
        UpdateUI();
    }

    public override void ResetStation()
    {
        base.ResetStation();
        paintDone = false;
        paintingEnabled = false;
        SetModel(null);
        UpdateUI();
    }

    // Needs a model to paint, and is done once a painting exists (submitted here, or a debug starting stage)
    public override bool CanUse(Player player)
    {
        if (paintDone) return false;
        if (player == null) return false;

        return (player.modelPolygons != null) && (player.painting == null);
    }

    public override void Activate()
    {
        base.Activate();

        if (player == null) player = FindAnyObjectByType<Player>();

        var polygons = (player != null) ? player.modelPolygons : null;
        if (polygons != model) SetModel(polygons);

        paintingEnabled = false;
        UpdateUI();

        if ((model != null) && !paintDone) ShowPrompt(() => paintingEnabled = true);
    }

    public override void Deactivate()
    {
        base.Deactivate();

        paintingEnabled = false;
    }

    public void Submit()
    {
        paintingEnabled = false;
        paintDone = true;

        if (player == null) player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            var painting = CreateExportTexture();
            player.SetPaintingScore(score);
            player.SetPainting(painting);
#if UNITY_EDITOR
            if (savePaintingOnSubmit) SavePaintingPNG(painting);
#endif
        }
        else
        {
            Debug.LogWarning("PaintingMG: no Player found to store the painting", this);
        }

        UpdateUI();
        canvasGroup.FadeOut(0.1f);
        if (LevelManager.instance != null) LevelManager.instance.ShowSkinProgress();
    }

#if UNITY_EDITOR
    // Debug helper: saves the painting so it can be assigned to Player.debugPainting and the earlier stations skipped
    void SavePaintingPNG(Texture2D painting)
    {
        string folder = DebugAssetUtils.NormalizeFolder(savePaintingFolder);
        var source = (player != null) ? player.conceptDrawingSource : null;
        string name = (source != null) ? source.name : "Concept";
        string path = $"{folder}/{name}_painting.png";

        DebugAssetUtils.SavePNG(painting, path);
        Debug.Log($"PaintingMG: painting saved to {path}", this);
    }
#endif

    public void ClearPainting()
    {
        if (regionPaint == null) return;

        for (int r = 0; r < regionCount; r++) regionPaint[r] = -1;
        RefreshTexture();
        UpdateUI();
    }

    public void SelectColor(int index)
    {
        if ((palette == null) || (index < 0) || (index >= palette.Count)) return;

        selectedColor = index;

        if (selectionMarker && (index < swatches.Count) && swatches[index])
        {
            selectionMarker.gameObject.SetActive(true);
            selectionMarker.SetParent(swatches[index].transform, false);
            selectionMarker.anchorMin = Vector2.zero;
            selectionMarker.anchorMax = Vector2.one;
            selectionMarker.offsetMin = Vector2.zero;
            selectionMarker.offsetMax = Vector2.zero;
            selectionMarker.SetAsLastSibling();
        }
    }

    void OnDestroy()
    {
        if (paintTexture) Destroy(paintTexture);
    }

    void Update()
    {
        if (!paintingEnabled || (paintImage == null) || (regionOf == null)) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        bool left = mouse.leftButton.wasPressedThisFrame;
        bool right = mouse.rightButton.wasPressedThisFrame;
        if (!left && !right) return;

        if (!TryGetNormalizedPoint(mouse.position.ReadValue(), out var n)) return;

        int x = Mathf.Clamp((int)(n.x * res), 0, res - 1);
        int y = Mathf.Clamp((int)(n.y * res), 0, res - 1);
        PaintRegionAt(x, y, left ? selectedColor : -1);
    }

    #region Model and regions

    void SetModel(Vector2[][] polygons)
    {
        model = polygons;
        modelList.Clear();
        regionOf = null;
        regionPaint = null;
        regionCount = 0;
        outsideRegion = -1;

        if (model != null)
        {
            foreach (var p in model) modelList.Add(p);
            BuildRegions();
        }

        if (outlineGraphic) outlineGraphic.SetPolygons(modelList, null);

        CreatePaintTexture();
        RefreshTexture();
    }

    // Rasterizes the model outlines as walls and labels the connected pockets between them.
    // Wall pixels are then handed to an adjacent interior region, so painting a region also colors its outline.
    void BuildRegions()
    {
        res = Mathf.Max(8, paintResolution);
        int n = res * res;

        var segments = new List<RegionCounter.Segment>();
        foreach (var poly in model) RegionCounter.AddPolygon(segments, poly);

        var wall = new bool[n];
        RegionCounter.Rasterize(segments, wall, res);

        regionOf = new int[n];
        for (int i = 0; i < n; i++) regionOf[i] = -1;

        regionCount = 0;
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            if (wall[i] || (regionOf[i] >= 0)) continue;

            int label = regionCount++;
            regionOf[i] = label;
            queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int j = queue.Dequeue();
                int x = j % res, y = j / res;
                if (x > 0) Visit(j - 1);
                if (x < res - 1) Visit(j + 1);
                if (y > 0) Visit(j - res);
                if (y < res - 1) Visit(j + res);
            }

            void Visit(int k)
            {
                if (wall[k] || (regionOf[k] >= 0)) return;
                regionOf[k] = label;
                queue.Enqueue(k);
            }
        }

        // The outside is whatever pixel (0,0) belongs to; if that's a wall, take the first labeled border pixel
        outsideRegion = regionOf[0];
        if (outsideRegion < 0)
        {
            for (int i = 0; i < n && outsideRegion < 0; i++)
            {
                int x = i % res, y = i / res;
                bool border = (x == 0) || (y == 0) || (x == res - 1) || (y == res - 1);
                if (border && (regionOf[i] >= 0)) outsideRegion = regionOf[i];
            }
        }

        // Walls join a neighbouring interior region; a few passes handle thicker walls where lines overlap
        for (int pass = 0; pass < 4; pass++)
        {
            bool last = (pass == 3);
            bool changed = false;
            for (int i = 0; i < n; i++)
            {
                if (!wall[i] || (regionOf[i] >= 0)) continue;

                int x = i % res, y = i / res;
                int interior = -1, any = -1;
                Consider(x > 0 ? i - 1 : -1);
                Consider(x < res - 1 ? i + 1 : -1);
                Consider(y > 0 ? i - res : -1);
                Consider(y < res - 1 ? i + res : -1);

                void Consider(int k)
                {
                    if (k < 0) return;
                    int r = regionOf[k];
                    if (r < 0) return;
                    if (r != outsideRegion) { if (interior < 0) interior = r; }
                    else any = r;
                }

                if (interior >= 0) { regionOf[i] = interior; changed = true; }
                else if (last && (any >= 0)) { regionOf[i] = any; changed = true; }
            }
            if (!changed && !last) break;
        }

        // Anything still unassigned (isolated wall clumps) goes to the outside
        for (int i = 0; i < n; i++)
        {
            if (regionOf[i] < 0) regionOf[i] = outsideRegion;
        }

        MergeSlivers(n);

        regionPaint = new int[regionCount];
        for (int r = 0; r < regionCount; r++) regionPaint[r] = -1;
    }

    // Merges regions smaller than minRegionArea into the neighbouring region they share the longest border with
    // (interior neighbours preferred over the outside), then compacts the region indices.
    void MergeSlivers(int n)
    {
        int minArea = Mathf.RoundToInt(n * minRegionArea);
        if (minArea <= 1) return;

        var area = new int[regionCount];
        for (int i = 0; i < n; i++) area[regionOf[i]]++;

        var contact = new Dictionary<int, int>();
        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int r = 0; r < regionCount; r++)
            {
                if ((r == outsideRegion) || (area[r] == 0) || (area[r] >= minArea)) continue;

                // Who does this sliver touch, and how much
                contact.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (regionOf[i] != r) continue;
                    int x = i % res, y = i / res;
                    if (x > 0) Touch(regionOf[i - 1]);
                    if (x < res - 1) Touch(regionOf[i + 1]);
                    if (y > 0) Touch(regionOf[i - res]);
                    if (y < res - 1) Touch(regionOf[i + res]);
                }

                void Touch(int other)
                {
                    if (other == r) return;
                    contact[other] = contact.TryGetValue(other, out int c) ? c + 1 : 1;
                }

                int best = -1, bestContact = -1;
                foreach (var kv in contact)
                {
                    if (kv.Key == outsideRegion) continue;
                    if (kv.Value > bestContact) { best = kv.Key; bestContact = kv.Value; }
                }
                if ((best < 0) && contact.ContainsKey(outsideRegion)) best = outsideRegion;
                if (best < 0) continue;

                for (int i = 0; i < n; i++) if (regionOf[i] == r) regionOf[i] = best;
                area[best] += area[r];
                area[r] = 0;
                merged = true;
            }
        }

        // Compact the indices so regionCount only counts surviving regions
        var remap = new int[regionCount];
        int next = 0;
        for (int r = 0; r < regionCount; r++) remap[r] = (area[r] > 0) ? next++ : -1;
        for (int i = 0; i < n; i++) regionOf[i] = remap[regionOf[i]];
        outsideRegion = (outsideRegion >= 0) ? remap[outsideRegion] : -1;
        regionCount = next;
    }

    void PaintRegionAt(int x, int y, int colorIndex)
    {
        int r = regionOf[y * res + x];
        if ((r < 0) || (r == outsideRegion)) return;
        if ((colorIndex >= 0) && ((palette == null) || (colorIndex >= palette.Count))) return;
        if (regionPaint[r] == colorIndex) return;

        regionPaint[r] = colorIndex;
        RefreshTexture();
        UpdateUI();
    }

    #endregion

    #region Textures

    void CreatePaintTexture()
    {
        if (paintTexture) Destroy(paintTexture);

        int size = (regionOf != null) ? res : Mathf.Max(8, paintResolution);
        paintTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "PaintingMG Painting"
        };
        paintPixels = new Color32[size * size];

        if (paintImage) paintImage.texture = paintTexture;
    }

    // Display: outside transparent, unpainted regions in unpaintedColor, painted regions in their palette color
    void RefreshTexture()
    {
        if (paintTexture == null) return;

        FillPixels(paintPixels, unpaintedColor);
        paintTexture.SetPixels32(paintPixels);
        paintTexture.Apply();
    }

    // Export: outside and unpainted regions transparent, painted regions in their palette color
    Texture2D CreateExportTexture()
    {
        int size = (regionOf != null) ? res : Mathf.Max(8, paintResolution);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = (player != null && player.conceptDrawingSource != null) ? $"Painting_{player.conceptDrawingSource.name}" : "Painting"
        };
        var pixels = new Color32[size * size];
        FillPixels(pixels, new Color(0, 0, 0, 0));
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    void FillPixels(Color32[] pixels, Color unpainted)
    {
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 unpainted32 = unpainted;

        if (regionOf == null)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            return;
        }

        // Palette colors as Color32, once
        var colors = new Color32[(palette != null) ? palette.Count : 0];
        for (int c = 0; c < colors.Length; c++) colors[c] = palette.GetColor(c);

        for (int i = 0; i < pixels.Length; i++)
        {
            int r = regionOf[i];
            if ((r < 0) || (r == outsideRegion)) { pixels[i] = clear; continue; }

            int c = regionPaint[r];
            pixels[i] = ((c >= 0) && (c < colors.Length)) ? colors[c] : unpainted32;
        }
    }

    #endregion

    #region UI

    void BuildSwatches()
    {
        foreach (var s in swatches) if (s) Destroy(s.gameObject);
        swatches.Clear();

        if ((palette == null) || (swatchPrefab == null) || (swatchContainer == null)) return;

        for (int i = 0; i < palette.Count; i++)
        {
            int index = i;
            var entry = palette[i];

            var swatch = Instantiate(swatchPrefab, swatchContainer);
            swatch.gameObject.SetActive(true);
            swatch.name = $"Swatch_{entry.name}";

            var image = swatch.targetGraphic as Image;
            if (image == null) image = swatch.GetComponent<Image>();
            if (image) image.color = entry.color;

            swatch.onClick.AddListener(() => SelectColor(index));
            swatches.Add(swatch);
        }

        if (swatches.Count > 0) SelectColor(0);
        else if (selectionMarker) selectionMarker.gameObject.SetActive(false);
    }

    bool TryGetNormalizedPoint(Vector2 screenPos, out Vector2 normalized)
    {
        normalized = default;

        var rt = paintImage.rectTransform;
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out var local)) return false;

        var rect = rt.rect;
        normalized = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        return (normalized.x >= 0.0f) && (normalized.x <= 1.0f) && (normalized.y >= 0.0f) && (normalized.y <= 1.0f);
    }

    void UpdateUI()
    {
        if (colorCountText)
        {
            int min = minColors;
            colorCountText.text = (min > 0) ? $"Colors: {colorsUsed} (min {min})" : $"Colors: {colorsUsed}";
        }
        if (regionText)
        {
            regionText.text = $"Painted: {paintedRegionCount} / {interiorRegionCount}";
        }
        if (submitButton) submitButton.interactable = canSubmit;
    }

    #endregion
}
