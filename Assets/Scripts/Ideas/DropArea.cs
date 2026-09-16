using System.Collections.Generic;
using NaughtyAttributes;
using UC;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

// Spot where the player can drop a carried idea. A trash area destroys the idea instead; any other area only
// accepts ideas from its library, holds one idea at a time, and swaps it with the carried one when occupied.
// An optional ScreenSpaceOutline on this object is lit while the player stands inside the radius.
public partial class DropArea : MonoBehaviour
{
    [SerializeField] 
    private bool            trash;
    [SerializeField]
    private float           radius = 1.0f;
    [SerializeField, HideIf(nameof(trash))] 
    private IdeaLibrarySO   ideaLibrary;
    [SerializeField, Tooltip("Where a dropped idea is placed; this transform if left empty")]
    private Transform       dropPoint;
    [SerializeField, HideIf(nameof(trash)), Tooltip("Local scale given to an idea dropped here (its own scale is restored on pickup)")]
    private Vector3         dropScale = Vector3.one;

    [Header("Feedback")]
    [SerializeField, Min(0), Tooltip("Scale punch played when something is dropped here")]
    private float           punchScale = 1.2f;
    [SerializeField, Min(0), Tooltip("Seconds for the scale punch (there and back)")]
    private float           punchTime = 0.2f;

    public bool     IsTrash => trash;
    public IdeaLibrarySO Library => ideaLibrary;
    public Idea     Current => current;
    public bool     HasIdea => current != null;

    [AutoStaticsCleanup]
    static List<DropArea>   areas = new List<DropArea>();

    // Every enabled drop area in the scene
    public static IReadOnlyList<DropArea> All => areas;

    ScreenSpaceOutline  outline;
    Idea                current;
    Vector3             baseScale;

    void OnEnable()
    {
        areas.Add(this);
    }

    void OnDisable()
    {
        areas.Remove(this);
        if (outline != null) outline.enabled = false;
    }

    void Start()
    {
        outline = GetComponent<ScreenSpaceOutline>();
        if (outline != null) outline.enabled = false;
        baseScale = transform.localScale;
    }

    // Area whose radius (XZ) contains the position; the closest one if several overlap. With an idea given, only
    // areas that accept it count, so overlapping areas on one table each answer for their own kind of idea.
    public static DropArea GetAt(Vector3 position, Idea idea = null)
    {
        DropArea    best = null;
        float       bestDist = float.MaxValue;

        foreach (var area in areas)
        {
            if (area == null) continue;
            if ((idea != null) && !area.Accepts(idea)) continue;

            float d = Vector3.Distance(area.transform.position.x0z(), position.x0z());
            if ((d < area.radius) && (d < bestDist))
            {
                bestDist = d;
                best = area;
            }
        }
        return best;
    }

    public void SetHighlight(bool on)
    {
        if ((outline != null) && (outline.enabled != on)) outline.enabled = on;
    }

    // Trash takes anything; other areas only ideas listed in their library (by reference or by idea name)
    public bool Accepts(Idea idea)
    {
        if (idea == null) return false;
        if (trash) return true;
        if ((ideaLibrary == null) || (idea.IdeaSO == null)) return false;

        foreach (var so in ideaLibrary.Ideas)
        {
            if (so == idea.IdeaSO) return true;
        }
        return ideaLibrary.Find(idea.IdeaSO.IdeaName) != null;
    }

    // Places the idea here, loose in the scene so it can be picked up again
    public void Place(Idea idea)
    {
        if (idea == null) return;

        Transform point = dropPoint != null ? dropPoint : transform;

        idea.Release();
        idea.transform.position = point.position;
        idea.transform.rotation = point.rotation;
        idea.transform.localScale = dropScale;
        idea.Area = this;
        current = idea;

        Punch();
    }

    // Forgets the idea (picked up again or destroyed)
    public void Clear(Idea idea)
    {
        if (current != idea) return;
        if (current != null) current.Area = null;
        current = null;
    }

    // Small scale punch for feedback
    public void Punch()
    {
        if ((punchTime <= 0.0f) || Mathf.Approximately(punchScale, 1.0f)) return;

        transform.Tween().Stop("DropAreaPunch", Tweener.StopBehaviour.Cancel);
        transform.localScale = baseScale;
        transform.LocalScaleTo(baseScale * punchScale, punchTime * 0.5f, "DropAreaPunch").Done(() =>
        {
            transform.LocalScaleTo(baseScale, punchTime * 0.5f, "DropAreaPunch");
        });
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
