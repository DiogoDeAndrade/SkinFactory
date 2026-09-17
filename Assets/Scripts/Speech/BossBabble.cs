using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach to the boss on the main menu: he keeps saying business things, one balloon after another, forever.
// Lines come from a text file (one per line, # comments skipped) and are picked at random without immediate
// repeats. Balloons go through the scene's SpeechBalloonManager.
public class BossBabble : MonoBehaviour
{
    [SerializeField, Tooltip("One line per line; blank lines and lines starting with # are skipped")]
    private TextAsset   linesFile;
    [SerializeField, TextArea(2, 6), Tooltip("Extra lines, added to the file's")]
    private List<string> extraLines = new List<string>();

    [Header("Balloon")]
    [SerializeField, Tooltip("What the balloon points at; this object if left empty")]
    private Transform   anchor;
    [SerializeField, Tooltip("World-space offset from the anchor to the balloon tip")]
    private Vector3     balloonOffset = new Vector3(0.0f, 1.8f, 0.0f);

    [Header("Timing")]
    [SerializeField, Min(0), Tooltip("Seconds before the first line")]
    private float       startDelay = 1.0f;
    [SerializeField, Tooltip("Seconds of silence between lines, min..max")]
    private Vector2     gap = new Vector2(1.0f, 2.5f);
    [SerializeField, Min(0), Tooltip("Added to the balloon's own reading time")]
    private float       extraHold = 0.5f;
    [SerializeField, Min(0), Tooltip("Seconds a line stays up when there is no balloon system to time it")]
    private float       fallbackTalkTime = 3.0f;

    List<string>    lines = new List<string>();
    SpeechBalloon   balloon;
    int             last = -1;

    void Start()
    {
        if (anchor == null) anchor = transform;
        LoadLines();
        StartCoroutine(BabbleCR());
    }

    void OnDisable()
    {
        if (balloon != null) balloon.Hide();
        balloon = null;
    }

    void LoadLines()
    {
        lines.Clear();
        if (linesFile != null)
        {
            foreach (var raw in linesFile.text.Split((char)10))
            {
                string line = raw.Trim();
                if ((line.Length > 0) && !line.StartsWith("#")) lines.Add(line);
            }
        }
        foreach (var line in extraLines)
        {
            if (!string.IsNullOrWhiteSpace(line)) lines.Add(line.Trim());
        }
        if (lines.Count == 0) Debug.LogWarning("BossBabble: no lines to say (assign a lines file)", this);
    }

    IEnumerator BabbleCR()
    {
        yield return new WaitForSeconds(startDelay);

        while (lines.Count > 0)
        {
            string line = Pick();
            balloon = SpeechBalloonManager.Show(line, anchor, balloonOffset);
            float talkTime = ((balloon != null) ? balloon.ReadTime : fallbackTalkTime) + extraHold;
            yield return new WaitForSeconds(talkTime);

            if (balloon != null) balloon.Hide();
            balloon = null;

            yield return new WaitForSeconds(Random.Range(gap.x, gap.y));
        }
    }

    string Pick()
    {
        if (lines.Count == 1) return lines[0];

        int index;
        do { index = Random.Range(0, lines.Count); } while (index == last);
        last = index;
        return lines[index];
    }
}
