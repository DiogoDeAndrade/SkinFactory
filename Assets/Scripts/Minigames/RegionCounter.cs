using System.Collections.Generic;
using UnityEngine;

public static class RegionCounter
{
    public struct Segment
    {
        public Vector2 a;
        public Vector2 b;

        public Segment(Vector2 a, Vector2 b)
        {
            this.a = a;
            this.b = b;
        }
    }

    // Counts the closed regions enclosed by a set of segments (normalized [0,1] coordinates).
    // The segments are rasterized as walls on a grid and the empty space is flood-filled: everything reachable
    // from the border is "outside", every other pocket is a closed region. Walls are 8-connected and the fill
    // is 4-connected, so a fill can never leak across a line. Pockets smaller than minAreaFraction of the grid
    // are ignored (rasterization slivers where lines nearly touch).
    // Open polylines count too, whenever they enclose space together with other lines.
    public static int CountClosedRegions(IReadOnlyList<Segment> segments, int resolution = 512, float minAreaFraction = 0.0002f, List<int> areas = null)
    {
        if (segments == null || segments.Count == 0) return 0;

        int res = Mathf.Max(8, resolution);
        int n = res * res;
        var wall = new bool[n];

        foreach (var seg in segments)
        {
            Vector2 a = seg.a * res;
            Vector2 b = seg.b * res;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)s / steps);
                int x = Mathf.Clamp((int)p.x, 0, res - 1);
                int y = Mathf.Clamp((int)p.y, 0, res - 1);
                wall[y * res + x] = true;
            }
        }

        var visited = new bool[n];
        var queue = new Queue<int>();

        // Everything reachable from the border is outside
        for (int x = 0; x < res; x++)
        {
            Flood(wall, visited, res, x, queue);
            Flood(wall, visited, res, (res - 1) * res + x, queue);
        }
        for (int y = 0; y < res; y++)
        {
            Flood(wall, visited, res, y * res, queue);
            Flood(wall, visited, res, y * res + res - 1, queue);
        }

        int minArea = Mathf.Max(1, Mathf.RoundToInt(n * minAreaFraction));
        int count = 0;
        for (int i = 0; i < n; i++)
        {
            if (wall[i] || visited[i]) continue;

            int area = Flood(wall, visited, res, i, queue);
            areas?.Add(area);
            if (area >= minArea) count++;
        }

        return count;
    }

    // 4-connected flood fill from a start index; returns the filled area (0 if the start is a wall or already visited)
    static int Flood(bool[] wall, bool[] visited, int res, int start, Queue<int> queue)
    {
        if (wall[start] || visited[start]) return 0;

        int area = 0;
        visited[start] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            area++;

            int x = i % res;
            int y = i / res;

            if (x > 0) Visit(i - 1);
            if (x < res - 1) Visit(i + 1);
            if (y > 0) Visit(i - res);
            if (y < res - 1) Visit(i + res);
        }

        return area;

        void Visit(int j)
        {
            if (wall[j] || visited[j]) return;
            visited[j] = true;
            queue.Enqueue(j);
        }
    }

    // Segments of a closed polygon
    public static void AddPolygon(List<Segment> segments, IReadOnlyList<Vector2> polygon)
    {
        int n = polygon.Count;
        if (n < 2) return;
        for (int i = 0; i < n; i++)
        {
            segments.Add(new Segment(polygon[i], polygon[(i + 1) % n]));
        }
    }
}
