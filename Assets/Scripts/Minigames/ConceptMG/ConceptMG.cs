using System.Collections;
using TMPro;
using UC;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ConceptMG : MinigameUI
{
    [Header("Concept")]
    [SerializeField] private ConceptSO          concept;

    [Header("UI references")]
    [SerializeField] private Image              conceptImage;   // Concept art shown during the intro
    [SerializeField] private Image              sketchImage;    // Light grey sketch the player traces (same rect as conceptImage)
    [SerializeField] private RawImage           drawImage;      // Player drawing surface (same rect, on top of the sketch)
    [SerializeField] private TextMeshProUGUI    scoreText;      // Optional score display
    [SerializeField] private Button             submitButton;   // Optional, disabled while the score is below minScoreToSubmit

    [Header("Intro")]
    [SerializeField] private float              conceptDisplayTime = 2.0f;
    [SerializeField] private float              crossfadeTime = 1.0f;

    [Header("Drawing")]
    [SerializeField, Min(1)] private int        resolutionDivider = 2;  // Draw at sketch resolution / divider
    [SerializeField, Min(0)] private float      brushRadius = 1.0f;     // In draw pixels
    [SerializeField] private Color              drawColor = Color.black;

    [Header("Scoring")]
    [SerializeField, Min(0)] private float      distanceThreshold = 4.0f;       // In sketch pixels
    [SerializeField, Range(0, 1)] private float sketchAlphaThreshold = 0.1f;    // Sketch pixel counts as painted above this alpha
    [SerializeField, Range(0, 1)] private float minScoreToSubmit = 0.3f;        // Submit button is interactable from this score up

#if UNITY_EDITOR
    [Header("Debug (editor only)")]
    [SerializeField] private bool               saveDrawingOnSubmit = false;            // Writes the submitted drawing as a PNG
    [SerializeField] private string             saveDrawingFolder = "Assets/Art/Debug"; // Project-relative folder for the PNG
#endif

    // Sketch analysis (at sketch resolution)
    Texture2D   sketchTexture;
    int         sketchWidth;
    int         sketchHeight;
    float[]     distanceField;          // Distance (in sketch pixels) to the nearest painted sketch pixel
    int         sketchPaintedPixels;    // Painted sketch pixels at sketch resolution
    int         sketchPaintedPixelsDraw;// Painted sketch pixels at draw resolution (denominator for the score)

    // Player drawing (at draw resolution)
    Texture2D   drawTexture;
    Color32[]   drawPixels;
    int         drawWidth;
    int         drawHeight;
    bool        drawDirty;

    // State
    bool        introDone;
    bool        drawDone;
    bool        drawingEnabled;
    bool        wasPressed;
    Vector2Int  lastDrawPixel;
    Coroutine   introCR;
    Canvas      canvas;

    public ConceptSO    currentConcept => concept;
    public float        score => ComputeScore();
    public bool         isDrawingEnabled => drawingEnabled;

    protected override void Start()
    {
        base.Start();

        canvas = GetComponentInParent<Canvas>();

        Set(concept);
    }

    public void Set(ConceptSO concept)
    {
        this.concept = concept;

        introDone = false;
        drawDone = false;
        drawingEnabled = false;
        wasPressed = false;
        if (introCR != null)
        {
            StopCoroutine(introCR);
            introCR = null;
        }

        if (concept == null) return;

        if (conceptImage) conceptImage.sprite = concept.ConceptArt;
        if (sketchImage) sketchImage.sprite = concept.Sketch;

        BuildSketchData();
        CreateDrawTexture();
        UpdateScoreUI();
    }

    public override void Activate()
    {
        base.Activate();

        if (concept == null) return;

        if (introDone)
        {
            SetAlpha(conceptImage, 0.0f);
            SetAlpha(sketchImage, 1.0f);
            SetAlpha(drawImage, 1.0f);
            drawingEnabled = true;
        }
        else
        {
            introCR = StartCoroutine(IntroCR());
        }
    }

    public override void Deactivate()
    {
        base.Deactivate();

        // If the intro was interrupted it plays again next time
        if (introCR != null)
        {
            StopCoroutine(introCR);
            introCR = null;
        }
        drawingEnabled = false;
        wasPressed = false;
    }

    public void ClearDrawing()
    {
        if (drawPixels == null) return;

        System.Array.Clear(drawPixels, 0, drawPixels.Length);
        drawDirty = true;
        wasPressed = false;
    }

    void OnDestroy()
    {
        ReleaseSketchData();
        if (drawTexture) Destroy(drawTexture);
    }

    IEnumerator IntroCR()
    {
        drawingEnabled = false;

        SetAlpha(conceptImage, 1.0f);
        SetAlpha(sketchImage, 0.0f);
        SetAlpha(drawImage, 0.0f);

        yield return new WaitForSeconds(conceptDisplayTime);

        float t = 0.0f;
        while (t < crossfadeTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / crossfadeTime);
            SetAlpha(conceptImage, 1.0f - a);
            SetAlpha(sketchImage, a);
            SetAlpha(drawImage, a);
            yield return null;
        }

        SetAlpha(conceptImage, 0.0f);
        SetAlpha(sketchImage, 1.0f);
        SetAlpha(drawImage, 1.0f);

        introDone = true;
        drawingEnabled = true;
        introCR = null;
    }

    void Update()
    {
        if (!drawingEnabled || drawImage == null || drawTexture == null) return;

        var pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.isPressed)
        {
            if (TryGetDrawPixel(pointer.position.ReadValue(), out var pixel))
            {
                if (wasPressed) DrawLine(lastDrawPixel, pixel);
                else DrawDot(pixel);

                lastDrawPixel = pixel;
                wasPressed = true;
            }
            else
            {
                // Pointer left the drawing area: lift the pen
                wasPressed = false;
            }
        }
        else
        {
            wasPressed = false;
        }

        if (drawDirty)
        {
            drawTexture.SetPixels32(drawPixels);
            drawTexture.Apply();
            drawDirty = false;
            UpdateScoreUI();
        }
    }

    #region Sketch analysis

    void BuildSketchData()
    {
        ReleaseSketchData();

        var sprite = concept.Sketch;
        if (sprite == null)
        {
            Debug.LogWarning($"ConceptMG: concept {concept.name} has no sketch sprite", this);
            return;
        }

        // Get the sketch as readable pixels
        Vector2 size = (Vector2)sprite.bounds.size * sprite.pixelsPerUnit;
        sketchWidth = Mathf.Max(1, Mathf.RoundToInt(size.x));
        sketchHeight = Mathf.Max(1, Mathf.RoundToInt(size.y));

        sketchTexture = RasterizeSprite(sprite, sketchWidth, sketchHeight);
        if (sketchTexture == null)
        {
            Debug.LogError("ConceptMG: failed to rasterize sketch sprite", this);
            return;
        }

        // Which sketch pixels are painted
        var pixels = sketchTexture.GetPixels32();
        byte alphaThreshold = (byte)Mathf.RoundToInt(sketchAlphaThreshold * 255.0f);
        var painted = new bool[pixels.Length];
        sketchPaintedPixels = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            painted[i] = pixels[i].a > alphaThreshold;
            if (painted[i]) sketchPaintedPixels++;
        }

        distanceField = DistanceField.Compute(painted, sketchWidth, sketchHeight);

        // Painted sketch pixels at draw resolution: a draw pixel counts if any sketch pixel under it is painted.
        // This is the denominator for the score, so a perfect trace scores ~1.
        int dw = Mathf.Max(1, sketchWidth / resolutionDivider);
        int dh = Mathf.Max(1, sketchHeight / resolutionDivider);
        sketchPaintedPixelsDraw = 0;
        for (int dy = 0; dy < dh; dy++)
        {
            int sy0 = dy * sketchHeight / dh;
            int sy1 = Mathf.Max(sy0 + 1, (dy + 1) * sketchHeight / dh);
            for (int dx = 0; dx < dw; dx++)
            {
                int sx0 = dx * sketchWidth / dw;
                int sx1 = Mathf.Max(sx0 + 1, (dx + 1) * sketchWidth / dw);
                bool any = false;
                for (int sy = sy0; sy < sy1 && !any; sy++)
                    for (int sx = sx0; sx < sx1; sx++)
                        if (painted[sy * sketchWidth + sx]) { any = true; break; }
                if (any) sketchPaintedPixelsDraw++;
            }
        }
    }

    // Returns a readable RGBA texture of the sprite at the given size.
    // Textured sprites are copied through a RenderTexture (works even if the source texture isn't readable);
    // vector (mesh) sprites are rendered with the Vector Graphics package.
    static Texture2D RasterizeSprite(Sprite sprite, int width, int height)
    {
        Texture2D result = null;

        if (sprite.texture != null)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;

            Rect tr = sprite.textureRect;
            Vector2 scale = new Vector2(tr.width / sprite.texture.width, tr.height / sprite.texture.height);
            Vector2 offset = new Vector2(tr.x / sprite.texture.width, tr.y / sprite.texture.height);
            Graphics.Blit(sprite.texture, rt, scale, offset);

            RenderTexture.active = rt;
            result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
        else
        {
            var shader = Shader.Find("Unlit/Vector");
            if (shader == null)
            {
                Debug.LogError("ConceptMG: Unlit/Vector shader not found (add it to Always Included Shaders if this happens in a build)");
                return null;
            }
            var mat = new Material(shader);
            result = VectorUtils.RenderSpriteToTexture2D(sprite, width, height, mat, 4);
            Destroy(mat);
        }

        if (result != null) result.name = "ConceptMG Sketch";
        return result;
    }

    void ReleaseSketchData()
    {
        if (sketchTexture) Destroy(sketchTexture);
        sketchTexture = null;
        distanceField = null;
        sketchPaintedPixels = 0;
        sketchPaintedPixelsDraw = 0;
    }

    #endregion

    #region Drawing

    void CreateDrawTexture()
    {
        if (drawTexture) Destroy(drawTexture);

        int baseW = (sketchWidth > 0) ? sketchWidth : 256;
        int baseH = (sketchHeight > 0) ? sketchHeight : 256;
        drawWidth = Mathf.Max(1, baseW / resolutionDivider);
        drawHeight = Mathf.Max(1, baseH / resolutionDivider);

        drawTexture = new Texture2D(drawWidth, drawHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "ConceptMG Drawing"
        };
        drawPixels = new Color32[drawWidth * drawHeight];
        drawTexture.SetPixels32(drawPixels);
        drawTexture.Apply();
        drawDirty = false;

        if (drawImage) drawImage.texture = drawTexture;
    }

    bool TryGetDrawPixel(Vector2 screenPos, out Vector2Int pixel)
    {
        pixel = default;

        var rt = drawImage.rectTransform;
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out var local)) return false;

        var rect = rt.rect;
        float nx = (local.x - rect.xMin) / rect.width;
        float ny = (local.y - rect.yMin) / rect.height;
        if (nx < 0.0f || nx > 1.0f || ny < 0.0f || ny > 1.0f) return false;

        pixel = new Vector2Int(Mathf.Clamp((int)(nx * drawWidth), 0, drawWidth - 1),
                               Mathf.Clamp((int)(ny * drawHeight), 0, drawHeight - 1));
        return true;
    }

    void DrawLine(Vector2Int from, Vector2Int to)
    {
        int steps = Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
        if (steps == 0)
        {
            DrawDot(to);
            return;
        }
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            DrawDot(new Vector2Int(Mathf.RoundToInt(Mathf.Lerp(from.x, to.x, t)),
                                   Mathf.RoundToInt(Mathf.Lerp(from.y, to.y, t))));
        }
    }

    void DrawDot(Vector2Int center)
    {
        Color32 c = drawColor;
        int r = Mathf.CeilToInt(brushRadius);
        float r2 = brushRadius * brushRadius;
        for (int y = -r; y <= r; y++)
        {
            int py = center.y + y;
            if (py < 0 || py >= drawHeight) continue;
            for (int x = -r; x <= r; x++)
            {
                int px = center.x + x;
                if (px < 0 || px >= drawWidth) continue;
                if (x * x + y * y > r2) continue;
                drawPixels[py * drawWidth + px] = c;
            }
        }
        drawDirty = true;
    }

    #endregion

    #region Scoring

    // Graded score: every drawn pixel contributes 1 - (distance / distanceThreshold), clamped to [0, 1],
    // where distance is sampled on the sketch distance field. A pixel on the sketch scores 1, one at the
    // threshold or beyond scores 0. The sum is divided by the number of painted sketch pixels at draw
    // resolution, so a perfect trace scores ~1.
    float ComputeScore()
    {
        if (distanceField == null || drawPixels == null || sketchPaintedPixelsDraw == 0) return 0.0f;

        float invThreshold = (distanceThreshold > 0.0f) ? (1.0f / distanceThreshold) : 0.0f;
        float sum = 0.0f;
        for (int y = 0; y < drawHeight; y++)
        {
            int sy = Mathf.Min(sketchHeight - 1, (int)((y + 0.5f) * sketchHeight / drawHeight));
            for (int x = 0; x < drawWidth; x++)
            {
                if (drawPixels[y * drawWidth + x].a == 0) continue;

                int sx = Mathf.Min(sketchWidth - 1, (int)((x + 0.5f) * sketchWidth / drawWidth));
                float d = distanceField[sy * sketchWidth + sx];

                if (distanceThreshold <= 0.0f) sum += (d <= 0.0f) ? 1.0f : 0.0f;
                else sum += Mathf.Clamp01(1.0f - d * invThreshold);
            }
        }

        return sum / sketchPaintedPixelsDraw;
    }

    void UpdateScoreUI()
    {
        float s = score;
        if (scoreText) scoreText.text = $"{s:P0}";
        if (submitButton) submitButton.interactable = (s >= minScoreToSubmit);
    }

    #endregion

    static void SetAlpha(Graphic g, float alpha)
    {
        if (g == null) return;
        var c = g.color;
        c.a = alpha;
        g.color = c;
    }

    public void Submit()
    {
        drawingEnabled = false;
        wasPressed = false;
        drawDone = true;

        var player = FindAnyObjectByType<Player>();
        if (player != null)
        {
            player.SetConceptDrawing(CreateDrawingCopy(), concept);
        }
        else
        {
            Debug.LogWarning("ConceptMG: no Player found to store the drawing", this);
        }

#if UNITY_EDITOR
        if (saveDrawingOnSubmit) SaveDrawingPNG();
#endif

        canvasGroup.FadeOut(0.1f);
    }

