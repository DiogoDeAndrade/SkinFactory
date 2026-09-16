using System.Collections.Generic;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.UI;

// Marketing station, or "community management": social media posts about the skin bounce around the play area
// in short waves. The player left-clicks the negative ones to make them go away and leaves the positive ones.
// The live rating is the ratio of positive to negative posts still up in the current wave (no negatives = 5
// stars, 4:1 = 4, ..., 1:1 or worse = 1). When a wave ends every post vanishes and the next wave starts, so the
// rating is only ever about the wave on screen. Submit keeps the current rating; it unlocks after minProcessed
// clicks so the player has to play a little, and from then on it is push your luck: keep playing to improve the
// rating, or bank it. Waves get busier, faster and shorter as they go.
public class MarketingMG : MinigameUI
{
    [Header("UI references")]
    [SerializeField, Tooltip("Posts move inside this rect (anchored to its centre)")]
    private RectTransform   playArea;
    [SerializeField] private MarketingPost      postPrefab;
    [SerializeField] private Button             submitButton;   // Interactable once minProcessed posts were clicked
    [SerializeField, Tooltip("Optional: the rating as text, see ratingFormat")]
    private TextMeshProUGUI                     ratingText;
    [SerializeField, Tooltip("Optional: the rating on a star row (a StarMeter instance)")]
    private LaunchRow                           ratingRow;
    [SerializeField, Tooltip("Optional: filled image showing the time left in the wave")]
    private Image                               waveFill;

    [Header("Rating")]
    [SerializeField, Tooltip("{0} = the stars")]
    private string  ratingFormat = "Outreach: {0}";
    [SerializeField] private string fullStar = "★";
    [SerializeField] private string emptyStar = "☆";
    [SerializeField, Min(0), Tooltip("Posts the player has to click before Submit unlocks")]
    private int     minProcessed = 3;

    [Header("Waves")]
    [SerializeField, Min(1)] private int    startPosts = 5;
    [SerializeField, Min(1)] private int    maxPosts = 12;
    [SerializeField, Min(0), Tooltip("Extra posts per wave")]
    private int                             postsPerWave = 1;
    [SerializeField, Range(0.1f, 0.9f), Tooltip("Share of negative posts in a wave (at least one)")]
    private float                           negativeShare = 0.4f;
    [SerializeField, Min(0.5f)] private float startDuration = 6.0f;
    [SerializeField, Min(0.5f)] private float minDuration = 3.0f;
    [SerializeField, Min(0), Tooltip("Seconds taken off the wave duration per wave")]
    private float                           durationStep = 0.3f;
    [SerializeField] private Vector2        startSpeed = new Vector2(60.0f, 120.0f);   // Canvas units per second, min..max
    [SerializeField, Min(1.0f), Tooltip("Speed multiplier applied per wave")]
    private float                           speedGrowth = 1.08f;
    [SerializeField, Min(0), Tooltip("Seconds between the last post vanishing and the next wave")]
    private float                           waveGap = 0.4f;
    [SerializeField, Min(0)] private float  vanishTime = 0.15f;

    [Header("Posts")]
    [SerializeField, Tooltip("Positive posts, one per line (empty lines and lines starting with # are skipped)")]
    private TextAsset   positiveFile;
    [SerializeField, Tooltip("Negative posts, one per line")]
    private TextAsset   negativeFile;

    public int  stars => currentStars;
    public int  processed => processedCount;
    public bool isDone => marketingDone;
    public bool canSubmit => !marketingDone && (processedCount >= minProcessed);

    List<MarketingPost> posts = new List<MarketingPost>();
    List<string>        positivePosts = new List<string>();
    List<string>        negativePosts = new List<string>();
    Player  player;
    int     wave;               // Waves started since the last reset
    float   waveTimeLeft;
    float   gapLeft;
    float   waveDuration;
    int     processedCount;
    int     currentStars = 1;
    bool    running;
    bool    marketingDone;
    int     lastPositive = -1;
    int     lastNegative = -1;

    protected override void Start()
    {
        base.Start();
        if (player == null) player = FindAnyObjectByType<Player>();
        if (submitButton != null) submitButton.onClick.AddListener(Submit);
        LoadPosts();
        UpdateUI();
    }

    public override void ResetStation()
    {
        StopWaves();
        marketingDone = false;
        processedCount = 0;
        currentStars = 1;
        wave = 0;
        UpdateUI();
    }

    // Once per day
    public override bool CanUse(Player player)
    {
        if (marketingDone || (player == null)) return false;
        return (postPrefab != null) && (playArea != null) && (positivePosts.Count > 0) && (negativePosts.Count > 0);
    }

    public override void Activate()
    {
        base.Activate();
        if (player == null) player = FindAnyObjectByType<Player>();
        if (!marketingDone) StartWaves();
        UpdateUI();
    }

    // Walking off clears the board; the processed count and wave difficulty carry on when the player comes back
    public override void Deactivate()
    {
        base.Deactivate();
        StopWaves();
    }

