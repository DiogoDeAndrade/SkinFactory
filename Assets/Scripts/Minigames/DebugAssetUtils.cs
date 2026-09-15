#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Editor-only helpers for saving debug snapshots into the project
public static class DebugAssetUtils
{
    public static string NormalizeFolder(string folder, string fallback = "Assets/Art/Debug")
    {
        if (string.IsNullOrEmpty(folder)) folder = fallback;
        return folder.Replace('\\', '/').TrimEnd('/');
    }

    // Writes a readable texture as a PNG and imports it with exact-pixel settings (no compression, no mips, point filter, readable)
    public static Texture2D SavePNG(Texture2D texture, string path)
    {
        if (texture == null) return null;

        var folder = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder)) System.IO.Directory.CreateDirectory(folder);
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());

        if (!path.StartsWith("Assets/")) return null;

        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Loads the ScriptableObject at path, or creates it if missing
    public static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            var folder = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) System.IO.Directory.CreateDirectory(folder);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }
}
#endif
