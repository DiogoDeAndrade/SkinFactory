using System.Collections.Generic;
using UC;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Modelling station: the player builds polygons over the concept drawing they are carrying.
// Left click starts a polygon / adds a vertex; clicking near the first vertex closes it.
// Right click cancels the polygon in progress, or deletes the completed polygon under the cursor.
public class ModellingMG : MinigameUI
{
    [Header("UI references")]
    [SerializeField] private RawImage           drawingImage;   // Shows the concept drawing the player is carrying
    [SerializeField] private PolygonGraphic     polygonGraphic; // Draws the polygons (same rect as drawingImage, on top)
    [SerializeField] private TextMeshProUGUI    scoreText;      // Optional
    [SerializeField] private TextMeshProUGUI    segmentText;    // Optional, "used / budget segments"
    [SerializeField] private TextMeshProUGUI    regionText;     // Optional, "closed regions / minimum"
    [SerializeField] private Button             submitButton;   // Optional, disabled while the score is below minScoreToSubmit

    [Header("Display")]
    [SerializeField, Range(0, 1)] private float drawingAlpha = 0.35f;  // Concept drawing is faded so the polygons stand out

    [Header("Editing")]
    [SerializeField, Min(0)] private float      snapRadius = 12.0f;         // Rect units: clicking this close to the first vertex closes the polygon
    [SerializeField, Min(0)] private float      minVertexDistance = 2.0f;   // Rect units: clicks this close to the previous vertex are ignored

    [Header("Scoring")]
    [SerializeField, Min(0)] private float      distanceThreshold = 2.0f;   // In drawing pixels
    [SerializeField, Range(0, 1)] private float minScoreToSubmit = 0.3f;

#if UNITY_EDITOR
    [Header("Debug (editor only)")]
    [SerializeField] private bool               saveModelOnSubmit = false;              // Writes a ModelDataSO (plus the drawing PNG) on submit
    [SerializeField] private string             saveModelFolder = "Assets/Art/Debug";   // Project-relative folder
#endif

    // Concept drawing being modelled (owned by the Player)
    Player      player;
    Texture2D   drawing;
    int         drawingWidth;
    int         drawingHeight;
    bool[]      drawnPixels;
    int         drawnPixelCount;
    float[]     drawingDistanceField;

    // Model: polygons in normalized [0,1] coordinates over the drawing rect, origin bottom-left
    List<List<Vector2>> polygons = new List<List<Vector2>>();
    List<Vector2>       current = new List<Vector2>();

    // State
    bool        editingEnabled;
    bool        modelDone;
    float       precision;
    float       recall;
    float       lastScore;
    int         closedRegions;
    Canvas      canvas;

    public IReadOnlyList<List<Vector2>> completedPolygons => polygons;
    public float    score => lastScore;
    public float    lastPrecision => precision;
    public float    lastRecall => recall;
    public bool     isEditingEnabled => editingEnabled;

    public int usedSegments
    {
        get
        {
            int n = 0;
            foreach (var p in polygons) n += p.Count;
            if (current.Count > 1) n += current.Count - 1;
            return n;
        }
    }

    public int  closedRegionCount => closedRegions;
    public int  segmentBudget => (player != null && player.conceptDrawingSource != null) ? player.conceptDrawingSource.SegmentBudget : 0;         // 0 = unlimited
    public int  minClosedRegions => (player != null && player.conceptDrawingSource != null) ? player.conceptDrawingSource.MinClosedRegions : 0;

    // True if adding this many segments would exceed the budget
    bool ExceedsBudget(int extraSegments)
    {
        int budget = segmentBudget;
        return (budget > 0) && (usedSegments + extraSegments > budget);
    }

    protected override void Start()
    {
        base.Start();

        canvas = GetComponentInParent<Canvas>();
        if (player == null) player = FindAnyObjectByType<Player>();

        ApplyDrawingAlpha();
        RefreshGraphic();
        UpdateUI();
    }

    public override void ResetStation()
    {
        modelDone = false;
        editingEnabled = false;
        SetDrawing(null);
        RefreshGraphic();
        UpdateUI();
    }

    // Needs a concept drawing to model, and is done once a model exists (submitted here, or a debug starting stage)
    public override bool CanUse(Player player)
    {
        if (modelDone) return false;
        if (player == null) return false;

        return (player.conceptDrawing != null) && (player.modelPolygons == null);
    }

