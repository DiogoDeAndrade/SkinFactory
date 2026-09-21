using System.Collections.Generic;
using System.Text;
using TMPro;
using UC;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Coding station: the player retypes a code snippet against the clock. Characters turn green when right and red
// when wrong; backspace takes the last one back, or the player can push on and leave the mistake behind.
// The time limit is built from the snippet (a base, so much per character, extra for newlines and indentation)
// and only runs while the player is typing. When it runs out, or the snippet is finished, the score is handed to
// the Player: right characters count 1, wrong ones -wrongPenalty, untyped ones 0, over the snippet length.
// Player controls are locked while typing, since WASD and space are just characters here; Escape leaves the
// station, and it stays unusable until the player walks off so it doesn't grab them straight back.
// One snippet per day, picked on reset from the code files and inline snippets.
public class CodingMG : MinigameUI
{
    [Header("Code")]
    [SerializeField, Tooltip("Code files the day's snippet is picked from")]
    private List<TextAsset>     codeFiles = new List<TextAsset>();
    [SerializeField, TextArea(4, 12), Tooltip("Inline snippets, added to the pool above")]
    private List<string>        codeSnippets = new List<string>();
    [SerializeField, Min(1), Tooltip("Spaces a tab in the snippet (or a Tab press) stands for")]
    private int                 tabSize = 4;

    [Header("UI references")]
    [SerializeField] private TextMeshProUGUI    codeText;       // The snippet, colored as the player types (rich text on, no wrap)
    [SerializeField] private TextMeshProUGUI    accuracyText;   // Optional, score and progress
    [SerializeField, Tooltip("Optional: filled image showing the time left")]
    private Image                               timeFill;
    [SerializeField, Tooltip("Optional: the stars earned so far on a star row (a StarMeter instance)")]
    private LaunchRow                           starRow;
    [SerializeField] private Button             submitButton;   // Optional, interactable once the snippet is fully typed
    [SerializeField, Tooltip("Played when the snippet is submitted (the rising code characters); optional")]
    private ParticleSystem                      submitParticles;

    [Header("Colors")]
    [SerializeField] private Color  pendingColor = new Color(0.55f, 0.6f, 0.65f, 1.0f);
    [SerializeField] private Color  correctColor = new Color(0.3f, 1.0f, 0.3f, 1.0f);
    [SerializeField] private Color  wrongColor = new Color(1.0f, 0.25f, 0.25f, 1.0f);
    [SerializeField, Tooltip("Highlight behind wrong characters, so a wrong space still shows")]
    private Color                   wrongMarkColor = new Color(1.0f, 0.2f, 0.2f, 0.35f);
    [SerializeField, Tooltip("Highlight on the character to type next")]
    private Color                   caretMarkColor = new Color(1.0f, 1.0f, 1.0f, 0.3f);
    [SerializeField, Tooltip("Color of the character to type next, so it stands out from the pending text and its highlight")]
    private Color                   caretColor = Color.white;
    [SerializeField, Tooltip("Underline the character to type next")]
    private bool                    caretUnderline = true;

    [Header("Time limit")]
    [SerializeField, Min(0), Tooltip("Seconds given regardless of the snippet")]
    private float               baseTime = 3.0f;
    [SerializeField, Min(0), Tooltip("Seconds per character of the snippet")]
    private float               timePerCharacter = 0.5f;
    [SerializeField, Min(0), Tooltip("Extra seconds per line break (Enter)")]
    private float               extraTimePerNewline = 1.0f;
    [SerializeField, Min(0), Tooltip("Extra seconds per tab's worth of indentation at the start of a line")]
    private float               extraTimePerIndent = 0.5f;

    [Header("Scoring")]
    [SerializeField, Min(0), Tooltip("A wrong character counts this much against the score (a right one counts 1, an untyped one 0)")]
    private float               wrongPenalty = 0.5f;

    [Header("Rules")]
    [SerializeField, Tooltip("Submit on its own once the last character is typed")]
    private bool                autoSubmit = true;
    [SerializeField, Min(0), Tooltip("Seconds after the last character before the auto submit (backspacing cancels it)")]
    private float               autoSubmitDelay = 0.75f;
    [SerializeField, Min(0)] private float backspaceRepeatDelay = 0.4f;
    [SerializeField, Min(0)] private float backspaceRepeatRate = 0.04f;

    // Score in [0,1] over the whole snippet: right characters 1, wrong -wrongPenalty, untyped 0
    public float    accuracy => ComputeScore();
    public int      stars => LaunchResults.StarsFor(accuracy);
    public bool     isComplete => typed.Count >= target.Length;
    public float    timeLeft { get; private set; }
    public float    timeLimit { get; private set; }
    public bool     isDone => codeDone;
    public string   snippet => target;

    enum CharState { Pending, Correct, Wrong, Caret }

    string      target = "";
    List<char>  typed = new List<char>();   // What the player typed, aligned to the target by index
    int         correctCount;
    Player      player;
    Keyboard    keyboard;                   // The one we subscribed to, so we can unsubscribe from the same
    bool        typingEnabled;
    bool        codeDone;
    bool        leaving;                    // Escape pressed: unusable until the player walks away
    bool        textDirty;
    float       autoSubmitLeft;
    float       backspaceTimer;
    int         lastSnippetIndex = -1;

