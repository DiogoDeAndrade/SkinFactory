using UnityEngine;

public static class TextureUtils
{
    // Readable RGBA32 copy of any texture (goes through a RenderTexture, so the source doesn't need Read/Write enabled)
    public static Texture2D ReadableCopy(Texture source, string name = null)
    {
        if (source == null) return null;

        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;

        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = string.IsNullOrEmpty(name) ? source.name + " (copy)" : name
        };
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }
}
