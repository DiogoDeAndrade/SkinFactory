using UC;
using UnityEngine;

// The release console: with the player standing at it (production, painting done and code written), holding the interact key
// pulls the lever; letting go lets it fall back. Once the lever reaches the on position the skin is released and
// LevelManager starts the next day. The lever transform is rotated from its rest pose to onRotation as progress.
public class ReleaseStation : Minigame
{
    [SerializeField, Min(0.01f), Tooltip("Seconds the interact key has to be held to pull the lever all the way")]
    private float       holdTime = 1.0f;
    [SerializeField, Min(0), Tooltip("Seconds for the lever to fall back from fully pulled when the key is let go (0 = snaps back)")]
    private float       releaseTime = 0.5f;

    [Header("Lever")]
    [SerializeField, Tooltip("Rotated from its rest pose towards the on rotation as the hold progresses; optional")]
    private Transform   lever;
    [SerializeField, Tooltip("Local euler rotation added to the lever's rest pose when fully pulled")]
    private Vector3     onRotation = new Vector3(0.0f, 0.0f, -55.0f);

    [Header("Sounds")]
    [SerializeField, Tooltip("Plays while the lever is being pulled, its pitch rising with the pull; cut when the key is let go or the lever locks")]
    private SoundDef    leverChargeSound;
    [SerializeField, Min(1), Tooltip("Pitch multiplier of the charge sound with the lever all the way (1 at rest)")]
    private float       leverChargeMaxPitch = 2.0f;
    [SerializeField] private SoundDef leverDropSound;   // The key is let go before the lever is all the way
    [SerializeField, Tooltip("The lever locks and the skin is released. Layered with the rise sound: plays only when both are assigned")]
    private SoundDef    launchSound;
    [SerializeField] private SoundDef launchRiseSound;

    // 0 = lever at rest, 1 = pulled all the way
    public float progress => Mathf.Clamp01(heldFor / holdTime);

    Player      player;
    Quaternion  leverRest;
    float       heldFor;
    bool        active;
    bool        fired;
    bool        wasPulling;
    AudioSource chargeSource;   // Pooled by the SoundManager: only ours while it still plays chargeClip
    AudioClip   chargeClip;
    float       chargePitch;

    public override bool CanUse(Player player)
    {
        if (player == null) return false;
        if ((LevelManager.instance != null) && (LevelManager.instance.state != LevelManager.State.Production)) return false;

        // A painting and the code: the skin is complete
        return (player.painting != null) && player.hasCode;
    }

    void Start()
    {
        if (lever != null) leverRest = lever.localRotation;
        UpdateLever();
    }

    public override void Activate()
    {
        active = true;
        fired = false;
        if (player == null) player = FindAnyObjectByType<Player>();
    }

    public override void Deactivate()
    {
        active = false;
    }

    void Update()
    {
        bool pulling = active && !fired && (player != null) && player.interactHeld;

        if (pulling)
        {
            if (!wasPulling) StartCharge();

            heldFor += Time.deltaTime;
            if (heldFor >= holdTime)
            {
                heldFor = holdTime;
                Fire();
            }
        }
        else if (heldFor > 0.0f)
        {
            // Falls back at a rate that takes releaseTime from fully pulled
            float rate = (releaseTime > 0.0f) ? (holdTime / releaseTime) : float.MaxValue;
            heldFor = Mathf.Max(0.0f, heldFor - rate * Time.deltaTime);
        }

        // Let go before the end: the charge is cut and the lever drops. Not after a launch, which has its own sound.
        if (wasPulling && !pulling && !fired)
        {
            StopCharge();
            leverDropSound?.Play();
        }
        wasPulling = pulling;

        UpdateCharge();
        UpdateLever();
    }

    void StartCharge()
    {
        StopCharge();

        chargeSource = leverChargeSound?.Play();
        chargeClip = (chargeSource != null) ? chargeSource.clip : null;
        chargePitch = (chargeSource != null) ? chargeSource.pitch : 1.0f;
    }

    void UpdateCharge()
    {
        if ((chargeSource == null) || (chargeSource.clip != chargeClip)) return;

        chargeSource.pitch = chargePitch * Mathf.Lerp(1.0f, leverChargeMaxPitch, progress);
    }

    void StopCharge()
    {
        if ((chargeSource != null) && (chargeSource.clip == chargeClip) && chargeSource.isPlaying) chargeSource.Stop();
        chargeSource = null;
        chargeClip = null;
    }

    void UpdateLever()
    {
        if (lever == null) return;
        lever.localRotation = leverRest * Quaternion.Euler(onRotation * progress);
    }

    void Fire()
    {
        fired = true;
        active = false;

        StopCharge();
        GameSounds.PlayTogether(launchSound, launchRiseSound);

        if (LevelManager.instance != null) LevelManager.instance.Release();
        else Debug.LogWarning("ReleaseStation: no LevelManager in the scene", this);
    }
}
