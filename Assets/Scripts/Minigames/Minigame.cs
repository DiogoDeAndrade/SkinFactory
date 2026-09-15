using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UC;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract partial class Minigame : MonoBehaviour
{
    [SerializeField] private float radius = 5.0f;
    [SerializeField] private float angularTolerance = 60.0f;

    [AutoStaticsCleanup]
    static List<Minigame>   minigames = new List<Minigame>();

    protected virtual void OnEnable()
    {
        minigames.Add(this);
    }

    private void OnDisable()
    {
        minigames.Remove(this);
    }

    static public Minigame GetMinigame(Transform t)
    {
        foreach (var mg in minigames)
        {
            if (!mg.CanUse()) continue;
            if (Vector3.Distance(mg.transform.position.x0z(), t.position.x0z()) < mg.radius)
            {
                if (Vector3.Angle(mg.transform.forward, -t.forward) < mg.angularTolerance)
                {
                    return mg;
                }
            }
        }

        return null;
    }

    public abstract void Activate();
    public abstract void Deactivate();
    public abstract bool CanUse();


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Handles.color = new Color(0.2f, 0.9f, 0.2f, 0.1f);
        Handles.DrawSolidArc(transform.position, Vector3.up, transform.forward.RotateY(angularTolerance), angularTolerance * 2.0f, radius);
    }
#endif
}
