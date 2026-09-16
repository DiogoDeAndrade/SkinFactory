using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Game flow. One skin per day:
//  1. Briefing: the boss camera comes on and the boss asks for two tags from the tag list
//  2. Brainstorm: the timer runs; the player fills the pitch drop areas with ideas. Once all are full the boss
//     judges the pitch (minMatchingIdeas of them must carry a requested tag). A rejected pitch has to be taken
//     apart before it can be pitched again; after maxAttempts rejections the boss picks the concept himself
//  3. Production: concept, modelling, painting, release
//  4. Launch: the results screen scores each station in stars and turns them into profit; enough profit starts
//     the next day, too little is game over. Timer out at any point = game over
// The timer only runs while the player is working (brainstorm and production), never while the boss talks.
public class LevelManager : MonoBehaviour
{
    public enum State { Briefing, Brainstorm, Verdict, Production, Launch, GameOver }

    [Header("Boss")]
    [SerializeField, Tooltip("Camera framing the boss; its GameObject is switched on while he talks and off otherwise")]
    private Camera              bossCamera;
    [SerializeField, Tooltip("What the boss balloon points at; this object if left empty")]
    private Transform           bossAnchor;
    [SerializeField, Tooltip("World-space offset from the anchor to the balloon tip")]
    private Vector3             bossBalloonOffset = new Vector3(0.0f, 1.8f, 0.0f);
    [SerializeField, Min(0), Tooltip("Seconds the boss camera stays on before the balloon appears and after it goes")]
    private float               bossPause = 0.5f;
    [SerializeField, Min(0), Tooltip("Seconds a line stays up when there is no balloon system to time it")]
    private float               fallbackTalkTime = 3.0f;
    [SerializeField, Min(0), Tooltip("Seconds between two lines said in a row (the thinking beat before a verdict)")]
    private float               lineGap = 0.6f;

    [SerializeField, Min(0), Tooltip("Distance (XZ) from the boss anchor within which the boss repeats the request during the brainstorm")]
    private float               bossTalkRange = 2.5f;

    [Header("Day transition")]
    [SerializeField, Min(0), Tooltip("Fullscreen wipe out / in around a day change (needs a FullscreenWiper in the scene)")]
    private float               dayWipeTime = 0.5f;
    [SerializeField]
    private WipeType            dayWipeType = WipeType.Random;

    [Header("Boss lines")]
    [SerializeField, Tooltip("{0} = the requested tags, e.g. \"Cute and Scary\"")]
    private string              requestLine = "I want something {0}!";
    [SerializeField, Tooltip("Said while weighing a pitch, before the verdict. {nouns}, {styles}, {gimmicks} (the pitch areas' library names) or {0}, {1}, {2} (pitch area order) = the pitched ideas")]
    private string              thinkLine = "{nouns} {styles} {gimmicks}?";
    [SerializeField]
    private string              rejectLine = "Your idea is rubbish!";
    [SerializeField, Tooltip("{0} = the concept the boss picks")]
    private string              giveUpLine = "All your ideas are rubbish. Let's do {0}!";
    [SerializeField, Tooltip("{0} = the concept the boss picks")]
    private string              pivotLine = "Let's pivot from that, we'll do {0} instead!";
    [SerializeField, TextArea, Tooltip("Said on game over, before the panel comes up")]
    private string              firedLine = "You're fired!\nWe have no place for slackers!";

    [Header("Pitch")]
    [SerializeField, Tooltip("Tags the boss can ask for, one per line; the union of the pitch areas' libraries if left empty")]
    private TextAsset           tagList;
    [SerializeField, Tooltip("Drop areas that make up the pitch; every non-trash area in the scene if left empty")]
    private List<DropArea>      pitchAreas = new List<DropArea>();
    [SerializeField, Min(1), Tooltip("How many tags the boss asks for")]
    private int                 requestedTagCount = 2;
    [SerializeField, Min(1), Tooltip("How many of the pitched ideas must fit the request")]
    private int                 minMatchingIdeas = 2;
    [SerializeField, Tooltip("An idea fits when it has every requested tag (on) or at least one of them (off)")]
    private bool                ideaNeedsAllTags = false;
    [SerializeField, Min(1), Tooltip("Rejections before the boss picks the concept himself")]
    private int                 maxAttempts = 3;

