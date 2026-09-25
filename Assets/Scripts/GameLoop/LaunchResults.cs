using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.UI;

// The "Skin Launched!" results screen. Fades in with only the title, then one row per category: the row appears
// with dark stars and the stars earned light up one at a time. Last comes the profit, counting up from zero on a
// bar scaled to the target: the fill and the profit marker grow toward the target marker at the end. Once the profit
// passes the target the target marker pops and the bar is scaled to the profit instead: it stays full, the profit
// marker stays at the end and the target marker slides back. The fill and the amount are colored by a gradient over
// profit / target. The caller gets the outcome once the screen has been read.
// Lives on the Launch panel (a vertical layout): rows are instantiated from the prefab and inserted after
// rowsAfter (the first separator).
[RequireComponent(typeof(CanvasGroup))]
public class LaunchResults : MonoBehaviour
{
    [Serializable]
    public struct Category
    {
        public string   label;
        public int      stars;

        public Category(string label, int stars)
        {
            this.label = label;
            this.stars = stars;
        }
    }

    [Header("References")]
    [SerializeField, Tooltip("Shown first, on its own")]
    private TextMeshProUGUI titleText;
    [SerializeField]
    private LaunchRow       rowPrefab;
    [SerializeField, Tooltip("Rows are inserted right after this sibling; right before the profit row if left empty")]
    private Transform       rowsAfter;
    [SerializeField, Tooltip("Row holding the profit label and bar, hidden until the profit is shown")]
    private GameObject      profitRoot;
    [SerializeField, Tooltip("Filled (horizontal) image the profit fills; the markers travel along its width")]
    private Image           profitFill;
    [SerializeField, Tooltip("Follows the fill's edge until the target is reached, then stays at the end. Only its x is driven")]
    private RectTransform   profitMarker;
    [SerializeField, Tooltip("The amount, inside the profit marker")]
    private TextMeshProUGUI profitText;
    [SerializeField, Tooltip("At the end of the bar until the profit passes it, then slides back. Only its x is driven")]
    private RectTransform   targetMarker;
    [SerializeField, Tooltip("Today's target, inside the target marker")]
    private TextMeshProUGUI targetText;

    [Header("Profit")]
    [SerializeField, Min(0), Tooltip("Dollars per star")]
    private int             starValue = 800;
    [SerializeField, Min(0), Tooltip("Random extra per star, 0 to this")]
    private int             starBonusMax = 100;
    [SerializeField, Min(0), Tooltip("Profit needed to survive the day, unless Show is given a target (LevelManager raises it every day)")]
    private int             profitTarget = 10000;
    [SerializeField, Tooltip("How the target is written; {0} = the value")]
    private string          targetFormat = "${0:N0}";
    [SerializeField, Tooltip("How the amount is written; {0} = the value")]
    private string          profitFormat = "${0:N0}";
    [SerializeField, Tooltip("Color of the fill and the amount; left = no profit, right = profit of gradientRange x the target")]
    private Gradient        profitGradient = DefaultGradient();
    [SerializeField, Min(0.01f), Tooltip("Profit, as a multiple of the target, at the right end of the gradient (1 = the target; past it the color holds)")]
    private float           gradientRange = 1.0f;
    [SerializeField, Min(1), Tooltip("Scale of the target marker's pop when the profit passes it")]
    private float           targetPopScale = 1.4f;
    [SerializeField, Min(0)] private float targetPopTime = 0.25f;

    [Header("Timing")]
    [SerializeField, Min(0)] private float fadeTime = 0.4f;
    [SerializeField, Min(0), Tooltip("Title alone before the first row")]
    private float           titleHold = 0.8f;
    [SerializeField, Min(0), Tooltip("Row visible before its first star lights")]
    private float           rowDelay = 0.35f;
    [SerializeField, Min(0), Tooltip("Between two stars of the same row")]
    private float           starDelay = 0.25f;
    [SerializeField, Min(0), Tooltip("After a row's last star, before the next row")]
    private float           afterRowDelay = 0.3f;
    [SerializeField, Min(0), Tooltip("Seconds the profit takes to count up")]
    private float           profitDuration = 2.0f;
    [SerializeField, Min(0), Tooltip("Everything shown, before the outcome is reported")]
    private float           endHold = 1.5f;

    public bool     isShowing { get; private set; }
    public int      profit { get; private set; }
    public bool     success => profit >= profitTarget;

