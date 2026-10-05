using System.Collections;
using UC;
using UnityEngine;

// Helpers for the sound patterns the game shares. Every sound in the game is an optional SoundDef reference: a
// plain one is played with sound?.Play() where it happens, and these cover the cases that take more than that.
// See sounds.md at the project root for the full list and where each reference is assigned.
public static class GameSounds
{
    // Two sounds layered into one effect: both or neither, half of it is worse than silence
    public static void PlayTogether(SoundDef a, SoundDef b)
    {
        if ((a == null) || (b == null)) return;

        a.Play();
        b.Play();
    }

    // One sound, then another once the first has finished: both or neither. Runs on the host in unscaled time, so
    // it carries on while the game is paused.
    public static void PlayChain(MonoBehaviour host, SoundDef first, SoundDef then)
    {
        if ((host == null) || (first == null) || (then == null)) return;

        host.StartCoroutine(ChainCR(first, then));
    }

    static IEnumerator ChainCR(SoundDef first, SoundDef then)
    {
        AudioSource source = first.Play();
        if ((source != null) && (source.clip != null))
        {
            yield return new WaitForSecondsRealtime(source.clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch)));
        }
        then.Play();
    }

    // A tick every time a clock drops through a whole second inside its last `window` seconds. Call it with the
    // time left before and after the frame's decrement.
    public static void ClockWarning(SoundDef sound, float before, float after, float window)
    {
        if ((sound == null) || (after <= 0.0f) || (after > window)) return;

        if (Mathf.CeilToInt(after) < Mathf.CeilToInt(before)) sound.Play();
    }

    // A burst of voice blips for a line of dialogue: one for every few characters, up to a limit. The variety
    // between blips comes from the SoundDef itself (several clips, a pitch range).
    public static IEnumerator BabbleCR(SoundDef sound, string line, float interval = 0.07f, int charsPerBlip = 3, int maxBlips = 14)
    {
        if ((sound == null) || string.IsNullOrEmpty(line)) yield break;

        int count = Mathf.Clamp(line.Length / Mathf.Max(1, charsPerBlip), 1, maxBlips);
        for (int i = 0; i < count; i++)
        {
            sound.Play();
            yield return new WaitForSeconds(interval);
        }
    }
}
