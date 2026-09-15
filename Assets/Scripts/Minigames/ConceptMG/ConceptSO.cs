using System.Collections.Generic;
using System.Globalization;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "ConceptSO", menuName = "Skin Factory/ConceptSO")]
public class ConceptSO : ScriptableObject
{
    [SerializeField] private VectorImage    baseSVG;
    [SerializeField] private Sprite         sketch;
    [SerializeField] private Sprite         conceptArt;

    [Header("Budgets")]
    [SerializeField, Min(0), FormerlySerializedAs("recommendedSegments")]
    [Tooltip("Maximum number of segments the player can use in the modelling station (0 = unlimited)")]
    private int     segmentBudget;
    [SerializeField, Min(0)]
    [Tooltip("Minimum number of closed regions the model must have before it can be submitted (needed by the painting station)")]
    private int     minClosedRegions;
    [SerializeField, Min(0)]
    [Tooltip("Minimum number of distinct palette colors the painting must use before it can be submitted")]
    private int     minColors;

    public VectorImage  BaseSVG => baseSVG;
    public Sprite       Sketch => sketch;
    public Sprite       ConceptArt => conceptArt;
    public int          SegmentBudget => segmentBudget;
    public int          MinClosedRegions => minClosedRegions;
    public int          MinColors => minColors;

#if UNITY_EDITOR
    [Button("Compute budgets from SVG")]
    void ComputeBudgetsFromSVG()
    {
        if (baseSVG == null)
        {
            Debug.LogWarning($"{name}: no base SVG assigned", this);
            return;
        }

        string path = UnityEditor.AssetDatabase.GetAssetPath(baseSVG);
        if (!TryLoadSVGSegments(path, out var segments)) return;

        segmentBudget = segments.Count;
        minClosedRegions = RegionCounter.CountClosedRegions(segments);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"{name}: {segmentBudget} segments, {minClosedRegions} closed regions (from {path})", this);
    }

    // Reads polygon / polyline / line elements from a straight-line SVG, normalized to its viewBox
    static bool TryLoadSVGSegments(string path, out List<RegionCounter.Segment> segments)
    {
        segments = new List<RegionCounter.Segment>();

        if (string.IsNullOrEmpty(path) || !path.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"Base SVG is not an .svg file: {path}");
            return false;
        }

        var doc = new System.Xml.XmlDocument();
        doc.Load(path);
        var root = doc.DocumentElement;

        // viewBox, falling back to width/height
        Rect viewBox = new Rect(0, 0, 256, 256);
        var vb = root.GetAttribute("viewBox");
        if (!string.IsNullOrEmpty(vb))
        {
            var v = ParseNumbers(vb);
            if (v.Count >= 4) viewBox = new Rect(v[0], v[1], v[2], v[3]);
        }
        else
        {
            var w = ParseNumbers(root.GetAttribute("width"));
            var h = ParseNumbers(root.GetAttribute("height"));
            if (w.Count > 0 && h.Count > 0) viewBox = new Rect(0, 0, w[0], h[0]);
        }

        Vector2 Norm(float x, float y) => new Vector2((x - viewBox.x) / viewBox.width, (y - viewBox.y) / viewBox.height);

        foreach (System.Xml.XmlElement el in doc.GetElementsByTagName("polygon"))
        {
            var pts = ParsePoints(el.GetAttribute("points"), Norm);
            for (int i = 0; i < pts.Count; i++) segments.Add(new RegionCounter.Segment(pts[i], pts[(i + 1) % pts.Count]));
        }
        foreach (System.Xml.XmlElement el in doc.GetElementsByTagName("polyline"))
        {
            var pts = ParsePoints(el.GetAttribute("points"), Norm);
            for (int i = 0; i < pts.Count - 1; i++) segments.Add(new RegionCounter.Segment(pts[i], pts[i + 1]));
        }
        foreach (System.Xml.XmlElement el in doc.GetElementsByTagName("line"))
        {
            var a = Norm(ParseFloat(el.GetAttribute("x1")), ParseFloat(el.GetAttribute("y1")));
            var b = Norm(ParseFloat(el.GetAttribute("x2")), ParseFloat(el.GetAttribute("y2")));
            segments.Add(new RegionCounter.Segment(a, b));
        }

        if (segments.Count == 0)
        {
            Debug.LogWarning($"No polygon/polyline/line elements found in {path} (curved paths are not supported)");
            return false;
        }
        return true;
    }

    static List<Vector2> ParsePoints(string points, System.Func<float, float, Vector2> norm)
    {
        var nums = ParseNumbers(points);
        var result = new List<Vector2>(nums.Count / 2);
        for (int i = 0; i + 1 < nums.Count; i += 2) result.Add(norm(nums[i], nums[i + 1]));
        return result;
    }

    static List<float> ParseNumbers(string s)
    {
        var result = new List<float>();
        if (string.IsNullOrEmpty(s)) return result;
        foreach (var tok in s.Split(new[] { ' ', ',', '\t', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            if (float.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) result.Add(v);
        }
        return result;
    }

    static float ParseFloat(string s)
    {
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0.0f;
    }
#endif
}
