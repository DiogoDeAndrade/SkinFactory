using UnityEngine;

public static class DistanceField
{
    // Unsigned Euclidean distance transform (8SSEDT / Danielsson): distance from every pixel to the nearest painted pixel.
    // Pixels are laid out row-major, origin at the bottom-left (Unity texture convention).
    public static float[] Compute(bool[] painted, int width, int height)
    {
        const int inf = 1 << 20;
        int n = width * height;
        var dx = new int[n];
        var dy = new int[n];
        for (int i = 0; i < n; i++)
        {
            dx[i] = painted[i] ? 0 : inf;
            dy[i] = painted[i] ? 0 : inf;
        }

        long DistSq(int i) => (long)dx[i] * dx[i] + (long)dy[i] * dy[i];

        void Compare(int x, int y, int ox, int oy)
        {
            int nx = x + ox, ny = y + oy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height) return;
            int i = y * width + x;
            int j = ny * width + nx;
            int cdx = dx[j] + ox;
            int cdy = dy[j] + oy;
            long candidate = (long)cdx * cdx + (long)cdy * cdy;
            if (candidate < DistSq(i))
            {
                dx[i] = cdx;
                dy[i] = cdy;
            }
        }

        // Pass 1
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Compare(x, y, -1, 0);
                Compare(x, y, 0, -1);
                Compare(x, y, -1, -1);
                Compare(x, y, 1, -1);
            }
            for (int x = width - 1; x >= 0; x--)
            {
                Compare(x, y, 1, 0);
            }
        }
        // Pass 2
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = width - 1; x >= 0; x--)
            {
                Compare(x, y, 1, 0);
                Compare(x, y, 0, 1);
                Compare(x, y, -1, 1);
                Compare(x, y, 1, 1);
            }
            for (int x = 0; x < width; x++)
            {
                Compare(x, y, -1, 0);
            }
        }

        var result = new float[n];
        for (int i = 0; i < n; i++)
        {
            result[i] = Mathf.Sqrt((float)DistSq(i));
        }
        return result;
    }

    // Graded closeness: 1 on the feature, 0 at threshold or beyond, linear in between
    public static float Grade(float distance, float threshold)
    {
        if (threshold <= 0.0f) return (distance <= 0.0f) ? 1.0f : 0.0f;
        return Mathf.Clamp01(1.0f - distance / threshold);
    }
}