    public override void Activate()
    {
        base.Activate();

        if (player == null) player = FindAnyObjectByType<Player>();

        var tex = (player != null) ? player.conceptDrawing : null;
        if (tex != drawing) SetDrawing(tex);

        editingEnabled = (drawing != null) && !modelDone;
        RefreshGraphic();
        UpdateUI();
    }

    public override void Deactivate()
    {
        base.Deactivate();

        editingEnabled = false;
        // Walking away drops the polygon in progress; completed polygons are kept
        if (current.Count > 0)
        {
            current.Clear();
            RefreshGraphic();
            UpdateUI();
        }
        if (polygonGraphic) polygonGraphic.SetCursor(null, false);
    }

    public void Submit()
    {
        editingEnabled = false;
        modelDone = true;
        current.Clear();

        if (player == null) player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            var model = new Vector2[polygons.Count][];
            for (int i = 0; i < polygons.Count; i++) model[i] = polygons[i].ToArray();
            player.SetModel(model);
#if UNITY_EDITOR
            if (saveModelOnSubmit) SaveModelAsset(model);
#endif
        }
        else
        {
            Debug.LogWarning("ModellingMG: no Player found to store the model", this);
        }

        RefreshGraphic();
        UpdateUI();
        canvasGroup.FadeOut(0.1f);
    }

#if UNITY_EDITOR
    // Debug helper: saves the model (and the drawing it was built on) as a ModelDataSO, assignable to Player.debugModel
    void SaveModelAsset(Vector2[][] model)
    {
        string folder = DebugAssetUtils.NormalizeFolder(saveModelFolder);
        var source = (player != null) ? player.conceptDrawingSource : null;
        string name = (source != null) ? source.name : "Concept";

        var drawingAsset = DebugAssetUtils.SavePNG(drawing, $"{folder}/{name}_model_drawing.png");

        string path = $"{folder}/{name}_model.asset";
        var asset = DebugAssetUtils.LoadOrCreateAsset<ModelDataSO>(path);
        asset.Set(source, drawingAsset, model);
        UnityEditor.EditorUtility.SetDirty(asset);
        UnityEditor.AssetDatabase.SaveAssets();

        Debug.Log($"ModellingMG: model saved to {path}", this);
    }
