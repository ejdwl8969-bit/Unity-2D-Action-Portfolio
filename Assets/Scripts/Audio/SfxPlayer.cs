using System;
using UnityEngine;

public enum SfxId
{
    BossMeteor,
    PlayerJump,
    BossSmash,
    LevelUpOpen,
    UpgradeSelect,
    UiClick,
    BowAttack,
    ExpCollect,
    WindSlash,
    SwordAttack,
    DaggerAttack
}

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[DefaultExecutionOrder(100)]
public sealed class SfxPlayer : MonoBehaviour
{
    [Serializable]
    private struct SfxEntry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volumeScale;
        [Min(0f)] public float minimumInterval;
    }

    private static SfxPlayer instance;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private SfxEntry[] entries = Array.Empty<SfxEntry>();

    private readonly float[] lastPlayedAt = new float[Enum.GetValues(typeof(SfxId)).Length];

    public static SfxPlayer Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = GameSettings.SfxVolume;
            audioSource.pitch = 1f;
        }

        for (int i = 0; i < lastPlayedAt.Length; i++)
            lastPlayedAt[i] = float.NegativeInfinity;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static bool Play(SfxId id)
    {
        return instance != null && instance.PlayInternal(id);
    }

    public static void SetVolume(float value)
    {
        if (instance != null)
            instance.ApplyVolume(value);
    }

    private bool PlayInternal(SfxId id)
    {
        if (!isActiveAndEnabled || audioSource == null || entries == null)
            return false;

        for (int i = 0; i < entries.Length; i++)
        {
            SfxEntry entry = entries[i];
            if (entry.id != id || entry.clip == null)
                continue;

            int index = (int)id;
            float now = Time.unscaledTime;
            if (entry.minimumInterval > 0f &&
                now < lastPlayedAt[index] + entry.minimumInterval)
            {
                return false;
            }

            lastPlayedAt[index] = now;
            audioSource.PlayOneShot(entry.clip, entry.volumeScale);
            return true;
        }

        return false;
    }

    private void ApplyVolume(float value)
    {
        if (audioSource != null)
            audioSource.volume = Mathf.Clamp01(value);
    }
}