    [Header("Concepts")]
    [SerializeField, Tooltip("Pool the day's skin is picked from")]
    private List<ConceptSO>     concepts = new List<ConceptSO>();
    [SerializeField, Tooltip("Concept station that receives the day's concept; found in the scene if left empty")]
    private ConceptMG           conceptStation;

    [Header("Day")]
    [SerializeField, Min(1)] private float dayDuration = 300.0f;    // Seconds
    [SerializeField, Tooltip("Where the player starts each day; left where they are if empty")]
    private Transform           playerSpawn;

    [Header("UI references")]
    [SerializeField] private TextMeshProUGUI    timerText;      // Optional, "mm:ss"
    [SerializeField] private TextMeshProUGUI    dayText;        // Optional, "Day N"
    [SerializeField] private TextMeshProUGUI    requestText;    // Optional, what the boss asked for today
    [SerializeField] private LaunchResults      launchResults;  // Results screen after a release; skipped if empty
    [SerializeField, Min(0), Tooltip("Stars for categories without a score yet (coding, marketing; every missing station in a debug start)")]
    private int                                 placeholderStars = 3;
    [SerializeField] private CanvasGroup        gameOverPanel;  // Shown on game over; wire its Retry button to Retry()
    [SerializeField] private TextMeshProUGUI    gameOverText;   // Optional, reason

    [Header("Events")]
    public UnityEvent<int>          onDayStarted;       // Day number
    public UnityEvent               onPitchAccepted;
    public UnityEvent<int>          onPitchRejected;    // Rejections so far today
    public UnityEvent<ConceptSO>    onConceptChosen;
    public UnityEvent               onReleased;
    public UnityEvent<string>       onGameOver;         // Reason

    public static LevelManager instance { get; private set; }

    public State                    state { get; private set; } = State.Briefing;
    public int                      day { get; private set; }
    public float                    timeLeft { get; private set; }
    public int                      attempts { get; private set; }
    public ConceptSO                currentConcept { get; private set; }
    public IReadOnlyList<string>    requestedTags => requested;

    // The timer only runs while the player is working, not while the boss talks or the day changes
    public bool timerRunning => ((state == State.Brainstorm) || (state == State.Production)) && !transitioning;

    Player          player;
    IdeaMachine[]   machines = new IdeaMachine[0];   // Outlined during the brainstorm
    bool            machinesLit;
    List<string>    requested = new List<string>();
    List<DropArea>  areas = new List<DropArea>();
    Coroutine       flowCR;
    bool            transitioning;      // Wiping between days
    SpeechBalloon   reminderBalloon;    // Request repeated over the boss while the player stands next to him
    bool            pitchDirty;     // Something changed since the last verdict, so a full pitch gets judged again
    int             lastConceptIndex = -1;

    void Awake()
    {
        if ((instance != null) && (instance != this))
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        player = FindAnyObjectByType<Player>();
        machines = FindObjectsByType<IdeaMachine>();
        if (conceptStation == null) conceptStation = FindAnyObjectByType<ConceptMG>();
        if (bossAnchor == null) bossAnchor = transform;

        HideGameOver();
        SetBossCamera(false);
        StartCoroutine(FirstDayCR());
    }

    // Wait a frame so every station has run its Start before the day resets them
    IEnumerator FirstDayCR()
    {
        yield return null;
        StartDay();
    }

    void Update()
    {
        if (state == State.GameOver) return;

        if (timerRunning)
        {
            timeLeft = Mathf.Max(0.0f, timeLeft - Time.deltaTime);
            UpdateTimerText();

            if (timeLeft <= 0.0f)
            {
                GameOver("Time's up!");
                return;
            }
        }

        if (state == State.Brainstorm) CheckPitch();
        UpdateReminder();
        UpdateMachineHighlight();
    }

