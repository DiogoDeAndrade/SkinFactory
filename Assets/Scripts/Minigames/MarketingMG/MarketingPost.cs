using TMPro;
using UC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One social media post drifting around the marketing play area: moves at a constant velocity and bounces off the
// area's edges. A left click kills it (MarketingMG decides whether that was the right call); at the end of a
// wave every survivor vanishes. Sized to its text on Setup.
[RequireComponent(typeof(RectTransform))]
public class MarketingPost : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text   label;
    [SerializeField] private Image      background;
    [SerializeField, Tooltip("Space around the text (x = left+right, y = top+bottom)")]
    private Vector2                     padding = new Vector2(28.0f, 14.0f);
    [SerializeField, Min(0)] private float killTime = 0.15f;

    [Header("Colors")]
    [SerializeField, Tooltip("Background of a positive post")]
    private Color                       positiveColor = Color.white;
    [SerializeField, Tooltip("Background of a negative post")]
    private Color                       negativeColor = new Color(0.85f, 0.85f, 0.9f, 1.0f);
    [SerializeField, Tooltip("Background flash when a positive post is killed by mistake")]
    private Color                       mistakeColor = new Color(1.0f, 0.3f, 0.3f, 1.0f);
    [SerializeField, Tooltip("Background flash when a negative post is killed")]
    private Color                       killColor = new Color(0.4f, 1.0f, 0.4f, 1.0f);

    public bool     isNegative { get; private set; }
    public bool     isAlive { get; private set; }
    public string   text => (label != null) ? label.text : "";

    RectTransform   rect;
    RectTransform   area;
    MarketingMG     owner;
    Vector2         velocity;
    Vector2         halfSize;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void Setup(MarketingMG owner, RectTransform area, string text, bool negative, Vector2 position, Vector2 velocity)
    {
        if (rect == null) rect = GetComponent<RectTransform>();

        this.owner = owner;
        this.area = area;
        this.velocity = velocity;
        isNegative = negative;
        isAlive = true;

        if (label != null)
        {
            // One line, measured unconstrained; the card is the text plus the label's own insets plus the padding
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.text = text;

            Vector2 textSize = label.GetPreferredValues(text, 32767.0f, 32767.0f);
            RectTransform lr = label.rectTransform;
            Vector2 inset = new Vector2(lr.offsetMin.x - lr.offsetMax.x, lr.offsetMin.y - lr.offsetMax.y);
            rect.sizeDelta = textSize + inset + padding + Vector2.one;
        }
        halfSize = rect.sizeDelta * 0.5f;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Clamp(position);

        if (background != null)
        {
            background.raycastTarget = true;
            background.color = negative ? negativeColor : positiveColor;
        }
    }

    void Update()
    {
        if (!isAlive || (area == null)) return;

        Vector2 pos = rect.anchoredPosition + velocity * Time.deltaTime;
        Vector2 limit = area.rect.size * 0.5f - halfSize;

        // Bounce: flip the component that crossed, keep the post inside
        if ((pos.x < -limit.x) || (pos.x > limit.x))
        {
            velocity.x = -velocity.x;
            pos.x = Mathf.Clamp(pos.x, -limit.x, limit.x);
        }
        if ((pos.y < -limit.y) || (pos.y > limit.y))
        {
            velocity.y = -velocity.y;
            pos.y = Mathf.Clamp(pos.y, -limit.y, limit.y);
        }
        rect.anchoredPosition = pos;
    }

    Vector2 Clamp(Vector2 pos)
    {
        Vector2 limit = area.rect.size * 0.5f - halfSize;
        return new Vector2(Mathf.Clamp(pos.x, -limit.x, limit.x), Mathf.Clamp(pos.y, -limit.y, limit.y));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isAlive || (eventData.button != PointerEventData.InputButton.Left)) return;
        if (owner != null) owner.PostClicked(this);
    }

    // Killed by the player; the flash says whether it was the right call
    public void Kill(bool mistake)
    {
        if (!isAlive) return;
        isAlive = false;

        if (background != null)
        {
            background.raycastTarget = false;
            background.color = mistake ? mistakeColor : killColor;
        }
        rect.LocalScaleTo(Vector3.zero, killTime, "PostKill").Done(() => Destroy(gameObject));
    }

    // End of the wave: gone without ceremony
    public void Vanish(float time)
    {
        if (!isAlive)
        {
            return;
        }
        isAlive = false;

        if (background != null) background.raycastTarget = false;
        if (time <= 0.0f) Destroy(gameObject);
        else rect.LocalScaleTo(Vector3.zero, time, "PostKill").Done(() => Destroy(gameObject));
    }
}
