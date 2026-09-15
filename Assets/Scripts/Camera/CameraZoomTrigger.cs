using UnityEngine;

// Cosmetic: while the player is inside this trigger volume, the orbit camera zooms in on them.
// Meant for spots like the skin display, where a closer look is nicer.
// The camera owns the zoom state (target + smoothing), so this only ever says "zoom to X" or "back to 1":
// entering and leaving in any order can't get it stuck.
[RequireComponent(typeof(BoxCollider))]
public class CameraZoomTrigger : MonoBehaviour
{
    [SerializeField, Tooltip("Orbit camera to zoom; found in the scene if left empty")]
    private OrbitCameraController       cameraController;
    [SerializeField, Range(0.1f, 1.0f), Tooltip("Camera distance multiplier while the player is inside")]
    private float                       zoomFactor = 0.6f;

    int insideCount;    // Player colliders currently inside

    void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void Awake()
    {
        if (cameraController == null) cameraController = FindAnyObjectByType<OrbitCameraController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<Player>() == null) return;

        insideCount++;
        Apply();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<Player>() == null) return;

        insideCount = Mathf.Max(0, insideCount - 1);
        Apply();
    }

    void OnDisable()
    {
        // Never leave the camera zoomed in if this trigger goes away
        insideCount = 0;
        Apply();
    }

    void Apply()
    {
        if (cameraController == null) return;

        cameraController.zoomFactor = (insideCount > 0) ? zoomFactor : 1.0f;
    }

    void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = new Color(0.3f, 0.7f, 1.0f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.3f, 0.7f, 1.0f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