    // The idea machines are outlined while the player is meant to be pitching
    void UpdateMachineHighlight()
    {
        bool lit = (state == State.Brainstorm);
        if (lit == machinesLit) return;
        machinesLit = lit;

        foreach (var machine in machines)
        {
            if (machine != null) machine.SetHighlight(lit);
        }
    }

    #region Day flow

    public void StartDay()
    {
        if (flowCR != null) StopCoroutine(flowCR);
        flowCR = null;
        HideReminder();

        day++;
        timeLeft = dayDuration;
        attempts = 0;
        pitchDirty = true;
        Time.timeScale = 1.0f;

        // Fresh pipeline: nothing carried, every station back to its initial state, no concept until the boss picks one.
        // A debug starting stage on the player (day one only) is kept, and the day jumps straight to production.
        if (player == null) player = FindAnyObjectByType<Player>();
        bool debugStart = (day == 1) && (player != null) && player.debugStageActive;
        if ((player != null) && (playerSpawn != null)) player.Teleport(playerSpawn);
        if (!debugStart)
        {
            player?.ResetPipeline();
            player?.DiscardIdea();
        }
        var stations = FindObjectsByType<MinigameUI>();
        foreach (var station in stations)
        {
            station.ResetStation();
        }
        SetDayConcept(null);
        ClearPitch();

        PickRequest();

        if (dayText) dayText.text = $"Day {day}";
        UpdateTimerText();
        onDayStarted?.Invoke(day);

        if (debugStart)
        {
            ConceptSO concept = (player.conceptDrawingSource != null) ? player.conceptDrawingSource : PickConcept();
            StartProduction(concept);
            return;
        }

        flowCR = StartCoroutine(BriefingCR());
    }

    IEnumerator BriefingCR()
    {
        state = State.Briefing;
        yield return BossSaysCR(RequestText());
        state = State.Brainstorm;
        flowCR = null;
    }

    // Judges the pitch once every area holds an idea. After a verdict at least one area has to be emptied before
    // the next pitch counts, so the player has to take a rejected idea apart rather than leave it there.
    void CheckPitch()
    {
        bool full = true;
        foreach (var area in areas)
        {
            if ((area == null) || !area.HasIdea)
            {
                full = false;
                pitchDirty = true;
            }
        }
        if ((areas.Count == 0) || !full || !pitchDirty) return;

        pitchDirty = false;
        flowCR = StartCoroutine(VerdictCR());
    }

    IEnumerator VerdictCR()
    {
        state = State.Verdict;

        // The boss reads the pitch back (a beat of thinking), then judges it in the same breath
        string think = PitchText();

        int matching = CountMatchingIdeas();
        if (matching >= minMatchingIdeas)
        {
            ConceptSO concept = PickConcept();
            if (player != null) player.SetBrainstormStars(BrainstormStars(matching));
            onPitchAccepted?.Invoke();
            yield return BossSaysCR(think, string.Format(pivotLine, ConceptName(concept)));
            StartProduction(concept);
        }
        else
        {
            attempts++;
            onPitchRejected?.Invoke(attempts);

            if (attempts >= maxAttempts)
            {
                // The pitch never landed: one star
                ConceptSO concept = PickConcept();
                if (player != null) player.SetBrainstormStars(1);
                yield return BossSaysCR(think, string.Format(giveUpLine, ConceptName(concept)));
                StartProduction(concept);
            }
            else
            {
                yield return BossSaysCR(think, rejectLine);
                state = State.Brainstorm;
            }
        }
        flowCR = null;
    }

    void StartProduction(ConceptSO concept)
    {
        ClearPitch();
        SetDayConcept(concept);
        state = State.Production;
    }

