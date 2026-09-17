using TMPro;
using UC;
using UnityEngine;
using UnityEngine.UI;

// One speech balloon on the UI canvas: a 9-sliced body, an optional tail and a label. Sized to its text (wrapping
// past maxTextWidth) and kept over a world anchor every frame, projected through whichever camera is rendering
// (SpeechBalloonManager.ActiveCamera), so it works for the boss camera as well as the main one.
// Fades in on Show and out on Hide; SpeechBalloonManager pools the instances. A trimmed-down take on the
// Blitz and Massive balloon: single page, no voice pacing, no emotes.
// Layout is authored on the prefab: the root's pivot is where the tail tip sits (that point lands on the anchor)
// and the tail is anchored to the body's bottom edge. Code only sizes the body and places it.
public class SpeechBalloon : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("9-sliced balloon body, stretched over this object")]
    private Image       body;
    [SerializeField, Tooltip("Tail image anchored to the body's bottom edge; optional")]
    private Image       tail;
    [SerializeField]
    private TMP_Text    label;
    [SerializeField]
    private CanvasGroup canvasGroup;

    [Header("Layout")]
    [SerializeField, Min(0.1f), Tooltip("Canvas pixels per sprite pixel, for the body border and the tail")]
    private float       pixelScale = 2.0f;
    [SerializeField, Min(0), Tooltip("Extra space between the body's border and the text")]
    private float       textInset = 4.0f;
    [SerializeField, Min(10), Tooltip("Text wraps past this width")]
    private float       maxTextWidth = 400.0f;
    [SerializeField, Min(0), Tooltip("Kept clear from the screen edges")]
    private float       screenMargin = 8.0f;

    [Header("Timing")]
    [SerializeField, Min(0)]
    private float       fadeTime = 0.15f;
    [SerializeField, Min(0), Tooltip("Reading time for any text")]
    private float       baseTime = 1.5f;
    [SerializeField, Min(0), Tooltip("Reading time added per character")]
    private float       timePerChar = 0.05f;

    // How long Show suggests the text stays up, from its length (the caller decides when to Hide)
    public float        ReadTime { get; private set; }
    public bool         IsShowing => showing;
    public Transform    Anchor => anchor;

    // GetPreferredValues takes the available space, not a hint: 0 wraps after every character
    const float kUnconstrained = 32767.0f;
    // TMP lays out a hair larger than it reports as preferred; without slack the last line can wrap away
    const float kMeasureEpsilon = 1.0f;

    RectTransform   root;
    RectTransform   parentRect;
    Canvas          canvas;
    Transform       anchor;
    Vector3         offset;
    bool            showing;

    void Awake()
    {
        Resolve();
    }

    void Resolve()
    {
        if (root == null) root = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        canvas = GetComponentInParent<Canvas>();
        parentRect = root.parent as RectTransform;
    }

    // Lays the text out and fades the balloon in over the anchor (world position plus a world-space offset)
    public void Show(string text, Transform anchor, Vector3 worldOffset)
    {
        Resolve();

        this.anchor = anchor;
        offset = worldOffset;
        showing = true;
        gameObject.SetActive(true);

        Layout(text ?? string.Empty);
        ReadTime = baseTime + timePerChar * (text?.Length ?? 0);
        UpdatePosition();

        if (canvasGroup != null) canvasGroup.FadeIn(fadeTime);
    }

    // Fades out, then deactivates so the manager can reuse the balloon
    public void Hide()
    {
        if (!showing) return;
        showing = false;

        if (canvasGroup == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // A Show during the fade replaces this tween (same name), so its Done never fires; the guard covers the
        // case where it does fire after a Show/Hide pair
        var tween = canvasGroup.FadeOut(fadeTime);
        if (tween != null) tween.Done(() => { if (!showing) gameObject.SetActive(false); });
        else gameObject.SetActive(false);
    }

    void Layout(string text)
    {
        // Border and tail sizes follow the sprites, scaled to canvas pixels
        Vector4 border = Vector4.zero;  // x = left, y = bottom, z = right, w = top (Unity's sprite border order)
        if ((body != null) && (body.sprite != null))
        {
            border = body.sprite.border * pixelScale;
            float refPPU = (canvas != null) ? canvas.referencePixelsPerUnit : 100.0f;
            body.pixelsPerUnitMultiplier = refPPU / (body.sprite.pixelsPerUnit * pixelScale);
        }
        if ((tail != null) && (tail.sprite != null))
        {
            tail.rectTransform.sizeDelta = tail.sprite.rect.size * pixelScale;
        }

        // Text inset: L, T, R, B. The bottom border includes the tail area
        Vector4 padding = new Vector4(border.x + textInset, border.w + textInset, border.z + textInset, border.y + textInset);

        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);

        if (label == null)
        {
            root.sizeDelta = new Vector2(padding.x + padding.z, padding.y + padding.w);
            return;
        }

        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.text = text;

        Vector2 size = label.GetPreferredValues(text, kUnconstrained, kUnconstrained);
        if (size.x > maxTextWidth) size = label.GetPreferredValues(text, maxTextWidth, kUnconstrained);
        size += Vector2.one * kMeasureEpsilon;

        // The label is stretch-anchored inside the root with the padding as offsets, so sizing the root sizes the label
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(padding.x, padding.w);
        lr.offsetMax = new Vector2(-padding.z, -padding.y);

        root.sizeDelta = new Vector2(size.x + padding.x + padding.z, size.y + padding.y + padding.w);
    }

    void LateUpdate()
    {
        UpdatePosition();
    }

    void UpdatePosition()
    {
        if (anchor == null)
        {
            // The anchor was destroyed under us
            if (showing) Hide();
            return;
        }
        if (parentRect == null) return;

        Camera cam = SpeechBalloonManager.ActiveCamera;
        if (cam == null) return;

        Vector3 screen = cam.WorldToScreenPoint(anchor.position + offset);
        bool inFront = screen.z > 0.0f;
        root.localScale = inFront ? Vector3.one : Vector3.zero;
        if (!inFront) return;

        Camera uiCam = ((canvas != null) && (canvas.renderMode != RenderMode.ScreenSpaceOverlay)) ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCam, out Vector2 local)) return;

        // Keep the whole balloon on screen: the canvas rect, expressed in the parent's local space (the parent can
        // be any rect under the canvas, not just its root)
        RectTransform canvasRect = ((canvas != null) && (canvas.rootCanvas != null)) ? canvas.rootCanvas.GetComponent<RectTransform>() : parentRect;
        Vector3 c0 = parentRect.InverseTransformPoint(canvasRect.TransformPoint(canvasRect.rect.min));
        Vector3 c1 = parentRect.InverseTransformPoint(canvasRect.TransformPoint(canvasRect.rect.max));
        Vector2 size = root.rect.size;
        Vector2 pivot = root.pivot;
        float minX = Mathf.Min(c0.x, c1.x) + screenMargin + pivot.x * size.x;
        float maxX = Mathf.Max(c0.x, c1.x) - screenMargin - (1.0f - pivot.x) * size.x;
        float minY = Mathf.Min(c0.y, c1.y) + screenMargin + pivot.y * size.y;
        float maxY = Mathf.Max(c0.y, c1.y) - screenMargin - (1.0f - pivot.y) * size.y;
        if (minX <= maxX) local.x = Mathf.Clamp(local.x, minX, maxX);
        if (minY <= maxY) local.y = Mathf.Clamp(local.y, minY, maxY);

        // Anchors sit at the parent's centre, so the anchored position is the local point relative to it
        root.anchoredPosition = local - parentRect.rect.center;
    }
}
