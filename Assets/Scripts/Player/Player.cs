using UnityEngine;

public class Player : MonoBehaviour
{
    MovementDirectionalXZ   movement;
    Minigame                currentMinigame;
    Animator                animator;

    static int workingID = Animator.StringToHash("Working");

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
}
