using System.Collections.Generic;
using UC;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

// An idea travelling out of the IdeaMachine: moves from a start to an end position over a set time, then waits
// there. It never removes itself; whatever takes the idea destroys it, and the machine spawns the next one.
// The machine calls Setup and Launch right after instantiating the prefab. The player highlights the nearest
// idea in range (screen space outline plus a balloon with its name) and can grab it, which parents it to the
// player's hold point.
public partial class Idea : MonoBehaviour
{
    [SerializeField, Tooltip("Set by the IdeaMachine on spawn; can be left empty on the prefab")]
    private IdeaSO  ideaSO;

    [Header("Highlight")]
    [SerializeField, Tooltip("Outline component to drive; created on this object if left empty")]
    private ScreenSpaceOutline  outline;
    [SerializeField] private Color highlightColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);
    [SerializeField, Min(0)] private float highlightWidth = 3.0f;

    [Header("Balloon")]
    [SerializeField, Tooltip("Where the name balloon points at, relative to the idea (world axes)")]
    private Vector3 balloonOffset = new Vector3(0.0f, 0.5f, 0.0f);

    public IdeaSO   IdeaSO => ideaSO;
    public bool     IsMoving => moving;
    public bool     IsHeld => held;

    // Drop area currently holding this idea (null while loose or carried)
    public DropArea Area { get; set; }

    [AutoStaticsCleanup]
    static List<Idea>   ideas = new List<Idea>();

    Vector3 baseScale = Vector3.one;    // Scale the prefab came with, restored on pickup
    Vector3 startPos;
    Vector3 endPos;
    float   duration;
    float   elapsed;
    bool    moving;
    bool    held;
    SpeechBalloon balloon;   // Name balloon, up while highlighted

    void Awake()
    {
        baseScale = transform.localScale;
    }

    void OnEnable()
    {
        ideas.Add(this);
    }

    void OnDisable()
    {
        ideas.Remove(this);
        if (outline != null) outline.enabled = false;
        HideBalloon();
    }

    void OnDestroy()
    {
        if (Area != null) Area.Clear(this);
    }

    void Start()
    {
        if (outline == null) outline = GetComponent<ScreenSpaceOutline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<ScreenSpaceOutline>();
            outline.outlineColor = highlightColor;
            outline.outlineWidth = highlightWidth;
            outline.Refresh();
        }
        outline.enabled = false;
    }

    // Closest idea to a position (XZ plane) that isn't held by anyone, or null if none is within range
    public static Idea GetNearest(Vector3 position, float range)
    {
        Idea    best = null;
        float   bestDist = range;

        foreach (var idea in ideas)
        {
            if ((idea == null) || idea.held) continue;

            float d = Vector3.Distance(idea.transform.position.x0z(), position.x0z());
            if (d < bestDist)
            {
                bestDist = d;
                best = idea;
            }
        }
        return best;
    }

    public void Setup(IdeaSO so)
    {
        ideaSO = so;
    }

    // Positions are in world space; the idea stops once it reaches the end
    public void Launch(Vector3 start, Vector3 end, float travelTime)
    {
        startPos = start;
        endPos = end;
        duration = travelTime;
        elapsed = 0.0f;
        moving = true;
        transform.position = startPos;
    }

    // Stops the movement without destroying the idea
    public void Stop()
    {
        moving = false;
    }

    // Outline plus a balloon with the idea's name
    public void SetHighlight(bool on)
    {
        if ((outline != null) && (outline.enabled != on)) outline.enabled = on;

        if (on) ShowBalloon();
        else HideBalloon();
    }

    void ShowBalloon()
    {
        if ((balloon != null) || (ideaSO == null)) return;
        balloon = SpeechBalloonManager.Show(ideaSO.DisplayName, transform, balloonOffset);
    }

    void HideBalloon()
    {
        if (balloon == null) return;
        balloon.Hide();
        balloon = null;
    }

    // Attaches the idea to a holder (e.g. the player's hold point), centered on it
    public void Grab(Transform holder)
    {
        moving = false;
        held = true;
        SetHighlight(false);
        if (Area != null) Area.Clear(this);

        transform.SetParent(holder, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = baseScale;
    }

    // Detaches the idea, leaving it where it currently is
    public void Release()
    {
        held = false;
        transform.SetParent(null, true);
    }

    void Update()
    {
        if (!moving) return;

        elapsed += Time.deltaTime;
        float t = duration > 0.0f ? Mathf.Clamp01(elapsed / duration) : 1.0f;
        transform.position = Vector3.Lerp(startPos, endPos, t);

        if (t >= 1.0f) moving = false;
    }
}