    void Update()
    {
        if (!running) return;

        if (posts.Count > 0)
        {
            waveTimeLeft -= Time.deltaTime;
            if (waveFill != null) waveFill.fillAmount = (waveDuration > 0.0f) ? Mathf.Clamp01(waveTimeLeft / waveDuration) : 0.0f;

            if (waveTimeLeft <= 0.0f)
            {
                EndWave();
                gapLeft = waveGap;
            }
        }
        else
        {
            gapLeft -= Time.deltaTime;
            if (gapLeft <= 0.0f) SpawnWave();
        }
    }

    #region Waves

    void StartWaves()
    {
        if (running) return;
        running = true;
        gapLeft = 0.0f;
        SpawnWave();
    }

    void StopWaves()
    {
        running = false;
        ClearPosts(0.0f);
    }

    void SpawnWave()
    {
        ClearPosts(0.0f);

        int count = Mathf.Min(maxPosts, startPosts + wave * postsPerWave);
        int negatives = Mathf.Clamp(Mathf.RoundToInt(count * negativeShare), 1, count - 1);
        waveDuration = Mathf.Max(minDuration, startDuration - wave * durationStep);
        waveTimeLeft = waveDuration;
        float speedScale = Mathf.Pow(speedGrowth, wave);
        wave++;

        Vector2 half = playArea.rect.size * 0.5f;
        for (int i = 0; i < count; i++)
        {
            bool negative = i < negatives;
            string text = negative ? Pick(negativePosts, ref lastNegative) : Pick(positivePosts, ref lastPositive);
            if (string.IsNullOrEmpty(text)) continue;

            Vector2 position = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            float speed = Random.Range(startSpeed.x, startSpeed.y) * speedScale;
            Vector2 velocity = Random.insideUnitCircle.normalized * speed;
            if (velocity.sqrMagnitude < 0.01f) velocity = Vector2.right * speed;

            MarketingPost post = Instantiate(postPrefab, playArea);
            post.name = negative ? "Post (bad)" : "Post (good)";
            post.Setup(this, playArea, text, negative, position, velocity);
            posts.Add(post);
        }

        RefreshRating();
        UpdateUI();
    }

    // Survivors vanish; the rating shown stays as it was until the next wave replaces it
    void EndWave()
    {
        ClearPosts(vanishTime);
    }

    void ClearPosts(float time)
    {
        foreach (var post in posts)
        {
            if (post != null) post.Vanish(time);
        }
        posts.Clear();
    }

    // One post per line; blank lines and # comments are skipped
    void LoadPosts()
    {
        positivePosts = ReadLines(positiveFile);
        negativePosts = ReadLines(negativeFile);
        if ((positivePosts.Count == 0) || (negativePosts.Count == 0))
        {
            Debug.LogWarning("MarketingMG: assign the positive and negative post files", this);
        }
    }

    static List<string> ReadLines(TextAsset file)
    {
        var result = new List<string>();
        if (file == null) return result;

        foreach (var raw in file.text.Split((char)10))
        {
            string line = raw.Trim();
            if ((line.Length == 0) || line.StartsWith("#")) continue;
            result.Add(line);
        }
        return result;
    }

    string Pick(List<string> pool, ref int last)
    {
        if ((pool == null) || (pool.Count == 0)) return null;
        if (pool.Count == 1) return pool[0];

        int index;
        do { index = Random.Range(0, pool.Count); } while (index == last);
        last = index;
        return pool[index];
    }

    #endregion

    #region Rating

    // Called by a post on left click. Negative: good riddance. Positive: a mistake, and it still goes.
    public void PostClicked(MarketingPost post)
    {
        if (!running || marketingDone || (post == null) || !post.isAlive) return;

        post.Kill(!post.isNegative);
        posts.Remove(post);
        processedCount++;

        RefreshRating();
        UpdateUI();
    }

    // Positive to negative ratio of the posts still up: no negatives = 5, otherwise the whole ratio, 1 to 5.
    // Nothing positive left is 1: there is nobody left to hear the marketing.
    void RefreshRating()
    {
        int good = 0;
        int bad = 0;
        foreach (var post in posts)
        {
            if ((post == null) || !post.isAlive) continue;
            if (post.isNegative) bad++;
            else good++;
        }

        if (good == 0) currentStars = 1;
        else if (bad == 0) currentStars = 5;
        else currentStars = Mathf.Clamp(good / bad, 1, 5);
    }

    public void Submit()
    {
        if (!canSubmit) return;

        marketingDone = true;
        StopWaves();

        if (player == null) player = FindAnyObjectByType<Player>();
        if (player != null) player.SetMarketingStars(currentStars);
        else Debug.LogWarning("MarketingMG: no Player found to store the marketing result", this);

        UpdateUI();
        canvasGroup.FadeOut(0.1f);
    }

    #endregion

    void UpdateUI()
    {
        if (ratingText != null)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 5; i++) sb.Append((i < currentStars) ? fullStar : emptyStar);
            ratingText.text = string.Format(ratingFormat, sb.ToString());
        }
        if (ratingRow != null) ratingRow.SetStars(currentStars);
        if (submitButton != null) submitButton.interactable = canSubmit;
        if ((waveFill != null) && (posts.Count == 0)) waveFill.fillAmount = 0.0f;
    }
}