#endif

    public void ClearModel()
    {
        polygons.Clear();
        current.Clear();
        RecomputeScore();
        RefreshGraphic();
        UpdateUI();
    }

    void SetDrawing(Texture2D tex)
    {
        drawing = tex;
        polygons.Clear();
        current.Clear();
        drawnPixels = null;
        drawingDistanceField = null;
        drawnPixelCount = 0;

        if (drawingImage) drawingImage.texture = tex;
        ApplyDrawingAlpha();

        if (tex != null)
        {
            drawingWidth = tex.width;
            drawingHeight = tex.height;

            var pixels = tex.GetPixels32();
            drawnPixels = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                drawnPixels[i] = pixels[i].a > 127;  // Tolerates alpha noise from imported/compressed PNGs
                if (drawnPixels[i]) drawnPixelCount++;
            }
            drawingDistanceField = DistanceField.Compute(drawnPixels, drawingWidth, drawingHeight);
        }

        RecomputeScore();
    }

    void Update()
    {
        if (!editingEnabled || polygonGraphic == null) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        bool inside = TryGetNormalizedPoint(mouse.position.ReadValue(), out var n);
        bool snapping = inside && (current.Count >= 3) && IsNear(n, current[0], snapRadius);

        polygonGraphic.SetCursor((inside && current.Count > 0) ? n : (Vector2?)null, snapping);

        if (mouse.leftButton.wasPressedThisFrame && inside)
        {
            if (snapping) ClosePolygon();
            else AddVertex(n);
        }
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            if (current.Count > 0) CancelPolygon();
            else if (inside) DeletePolygonAt(n);
        }
    }

    #region Editing

    void AddVertex(Vector2 n)
    {
        if ((current.Count > 0) && IsNear(n, current[current.Count - 1], minVertexDistance)) return;

        // A new polygon needs room for at least a triangle; a new vertex adds one segment
        if (ExceedsBudget((current.Count == 0) ? 3 : 1)) return;

        current.Add(n);
        RefreshGraphic();
        UpdateUI();
    }

    void ClosePolygon()
    {
        if (current.Count < 3) return;
        if (ExceedsBudget(1)) return;   // The closing segment

        polygons.Add(new List<Vector2>(current));
        current.Clear();
        RecomputeScore();
        RefreshGraphic();
        UpdateUI();
    }

    void CancelPolygon()
    {
        current.Clear();
        RefreshGraphic();
        UpdateUI();
    }

    void DeletePolygonAt(Vector2 n)
    {
        // Topmost (most recent) polygon under the cursor wins
        for (int i = polygons.Count - 1; i >= 0; i--)
        {
            if (PointInPolygon(polygons[i], n))
            {
                polygons.RemoveAt(i);
                RecomputeScore();
                RefreshGraphic();
                UpdateUI();
                return;
            }
        }
    }

    bool IsNear(Vector2 a, Vector2 b, float rectUnits)
    {
        return Vector2.Distance(polygonGraphic.ToLocal(a), polygonGraphic.ToLocal(b)) <= rectUnits;
    }

    bool TryGetNormalizedPoint(Vector2 screenPos, out Vector2 normalized)
    {
        normalized = default;

        var rt = polygonGraphic.rectTransform;
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out var local)) return false;

        var rect = rt.rect;
        normalized = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        return (normalized.x >= 0.0f) && (normalized.x <= 1.0f) && (normalized.y >= 0.0f) && (normalized.y <= 1.0f);
    }

    static bool PointInPolygon(List<Vector2> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if (((a.y > p.y) != (b.y > p.y)) &&
                (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

    #endregion

    #region Scoring

    // Precision: how much of the polygon outline lies on drawn pixels (sampled along every edge against the drawing's distance field).
    // Recall: how much of the drawing is covered by polygon outlines (every drawn pixel against the outline's distance field).
    // Score: harmonic mean of the two, so neither a single perfect triangle nor a carpet of segments scores well.
    void RecomputeScore()
    {
        precision = 0.0f;
        recall = 0.0f;
        lastScore = 0.0f;

        // Closed regions of the model (independent of the drawing)
        closedRegions = 0;
        if (polygons.Count > 0)
        {
            var segments = new List<RegionCounter.Segment>();
            foreach (var poly in polygons) RegionCounter.AddPolygon(segments, poly);
            closedRegions = RegionCounter.CountClosedRegions(segments);
        }

        if ((drawingDistanceField == null) || (drawnPixelCount == 0) || (polygons.Count == 0)) return;

        int w = drawingWidth, h = drawingHeight;
        var covered = new bool[w * h];
        float precisionSum = 0.0f;
        int samples = 0;

        foreach (var poly in polygons)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = new Vector2(poly[i].x * w, poly[i].y * h);
                Vector2 b = new Vector2(poly[(i + 1) % n].x * w, poly[(i + 1) % n].y * h);

                // Half-pixel steps so the outline rasterization has no gaps
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, (float)s / steps);
                    int px = Mathf.Clamp((int)p.x, 0, w - 1);
                    int py = Mathf.Clamp((int)p.y, 0, h - 1);
                    int idx = py * w + px;

                    precisionSum += DistanceField.Grade(drawingDistanceField[idx], distanceThreshold);
                    samples++;
                    covered[idx] = true;
                }
            }
        }
        precision = (samples > 0) ? precisionSum / samples : 0.0f;

        var outlineDistanceField = DistanceField.Compute(covered, w, h);
        float recallSum = 0.0f;
        for (int i = 0; i < drawnPixels.Length; i++)
        {
            if (!drawnPixels[i]) continue;
            recallSum += DistanceField.Grade(outlineDistanceField[i], distanceThreshold);
        }
        recall = recallSum / drawnPixelCount;

        lastScore = (precision + recall > 0.0f) ? (2.0f * precision * recall) / (precision + recall) : 0.0f;
    }

    #endregion

    void ApplyDrawingAlpha()
    {
        if (drawingImage == null) return;
        var c = drawingImage.color;
        c.a = drawingAlpha;
        drawingImage.color = c;
    }

    void RefreshGraphic()
    {
        if (polygonGraphic) polygonGraphic.SetPolygons(polygons, current);
    }

    void UpdateUI()
    {
        if (scoreText) scoreText.text = $"{lastScore:P0}";
        if (segmentText)
        {
            int budget = segmentBudget;
            segmentText.text = (budget > 0) ? $"Segments: {usedSegments} / {budget} max" : $"Segments: {usedSegments}";
        }
        if (regionText)
        {
            int min = minClosedRegions;
            regionText.text = (min > 0) ? $"Regions: {closedRegions} (min {min})" : $"Regions: {closedRegions}";
        }
        if (submitButton)
        {
            submitButton.interactable = !modelDone && (polygons.Count > 0) && (lastScore >= minScoreToSubmit) && (closedRegions >= minClosedRegions);
        }
    }
}