    protected override void Start()
    {
        base.Start();

        if (player == null) player = FindAnyObjectByType<Player>();
        if (codeText != null)
        {
            codeText.richText = true;
            codeText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        PickSnippet();
        UpdateUI();
        StopParticles();
    }

    void OnDisable()
    {
        StopTyping();
    }

    public override void ResetStation()
    {
        base.ResetStation();
        StopTyping();
        codeDone = false;
        leaving = false;
        typed.Clear();
        correctCount = 0;
        PickSnippet();
        UpdateUI();
        StopParticles();
    }

    // Nothing left over from the editor preview or the previous day
    void StopParticles()
    {
        if (submitParticles != null) submitParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // Once per day, during production only; after an Escape the player has to walk off before it can be used again
    public override bool CanUse(Player player)
    {
        if (codeDone || leaving) return false;
        if (player == null) return false;
        if ((LevelManager.instance != null) && (LevelManager.instance.state != LevelManager.State.Production)) return false;
        return target.Length > 0;
    }

    public override void Activate()
    {
        base.Activate();

        if (player == null) player = FindAnyObjectByType<Player>();
        if (target.Length == 0) PickSnippet();

        textDirty = true;
        UpdateUI();

        if (!codeDone) ShowPrompt(StartTyping);
    }

    public override void Deactivate()
    {
        base.Deactivate();
        StopTyping();
    }

    void Update()
    {
        // The station stays off limits after an Escape until the player moves away
        if (leaving && (player != null) && player.isMoving) leaving = false;

        if (!typingEnabled || codeDone) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            Leave();
            return;
        }

        // Backspace, with key repeat while held
        if (kb.backspaceKey.wasPressedThisFrame)
        {
            Backspace();
            backspaceTimer = backspaceRepeatDelay;
        }
        else if (kb.backspaceKey.isPressed)
        {
            backspaceTimer -= Time.deltaTime;
            if (backspaceTimer <= 0.0f)
            {
                Backspace();
                backspaceTimer = backspaceRepeatRate;
            }
        }

        if (textDirty) UpdateUI();

        // The clock only runs while typing; out of time, whatever is on screen is submitted
        timeLeft = Mathf.Max(0.0f, timeLeft - Time.deltaTime);
        UpdateTimeFill();
        if (timeLeft <= 0.0f)
        {
            Submit();
            return;
        }

        if (autoSubmit && isComplete)
        {
            autoSubmitLeft -= Time.deltaTime;
            if (autoSubmitLeft <= 0.0f) Submit();
        }
    }

    #region Typing

    void StartTyping()
    {
        if (typingEnabled) return;
        typingEnabled = true;
        autoSubmitLeft = autoSubmitDelay;

        keyboard = Keyboard.current;
        if (keyboard != null) keyboard.onTextInput += OnTextInput;

        if (player != null) player.LockControls(true);
    }

    void StopTyping()
    {
        if (!typingEnabled) return;
        typingEnabled = false;

        if (keyboard != null) keyboard.onTextInput -= OnTextInput;
        keyboard = null;

        if (player != null) player.LockControls(false);
    }

    // Escape: hands the player back; the Player deactivates the station on its next update (CanUse is false now)
    public void Leave()
    {
        leaving = true;
        StopTyping();
    }

    // Keyboard text input, layout and shift already applied. Enter comes in as a newline, tab as a tab.
    void OnTextInput(char c)
    {
        if (!typingEnabled || codeDone) return;

        if (c == '\r') c = '\n';
        if (c == '\t')
        {
            TypeTab();
            return;
        }
        if (char.IsControl(c) && (c != '\n')) return;

        TypeChar(c);
    }

    void TypeChar(char c)
    {
        if (typed.Count >= target.Length) return;

        typed.Add(c);
        if (c == target[typed.Count - 1]) correctCount++;
        Changed();
    }

    // A Tab press stands for the spaces expected at the caret (up to tabSize); where none are expected it is one
    // wrong space, so the snippet's indentation can be typed with Tab as well as with the space bar
    void TypeTab()
    {
        int n = 0;
        while ((n < tabSize) && (typed.Count < target.Length) && (target[typed.Count] == ' '))
        {
            TypeChar(' ');
            n++;
        }
        if (n == 0) TypeChar(' ');
    }

    void Backspace()
    {
        if (typed.Count == 0) return;

        int i = typed.Count - 1;
        if (typed[i] == target[i]) correctCount--;
        typed.RemoveAt(i);
        Changed();
    }

    void Changed()
    {
        textDirty = true;
        autoSubmitLeft = autoSubmitDelay;
    }

    #endregion

    public void Submit()
    {
        if (codeDone) return;

        codeDone = true;
        float result = ComputeScore();
        StopTyping();

        if (player == null) player = FindAnyObjectByType<Player>();
        if (player != null) player.SetCodingAccuracy(result);
        else Debug.LogWarning("CodingMG: no Player found to store the coding result", this);

        if (submitParticles != null) submitParticles.Play(true);

        UpdateUI();
        canvasGroup.FadeOut(0.1f);
    }

    // Over the whole snippet, so it is also what the player gets if the time ran out right now
    float ComputeScore()
    {
        if (target.Length == 0) return 1.0f;
        int wrong = typed.Count - correctCount;
        return Mathf.Clamp01((correctCount - wrongPenalty * wrong) / target.Length);
    }

    // Time for the snippet: a base, so much per character, and extra for every line break and every tab's worth
    // of indentation at the start of a line, since those take longer to type than a plain character
    float ComputeTimeLimit()
    {
        float time = baseTime + timePerCharacter * target.Length;

        foreach (var line in target.Split('\n'))
        {
            int leading = line.Length - line.TrimStart(' ').Length;
            time += extraTimePerIndent * Mathf.CeilToInt(leading / (float)Mathf.Max(1, tabSize));
        }
        for (int i = 0; i < target.Length; i++)
        {
            if (target[i] == '\n') time += extraTimePerNewline;
        }
        return time;
    }

    #region Snippets

    // Random from the pool (files and inline), avoiding the last one. Line endings, tabs and trailing spaces are
    // normalized so everything on screen is something the player can actually type.
    void PickSnippet()
    {
        var pool = new List<string>();
        foreach (var file in codeFiles)
        {
            if ((file != null) && !string.IsNullOrWhiteSpace(file.text)) pool.Add(file.text);
        }
        foreach (var s in codeSnippets)
        {
            if (!string.IsNullOrWhiteSpace(s)) pool.Add(s);
        }

        typed.Clear();
        correctCount = 0;
        textDirty = true;

        if (pool.Count == 0)
        {
            target = "";
            timeLimit = timeLeft = 0.0f;
            Debug.LogWarning("CodingMG: no code snippets assigned", this);
            return;
        }

        int index;
        if (pool.Count == 1) index = 0;
        else do { index = Random.Range(0, pool.Count); } while (index == lastSnippetIndex);
        lastSnippetIndex = index;

        target = Normalize(pool[index]);
        timeLimit = ComputeTimeLimit();
        timeLeft = timeLimit;
    }

    string Normalize(string code)
    {
        code = code.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", new string(' ', tabSize));

        var lines = code.Split('\n');
        for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();

        return string.Join("\n", lines).Trim('\n');
    }

    #endregion

    #region UI

    void UpdateUI()
    {
        if (textDirty) RebuildText();

        if (accuracyText)
        {
            int percent = Mathf.RoundToInt(ComputeScore() * 100.0f);
            accuracyText.text = $"Score {percent}%   {typed.Count} / {target.Length}";
        }
        if (starRow != null) starRow.SetStars(stars);
        if (submitButton) submitButton.interactable = isComplete && !codeDone;
        UpdateTimeFill();
    }

    void UpdateTimeFill()
    {
        if (timeFill != null) timeFill.fillAmount = (timeLimit > 0.0f) ? Mathf.Clamp01(timeLeft / timeLimit) : 0.0f;
    }

    CharState StateAt(int i)
    {
        if (i < typed.Count) return (typed[i] == target[i]) ? CharState.Correct : CharState.Wrong;
        if ((i == typed.Count) && !codeDone) return CharState.Caret;
        return CharState.Pending;
    }

    // Rich text: one color/mark run per stretch of characters in the same state. The code itself goes inside
    // <noparse> so its own angle brackets are not read as tags.
    void RebuildText()
    {
        textDirty = false;
        if (codeText == null) return;

        var sb = new StringBuilder(target.Length * 2);
        int i = 0;
        while (i < target.Length)
        {
            CharState state = StateAt(i);
            int j = i + 1;
            while ((j < target.Length) && (StateAt(j) == state)) j++;

            OpenRun(sb, state);
            sb.Append("<noparse>").Append(target, i, j - i).Append("</noparse>");
            CloseRun(sb, state);
            i = j;
        }
        codeText.text = sb.ToString();
    }

    void OpenRun(StringBuilder sb, CharState state)
    {
        switch (state)
        {
            case CharState.Correct: sb.Append("<color=#").Append(Hex(correctColor)).Append('>'); break;
            case CharState.Wrong:   sb.Append("<color=#").Append(Hex(wrongColor)).Append("><mark=#").Append(Hex(wrongMarkColor)).Append('>'); break;
            case CharState.Caret:
                sb.Append("<color=#").Append(Hex(caretColor)).Append("><mark=#").Append(Hex(caretMarkColor)).Append('>');
                if (caretUnderline) sb.Append("<u>");
                break;
            default:                sb.Append("<color=#").Append(Hex(pendingColor)).Append('>'); break;
        }
    }

    void CloseRun(StringBuilder sb, CharState state)
    {
        if ((state == CharState.Caret) && caretUnderline) sb.Append("</u>");
        if ((state == CharState.Wrong) || (state == CharState.Caret)) sb.Append("</mark>");
        sb.Append("</color>");
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGBA(c);

    #endregion
}
