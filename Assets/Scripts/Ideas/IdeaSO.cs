using System;
using System.Collections.Generic;
using UnityEngine;

// Data for one idea the IdeaMachine can produce: a name plus the tags it matches against.
// Created from *.idea files by the IdeaImporter (one sub-asset per line), but can also be made by hand.
[CreateAssetMenu(fileName = "IdeaSO", menuName = "Skin Factory/IdeaSO")]
public class IdeaSO : ScriptableObject
{
    [SerializeField, Tooltip("Identifier (asset-safe, unique within its library)")]
    private string          ideaName;
    [SerializeField, Tooltip("Text shown to the player")]
    private string          displayName;
    [SerializeField] private List<string> tags = new List<string>();

    public string               IdeaName => ideaName;
    public string               DisplayName => string.IsNullOrEmpty(displayName) ? ideaName : displayName;
    public IReadOnlyList<string> Tags => tags;

    public bool HasTag(string tag)
    {
        foreach (var t in tags)
        {
            if (string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public bool HasAnyTag(IEnumerable<string> wanted)
    {
        foreach (var w in wanted) if (HasTag(w)) return true;
        return false;
    }

    public bool HasAllTags(IEnumerable<string> wanted)
    {
        foreach (var w in wanted) if (!HasTag(w)) return false;
        return true;
    }

    public void Set(string ideaName, string displayName, IEnumerable<string> tags)
    {
        this.ideaName = ideaName;
        this.displayName = displayName;
        this.tags = new List<string>(tags);
    }
}
