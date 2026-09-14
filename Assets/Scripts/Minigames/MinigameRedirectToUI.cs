using UC;
using UnityEngine;

public partial class MinigameRedirectToUI : Minigame
{
    [SerializeField] Hypertag minigameUITag;

    MinigameUI actualMinigame;

    protected void Start()
    {
        actualMinigame = minigameUITag.FindFirst<MinigameUI>();
    }

    public override void Activate()
    {
        actualMinigame?.Activate();
    }

    public override void Deactivate()
    {
        actualMinigame?.Deactivate();
    }
}
