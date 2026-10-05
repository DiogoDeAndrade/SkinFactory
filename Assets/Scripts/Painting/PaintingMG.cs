using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Painting station: the player paints the model they are carrying with a brush, against the clock.
// Left button paints with the selected color and brush size (round brushes), right button erases.
// The closed regions of the model are the areas being judged. An area takes the color most of it is painted with,
// as long as that covers at least areaThreshold of it; otherwise it is unpainted. Each area is worth P / N, P being
// the fraction covered by its color and N the number of areas. Going over the lines costs the area that was being
// painted: paint of its color found just over its edge comes off its P, measured against the area's own size, so
// a careless blob over a small area is worth nothing while the same overshoot barely dents a big one.
// Paint outside the model (the region containing pixel 0,0) is not an area: it shows on the canvas, never reaches
// the skin, and counts against the whole score.
// The time limit is built from the model (a base, so much per area) and only runs while the player is painting.
// When it runs out, whatever is on the canvas is submitted.
public class PaintingMG : MinigameUI
{
    [Header("Palette")]
    [SerializeField] private PaletteSO          palette;

    [Header("UI references")]
    [SerializeField] private RawImage           paintImage;         // Shows the painting
    [SerializeField] private PolygonGraphic     outlineGraphic;     // Draws the model outlines on top (same rect as paintImage)
    [SerializeField] private RectTransform      swatchContainer;    // Parent for the palette swatches (give it a layout group)
    [SerializeField] private Button             swatchPrefab;       // Button with an Image; one instance per palette color
    [SerializeField] private RectTransform      selectionMarker;    // Optional, parented to the selected swatch
    [SerializeField, Tooltip("One button per brush size, in the same order as the brush sizes")]
    private Button[]                            brushButtons;
    [SerializeField] private RectTransform      brushSelectionMarker;   // Optional, parented to the selected brush button
    [SerializeField, Tooltip("Optional: round graphic stretched over the pixels the brush would paint, tinted with the selected color")]
    private RectTransform                       brushCursor;
    [SerializeField, Tooltip("Optional: filled image showing the time left")]
    private Image                               timeFill;
    [SerializeField, Tooltip("Optional: the stars earned so far on a star row (a StarMeter instance)")]
    private LaunchRow                           starRow;
    [SerializeField] private TextMeshProUGUI    colorCountText;     // Optional, "colors used (min N)"
    [SerializeField] private TextMeshProUGUI    regionText;         // Optional, "areas with a color / total areas"
    [SerializeField] private Button             submitButton;       // Optional, interactable while painting

    [Header("Painting")]
    [SerializeField, Min(8)] private int        paintResolution = 256;  // Pixels; also the resolution of the exported image
    [SerializeField] private Color              unpaintedColor = new Color(0.85f, 0.85f, 0.85f, 1.0f); // Display only, never exported
    [SerializeField, Tooltip("Brushes are round, this many paint pixels across")]
    private int[]                               brushSizes = { 3, 7, 15 };
    [SerializeField, Min(0), Tooltip("Index of the brush selected at the start")]
    private int                                 startBrush = 1;
    [SerializeField, Range(0.0f, 0.05f)]
    [Tooltip("Regions smaller than this fraction of the canvas are slivers (overlapping or nearly touching polygons) and get merged into the neighbour they touch most")]
    private float                               minRegionArea = 0.002f;

    [Header("Time limit")]
    [SerializeField, Min(0), Tooltip("Seconds given regardless of the model")]
    private float                               baseTime = 15.0f;
    [SerializeField, Min(0), Tooltip("Seconds per area of the model")]
    private float                               timePerArea = 5.0f;

    [Header("Scoring")]
    [SerializeField, Range(0, 1), Tooltip("An area takes a color once this much of it is painted with that color; below it the area is unpainted and worth nothing")]
    private float                               areaThreshold = 0.7f;
    [SerializeField, Min(0), Tooltip("Paint of an area's color found over its edge (outside the model, or in a neighbour that took another color) comes off that area's worth: the spilled pixels over the area's own pixels, times this (0 = no penalty)")]
    private float                               spillPenalty = 2.0f;
    [SerializeField, Min(0), Tooltip("Paint pixels: how far over an area's edge paint of its color still counts as spilled from it")]
    private float                               spillRange = 8.0f;
    [SerializeField, Min(0), Tooltip("Paint pixels: spill this close to the edge is forgiven, since the outline itself belongs to one side or the other")]
    private float                               spillTolerance = 1.5f;
    [SerializeField, Tooltip("Using fewer colors than the concept's minimum scales the score by colors used / minimum")]
    private bool                                scaleScoreByMinColors = true;
    [SerializeField, Min(0), Tooltip("Score lost when the paint outside the model covers as many pixels as the model itself; less paint loses proportionally less (0 = no penalty)")]
    private float                               outsidePenalty = 1.0f;
    [SerializeField, Tooltip("The painting handed to the player has every area filled with the color it took (unpainted areas left empty); off = the brush strokes as painted")]
    private bool                                exportAreaColors = true;

