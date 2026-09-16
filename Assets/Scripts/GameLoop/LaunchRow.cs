using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.UI;

// One line of the launch results: a category label and its stars. Starts hidden with every star dark;
// LaunchResults fades it in and lights the stars one at a time, each with a scale pop.
// How a star is lit depends on what the list points at, so prefabs keep their own colors:
//  - a star with a child named litChildName (StarBase root: outline + yellow "Star" child): the child is switched on
//  - a star that is itself the child of another image (the yellow "Star" child listed directly): it is switched on
//  - a standalone star image: tinted emptyColor while dark, back to its own color when lit
[RequireComponent(typeof(CanvasGroup))]
public class LaunchRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI    label;
    [SerializeField, Tooltip("Left to right")]
    private List<Image>                         stars = new List<Image>();

    [Header("Look")]
    [SerializeField, Tooltip("Child of a star switched on when it is earned, when the star has one")]
    private string  litChildName = "Star";
    [SerializeField, Tooltip("Tint of a standalone star image while it is not earned")]
    private Color   emptyColor = new Color(0.22f, 0.22f, 0.28f, 1.0f);
    [SerializeField, Min(1)] private float popScale = 1.4f;
    [SerializeField, Min(0)] private float popTime = 0.25f;

    public int starCount => stars.Count;

    enum Mode { Tint, ToggleSelf, ToggleChild }

    CanvasGroup     canvasGroup;
    List<Color>     ownColors = new List<Color>();   // Each star's color as authored, restored when lit

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        foreach (var star in stars) ownColors.Add((star != null) ? star.color : Color.white);
    }

    // Label set, every star dark, row invisible
    public void Setup(string text)
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (label != null) label.text = text;

        for (int i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            if (star == null) continue;

            switch (ModeFor(star, out GameObject toggled))
            {
                case Mode.ToggleChild:
                case Mode.ToggleSelf:
                    toggled.SetActive(false);
                    break;
                default:
                    star.color = emptyColor;
                    break;
            }
            star.transform.localScale = Vector3.one;
        }
        canvasGroup.alpha = 0.0f;
    }

    public Tweener.BaseInterpolator Show(float fadeTime) => canvasGroup.FadeIn(fadeTime);

    public void LightStar(int index)
    {
        if ((index < 0) || (index >= stars.Count) || (stars[index] == null)) return;

        var star = stars[index];
        switch (ModeFor(star, out GameObject toggled))
        {
            case Mode.ToggleChild:
            case Mode.ToggleSelf:
                toggled.SetActive(true);
                break;
            default:
                star.color = (index < ownColors.Count) ? ownColors[index] : Color.white;
                break;
        }

        if (popTime <= 0.0f) return;
        Transform t = star.transform;
        t.Tween().Stop("StarPop", Tweener.StopBehaviour.Cancel);
        t.localScale = Vector3.one;
        t.LocalScaleTo(Vector3.one * popScale, popTime * 0.5f, "StarPop").Done(() =>
        {
            t.LocalScaleTo(Vector3.one, popTime * 0.5f, "StarPop");
        });
    }

    Mode ModeFor(Image star, out GameObject toggled)
    {
        toggled = null;

        if (!string.IsNullOrEmpty(litChildName))
        {
            Transform child = star.transform.Find(litChildName);
            if (child != null)
            {
                toggled = child.gameObject;
                return Mode.ToggleChild;
            }
        }

        Transform parent = star.transform.parent;
        if ((parent != null) && (parent != transform) && (parent.GetComponent<Image>() != null))
        {
            toggled = star.gameObject;
            return Mode.ToggleSelf;
        }

        return Mode.Tint;
    }
}