    // Sets what the player has to make today (null = nothing yet, the concept station stays unusable)
    public void SetDayConcept(ConceptSO concept)
    {
        currentConcept = concept;

        if (conceptStation == null) conceptStation = FindAnyObjectByType<ConceptMG>();
        if (conceptStation != null) conceptStation.Set(concept);
        else if (concept != null) Debug.LogWarning("LevelManager: no ConceptMG in the scene to receive the day's concept", this);

        if (concept != null) onConceptChosen?.Invoke(concept);
    }

    // Called by the release station once a painting exists. Success is just completion for now.
    public void Release()
    {
        if (state != State.Production) return;
        if ((player == null) || (player.painting == null))
        {
            Debug.LogWarning("LevelManager: release requested without a finished skin", this);
            return;
        }

        onReleased?.Invoke();

        if (launchResults == null)
        {
            NextDay();
            return;
        }

        // Clock stopped, controls off, results up; the screen reports back once it has been read
        state = State.Launch;
        HideReminder();
        if (player != null) player.LockControls(true);
        launchResults.Show(BuildLaunchCategories(), OnLaunchDone);
    }

    void OnLaunchDone(bool success, int profit)
    {
        if (state != State.Launch) return;

        if (success)
        {
            NextDay();
        }
        else
        {
            launchResults.Hide(dayWipeTime);
            GameOver($"Not enough profit (${profit:N0})");
        }
    }

    // Stars per category from the day's station scores; placeholders where there is no score yet
    List<LaunchResults.Category> BuildLaunchCategories()
    {
        bool debug = (player != null) && player.debugStageActive;
        int Stars(float score) => (score >= 0.0f) ? LaunchResults.StarsFor(score) : (debug ? placeholderStars : 0);

        int brainstorm = (player != null) ? player.brainstormStars : -1;
        if (brainstorm < 0) brainstorm = debug ? placeholderStars : 0;

        return new List<LaunchResults.Category>
        {
            new LaunchResults.Category("Brainstorm", brainstorm),
            new LaunchResults.Category("Concept",   Stars((player != null) ? player.conceptScore : -1.0f)),
            new LaunchResults.Category("Modelling", Stars((player != null) ? player.modelScore : -1.0f)),
            new LaunchResults.Category("Texturing", Stars((player != null) ? player.paintingScore : -1.0f)),
            new LaunchResults.Category("Coding",    placeholderStars),
            new LaunchResults.Category("Marketing", placeholderStars),
        };
    }

    // Next day behind a wipe when there is a wiper, straight away otherwise
    void NextDay()
    {
        if (FullscreenWiper.hasWiper && (dayWipeTime > 0.0f))
        {
            StartCoroutine(DayTransitionCR());
        }
        else
        {
            if (launchResults != null) launchResults.Hide(0.0f);
            StartDay();
            if (player != null) player.LockControls(false);
        }
    }

    // Wipe out, new day under the cover, wipe back in. Runs outside flowCR: StartDay stops that one.
    IEnumerator DayTransitionCR()
    {
        transitioning = true;
        if (player != null) player.LockControls(true);

        bool covered = false;
        FullscreenWiper.WipeOut(dayWipeTime, dayWipeType, () => covered = true);
        while (!covered) yield return null;

        if (launchResults != null) launchResults.Hide(0.0f);
        StartDay();
        transitioning = false;

        bool revealed = false;
        FullscreenWiper.WipeIn(dayWipeTime, dayWipeType, () => revealed = true);
        while (!revealed) yield return null;

        if (player != null) player.LockControls(false);
    }

    // Timer stops, the boss fires the player on camera, then the panel comes up and time freezes
    public void GameOver(string reason)
    {
        if (state == State.GameOver) return;

        if (flowCR != null) StopCoroutine(flowCR);
        flowCR = null;
        HideReminder();

        state = State.GameOver;
        timeLeft = 0.0f;
        UpdateTimerText();
        onGameOver?.Invoke(reason);

        flowCR = StartCoroutine(GameOverCR(reason));
    }