    [Header("Station sounds")]
    [SerializeField] private SoundDef           selectSound;        // A color or a brush picked
    [SerializeField] private SoundDef           areaClaimedSound;   // An area passes the threshold and takes its color
    [SerializeField, Tooltip("Ticks once a second through the last seconds of the time limit")]
    private SoundDef                            clockWarningSound;
    [SerializeField, Min(0), Tooltip("Seconds left when the clock warning starts")]
    private float                               clockWarningTime = 5.0f;
    [SerializeField, Tooltip("The time ran out (instead of the submit sound)")]
    private SoundDef                            timeUpSound;

#if UNITY_EDITOR
    [Header("Debug (editor only)")]
    [SerializeField] private bool               savePaintingOnSubmit = false;             // Writes the painting as a PNG on submit
    [SerializeField] private string             savePaintingFolder = "Assets/Art/Debug";  // Project-relative folder
#endif

    const int noPaint = -1;     // Unpainted pixel / area; also what the eraser paints
    const int noStroke = -2;    // Brush lifted

    static readonly Color32 clear32 = new Color32(0, 0, 0, 0);

    // Model being painted (owned by the Player)
    Player          player;
    Vector2[][]     model;
    List<Vector2[]> modelList = new List<Vector2[]>();

    // Region map at paint resolution
    int             res;
    int[]           regionOf;       // Per pixel: region index
    int             regionCount;    // Including the outside
    int             outsideRegion;  // Region containing pixel (0,0), not an area (-1 if none)
    int[]           regionArea;     // Per region: pixels
    int             interiorArea;   // Pixels of all the areas together
    int[][]         spillBand;      // Per region: the pixels just over its edge, where its color counts as spilled

    // Painting
    int             paletteSize;
    Color32[]       paletteColors;
    Color32         unpainted32;
    int[]           paintOf;        // Per pixel: palette index, or noPaint
    int[]           paintCount;     // Per region and color (region * paletteSize + color): pixels painted with it
    int             outsidePainted; // Painted pixels outside the model
    int[]           regionColor;    // Per region: palette index of the color it took, or noPaint
    bool[]          colorSeen;
    float           lastScore;
    int             paintedRegions;
    int             usedColors;

    // Display
    Texture2D       paintTexture;
    Color32[]       paintPixels;
    bool            paintDirty;
    List<Button>    swatches = new List<Button>();
    int             selectedColor = -1;
    int             selectedBrush;
    Graphic         brushCursorGraphic;
    float           brushCursorAlpha = 1.0f;

    // State
    bool            paintingEnabled;
    bool            paintDone;
    int             strokeColor = noStroke; // What the stroke in progress paints
    Vector2Int      lastPixel;
    Canvas          canvas;

    public int      selectedColorIndex => selectedColor;
    public int      selectedBrushIndex => selectedBrush;
    public int      interiorRegionCount => (regionOf == null) ? 0 : regionCount - ((outsideRegion >= 0) ? 1 : 0);
    public int      minColors => (player != null && player.conceptDrawingSource != null) ? player.conceptDrawingSource.MinColors : 0;
    public int      paintedRegionCount => paintedRegions;   // Areas that took a color
    public int      colorsUsed => usedColors;               // Distinct colors over those areas
    public float    score => lastScore;                     // In [0,1], like the other station scores
    public int      stars => LaunchResults.StarsFor(lastScore);
    public float    timeLeft { get; private set; }
    public float    timeLimit { get; private set; }
    public bool     isDone => paintDone;

    int brushSize => ((brushSizes != null) && (selectedBrush < brushSizes.Length)) ? Mathf.Max(1, brushSizes[selectedBrush]) : 1;

