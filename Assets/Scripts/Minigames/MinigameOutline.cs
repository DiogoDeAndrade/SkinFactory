using UC;
using UnityEngine;

// Cosmetic: outlines a station (every renderer below it) while the player can work there.
// The outline goes away when the station isn't usable (missing prerequisites or already done) and while the
// player is actually using it. Needs the Screen Space Outline renderer feature on the URP renderer.
[RequireComponent(typeof(Minigame))]
public class MinigameOutline : MonoBehaviour
{
    [SerializeField, Tooltip("Outline component to drive; created on this object if left empty")]
    private ScreenSpaceOutline  outline;
    [SerializeField] private Color availableColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
    [SerializeField, Min(0)] private float width = 3.0f;
    [SerializeField, Tooltip("Keep the outline while the player is working at this station, in the in-range color")]
    private bool                showWhileActive = false;
    [SerializeField, Tooltip("Outline color while the player is at the station (needs Show While Active)")]
    private Color               inRangeColor = Color.yellow;

    Minigame    minigame;
    Player      player;
    Color       restColor;      // The outline's own color, restored when the player is not at the station

    void Start()
    {
        minigame = GetComponent<Minigame>();
        player = FindAnyObjectByType<Player>();

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
        if ((outline == null) || (minigame == null)) return;
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
        if (outline.outlineColor != color) outline.outlineColor = color;
        if (outline.enabled != show) outline.enabled = show;
    }

    void OnDisable()
    {
        if (outline != null) outline.enabled = false;
    }
}
