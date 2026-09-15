using UnityEngine;

public class Player : MonoBehaviour
{
    MovementDirectionalXZ   movement;
    Minigame                currentMinigame;
    Animator                animator;

    static int workingID = Animator.StringToHash("Working");

    // Drawing produced in the concept art station (owned by the player, destroyed on replace)
    public Texture2D    conceptDrawing { get; private set; }
    public ConceptSO    conceptDrawingSource { get; private set; }

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
        Minigame mg = Minigame.GetMinigame(transform);
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
    }
}