    protected override void Start()
    {
        base.Start();

        canvas = GetComponentInParent<Canvas>();
        if (player == null) player = FindAnyObjectByType<Player>();

        SetupBrushCursor();
        BuildSwatches();
        SetupBrushes();
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

        if ((model != null) && !paintDone) ShowPrompt(StartPainting);
    }

    // Walking away keeps the painting and the time left
    public override void Deactivate()
    {
        base.Deactivate();

        StopPainting();
        UpdateUI();
    }

    void StartPainting()
    {
        paintingEnabled = true;
        UpdateUI();
    }

    void StopPainting()
    {
        paintingEnabled = false;
        strokeColor = noStroke;
        ShowBrushCursor(false);
    }

    public void Submit()
    {
        Finish(false);
    }

    // Hands the painting in, whether the player submitted or the time ran out
    void Finish(bool timedOut)
    {
        if (paintDone) return;

        StopPainting();
        paintDone = true;
        Evaluate();

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
        if (timedOut) timeUpSound?.Play();
        else submitSound?.Play();
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
        if (paintOf == null) return;

        for (int i = 0; i < paintOf.Length; i++) paintOf[i] = noPaint;
        System.Array.Clear(paintCount, 0, paintCount.Length);
        outsidePainted = 0;
        strokeColor = noStroke;

        RefreshTexture();
        Evaluate();
        UpdateUI();
    }

    public void SelectColor(int index)
    {
        if ((palette == null) || (index < 0) || (index >= palette.Count)) return;

        selectedColor = index;

        if (selectionMarker && (index < swatches.Count) && swatches[index]) MoveMarker(selectionMarker, swatches[index].transform);

        if (brushCursorGraphic)
        {
            Color c = palette.GetColor(index);
            c.a = brushCursorAlpha;
            brushCursorGraphic.color = c;
        }
    }

    public void SelectBrush(int index)
    {
        if ((brushSizes == null) || (brushSizes.Length == 0)) return;

        selectedBrush = Mathf.Clamp(index, 0, brushSizes.Length - 1);

        if (brushSelectionMarker && (brushButtons != null) && (selectedBrush < brushButtons.Length) && brushButtons[selectedBrush])
        {
            MoveMarker(brushSelectionMarker, brushButtons[selectedBrush].transform);
        }
    }

    void OnDestroy()
    {
        if (paintTexture) Destroy(paintTexture);
    }

    void Update()
    {
        if (!paintingEnabled || paintDone || (paintImage == null) || (regionOf == null)) return;

        UpdateBrush();

        if (paintDirty)
        {
            paintTexture.SetPixels32(paintPixels);
            paintTexture.Apply();
            paintDirty = false;

            // The UI shows whole stars, areas and colors, so most strokes don't change it
            int prevStars = stars, prevRegions = paintedRegions, prevColors = usedColors;
            Evaluate();
            if ((stars != prevStars) || (paintedRegions != prevRegions) || (usedColors != prevColors)) UpdateUI();
            if (paintedRegions > prevRegions) areaClaimedSound?.Play();
        }

        // The clock only runs while painting; out of time, whatever is on the canvas is submitted.
        // No time at all (base and per area both zero) means no limit.
        if (timeLimit <= 0.0f) return;

        float before = timeLeft;
        timeLeft = Mathf.Max(0.0f, timeLeft - Time.deltaTime);
        GameSounds.ClockWarning(clockWarningSound, before, timeLeft, clockWarningTime);
        UpdateTimeFill();
        if (timeLeft <= 0.0f) Finish(true);
    }

    #region Model and regions

    void SetModel(Vector2[][] polygons)
    {
        model = polygons;
        modelList.Clear();
        regionOf = null;
        regionArea = null;
        regionColor = null;
        spillBand = null;
        paintOf = null;
        paintCount = null;
        regionCount = 0;
        outsideRegion = -1;
        interiorArea = 0;
        outsidePainted = 0;
        strokeColor = noStroke;

        if (model != null)
        {
            foreach (var p in model) modelList.Add(p);
            BuildRegions();
            CreatePaintData();
        }

        if (outlineGraphic) outlineGraphic.SetPolygons(modelList, null);

        timeLimit = (model != null) ? baseTime + timePerArea * interiorRegionCount : 0.0f;
        timeLeft = timeLimit;

        CreatePaintTexture();
        RefreshTexture();
        Evaluate();
    }

    // Rasterizes the model outlines as walls and labels the connected pockets between them.
    // Wall pixels are then handed to an adjacent interior region, so an area includes its outline.
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

