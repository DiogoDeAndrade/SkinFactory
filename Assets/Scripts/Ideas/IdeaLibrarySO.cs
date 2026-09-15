using System;
using System.Collections.Generic;
using UnityEngine;

// All the ideas parsed from one *.idea file (the file's main asset; each IdeaSO is a sub-asset).
public class IdeaLibrarySO : ScriptableObject
{
    [SerializeField] private List<IdeaSO> ideas = new List<IdeaSO>();

    public IReadOnlyList<IdeaSO>    Ideas => ideas;
    public int                      Count => ideas.Count;
    public IdeaSO                   this[int index] => ideas[index];

    public IdeaSO Find(string ideaName)
    {
        foreach (var idea in ideas)
        {
            if ((idea != null) && string.Equals(idea.IdeaName, ideaName, StringComparison.OrdinalIgnoreCase)) return idea;
        }
        return null;
    }

    // Every distinct tag used by the ideas, in first-seen order
    public List<string> GetAllTags()
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var idea in ideas)
        {
            if (idea == null) continue;
            foreach (var t in idea.Tags)
            {
                if (seen.Add(t)) result.Add(t);
            }
        }
        return result;
    }

    public List<IdeaSO> WithTag(string tag)
    {
        var result = new List<IdeaSO>();
        foreach (var idea in ideas)
        {
            if ((idea != null) && idea.HasTag(tag)) result.Add(idea);
        }
        return result;
    }

    public void Set(IEnumerable<IdeaSO> ideas)
    {
        this.ideas = new List<IdeaSO>(ideas);
    }
}
