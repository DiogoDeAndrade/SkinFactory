using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

// Imports *.idea files: one idea per line, "<idea name>, <tag1>, <tag2>, ..., <tagN>".
// Blank lines and lines starting with # are ignored. The file becomes an IdeaLibrarySO with one IdeaSO
// sub-asset per line; the display name is the text as written and the name is an asset-safe version of it.
[ScriptedImporter(1, "idea")]
public class IdeaImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var library = ScriptableObject.CreateInstance<IdeaLibrarySO>();
        library.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

        var ideas = new List<IdeaSO>();
        var usedNames = new HashSet<string>();

        string[] lines = File.ReadAllLines(ctx.assetPath, Encoding.UTF8);
        for (int lineNo = 0; lineNo < lines.Length; lineNo++)
        {
            string line = lines[lineNo].Trim();
            if ((line.Length == 0) || line.StartsWith("#")) continue;

            var fields = line.Split(',');
            string displayName = fields[0].Trim();
            if (displayName.Length == 0)
            {
                ctx.LogImportWarning($"{ctx.assetPath}: line {lineNo + 1} has no idea name, skipped");
                continue;
            }

            var tags = new List<string>();
            for (int i = 1; i < fields.Length; i++)
            {
                string tag = fields[i].Trim();
                if ((tag.Length > 0) && !tags.Contains(tag)) tags.Add(tag);
            }

            string ideaName = MakeUnique(Sanitize(displayName), usedNames);

            var idea = ScriptableObject.CreateInstance<IdeaSO>();
            idea.name = ideaName;
            idea.Set(ideaName, displayName, tags);
            ctx.AddObjectToAsset(ideaName, idea);
            ideas.Add(idea);
        }

        library.Set(ideas);
        ctx.AddObjectToAsset("library", library);
        ctx.SetMainObject(library);
    }

    // Letters, digits and underscores only, so the name is safe as an asset/sub-asset identifier
    static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || (c == '-') || (c == '_')) sb.Append('_');
        }
        return (sb.Length > 0) ? sb.ToString() : "Idea";
    }

    static string MakeUnique(string name, HashSet<string> used)
    {
        string candidate = name;
        int n = 2;
        while (!used.Add(candidate))
        {
            candidate = $"{name}_{n++}";
        }
        return candidate;
    }
}
