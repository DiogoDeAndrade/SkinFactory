using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draws polygon outlines and vertices inside its RectTransform.
// All coordinates are normalized [0,1] over the rect, origin at the bottom-left (same as a texture shown in a RawImage).
[RequireComponent(typeof(CanvasRenderer))]
public class PolygonGraphic : MaskableGraphic
{
    [SerializeField, Min(0)] private float  lineWidth = 2.0f;
    [SerializeField, Min(0)] private float  vertexSize = 6.0f;
    [SerializeField] private Color          polygonColor = new Color(0.15f, 0.55f, 1.0f, 1.0f);
    [SerializeField] private Color          currentColor = new Color(1.0f, 0.6f, 0.1f, 1.0f);
    [SerializeField] private Color          cursorColor = new Color(1.0f, 0.6f, 0.1f, 0.5f);
    [SerializeField] private Color          snapColor = new Color(0.2f, 1.0f, 0.2f, 1.0f);

    IReadOnlyList<IReadOnlyList<Vector2>>   polygons;
    IReadOnlyList<Vector2>                  current;
    Vector2?                                cursor;
    bool                                    snapping;

    public override Texture mainTexture => s_WhiteTexture;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetPolygons(IReadOnlyList<IReadOnlyList<Vector2>> polygons, IReadOnlyList<Vector2> current)
    {
        this.polygons = polygons;
        this.current = current;
        SetVerticesDirty();
    }

    public void SetCursor(Vector2? normalized, bool snapping)
    {
        if ((cursor == normalized) && (this.snapping == snapping)) return;

        cursor = normalized;
        this.snapping = snapping;
        SetVerticesDirty();
    }

    // Normalized [0,1] -> local rect coordinates
    public Vector2 ToLocal(Vector2 n)
    {
        var rect = rectTransform.rect;
        return new Vector2(rect.xMin + n.x * rect.width, rect.yMin + n.y * rect.height);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (polygons != null)
        {
            foreach (var poly in polygons)
            {
                int n = poly.Count;
                for (int i = 0; i < n; i++)
                {
                    AddLine(vh, ToLocal(poly[i]), ToLocal(poly[(i + 1) % n]), lineWidth, polygonColor);
                }
                for (int i = 0; i < n; i++)
                {
                    AddDot(vh, ToLocal(poly[i]), vertexSize, polygonColor);
                }
            }
        }

        if ((current != null) && (current.Count > 0))
        {
            for (int i = 0; i < current.Count - 1; i++)
            {
                AddLine(vh, ToLocal(current[i]), ToLocal(current[i + 1]), lineWidth, currentColor);
            }
            if (cursor.HasValue)
            {
                AddLine(vh, ToLocal(current[current.Count - 1]), ToLocal(cursor.Value), lineWidth, cursorColor);
            }
            for (int i = 1; i < current.Count; i++)
            {
                AddDot(vh, ToLocal(current[i]), vertexSize, currentColor);
            }
            // First vertex is the closing target
            AddDot(vh, ToLocal(current[0]), snapping ? vertexSize * 1.6f : vertexSize, snapping ? snapColor : currentColor);
        }
    }

    static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();
        Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
        AddQuad(vh, a - n, b - n, b + n, a + n, color);
    }

    static void AddDot(VertexHelper vh, Vector2 p, float size, Color color)
    {
        float h = size * 0.5f;
        AddQuad(vh, p + new Vector2(-h, -h), p + new Vector2(h, -h), p + new Vector2(h, h), p + new Vector2(-h, h), color);
    }

    static void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color color)
    {
        int start = vh.currentVertCount;
        var v = UIVertex.simpleVert;
        v.color = color;
        v.uv0 = Vector2.zero;

        v.position = p0; vh.AddVert(v);
        v.position = p1; vh.AddVert(v);
        v.position = p2; vh.AddVert(v);
        v.position = p3; vh.AddVert(v);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
