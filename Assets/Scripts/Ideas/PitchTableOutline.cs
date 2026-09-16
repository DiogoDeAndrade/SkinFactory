using UC;
using UnityEngine;

// Cosmetic: outlines the pitch table while the player is brainstorming and carrying an idea, so it reads as
// "bring it here". Goes off once the player is close, where the drop areas' own outlines take over.
// Same outline options as MinigameOutline: list the renderers to outline (they can sit on
// siblings), or leave the list empty to use the ScreenSpaceOutline on this object, created over every renderer
// below it if there is none. Needs the Screen Space Outline renderer feature on the URP renderer.
public class PitchTableOutline : MonoBehaviour
{
    [SerializeField, Tooltip("Exact renderers to outline; when set, the outline component below is not used")]
    private Renderer[]          renderers;
    [SerializeField, Tooltip("Outline component to drive when no renderers are listed; created on this object if left empty")]
    private ScreenSpaceOutline  outline;
    [SerializeField] private Color  color = new Color(1.0f, 0.85f, 0.2f, 1.0f);
    [SerializeField, Min(0)] private float width = 3.0f;
    [SerializeField, Tooltip("Only when one of the pitch drop areas would take the carried idea (off = any carried idea)")]
    private bool                onlyWhenAccepted = false;
    [SerializeField, Min(0), Tooltip("Distance (XZ) from this object within which the table outline goes off, leaving the drop areas' own outlines to guide the player (0 = never)")]
    private float               nearRange = 4.0f;

    Player          player;
    OutlineTarget   target;         // Own registration when renderers are listed
    bool            shown;

    bool usesOwnTarget => (renderers != null) && (renderers.Length > 0);

    void Start()
    {
        player = FindAnyObjectByType<Player>();

        if (usesOwnTarget)
        {
            target = new OutlineTarget
            {
                renderers = renderers,
                color = color,
                width = width,
            };
            return;
        }

        if (outline == null) outline = GetComponent<ScreenSpaceOutline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<ScreenSpaceOutline>();
            outline.outlineColor = color;
            outline.outlineWidth = width;
            outline.Refresh();
        }
        outline.enabled = false;
    }

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<Player>();
            if (player == null) return;
        }

        bool brainstorm = (LevelManager.instance != null) && (LevelManager.instance.state == LevelManager.State.Brainstorm);
        bool near = (nearRange > 0.0f) && (Vector3.Distance(player.transform.position.x0z(), transform.position.x0z()) < nearRange);
        bool show = brainstorm && !near && (player.heldIdea != null) && (!onlyWhenAccepted || Accepted(player.heldIdea));
        Apply(show);
    }

    // Any non-trash drop area that takes the idea
    static bool Accepted(Idea idea)
    {
        foreach (var area in DropArea.All)
        {
            if ((area != null) && !area.IsTrash && area.Accepts(idea)) return true;
        }
        return false;
    }

    void Apply(bool show)
    {
        if (show == shown) return;
        shown = show;

        if (usesOwnTarget)
        {
            if (target == null) return;
            if (show) OutlineRegistry.Register(target);
            else OutlineRegistry.Unregister(target);
            return;
        }

        if (outline != null) outline.enabled = show;
    }

    void OnDisable()
    {
        Apply(false);
    }
}
