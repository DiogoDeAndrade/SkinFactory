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

    // Idea the player is carrying (null when not carrying one)
    public Idea heldIdea { get; private set; }

    [Header("Debug")]
    [SerializeField, Tooltip("Start already carrying this drawing (a PNG saved by the concept station), skipping that station")]
    private Texture2D   debugConceptDrawing;
    [SerializeField, Tooltip("Concept the debug drawing belongs to (used for the segment hint)")]
    private ConceptSO   debugConceptSource;
    [SerializeField, Tooltip("Start already carrying this model (saved by the modelling station), skipping the concept and modelling stations. Overrides the debug drawing above.")]
    private ModelDataSO debugModel;
    [SerializeField, Tooltip("Start already carrying this painting (a PNG saved by the painting station), skipping all three stations. Needs the debug model above for the earlier data.")]
    private Texture2D   debugPainting;

    // Drawing produced in the concept art station (owned by the player, destroyed on replace)
    public Texture2D    conceptDrawing { get; private set; }
    public ConceptSO    conceptDrawingSource { get; private set; }

    // Model produced in the modelling station: polygons in normalized [0,1] coordinates over the concept drawing
    public Vector2[][]  modelPolygons { get; private set; }

    public void SetModel(Vector2[][] polygons)
    {
        modelPolygons = polygons;
    }

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

    // Drops everything carried (new day): drawing, model, painting and the built skin
    public void ResetPipeline()
    {
        if (conceptDrawing != null) Destroy(conceptDrawing);
        conceptDrawing = null;
        conceptDrawingSource = null;
        modelPolygons = null;
        if (painting != null) Destroy(painting);
        painting = null;
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

        UpdateIdeaInteraction();
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

        // Only an area the carried idea can actually be dropped into lights up
        DropArea area = (heldIdea != null) ? DropArea.GetAt(transform.position) : null;
        if ((area != null) && !area.Accepts(heldIdea)) area = null;

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
