using UnityEngine;

// The "launch button": a station that releases the finished skin when the player steps up to it.
// Usable only while the player carries a painting; releasing hands over to GameLoop, which starts the next day.
public class ReleaseStation : Minigame
{
    [SerializeField, Min(0), Tooltip("Seconds the player has to stay at the station before the release fires (0 = immediate)")]
    private float   holdTime = 0.5f;

    float   heldFor;
    bool    active;

    public override bool CanUse(Player player)
    {
        if (player == null) return false;
        if ((GameLoop.instance != null) && (GameLoop.instance.state != GameLoop.State.Running)) return false;

        return player.painting != null;
    }

    public override void Activate()
    {
        active = true;
        heldFor = 0.0f;
        if (holdTime <= 0.0f) Fire();
    }

    public override void Deactivate()
    {
        active = false;
        heldFor = 0.0f;
    }

    void Update()
    {
        if (!active) return;

        heldFor += Time.deltaTime;
        if (heldFor >= holdTime) Fire();
    }

    void Fire()
    {
        active = false;
        heldFor = 0.0f;

        if (GameLoop.instance != null) GameLoop.instance.Release();
        else Debug.LogWarning("ReleaseStation: no GameLoop in the scene", this);
    }
}
