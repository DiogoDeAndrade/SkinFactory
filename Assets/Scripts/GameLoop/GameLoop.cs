using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Main loop: one skin per day. A day starts with a concept to make, runs against a timer, and ends when the
// skin is released (next day) or the timer runs out (game over).
// The concept is picked here at day start for now; the brainstorm station will take that over through SetDayConcept.
public class GameLoop : MonoBehaviour
{
    public enum State { Running, GameOver }

    [Header("Concepts")]
    [SerializeField, Tooltip("Pool the day's skin is picked from")]
    private List<ConceptSO>     concepts = new List<ConceptSO>();
    [SerializeField, Tooltip("Concept station that receives the day's concept; found in the scene if left empty")]
    private ConceptMG           conceptStation;

    [Header("Day")]
    [SerializeField, Min(1)] private float dayDuration = 300.0f;    // Seconds

    [Header("UI references")]
    [SerializeField] private TextMeshProUGUI    timerText;      // Optional, "mm:ss"
    [SerializeField] private TextMeshProUGUI    dayText;        // Optional, "Day N"
    [SerializeField] private CanvasGroup        gameOverPanel;  // Shown on game over; wire its Retry button to Retry()
    [SerializeField] private TextMeshProUGUI    gameOverText;   // Optional, reason

    [Header("Events")]
    public UnityEvent<int>      onDayStarted;   // Day number
    public UnityEvent           onReleased;
    public UnityEvent<string>   onGameOver;     // Reason

    public static GameLoop instance { get; private set; }

    public State        state { get; private set; } = State.Running;
    public int          day { get; private set; }
    public float        timeLeft { get; private set; }
    public ConceptSO    currentConcept { get; private set; }

    Player  player;
    int     lastConceptIndex = -1;

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
        if (conceptStation == null) conceptStation = FindAnyObjectByType<ConceptMG>();

        HideGameOver();
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
        if (state != State.Running) return;

        timeLeft = Mathf.Max(0.0f, timeLeft - Time.deltaTime);
        UpdateTimerText();

        if (timeLeft <= 0.0f) GameOver("Time's up!");
    }

    #region Day flow

    public void StartDay()
    {
        day++;
        timeLeft = dayDuration;
        state = State.Running;
        Time.timeScale = 1.0f;

        // Fresh pipeline: nothing carried, every station back to its initial state
        if (player == null) player = FindAnyObjectByType<Player>();
        player?.ResetPipeline();
        var objects = FindObjectsByType<MinigameUI>();
        foreach (var station in objects)
        {
            station.ResetStation();
        }

        SetDayConcept(PickConcept());

        if (dayText) dayText.text = $"Day {day}";
        UpdateTimerText();
        onDayStarted?.Invoke(day);
    }

    // Sets what the player has to make today (the brainstorm station will call this once it exists)
    public void SetDayConcept(ConceptSO concept)
    {
        currentConcept = concept;

        if (conceptStation == null) conceptStation = FindAnyObjectByType<ConceptMG>();
        if (conceptStation != null) conceptStation.Set(concept);
        else Debug.LogWarning("GameLoop: no ConceptMG in the scene to receive the day's concept", this);
    }

    ConceptSO PickConcept()
    {
        if (concepts.Count == 0)
        {
            Debug.LogWarning("GameLoop: no concepts assigned", this);
            return null;
        }
        if (concepts.Count == 1) return concepts[0];

        // Random, avoiding yesterday's
        int index;
        do { index = Random.Range(0, concepts.Count); } while (index == lastConceptIndex);
        lastConceptIndex = index;
        return concepts[index];
    }

    // Called by the release station once a painting exists. Success is just completion for now.
    public void Release()
    {
        if (state != State.Running) return;
        if ((player == null) || (player.painting == null))
        {
            Debug.LogWarning("GameLoop: release requested without a finished skin", this);
            return;
        }

        onReleased?.Invoke();
        StartDay();
    }

    public void GameOver(string reason)
    {
        if (state == State.GameOver) return;

        state = State.GameOver;
        timeLeft = 0.0f;
        UpdateTimerText();
        Time.timeScale = 0.0f;

        if (gameOverText) gameOverText.text = reason;
        if (gameOverPanel)
        {
            gameOverPanel.alpha = 1.0f;
            gameOverPanel.interactable = true;
            gameOverPanel.blocksRaycasts = true;
        }

        onGameOver?.Invoke(reason);
    }

    // Wire the game over panel's Retry button here
    public void Retry()
    {
        Time.timeScale = 1.0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

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
