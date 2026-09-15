using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

// Metadata tags for palette colors. Painting restrictions later on are expressed as masks over these
// (e.g. "only Cold colors", "only Bright colors"). A color can carry several tags.
[Flags]
public enum ColorCategory
{
    None        = 0,
    Warm        = 1 << 0,   // Reds, oranges, yellows, warm browns
    Cold        = 1 << 1,   // Greens, cyans, blues, purples
    Neutral     = 1 << 2,   // Greys, black, white (no real hue)
    Bright      = 1 << 3,   // High perceived brightness
    Dark        = 1 << 4,   // Low perceived brightness
    Saturated   = 1 << 5,   // Strong, vivid hue
    Muted       = 1 << 6,   // Weak hue, but not neutral
    Pastel      = 1 << 7,   // Bright and soft
}

[CreateAssetMenu(fileName = "Palette", menuName = "Skin Factory/Palette")]
public class PaletteSO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string                       name;
        public Color                        color = Color.white;
        [EnumFlags] public ColorCategory    categories;

        public bool HasAny(ColorCategory mask) => (categories & mask) != 0;
        public bool HasAll(ColorCategory mask) => (categories & mask) == mask;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public int                  Count => entries.Count;
    public IReadOnlyList<Entry> Entries => entries;
    public Entry                this[int index] => entries[index];

    public Color GetColor(int index) => entries[index].color;

    // Index of the entry matching the color (RGB distance), or -1
    public int IndexOf(Color color, float tolerance = 0.02f)
    {
        int best = -1;
        float bestDist = tolerance;
        for (int i = 0; i < entries.Count; i++)
        {
            float d = DistanceRGB(entries[i].color, color);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    public int IndexOf(Color32 color) => IndexOf((Color)color, 0.02f);

    // Indices of the entries that have any of the anyOf tags (or all entries if anyOf is None) and all of the allOf tags
    public List<int> GetIndices(ColorCategory anyOf, ColorCategory allOf = ColorCategory.None)
    {
        var result = new List<int>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if ((anyOf != ColorCategory.None) && !e.HasAny(anyOf)) continue;
            if ((allOf != ColorCategory.None) && !e.HasAll(allOf)) continue;
            result.Add(i);
        }
        return result;
    }

    public bool Matches(int index, ColorCategory anyOf, ColorCategory allOf = ColorCategory.None)
    {
        var e = entries[index];
        if ((anyOf != ColorCategory.None) && !e.HasAny(anyOf)) return false;
        if ((allOf != ColorCategory.None) && !e.HasAll(allOf)) return false;
        return true;
    }

    public static float DistanceRGB(Color a, Color b)
    {
        float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    #region Classification

    // Perceived brightness in [0,1]
    public static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    // Chroma (max - min channel): how much actual hue a color carries. More robust than HSV saturation for
    // very dark or very light colors, whose hue is meaningless.
    public static float Chroma(Color c) => Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);

    const float neutralChroma = 0.12f;

    // Rule-based tagging from HSV. A starting point: entries can be edited by hand afterwards.
    public static ColorCategory Classify(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        float lum = Luminance(c);

        var cat = ColorCategory.None;

        if (Chroma(c) < neutralChroma)
        {
            cat |= ColorCategory.Neutral;
        }
        else
        {
            // Temperature: red -> yellow -> yellow-green is warm, green -> blue -> purple is cold, magenta-red is warm again
            bool warm = (h < 0.25f) || (h >= 0.9f);
            cat |= warm ? ColorCategory.Warm : ColorCategory.Cold;

            if (s >= 0.6f && v >= 0.4f) cat |= ColorCategory.Saturated;
            if (s < 0.45f) cat |= ColorCategory.Muted;
            if (s < 0.55f && v >= 0.8f) cat |= ColorCategory.Pastel;
        }

        if (lum >= 0.6f) cat |= ColorCategory.Bright;
        else if (lum < 0.3f) cat |= ColorCategory.Dark;

        return cat;
    }

    // Human-readable name from HSV, e.g. "Dark Red", "Light Blue", "Grey"
    public static string AutoName(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        float lum = Luminance(c);

        if (Chroma(c) < neutralChroma)
        {
            if (lum < 0.12f) return "Black";
            if (lum > 0.92f) return "White";
            return (lum < 0.4f) ? "Dark Grey" : (lum > 0.65f) ? "Light Grey" : "Grey";
        }

        string hue;
        if (h < 0.04f || h >= 0.95f) hue = "Red";
        else if (h < 0.10f) hue = (v < 0.65f && s > 0.4f) ? "Brown" : "Orange";
        else if (h < 0.18f) hue = "Yellow";
        else if (h < 0.42f) hue = "Green";
        else if (h < 0.53f) hue = "Cyan";
        else if (h < 0.70f) hue = "Blue";
        else if (h < 0.83f) hue = "Purple";
        else hue = "Pink";

        string prefix = (lum < 0.3f) ? "Dark " : (lum >= 0.6f && s < 0.55f) ? "Light " : "";
        return prefix + hue;
    }

    #endregion

#if UNITY_EDITOR
    [Header("Editor tools")]
    [SerializeField] private Texture2D  importTexture;
    [SerializeField, Min(1)] private int importMaxColors = 16;

    [Button("Import colors from texture")]
    void ImportFromTexture()
    {
        if (importTexture == null)
        {
            Debug.LogWarning($"{name}: assign a texture to import from", this);
            return;
        }

        // Count unique opaque colors (8-bit quantized), most frequent first
        var readable = TextureUtils.ReadableCopy(importTexture, "palette import");
        var pixels = readable.GetPixels32();
        DestroyImmediate(readable);

        var counts = new Dictionary<int, int>();
        foreach (var p in pixels)
        {
            if (p.a < 128) continue;
            int key = (p.r << 16) | (p.g << 8) | p.b;
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        var keys = new List<int>(counts.Keys);
        keys.Sort((a, b) => counts[b].CompareTo(counts[a]));

        entries.Clear();
        for (int i = 0; i < keys.Count && i < importMaxColors; i++)
        {
            int k = keys[i];
            var c = new Color(((k >> 16) & 255) / 255.0f, ((k >> 8) & 255) / 255.0f, (k & 255) / 255.0f, 1.0f);
            entries.Add(new Entry { name = AutoName(c), color = c, categories = Classify(c) });
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"{name}: imported {entries.Count} colors from {importTexture.name} ({keys.Count} unique found)", this);
    }

    [Button("Auto-assign categories")]
    void AutoCategorize()
    {
        foreach (var e in entries) e.categories = Classify(e.color);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [Button("Auto-name entries")]
    void AutoNameEntries()
    {
        foreach (var e in entries) e.name = AutoName(e.color);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [Button("Sort by hue")]
    void SortByHue()
    {
        entries.Sort((a, b) =>
        {
            Color.RGBToHSV(a.color, out float ha, out float sa, out float va);
            Color.RGBToHSV(b.color, out float hb, out float sb, out float vb);
            bool na = Chroma(a.color) < neutralChroma, nb = Chroma(b.color) < neutralChroma;
            if (na != nb) return na ? 1 : -1;          // Neutrals last
            if (na) return va.CompareTo(vb);            // Neutrals by brightness
            int c = ha.CompareTo(hb);
            return (c != 0) ? c : va.CompareTo(vb);
        });
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
