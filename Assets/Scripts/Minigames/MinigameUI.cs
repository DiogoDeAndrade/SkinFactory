using UC;
using UnityEngine;

public abstract class MinigameUI : MonoBehaviour
{
    CanvasGroup canvasGroup;

    protected virtual void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0.0f;
    }

    public virtual void Activate()
    {
        canvasGroup.FadeIn(0.15f);
    }

    public virtual void Deactivate()
    {
        canvasGroup.FadeOut(0.15f);
    }
}