#if UNITY_EDITOR
    // Debug helper: saves the drawing so it can be assigned to Player.debugConceptDrawing and the concept station skipped
    void SaveDrawingPNG()
    {
        if (drawTexture == null) return;

        string folder = DebugAssetUtils.NormalizeFolder(saveDrawingFolder);
        string name = (concept != null) ? concept.name : "Concept";
        string path = $"{folder}/{name}_drawing.png";

        DebugAssetUtils.SavePNG(drawTexture, path);
        Debug.Log($"ConceptMG: drawing saved to {path}", this);
    }
#endif

    // Standalone copy of the drawing, so later Set/Clear calls don't touch what the player keeps
    Texture2D CreateDrawingCopy()
    {
        if (drawPixels == null) return null;

        var copy = new Texture2D(drawWidth, drawHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = (concept != null) ? $"Drawing_{concept.name}" : "Drawing"
        };
        copy.SetPixels32(drawPixels);
        copy.Apply();
        return copy;
    }

    // Not usable once submitted, or when the player already carries a drawing (e.g. a debug starting stage)
    // Unusable until the day's concept is known (LevelManager sets it once the pitch is done)
    public override bool CanUse(Player player) => (concept != null) && !drawDone && ((player == null) || (player.conceptDrawing == null));

    public override void ResetStation() => Set(concept);
}
