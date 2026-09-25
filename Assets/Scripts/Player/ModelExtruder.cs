using System.Collections.Generic;
using UC;
using UnityEngine;

// Turns the model (polygons in normalized [0,1] canvas coordinates, origin bottom-left) into a solid mesh: every
// polygon is triangulated with UC's Earcut (as NavMesh2d does) and extruded both ways from z = 0, flat shaded.
// Caps are UV mapped with the canvas coordinates and sides with the coordinates of their edge, so a texture drawn over
// the canvas (the drawing, the painting) lands where it was drawn; the front cap (-Z) reads the right way round.
// Polygons are layered: one overlapping a larger polygon (an inset, or a crossing one) is made thicker than it by
// layerOffset per side, so overlapping faces never share a depth and don't z-fight.
public static class ModelExtruder
{
    // Earcut cleanup tolerances in canvas units (the canvas is 1 wide, a drawing pixel ~0.008): far below a pixel,
    // well above float noise. Earcut's defaults are meant for world units
    const float DuplicateEpsilon = 1e-5f;   // Consecutive points closer than this are one point
    const float CollinearEpsilon = 1e-4f;   // A point this close to the line through its neighbours is dropped
    const float MinTriangleArea = 1e-8f;    // Smaller triangles are dropped

    public static Mesh Build(Vector2[][] polygons, Vector2 size, float depth, float layerOffset)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        int[] layers = ComputeLayers(polygons);
        for (int i = 0; i < polygons.Length; i++)
        {
            if ((polygons[i] == null) || (polygons[i].Length < 3)) continue;

            float halfDepth = depth * 0.5f + layers[i] * layerOffset;
            AddPrism(polygons[i], size, halfDepth, vertices, normals, uvs, triangles);
        }

        var mesh = new Mesh { name = "Skin model" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddPrism(Vector2[] polygon, Vector2 size, float halfDepth,
                         List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
    {
        var outline = new Polyline();
        foreach (var p in polygon) outline.Add(new Vector3(p.x, p.y, 0.0f));

        // With no holes the output vertices are the cleaned up outline, counter-clockwise
        List<Vector3> ring = null;
        List<int> capTriangles = null;
        outline.Triangulate_EarCut(null, ref ring, ref capTriangles, DuplicateEpsilon, CollinearEpsilon, MinTriangleArea);
        if ((ring == null) || (ring.Count < 3) || (capTriangles == null) || (capTriangles.Count < 3)) return;

        var ringUV = new Vector2[ring.Count];
        for (int k = 0; k < ring.Count; k++) ringUV[k] = new Vector2(ring[k].x, ring[k].y);

        Vector3 Position(Vector2 uv, float z) => new Vector3((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y, z);

        // Caps
        foreach (float z in new[] { -halfDepth, halfDepth })
        {
            Vector3 normal = (z < 0.0f) ? Vector3.back : Vector3.forward;
            int start = vertices.Count;
            foreach (var uv in ringUV)
            {
                vertices.Add(Position(uv, z));
                normals.Add(normal);
                uvs.Add(uv);
            }
            for (int t = 0; t + 2 < capTriangles.Count; t += 3)
            {
                AddTriangle(vertices, triangles, start + capTriangles[t], start + capTriangles[t + 1], start + capTriangles[t + 2], normal);
            }
        }

        // Sides: a quad per edge, facing out (right of the edge, for a counter-clockwise ring)
        for (int k = 0; k < ringUV.Length; k++)
        {
            Vector2 a = ringUV[k];
            Vector2 b = ringUV[(k + 1) % ringUV.Length];
            Vector3 edge = Position(b, 0.0f) - Position(a, 0.0f);
            if (edge.sqrMagnitude < 1e-12f) continue;

            Vector3 normal = new Vector3(edge.y, -edge.x, 0.0f).normalized;
            int start = vertices.Count;
            vertices.Add(Position(a, -halfDepth));
            vertices.Add(Position(b, -halfDepth));
            vertices.Add(Position(b, halfDepth));
            vertices.Add(Position(a, halfDepth));
            for (int v = 0; v < 4; v++) normals.Add(normal);
            uvs.Add(a);
            uvs.Add(b);
            uvs.Add(b);
            uvs.Add(a);

            AddTriangle(vertices, triangles, start, start + 1, start + 2, normal);
            AddTriangle(vertices, triangles, start, start + 2, start + 3, normal);
        }
    }

    // Winds the triangle so its front face is on the normal's side
    static void AddTriangle(List<Vector3> vertices, List<int> triangles, int i0, int i1, int i2, Vector3 normal)
    {
        Vector3 facing = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
        triangles.Add(i0);
        if (Vector3.Dot(facing, normal) >= 0.0f)
        {
            triangles.Add(i1);
            triangles.Add(i2);
        }
        else
        {
            triangles.Add(i2);
            triangles.Add(i1);
        }
    }

    #region Layers

    // Largest polygons first: each one sits a layer above the highest larger polygon it overlaps (0 if none)
    static int[] ComputeLayers(Vector2[][] polygons)
    {
        int n = polygons.Length;
        var order = new int[n];
        var keys = new float[n];
        for (int i = 0; i < n; i++)
        {
            order[i] = i;
            keys[i] = (polygons[i] != null) ? -Mathf.Abs(SignedArea(polygons[i])) : 0.0f;
        }
        System.Array.Sort(keys, order);

        var layers = new int[n];
        for (int oi = 0; oi < n; oi++)
        {
            int i = order[oi];
            if (polygons[i] == null) continue;

            for (int oj = 0; oj < oi; oj++)
            {
                int j = order[oj];
                if ((polygons[j] != null) && Overlap(polygons[i], polygons[j])) layers[i] = Mathf.Max(layers[i], layers[j] + 1);
            }
        }
        return layers;
    }

    // One inside the other (a vertex of either inside the other) or crossing edges
    static bool Overlap(Vector2[] a, Vector2[] b)
    {
        foreach (var p in a) if (Inside(b, p)) return true;
        foreach (var p in b) if (Inside(a, p)) return true;

        for (int i = 0; i < a.Length; i++)
        {
            Vector2 a0 = a[i], a1 = a[(i + 1) % a.Length];
            for (int j = 0; j < b.Length; j++)
            {
                if (SegmentsCross(a0, a1, b[j], b[(j + 1) % b.Length])) return true;
            }
        }
        return false;
    }

    static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[i], b = poly[j];
            if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)) inside = !inside;
        }
        return inside;
    }

    static bool SegmentsCross(Vector2 p0, Vector2 p1, Vector2 q0, Vector2 q1)
    {
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        float d1 = Cross(q0, q1, p0), d2 = Cross(q0, q1, p1);
        float d3 = Cross(p0, p1, q0), d4 = Cross(p0, p1, q1);
        return (d1 * d2 < 0.0f) && (d3 * d4 < 0.0f);
    }

    static float SignedArea(Vector2[] poly)
    {
        float area = 0.0f;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) area += poly[j].x * poly[i].y - poly[i].x * poly[j].y;
        return area * 0.5f;
    }

    #endregion
}