    #endregion

    #region Painting

    // An empty canvas for the current regions
    void CreatePaintData()
    {
        int n = res * res;

        paletteSize = (palette != null) ? palette.Count : 0;
        paletteColors = new Color32[paletteSize];
        for (int c = 0; c < paletteSize; c++) paletteColors[c] = palette.GetColor(c);
        unpainted32 = unpaintedColor;

        regionArea = new int[regionCount];
        interiorArea = 0;
        for (int i = 0; i < n; i++)
        {
            int r = regionOf[i];
            if (r < 0) continue;

            regionArea[r]++;
            if (r != outsideRegion) interiorArea++;
        }

        regionColor = new int[regionCount];
        for (int r = 0; r < regionCount; r++) regionColor[r] = noPaint;

        paintOf = new int[n];
        for (int i = 0; i < n; i++) paintOf[i] = noPaint;

        paintCount = new int[regionCount * paletteSize];
        colorSeen = new bool[paletteSize];

        BuildSpillBands();
    }

    // Per area, the pixels over its edge further than spillTolerance and up to spillRange: paint of the area's
    // color found there has spilled from it. Each distance field only spans the area's bounds plus the range.
    void BuildSpillBands()
    {
        spillBand = new int[regionCount][];

        var min = new Vector2Int[regionCount];
        var max = new Vector2Int[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            min[r] = new Vector2Int(res, res);
            max[r] = new Vector2Int(-1, -1);
        }
        for (int i = 0; i < regionOf.Length; i++)
        {
            int r = regionOf[i];
            if (r < 0) continue;

            var p = new Vector2Int(i % res, i / res);
            min[r] = Vector2Int.Min(min[r], p);
            max[r] = Vector2Int.Max(max[r], p);
        }

        int margin = Mathf.CeilToInt(spillRange);
        var band = new List<int>();
        for (int r = 0; r < regionCount; r++)
        {
            band.Clear();
            if ((r != outsideRegion) && (regionArea[r] > 0) && (spillRange > spillTolerance))
            {
                int x0 = Mathf.Max(0, min[r].x - margin), x1 = Mathf.Min(res - 1, max[r].x + margin);
                int y0 = Mathf.Max(0, min[r].y - margin), y1 = Mathf.Min(res - 1, max[r].y + margin);
                int w = x1 - x0 + 1, h = y1 - y0 + 1;

                var inside = new bool[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++) inside[y * w + x] = (regionOf[(y0 + y) * res + x0 + x] == r);
                }

                float[] distance = DistanceField.Compute(inside, w, h);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float d = distance[y * w + x];
                        if ((d > spillTolerance) && (d <= spillRange)) band.Add((y0 + y) * res + x0 + x);
                    }
                }
            }
            spillBand[r] = band.ToArray();
        }
    }

    // Left button paints, right button erases; a held button draws a line from where the pointer was last frame.
    // Leaving the canvas lifts the brush.
    void UpdateBrush()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool inside = TryGetPaintPixel(mouse.position.ReadValue(), out var pixel);
        ShowBrushCursor(inside, pixel);

        int color = noStroke;
        if (inside)
        {
            if (mouse.leftButton.isPressed) color = (selectedColor >= 0) ? selectedColor : noStroke;
            else if (mouse.rightButton.isPressed) color = noPaint;
        }

        if (color == noStroke)
        {
            strokeColor = noStroke;
            return;
        }

        if (strokeColor != color) Stamp(pixel, color);
        else if (pixel != lastPixel) StampLine(lastPixel, pixel, color);

        strokeColor = color;
        lastPixel = pixel;
    }

    void StampLine(Vector2Int from, Vector2Int to, int color)
    {
        int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            Stamp(new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                                 Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t))), color);
        }
    }

    // The brush disc centered on a pixel, clipped to the canvas. The radius is a touch under half the size, so
    // the small brushes come out round too (3 across is a plus, not a square).
    void Stamp(Vector2Int center, int color)
    {
        int size = brushSize;
        int x0 = center.x - size / 2;
        int y0 = center.y - size / 2;
        float mid = (size - 1) * 0.5f;      // Disc center, from the corner pixel of its box
        float radius = size * 0.5f - 0.25f;
        float radius2 = radius * radius;

        for (int by = 0; by < size; by++)
        {
            int y = y0 + by;
            if ((y < 0) || (y >= res)) continue;

            float dy = by - mid;
            for (int bx = 0; bx < size; bx++)
            {
                int x = x0 + bx;
                if ((x < 0) || (x >= res)) continue;

                float dx = bx - mid;
                if (dx * dx + dy * dy <= radius2) SetPaint(y * res + x, color);
            }
        }
    }

    // One pixel, keeping the counts the score is built from up to date: per area and color inside the model,
    // just how many outside it
    void SetPaint(int i, int color)
    {
        int r = regionOf[i];
        if (r < 0) return;

        int old = paintOf[i];
        if (old == color) return;

        bool outside = (r == outsideRegion);
        if (outside)
        {
            if (old < 0) outsidePainted++;
            else if (color < 0) outsidePainted--;
        }
        else
        {
            if (old >= 0) paintCount[r * paletteSize + old]--;
            if (color >= 0) paintCount[r * paletteSize + color]++;
        }
        paintOf[i] = color;

        paintPixels[i] = (color >= 0) ? paletteColors[color] : (outside ? clear32 : unpainted32);
        paintDirty = true;
    }

    bool TryGetPaintPixel(Vector2 screenPos, out Vector2Int pixel)
    {
        pixel = default;

        var rt = paintImage.rectTransform;
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out var local)) return false;

        var rect = rt.rect;
        float nx = (local.x - rect.xMin) / rect.width;
        float ny = (local.y - rect.yMin) / rect.height;
        if ((nx < 0.0f) || (nx > 1.0f) || (ny < 0.0f) || (ny > 1.0f)) return false;

        pixel = new Vector2Int(Mathf.Clamp((int)(nx * res), 0, res - 1),
                               Mathf.Clamp((int)(ny * res), 0, res - 1));
        return true;
    }

    #endregion

    #region Scoring

    // Each area takes the color most of it is painted with, as long as that covers at least areaThreshold of the
    // area; below the threshold it is unpainted and worth nothing. An area with a color is worth the fraction of
    // it that color covers, minus what it spilled over its edge. The score is the average over the areas, scaled
    // down when fewer colors than the concept asks for were used, minus the paint outside the model measured
    // against the size of the model.
    void Evaluate()
    {
        lastScore = 0.0f;
        paintedRegions = 0;
        usedColors = 0;
        if (regionColor == null) return;

        System.Array.Clear(colorSeen, 0, colorSeen.Length);

        // The color every area took (the spill of one depends on the colors of its neighbours)
        for (int r = 0; r < regionCount; r++)
        {
            regionColor[r] = noPaint;
            if ((r == outsideRegion) || (regionArea[r] == 0)) continue;

            int best = noPaint, bestCount = 0;
            for (int c = 0; c < paletteSize; c++)
            {
                int count = paintCount[r * paletteSize + c];
                if (count > bestCount) { best = c; bestCount = count; }
            }

            if ((best < 0) || ((float)bestCount / regionArea[r] < areaThreshold)) continue;

            regionColor[r] = best;
            paintedRegions++;
            if (!colorSeen[best])
            {
                colorSeen[best] = true;
                usedColors++;
            }
        }

        float sum = 0.0f;
        for (int r = 0; r < regionCount; r++)
        {
            int color = regionColor[r];
            if (color < 0) continue;

            float covered = (float)paintCount[r * paletteSize + color] / regionArea[r];
            float spilled = (spillPenalty > 0.0f) ? spillPenalty * CountSpill(r) / regionArea[r] : 0.0f;
            sum += Mathf.Max(0.0f, covered - spilled);
        }

        int areas = interiorRegionCount;
        if (areas > 0) lastScore = sum / areas;

        // A model with fewer areas than the concept's minimum colors can't be asked for more colors than areas
        int neededColors = Mathf.Min(minColors, areas);
        if (scaleScoreByMinColors && (usedColors < neededColors)) lastScore *= (float)usedColors / neededColors;

        if (interiorArea > 0) lastScore = Mathf.Max(0.0f, lastScore - outsidePenalty * outsidePainted / interiorArea);
    }

    // Pixels of an area's color over its edge that don't belong there: outside the model, or in a neighbour that
    // took another color (or none). A neighbour of the same color may have painted them itself, so those don't count.
    int CountSpill(int r)
    {
        int color = regionColor[r];
        int spill = 0;
        foreach (int i in spillBand[r])
        {
            if (paintOf[i] != color) continue;

            int other = regionOf[i];
            if ((other < 0) || (other == outsideRegion) || (regionColor[other] != color)) spill++;
        }
        return spill;
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

    // Display: painted pixels in their palette color, wherever they are; unpainted ones in unpaintedColor inside
    // the model and transparent outside it
    void RefreshTexture()
    {
        if (paintTexture == null) return;

        for (int i = 0; i < paintPixels.Length; i++)
        {
            int r = (regionOf != null) ? regionOf[i] : -1;
            if (r < 0) paintPixels[i] = clear32;
            else if (paintOf[i] >= 0) paintPixels[i] = paletteColors[paintOf[i]];
            else paintPixels[i] = (r == outsideRegion) ? clear32 : unpainted32;
        }

        paintTexture.SetPixels32(paintPixels);
        paintTexture.Apply();
        paintDirty = false;
    }

    // Export: outside (painted or not) and unpainted transparent; either every area filled with the color it took,
    // or the strokes as painted
    Texture2D CreateExportTexture()
    {
        int size = (regionOf != null) ? res : Mathf.Max(8, paintResolution);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = (player != null && player.conceptDrawingSource != null) ? $"Painting_{player.conceptDrawingSource.name}" : "Painting"
        };

        var pixels = new Color32[size * size];  // All transparent
        if (regionOf != null)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                int r = regionOf[i];
                if ((r < 0) || (r == outsideRegion)) continue;

                int c = exportAreaColors ? regionColor[r] : paintOf[i];
                if (c >= 0) pixels[i] = paletteColors[c];
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
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

            swatch.onClick.AddListener(() => { SelectColor(index); selectSound?.Play(); });
            swatches.Add(swatch);
        }

        if (swatches.Count > 0) SelectColor(0);
        else if (selectionMarker) selectionMarker.gameObject.SetActive(false);
    }

    void SetupBrushes()
    {
        if (brushButtons != null)
        {
            for (int i = 0; i < brushButtons.Length; i++)
            {
                int index = i;
                if (brushButtons[i]) brushButtons[i].onClick.AddListener(() => { SelectBrush(index); selectSound?.Play(); });
            }
        }

        SelectBrush(startBrush);
    }

    // The cursor is placed by its bottom-left corner and sized in rect units, whatever it is parented to
    void SetupBrushCursor()
    {
        if (brushCursor == null) return;

        brushCursor.anchorMin = brushCursor.anchorMax = new Vector2(0.5f, 0.5f);
        brushCursor.pivot = Vector2.zero;

        brushCursorGraphic = brushCursor.GetComponent<Graphic>();
        if (brushCursorGraphic)
        {
            brushCursorGraphic.raycastTarget = false;
            brushCursorAlpha = brushCursorGraphic.color.a;
        }

        brushCursor.gameObject.SetActive(false);
    }

    // Over the box around the disc a stamp at this pixel would paint
    void ShowBrushCursor(bool show, Vector2Int pixel = default)
    {
        if (brushCursor == null) return;

        if (brushCursor.gameObject.activeSelf != show) brushCursor.gameObject.SetActive(show);
        if (!show || (paintImage == null) || (res <= 0)) return;

        var rt = paintImage.rectTransform;
        var rect = rt.rect;
        var unit = new Vector2(rect.width / res, rect.height / res);
        int size = brushSize;

        var corner = new Vector2(rect.xMin + (pixel.x - size / 2) * unit.x, rect.yMin + (pixel.y - size / 2) * unit.y);
        brushCursor.position = rt.TransformPoint(corner);
        brushCursor.sizeDelta = unit * size;
    }

    // Stretches a selection marker over a button, drawn on top of it
    static void MoveMarker(RectTransform marker, Transform target)
    {
        marker.gameObject.SetActive(true);
        marker.SetParent(target, false);
        marker.anchorMin = Vector2.zero;
        marker.anchorMax = Vector2.one;
        marker.offsetMin = Vector2.zero;
        marker.offsetMax = Vector2.zero;
        marker.SetAsLastSibling();
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
            regionText.text = $"Areas: {paintedRegionCount} / {interiorRegionCount}";
        }
        if (starRow != null) starRow.SetStars(stars);
        if (submitButton) submitButton.interactable = paintingEnabled && !paintDone;
        UpdateTimeFill();
    }

    void UpdateTimeFill()
    {
        if (timeFill != null) timeFill.fillAmount = (timeLimit > 0.0f) ? Mathf.Clamp01(timeLeft / timeLimit) : 0.0f;
    }

    #endregion
}
