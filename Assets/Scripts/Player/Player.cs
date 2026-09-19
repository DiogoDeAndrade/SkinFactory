using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    MovementDirectionalXZ   movement;
    Minigame                currentMinigame;
    Animator                animator;
    Idea                    nearbyIdea;     // Idea currently highlighted (in picking range)
    DropArea                nearbyDropArea; // Drop area the player is standing in (outline lit)

    static int workingID = Animator.StringToHash("Working");

    // Station the player is currently working at (null when walking around)
    public Minigame activeMinigame => currentMinigame;

    [Header("Skin")]
    [SerializeField, Tooltip("Where the voxel-extruded skin is built once a painting exists")]
    private VoxelSkin   skin;

    [Header("Interaction")]
    [SerializeField, Min(0), Tooltip("Distance (XZ) within which an idea is highlighted and can be grabbed")]
    private float       pickRange = 1.5f;
    [SerializeField, ShowIf(nameof(needNewInputSystem))]
    private PlayerInput playerInput;
    [SerializeField, InputPlayer(nameof(playerInput)), Tooltip("Pressing it grabs the highlighted idea"), InputButton]
    private UC.InputControl interactInput;
    [SerializeField, Tooltip("Where a grabbed idea is parented (on top of the player)")]
    private Transform   holdPoint;

    public bool needNewInputSystem => (interactInput != null) && (interactInput.type == UC.InputControl.InputType.NewInput);

    // Interact key currently held (stations that need a hold, e.g. the release console)
    public bool interactHeld => (interactInput != null) && interactInput.IsPressed();

    // Idea the player is carrying (null when not carrying one)
    public Idea heldIdea { get; private set; }

    // Movement and idea interaction switched off (a station that reads the keyboard, e.g. the coding one)
    public bool controlsLocked { get; private set; }
    public bool isMoving => (movement != null) && movement.isMoving;

    public void LockControls(bool locked)
    {
        if (controlsLocked == locked) return;
        controlsLocked = locked;

        if (movement == null) movement = GetComponent<MovementDirectionalXZ>();
        if (movement != null) movement.SetActive(!locked);
    }

    [Header("Debug")]
    [SerializeField, Tooltip("Use the debug starting stage below; off = a normal start, the fields are kept for later")]
    private bool        debugMode = false;
    [SerializeField, Tooltip("Start already carrying this drawing (a PNG saved by the concept station), skipping that station"), ShowIf(nameof(debugMode))]
    private Texture2D   debugConceptDrawing;
    [SerializeField, Tooltip("Concept the debug drawing belongs to (used for the segment hint)"), ShowIf(nameof(debugMode))]
    private ConceptSO   debugConceptSource;
    [SerializeField, Tooltip("Start already carrying this model (saved by the modelling station), skipping the concept and modelling stations. Overrides the debug drawing above."), ShowIf(nameof(debugMode))]
    private ModelDataSO debugModel;
    [SerializeField, Tooltip("Start already carrying this painting (a PNG saved by the painting station), skipping all three stations. Needs the debug model above for the earlier data."), ShowIf(nameof(debugMode))]
    private Texture2D   debugPainting;
    [SerializeField, Tooltip("Skip the briefing and brainstorm: every day starts straight in production with a random concept"), ShowIf(nameof(debugMode))]
    private bool        debugSkipBrainstorm = false;

    // A debug starting stage is on, so the first day should keep it instead of resetting the pipeline
    public bool debugStageActive => debugMode && ((debugConceptDrawing != null) || (debugModel != null) || (debugPainting != null));

    // Debug: the day skips the briefing and brainstorm and starts in production (brainstorm scored as a placeholder)
    public bool skipBrainstorm => debugMode && debugSkipBrainstorm;

    // Drawing produced in the concept art station (owned by the player, destroyed on replace)
    public Texture2D    conceptDrawing { get; private set; }
    public ConceptSO    conceptDrawingSource { get; private set; }

    // Model produced in the modelling station: polygons in normalized [0,1] coordinates over the concept drawing
    public Vector2[][]  modelPolygons { get; private set; }

    public void SetModel(Vector2[][] polygons)
    {
        modelPolygons = polygons;
    }

    // Station scores in [0,1] for the launch results, -1 while that station has not been done this day
    public int          brainstormStars { get; private set; } = -1;    // Already in stars (1-5), set when the pitch lands
    public float        conceptScore { get; private set; } = -1.0f;
    public float        modelScore { get; private set; } = -1.0f;
    public float        paintingScore { get; private set; } = -1.0f;
    public float        codingAccuracy { get; private set; } = -1.0f;   // Right characters over the snippet length
    public int          marketingStars { get; private set; } = -1;     // Already in stars (1-5), set on marketing submit
    public bool         hasCode => codingAccuracy >= 0.0f;

    public void SetBrainstormStars(int stars) { brainstormStars = Mathf.Clamp(stars, 0, 5); }
    public void SetConceptScore(float score) { conceptScore = Mathf.Clamp01(score); }
    public void SetModelScore(float score) { modelScore = Mathf.Clamp01(score); }
    public void SetPaintingScore(float score) { paintingScore = Mathf.Clamp01(score); }
    public void SetCodingAccuracy(float accuracy) { codingAccuracy = Mathf.Clamp01(accuracy); }
    public void SetMarketingStars(int stars) { marketingStars = Mathf.Clamp(stars, 0, 5); }

    // Painting produced in the painting station: palette colors per pixel, transparent outside the skin
    public Texture2D    painting { get; private set; }

    public void SetPainting(Texture2D painting)
    {
        if ((this.painting != null) && (this.painting != painting)) Destroy(this.painting);

        this.painting = painting;

        if (skin == null) skin = FindAnyObjectByType<VoxelSkin>();
        if (skin != null) skin.Build(painting);
        else Debug.LogWarning("Player: no VoxelSkin in the scene, the skin was not built", this);
    }

    // Moves the player to a point (new day), through the rigidbody so physics does not drag it back
    public void Teleport(Transform point)
    {
        if (point == null) return;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = point.position;
            rb.rotation = point.rotation;
        }
        transform.SetPositionAndRotation(point.position, point.rotation);
    }

    // Drops everything carried (new day): drawing, model, painting and the built skin
    public void ResetPipeline()
    {
        if (conceptDrawing != null) Destroy(conceptDrawing);
        conceptDrawing = null;
        conceptDrawingSource = null;
        modelPolygons = null;
        if (painting != null) Destroy(painting);
        painting = null;
        brainstormStars = -1;
        conceptScore = -1.0f;
        modelScore = -1.0f;
        paintingScore = -1.0f;
        codingAccuracy = -1.0f;
        marketingStars = -1;
        if (skin == null) skin = FindAnyObjectByType<VoxelSkin>();
        if (skin != null) skin.Clear();
    }

    public void SetConceptDrawing(Texture2D drawing, ConceptSO source)
    {
        if ((conceptDrawing != null) && (conceptDrawing != drawing)) Destroy(conceptDrawing);

        conceptDrawing = drawing;
        conceptDrawingSource = source;
    }

    private void Start()
    {
        animator = GetComponentInChildren<Animator>();
        movement = GetComponent<MovementDirectionalXZ>();

        if (interactInput != null) interactInput.playerInput = playerInput;
        if (holdPoint == null) holdPoint = transform;

        if (!debugMode) return;

        if (debugConceptDrawing != null)
        {
            SetConceptDrawing(TextureUtils.ReadableCopy(debugConceptDrawing, "Debug concept drawing"), debugConceptSource);
        }
        if (debugModel != null)
        {
            if (debugModel.Drawing != null) SetConceptDrawing(TextureUtils.ReadableCopy(debugModel.Drawing, "Debug concept drawing"), debugModel.Concept);
            SetModel(debugModel.GetPolygons());
        }
        if (debugPainting != null)
        {
            SetPainting(TextureUtils.ReadableCopy(debugPainting, "Debug painting"));
        }
    }

    void Update()
    {
        if (movement.isMoving)
        {
            DisableMinigame();
        }
        else
        {
            EnableMinigame();
        }

        animator.SetBool(workingID, (currentMinigame != null));

        // Space is a character while a keyboard station has the controls
        if (!controlsLocked) UpdateIdeaInteraction();
    }

    // Highlights the nearest idea in range and the drop area the player stands in, if it takes the carried idea.
    // The interaction key drops the carried idea into the area (trash destroys it), or grabs the nearby idea.
    void UpdateIdeaInteraction()
    {
        Idea nearest = (heldIdea == null) ? Idea.GetNearest(transform.position, pickRange) : null;

        if (nearbyIdea != nearest)
        {
            if (nearbyIdea != null) nearbyIdea.SetHighlight(false);
            nearbyIdea = nearest;
            if (nearbyIdea != null) nearbyIdea.SetHighlight(true);
        }

        // Only an area the carried idea can actually be dropped into counts (and lights up)
        DropArea area = (heldIdea != null) ? DropArea.GetAt(transform.position, heldIdea) : null;

        if (nearbyDropArea != area)
        {
            if (nearbyDropArea != null) nearbyDropArea.SetHighlight(false);
            nearbyDropArea = area;
            if (nearbyDropArea != null) nearbyDropArea.SetHighlight(true);
        }

        if ((interactInput == null) || !interactInput.IsDown()) return;

        if ((heldIdea != null) && (nearbyDropArea != null))
        {
            DropIdeaInto(nearbyDropArea);
        }
        else if (nearbyIdea != null)
        {
            GrabIdea(nearbyIdea);
        }
    }

    // Trash destroys the carried idea; other areas take it if it is in their library, swapping with whatever
    // they already hold. Returns false when the area does not accept the idea.
    public bool DropIdeaInto(DropArea area)
    {
        if ((heldIdea == null) || (area == null)) return false;
        if (!area.Accepts(heldIdea)) return false;

        if (area.IsTrash)
        {
            Idea trashed = heldIdea;
            heldIdea = null;
            Destroy(trashed.gameObject);
            area.Punch();
            return true;
        }

        Idea previous = area.Current;
        Idea dropped = heldIdea;
        heldIdea = null;

        if (previous != null) area.Clear(previous);
        area.Place(dropped);

        if (previous != null) GrabIdea(previous);

        return true;
    }

    public void GrabIdea(Idea idea)
    {
        if (idea == null) return;
        if (heldIdea == idea) return;

        DropIdea();

        heldIdea = idea;
        heldIdea.Grab(holdPoint);

        if (nearbyIdea == idea) nearbyIdea = null;
    }

    // Lets go of the carried idea, leaving it where it is
    public void DropIdea()
    {
        if (heldIdea == null) return;

        heldIdea.Release();
        heldIdea = null;
    }

    // Destroys the carried idea (new day)
    public void DiscardIdea()
    {
        if (heldIdea == null) return;

        Idea idea = heldIdea;
        heldIdea = null;
        Destroy(idea.gameObject);
    }

    void EnableMinigame()
    {
        Minigame mg = Minigame.GetMinigame(this);
        if (mg)
        {
            if (currentMinigame != mg)
            {
                DisableMinigame();
                currentMinigame = mg;

                currentMinigame.Activate();
            }
        }
        else
        {
            DisableMinigame();
        }
    }

    void DisableMinigame()
    {
        currentMinigame?.Deactivate();
        currentMinigame = null;
    }

    void OnDestroy()
    {
        if (conceptDrawing != null) Destroy(conceptDrawing);
        if (painting != null) Destroy(painting);
    }
}