    CanvasGroup             canvasGroup;
    List<LaunchRow>         rows = new List<LaunchRow>();
    Coroutine               showCR;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0.0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // The profit row (label plus bar) is not part of the layout until its turn
        if (profitRoot != null) profitRoot.SetActive(false);
    }

    // Plays the whole sequence, then reports success (profit reached the target) and the profit.
    // A target of 0 or less keeps the one set on this component.
    public void Show(IList<Category> categories, Action<bool, int> onDone, int target = 0)
    {
        if (target > 0) profitTarget = target;
        if (targetText != null) targetText.text = string.Format(targetFormat, profitTarget);

        if (showCR != null) StopCoroutine(showCR);
        showCR = StartCoroutine(ShowCR(categories, onDone));
    }

    public void Hide(float time)
    {
        if (showCR != null) StopCoroutine(showCR);
        showCR = null;
        isShowing = false;

        if (time > 0.0f) canvasGroup.FadeOut(time);
        else
        {
            canvasGroup.Tween().Stop("CanvasAlpha", Tweener.StopBehaviour.Cancel);
            canvasGroup.alpha = 0.0f;
        }
    }

    IEnumerator ShowCR(IList<Category> categories, Action<bool, int> onDone)
    {
        isShowing = true;
        ClearRows();

        // Title alone
        if (titleText != null) titleText.alpha = 1.0f;
        if (profitRoot != null) profitRoot.SetActive(false);
        if (targetMarker != null)
        {
            targetMarker.Tween().Stop("TargetPop", Tweener.StopBehaviour.Cancel);
            targetMarker.localScale = Vector3.one;
        }
        canvasGroup.FadeIn(fadeTime);
        yield return new WaitForSeconds(fadeTime + titleHold);

        // Rows, one at a time, stars one at a time
        int totalStars = 0;
        foreach (var category in categories)
        {
            LaunchRow row = CreateRow(category.label);
            if (row == null) continue;

            row.Show(fadeTime);
            yield return new WaitForSeconds(fadeTime + rowDelay);

            int stars = Mathf.Clamp(category.stars, 0, row.starCount);
            for (int i = 0; i < stars; i++)
            {
                row.LightStar(i);
                totalStars++;
                yield return new WaitForSeconds(starDelay);
            }
            yield return new WaitForSeconds(afterRowDelay);
        }

        // Profit: each star is worth starValue plus a random bonus
        profit = 0;
        for (int i = 0; i < totalStars; i++) profit += starValue + UnityEngine.Random.Range(0, starBonusMax + 1);

        if (profitRoot != null)
        {
            profitRoot.SetActive(true);
            var group = profitRoot.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0.0f;
                group.FadeIn(fadeTime);
            }
        }
        // The bar needs its laid out size before the markers are placed on it
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        SetProfit(0);

        float elapsed = 0.0f;
        int shown = 0;
        while (shown < profit)
        {
            elapsed += Time.deltaTime;
            float t = (profitDuration > 0.0f) ? Mathf.Clamp01(elapsed / profitDuration) : 1.0f;
            int value = Mathf.RoundToInt(profit * t);
            if ((shown < profitTarget) && (value >= profitTarget)) PopTarget();
            shown = value;
            SetProfit(shown);
            yield return null;
        }

        yield return new WaitForSeconds(endHold);

        showCR = null;
        onDone?.Invoke(success, profit);
    }

    // Up to the target the bar is scaled to the target: the fill and the profit marker grow toward the target
    // marker at the end. Past it the bar is scaled to the profit: full, profit marker at the end, target sliding back.
    void SetProfit(int value)
    {
        float ratio = (profitTarget > 0) ? (float)value / profitTarget : 1.0f;
        float profitPos = Mathf.Clamp01(ratio);
        float targetPos = (ratio > 1.0f) ? (1.0f / ratio) : 1.0f;
        Color color = profitGradient.Evaluate(Mathf.Clamp01(ratio / gradientRange));

        if (profitFill != null)
        {
            profitFill.fillAmount = profitPos;
            profitFill.color = color;
        }
        if (profitText != null)
        {
            profitText.text = string.Format(profitFormat, value);
            profitText.color = color;
        }
        PlaceOnBar(profitMarker, profitPos);
        PlaceOnBar(targetMarker, targetPos);
    }

    // Moves a marker horizontally to t along the fill's rect (0 = left end, 1 = right end); its y stays as authored
    void PlaceOnBar(RectTransform marker, float t)
    {
        if ((marker == null) || (profitFill == null)) return;

        RectTransform bar = profitFill.rectTransform;
        Rect rect = bar.rect;
        Vector3 world = bar.TransformPoint(new Vector3(Mathf.Lerp(rect.xMin, rect.xMax, t), rect.center.y, 0.0f));

        Vector3 pos = marker.localPosition;
        pos.x = marker.parent.InverseTransformPoint(world).x;
        marker.localPosition = pos;
    }

    void PopTarget()
    {
        if ((targetMarker == null) || (targetPopTime <= 0.0f)) return;

        Transform t = targetMarker;
        t.Tween().Stop("TargetPop", Tweener.StopBehaviour.Cancel);
        t.localScale = Vector3.one;
        t.LocalScaleTo(Vector3.one * targetPopScale, targetPopTime * 0.5f, "TargetPop").Done(() =>
        {
            t.LocalScaleTo(Vector3.one, targetPopTime * 0.5f, "TargetPop");
        });
    }

    // Red, through yellow, to green at the target
    static Gradient DefaultGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.0f, 0.3f, 0.3f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.85f, 0.2f), 0.75f),
                new GradientColorKey(new Color(0.3f, 1.0f, 0.3f), 1.0f),
            },
            new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) });
        return gradient;
    }

    LaunchRow CreateRow(string label)
    {
        if (rowPrefab == null)
        {
            Debug.LogWarning("LaunchResults: no row prefab assigned", this);
            return null;
        }

        LaunchRow row = Instantiate(rowPrefab, transform);
        row.name = $"Row {label}";
        if (rowsAfter != null) row.transform.SetSiblingIndex(rowsAfter.GetSiblingIndex() + 1 + rows.Count);
        else if (profitRoot != null) row.transform.SetSiblingIndex(profitRoot.transform.GetSiblingIndex());
        row.Setup(label);
        rows.Add(row);
        return row;
    }

    void ClearRows()
    {
        foreach (var row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }
        rows.Clear();
    }

    // 100% = 5 stars, rounding up; the epsilon keeps 3/5 from becoming 4 through float error
    public static int StarsFor(float score, int maxStars = 5)
    {
        if (score < 0.0f) return 0;
        return Mathf.Clamp(Mathf.CeilToInt(score * maxStars - 0.001f), 0, maxStars);
    }
}
