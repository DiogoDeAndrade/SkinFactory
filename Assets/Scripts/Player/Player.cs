using UnityEngine;

public class Player : MonoBehaviour
{
    MovementDirectionalXZ   movement;
    Minigame                currentMinigame;
    Animator                animator;

    static int workingID = Animator.StringToHash("Working");

    // Station the player is currently working at (null when walking around)
    public Minigame activeMinigame => currentMinigame;

    [Header("Skin")]
    [SerializeField, Tooltip("Where the voxel-extruded skin is built once a painting exists")]
    private VoxelSkin   skin;

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
