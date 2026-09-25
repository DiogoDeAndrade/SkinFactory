using System.Collections;
using UC;
using UnityEngine;

public abstract class MinigameUI : MonoBehaviour
{
    [Header("Prompt")]
    [SerializeField] protected CanvasGroup  promptGroup;            // "Draw", "Code", ... card with the input device icon; optional
    [SerializeField] protected float        promptFadeTime = 0.25f;
    [SerializeField] protected float        promptDisplayTime = 1.5f;

    protected CanvasGroup canvasGroup;

    bool        promptShown;    // Only the first time the minigame runs each day (cleared by ResetStation / ResetPrompt)
    Coroutine   promptCR;

    protected bool isPromptRunning => promptCR != null;

    // There is a prompt card and it hasn't been shown today (for a subclass running the card itself, see ConceptMG)
    protected bool promptPending => (promptGroup != null) && !promptShown;
    protected void MarkPromptShown() { promptShown = true; }

    protected virtual void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0.0f;

        if (promptGroup != null) promptGroup.alpha = 0.0f;
    }    

    public virtual void Activate()
    {
        canvasGroup.FadeIn(0.15f);
    }

    public virtual void Deactivate()
    {
        HidePrompt();
        canvasGroup.FadeOut(0.15f);
    }

    public virtual bool CanUse(Player player) => true;

    // Back to the initial state for a new day (nothing done, nothing carried over). Overrides must call base.
    public virtual void ResetStation()
    {
        ResetPrompt();
    }

    // The prompt card shows again on the next activation (new day, or a new thing to work on)
    protected void ResetPrompt()
    {
        HidePrompt();
        promptShown = false;
    }

    // Shows the prompt card (first time only), then runs onDone. If there's nothing to show, onDone runs right away.
    // Deactivate cancels a prompt in progress; it plays again next time.
    protected void ShowPrompt(System.Action onDone)
    {
        HidePrompt();

        if ((promptGroup == null) || promptShown)
        {
            onDone?.Invoke();
            return;
        }

        promptCR = StartCoroutine(ShowPromptCR(onDone));
    }

    IEnumerator ShowPromptCR(System.Action onDone)
    {
        yield return PromptCR();
        promptCR = null;
        onDone?.Invoke();
    }

    // Fade in, wait, fade out
    protected IEnumerator PromptCR()
    {
        if ((promptGroup == null) || promptShown) yield break;

        promptGroup.alpha = 0.0f;
        promptGroup.FadeIn(promptFadeTime);
        yield return new WaitForSeconds(promptFadeTime + promptDisplayTime);

        promptGroup.FadeOut(promptFadeTime);
        yield return new WaitForSeconds(promptFadeTime);

        promptShown = true;
    }

    protected void HidePrompt()
    {
        if (promptCR != null)
        {
            StopCoroutine(promptCR);
            promptCR = null;
        }
        if ((promptGroup != null) && (promptGroup.alpha > 0.0f)) promptGroup.FadeOut(promptFadeTime);
    }
}
