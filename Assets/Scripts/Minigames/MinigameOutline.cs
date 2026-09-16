using UC;
using UnityEngine;

// Cosmetic: outlines a station while the player can work there.
// The outline goes away when the station isn't usable (missing prerequisites or already done) and, unless
// showWhileActive is on, while the player is actually using it. Needs the Screen Space Outline renderer feature
// on the URP renderer.
// What gets outlined: the renderers listed in Renderers when there are any (the station's meshes can live on
// siblings rather than children); otherwise the ScreenSpaceOutline on this object, created over every renderer
// below it if there is none.
[RequireComponent(typeof(Minigame))]
public class MinigameOutline : MonoBehaviour
{
    [SerializeField, Tooltip("Exact renderers to outline; when set, the outline component below is not used")]
    private Renderer[]          renderers;
    [SerializeField, Tooltip("Outline component to drive when no renderers are listed; created on this object if left empty")]
    private ScreenSpaceOutline  outline;
    [SerializeField] private Color availableColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
    [SerializeField, Min(0)] private float width = 3.0f;
    [SerializeField, Tooltip("Keep the outline while the player is working at this station, in the in-range color")]
    private bool                showWhileActive = false;
    [SerializeField, Tooltip("Outline color while the player is at the station (needs Show While Active)")]
    private Color               inRangeColor = Color.yellow;

    Minigame        minigame;
    Player          player;
    Color           restColor;      // The color shown when the player is not at the station
    OutlineTarget   target;         // Own registration when renderers are listed
    bool            targetShown;

    bool usesOwnTarget => (renderers != null) && (renderers.Length > 0);

    void Start()
    {
        minigame = GetComponent<Minigame>();
        player = FindAnyObjectByType<Player>();

        if (usesOwnTarget)
        {
            target = new OutlineTarget
            {
                renderers = renderers,
                color = availableColor,
                width = width,
            };
            restColor = availableColor;
            return;
        }

        if (outline == null) outline = GetComponent<ScreenSpaceOutline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<ScreenSpaceOutline>();
            outline.outlineColor = availableColor;
            outline.outlineWidth = width;
            outline.Refresh();
        }
        restColor = outline.outlineColor;
        outline.enabled = false;
    }

    void Update()
    {
        if (minigame == null) return;
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
            if (player == null) return;
        }

        bool available = minigame.CanUse(player);
        bool active = (player.activeMinigame == minigame);
        bool show = available && (showWhileActive || !active);

        // In range: the outline switches to the in-range color so the player knows the station is theirs
        Color color = active ? inRangeColor : restColor;
        Apply(show, color);
    }

    void Apply(bool show, Color color)
    {
        if (usesOwnTarget)
        {
            if (target == null) return;
            target.color = color;
            if (show == targetShown) return;
            targetShown = show;
            if (show) OutlineRegistry.Register(target);
            else OutlineRegistry.Unregister(target);
            return;
        }

        if (outline == null) return;
        if (outline.outlineColor != color) outline.outlineColor = color;
        if (outline.enabled != show) outline.enabled = show;
    }

    void OnDisable()
    {
        if (target != null && targetShown)
        {
            OutlineRegistry.Unregister(target);
            targetShown = false;
        }
        if (outline != null) outline.enabled = false;
    }
}
