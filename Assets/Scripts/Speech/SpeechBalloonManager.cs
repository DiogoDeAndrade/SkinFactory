using System.Collections.Generic;
using UnityEngine;

// Hands out SpeechBalloons on the UI canvas: Show takes a pooled balloon (instantiating the prefab when none is
// free) and the balloon returns itself to the pool once Hide has faded it out. Also decides which camera the
// balloons project through. One per scene, with the balloon prefab assigned.
public class SpeechBalloonManager : MonoBehaviour
{
    [SerializeField]
    private SpeechBalloon   balloonPrefab;
    [SerializeField, Tooltip("Balloons are parented here; the first Canvas in the scene if left empty")]
    private RectTransform   container;

    public static SpeechBalloonManager instance { get; private set; }

    List<SpeechBalloon> pool = new List<SpeechBalloon>();

    void Awake()
    {
        if ((instance != null) && (instance != this))
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (container == null)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null) container = canvas.GetComponent<RectTransform>();
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Shows text over the anchor (world position plus a world-space offset). Returns null when no manager or
    // prefab is set up, so callers can just null-check and carry on.
    public static SpeechBalloon Show(string text, Transform anchor, Vector3 worldOffset)
    {
        if (instance == null)
        {
            Debug.LogWarning("SpeechBalloonManager: none in the scene, no balloon shown");
            return null;
        }
        return instance.ShowBalloon(text, anchor, worldOffset);
    }

    // Camera the balloons project through: the active camera with the highest depth, which is the boss camera
    // while it is on and the main camera otherwise
    public static Camera ActiveCamera
    {
        get
        {
            Camera best = null;
            foreach (var cam in Camera.allCameras)
            {
                if ((cam == null) || !cam.enabled || !cam.gameObject.activeInHierarchy) continue;
                if ((best == null) || (cam.depth > best.depth)) best = cam;
            }
            return (best != null) ? best : Camera.main;
        }
    }

    SpeechBalloon ShowBalloon(string text, Transform anchor, Vector3 worldOffset)
    {
        SpeechBalloon balloon = GetFree();
        if (balloon == null) return null;

        balloon.Show(text, anchor, worldOffset);
        return balloon;
    }

    // An inactive balloon is a free one (Hide deactivates once the fade is done)
    SpeechBalloon GetFree()
    {
        foreach (var balloon in pool)
        {
            if ((balloon != null) && !balloon.gameObject.activeSelf) return balloon;
        }

        if (balloonPrefab == null)
        {
            Debug.LogWarning("SpeechBalloonManager: no balloon prefab assigned", this);
            return null;
        }

        Transform parent = (container != null) ? container : transform;
        SpeechBalloon created = Instantiate(balloonPrefab, parent);
        created.name = balloonPrefab.name;
        created.gameObject.SetActive(false);
        pool.Add(created);
        return created;
    }
}
