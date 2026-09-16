using System.Collections.Generic;
using UC;
using UnityEngine;

// Spawns one Idea prefab at a time and sends it from the start to the end position, where it waits. Ideas are
// never removed by the machine or by themselves; once something else destroys the current one, the next is spawned. Ideas are drawn from the list without replacement;
// the bag refills once empty.
public class IdeaMachine : MonoBehaviour
{
    [Header("Ideas")]
    [SerializeField, Tooltip("Library (imported *.idea file) the ideas are drawn from; the list below is used when this is empty")]
    private IdeaLibrarySO   library;
    [SerializeField, Tooltip("Pool the ideas are drawn from (without replacement, refilled when empty)")]
    private List<IdeaSO>    ideas = new List<IdeaSO>();
    [SerializeField]
    private Idea            ideaPrefab;

    [Header("Path")]
    [SerializeField, Tooltip("Where ideas appear; this transform if left empty")]
    private Transform       startPoint;
    [SerializeField, Tooltip("Where ideas end up and wait to be taken")]
    private Transform       endPoint;
    [SerializeField, Min(0), Tooltip("Seconds the idea takes to travel from start to end")]
    private float           travelTime = 3.0f;
    [SerializeField, Min(0), Tooltip("Seconds to wait after an idea is gone before spawning the next one")]
    private float           spawnDelay = 0.0f;
    [SerializeField, Tooltip("Spawn the first idea on Start")]
    private bool            spawnOnStart = true;

    [Header("Highlight")]
    [SerializeField, Tooltip("Outline lit while the player should be using the machine (brainstorm); created on this object if left empty")]
    private ScreenSpaceOutline  outline;
    [SerializeField] private Color highlightColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
    [SerializeField, Min(0)] private float highlightWidth = 3.0f;

    public Idea             CurrentIdea => current;

    Idea            current;
    List<IdeaSO>    bag = new List<IdeaSO>();
    float           delayLeft;

    void Start()
    {
        if (outline == null) outline = GetComponentInChildren<ScreenSpaceOutline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<ScreenSpaceOutline>();
            outline.outlineColor = highlightColor;
            outline.outlineWidth = highlightWidth;
            outline.Refresh();
        }
        outline.enabled = false;

        if (spawnOnStart) Spawn();
        else delayLeft = spawnDelay;
    }

    void OnDisable()
    {
        if (outline != null) outline.enabled = false;
    }

    public void SetHighlight(bool on)
    {
        if ((outline != null) && (outline.enabled != on)) outline.enabled = on;
    }

    void Update()
    {
        // Unity's overloaded == also catches a destroyed idea
        if (current != null) return;

        delayLeft -= Time.deltaTime;
        if (delayLeft <= 0.0f) Spawn();
    }

    // Spawns the next idea immediately, replacing any current one
    public void Spawn()
    {
        if (current != null) Destroy(current.gameObject);
        current = null;
        delayLeft = spawnDelay;

        if (ideaPrefab == null)
        {
            Debug.LogWarning("IdeaMachine: no idea prefab assigned", this);
            return;
        }
        if (endPoint == null)
        {
            Debug.LogWarning("IdeaMachine: no end point assigned", this);
            return;
        }

        IdeaSO so = Draw();
        if (so == null)
        {
            Debug.LogWarning("IdeaMachine: no ideas to draw from", this);
            return;
        }

        Transform origin = startPoint != null ? startPoint : transform;

        current = Instantiate(ideaPrefab, origin.position, origin.rotation);
        current.name = $"{ideaPrefab.name} ({so.name})";
        current.Setup(so);
        current.Launch(origin.position, endPoint.position, travelTime);
    }

    // Without replacement: refills the bag from the idea list once it runs dry
    IdeaSO Draw()
    {
        if (bag.Count == 0)
        {
            IEnumerable<IdeaSO> source = (library != null) ? library.Ideas : ideas;
            foreach (var idea in source)
            {
                if (idea != null) bag.Add(idea);
            }
            if (bag.Count == 0) return null;
        }

        int index = Random.Range(0, bag.Count);
        IdeaSO so = bag[index];
        bag.RemoveAt(index);
        return so;
    }

    void OnDrawGizmosSelected()
    {
        if (endPoint == null) return;

        Vector3 from = startPoint != null ? startPoint.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, endPoint.position);
        Gizmos.DrawWireSphere(endPoint.position, 0.1f);
    }
}