    IEnumerator GameOverCR(string reason)
    {
        if (player != null) player.LockControls(true);

        // The camera stays on the boss behind the panel
        yield return BossSpeechCR(false, firedLine);

        Time.timeScale = 0.0f;

        if (gameOverText) gameOverText.text = reason;
        if (gameOverPanel)
        {
            gameOverPanel.alpha = 1.0f;
            gameOverPanel.interactable = true;
            gameOverPanel.blocksRaycasts = true;
        }
        flowCR = null;
    }

    // Wire the game over panel's Retry button here
    public void Retry()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    #endregion

    #region Boss

    // While brainstorming, standing next to the boss makes him repeat the request (no camera switch); the balloon
    // goes when the player walks off or the state changes
    void UpdateReminder()
    {
        bool near = (state == State.Brainstorm) && (player != null) && (bossAnchor != null) &&
                    (Vector3.Distance(player.transform.position.x0z(), bossAnchor.position.x0z()) < bossTalkRange);

        if (near && (reminderBalloon == null))
        {
            reminderBalloon = SpeechBalloonManager.Show(RequestText(), bossAnchor, bossBalloonOffset);
        }
        else if (!near)
        {
            HideReminder();
        }
    }

    void HideReminder()
    {
        if (reminderBalloon == null) return;
        reminderBalloon.Hide();
        reminderBalloon = null;
    }

    string RequestText() => string.Format(requestLine, JoinTags(requested));

    // Camera on, each line up for its reading time (a gap between them), camera off. Empty lines are skipped.
    IEnumerator BossSaysCR(params string[] lines) => BossSpeechCR(true, lines);

    // Same, with the choice of leaving the camera on the boss afterwards
    IEnumerator BossSpeechCR(bool cameraOffAfter, params string[] lines)
    {
        HideReminder();
        SetBossCamera(true);
        yield return new WaitForSeconds(bossPause);

        bool first = true;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!first) yield return new WaitForSeconds(lineGap);
            first = false;

