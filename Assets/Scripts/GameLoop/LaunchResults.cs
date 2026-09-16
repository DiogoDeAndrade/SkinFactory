using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;

// The "Skin Launched!" results screen. Fades in with only the title, then one row per category: the row appears
// with dark stars and the stars earned light up one at a time. Last comes the profit, counting up from zero, red
// until it reaches the target and green from there. The caller gets the outcome once the screen has been read.
// Lives on the Launch panel (a vertical layout): rows are instantiated from the prefab and inserted after
// rowsAfter (the first separator), the profit text is cloned from the title if none is assigned.
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
    [SerializeField, Tooltip("Rows are inserted right after this sibling; at the end of the panel if left empty")]
    private Transform       rowsAfter;
    [SerializeField, Tooltip("Profit line; a copy of the title at the bottom of the panel if left empty")]
    private TextMeshProUGUI profitText;
    [SerializeField, Tooltip("Object hidden until the profit is shown (the row holding the label and the amount); the profit text's parent if left empty")]
    private GameObject      profitRoot;

    [Header("Profit")]
    [SerializeField, Min(0), Tooltip("Dollars per star")]
    private int             starValue = 800;
    [SerializeField, Min(0), Tooltip("Random extra per star, 0 to this")]
    private int             starBonusMax = 100;
    [SerializeField, Min(0), Tooltip("Profit needed to survive the day, unless Show is given a target (LevelManager raises it every day)")]
    private int             profitTarget = 10000;
    [SerializeField, Tooltip("Optional: today's target, see targetFormat")]
    private TextMeshProUGUI targetText;
    [SerializeField, Tooltip("How the target is written; {0} = the value")]
    private string          targetFormat = "Target: ${0:N0}";
    [SerializeField, Tooltip("How the amount is written; {0} = the value")]
    private string          profitFormat = "${0:N0}";
    [SerializeField] private Color lossColor = new Color(1.0f, 0.3f, 0.3f, 1.0f);
    [SerializeField] private Color profitColor = new Color(0.3f, 1.0f, 0.3f, 1.0f);

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

        if ((profitText == null) && (titleText != null))
        {
            profitText = Instantiate(titleText, titleText.transform.parent);
            profitText.name = "Profit";
            profitText.transform.SetAsLastSibling();
        }
        if (profitText != null) profitText.alpha = 0.0f;

        // The profit row (label plus amount) is not part of the layout until its turn
        if ((profitRoot == null) && (profitText != null) && (profitText.transform.parent != transform))
        {
            profitRoot = profitText.transform.parent.gameObject;
        }
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
        if (profitText != null) profitText.alpha = 0.0f;
        if (profitRoot != null) profitRoot.SetActive(false);
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

        if (profitText != null)
        {
            SetProfitText(0);
            profitText.alpha = 1.0f;
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

            float elapsed = 0.0f;
            while (elapsed < profitDuration)
            {
                elapsed += Time.deltaTime;
                float t = (profitDuration > 0.0f) ? Mathf.Clamp01(elapsed / profitDuration) : 1.0f;
                SetProfitText(Mathf.RoundToInt(profit * t));
                yield return null;
            }
            SetProfitText(profit);
        }

        yield return new WaitForSeconds(endHold);

        showCR = null;
        onDone?.Invoke(success, profit);
    }

    void SetProfitText(int value)
    {
        profitText.text = string.Format(profitFormat, value);
        profitText.color = (value >= profitTarget) ? profitColor : lossColor;
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
        else if (profitText != null) row.transform.SetSiblingIndex(profitText.transform.GetSiblingIndex());
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
