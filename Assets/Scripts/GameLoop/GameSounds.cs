using System.Collections;
using UC;
using UnityEngine;

// Helpers for the sound patterns the game shares. Every sound in the game is an optional SoundDef reference: a
// plain one is played with sound?.Play() where it happens, and these cover the cases that take more than that.
public static class GameSounds
{
    // A tick every time a clock drops through a whole second inside its last `window` seconds. Call it with the
    // time left before and after the frame's decrement.
    public static void ClockWarning(SoundDef sound, float before, float after, float window)
    {
        if ((sound == null) || (after <= 0.0f) || (after > window)) return;

        if (Mathf.CeilToInt(after) < Mathf.CeilToInt(before)) sound.Play();
    }

    // A looping AudioSource heard only while a stroke is being drawn (pencil, brush): at `volume` on every frame
    // of the stroke, and otherwise fading out at a rate that takes fadeTime from that volume. Unscaled time, so it
    // still dies away if the game freezes mid-stroke.
    public static void StrokeLoop(AudioSource source, bool stroking, float volume, float fadeTime)
    {
        if (source == null) return;

        if (stroking)
        {
            source.volume = volume;
            if (!source.isPlaying) source.Play();
        }
        else if (source.volume > 0.0f)
        {
            bool fades = (fadeTime > 0.0f) && (volume > 0.0f);
            source.volume = fades ? Mathf.MoveTowards(source.volume, 0.0f, volume * Time.unscaledDeltaTime / fadeTime) : 0.0f;
        }
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