            SpeechBalloon balloon = SpeechBalloonManager.Show(line, bossAnchor, bossBalloonOffset);
            float talkTime = (balloon != null) ? balloon.ReadTime : fallbackTalkTime;
            yield return new WaitForSeconds(talkTime);
            if (balloon != null) balloon.Hide();
        }

        yield return new WaitForSeconds(bossPause);
        if (cameraOffAfter) SetBossCamera(false);
    }

    // The pitch read back as one line: {0}, {1}... by pitch area order, {nouns}, {styles}... by the area's library name
    string PitchText()
    {
        string text = thinkLine;
        for (int i = 0; i < areas.Count; i++)
        {
            DropArea area = areas[i];
            IdeaSO idea = ((area != null) && (area.Current != null)) ? area.Current.IdeaSO : null;
            string name = (idea != null) ? idea.DisplayName : "";

            text = text.Replace("{" + i + "}", name);
            if ((area != null) && (area.Library != null))
            {
                text = Regex.Replace(text, Regex.Escape("{" + area.Library.name + "}"), name.Replace("$", "$$"), RegexOptions.IgnoreCase);
            }
        }
        return text;
    }

    void SetBossCamera(bool on)
    {
        if (bossCamera == null)
        {
            if (on) Debug.LogWarning("LevelManager: no boss camera assigned", this);
            return;
        }
        if (bossCamera.gameObject.activeSelf != on) bossCamera.gameObject.SetActive(on);
    }

    #endregion

    #region Pitch

    // Picks the day's tags (distinct, random) from the tag list or, failing that, the pitch areas' libraries
    void PickRequest()
    {
        requested.Clear();
        RefreshAreas();

        List<string> pool = GetTagPool();
        int count = Mathf.Min(requestedTagCount, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            requested.Add(pool[index]);
            pool.RemoveAt(index);
        }

        if (requested.Count == 0) Debug.LogWarning("LevelManager: no tags to ask for (assign a tag list or libraries to the drop areas)", this);
        if (requestText) requestText.text = JoinTags(requested);
    }

    List<string> GetTagPool()
    {
        var pool = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        if (tagList != null)
        {
            foreach (var line in tagList.text.Split('\n'))
            {
                string tag = line.Trim();
                if ((tag.Length > 0) && seen.Add(tag)) pool.Add(tag);
            }
        }
        if (pool.Count > 0) return pool;

        foreach (var area in areas)
        {
            if ((area == null) || (area.Library == null)) continue;
            foreach (var tag in area.Library.GetAllTags())
            {
                if (seen.Add(tag)) pool.Add(tag);
            }
        }
        return pool;
    }

    // The serialized list, or every non-trash drop area in the scene
    void RefreshAreas()
    {
        areas.Clear();
        foreach (var area in pitchAreas)
        {
            if ((area != null) && !area.IsTrash) areas.Add(area);
        }
        if (areas.Count > 0) return;

        foreach (var area in DropArea.All)
        {
            if ((area != null) && !area.IsTrash) areas.Add(area);
        }
    }

    // Stars for an accepted pitch: 5, minus one per earlier rejection, minus one when not every idea matched,
    // minus one when a requested tag was left uncovered (three cute ideas for "Cute and Scary"). Never below 1.
    int BrainstormStars(int matching)
    {
        int stars = 5 - attempts;
        if (matching < areas.Count) stars--;
        if (!AllRequestedTagsCovered()) stars--;
        return Mathf.Clamp(stars, 1, 5);
    }

    bool AllRequestedTagsCovered()
    {
        foreach (var tag in requested)
        {
            bool covered = false;
            foreach (var area in areas)
            {
                IdeaSO idea = ((area != null) && (area.Current != null)) ? area.Current.IdeaSO : null;
                if ((idea != null) && idea.HasTag(tag))
                {
                    covered = true;
                    break;
                }
            }
            if (!covered) return false;
        }
        return true;
    }

    int CountMatchingIdeas()
    {
        int count = 0;
        foreach (var area in areas)
        {
            if ((area == null) || (area.Current == null) || (area.Current.IdeaSO == null)) continue;

            IdeaSO idea = area.Current.IdeaSO;
            bool fits = ideaNeedsAllTags ? idea.HasAllTags(requested) : idea.HasAnyTag(requested);
            if (fits) count++;
        }
        return count;
    }

    // Destroys whatever sits in the pitch areas
    void ClearPitch()
    {
        RefreshAreas();
        foreach (var area in areas)
        {
            if ((area == null) || (area.Current == null)) continue;
            Destroy(area.Current.gameObject);
        }
        pitchDirty = true;
    }

    // "A", "A and B", "A, B and C"
    static string JoinTags(List<string> tags)
    {
        if (tags.Count == 0) return "";
        if (tags.Count == 1) return tags[0];

        var sb = new StringBuilder();
        for (int i = 0; i < tags.Count; i++)
        {
            if (i > 0) sb.Append((i == tags.Count - 1) ? " and " : ", ");
            sb.Append(tags[i]);
        }
        return sb.ToString();
    }

    #endregion

    #region Concepts

    ConceptSO PickConcept()
    {
        if (concepts.Count == 0)
        {
            Debug.LogWarning("LevelManager: no concepts assigned", this);
            return null;
        }
        if (concepts.Count == 1) return concepts[0];

        // Random, avoiding the last one
        int index;
        do { index = Random.Range(0, concepts.Count); } while (index == lastConceptIndex);
        lastConceptIndex = index;
        return concepts[index];
    }

    static string ConceptName(ConceptSO concept) => (concept != null) ? concept.DisplayName : "something";

    #endregion

    void HideGameOver()
    {
        if (gameOverPanel == null) return;

        gameOverPanel.alpha = 0.0f;
        gameOverPanel.interactable = false;
        gameOverPanel.blocksRaycasts = false;
    }

    void UpdateTimerText()
    {
        if (timerText == null) return;

        int total = Mathf.CeilToInt(timeLeft);
        timerText.text = $"{total / 60:00}:{total % 60:00}";
    }
}
