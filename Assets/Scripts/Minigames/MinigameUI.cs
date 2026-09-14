using UC;
using UnityEngine;

public abstract class MinigameUI : MonoBehaviour
{
    [SerializeField] private ConceptSO concept;

    CanvasGroup canvasGroup;

    protected virtual void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0.0f;

        Set(concept);
    }

    void Set(ConceptSO concept)
    {
        this.concept = concept;

        if (concept == null) return;
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
