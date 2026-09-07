using UnityEngine;

// Shared by Title and Pause. The legacy master value migrates to SFX once.
public static class GameSettings
{
    public const string MasterVolumeKey = "MasterVolume";
    public const string SfxVolumeKey = "SfxVolume";
    public const string BgmVolumeKey = "BgmVolume";
    public const string CameraShakeEnabledKey = "CameraShakeEnabled";

    private const float DefaultSfxVolume = 1f;
    private const float DefaultBgmVolume = 0.6f;
    private static bool volumeKeysInitialized;

    public static float SfxVolume
    {
        get
        {
            EnsureVolumeKeys();
            return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        }
    }

    public static float BgmVolume
    {
        get
        {
            EnsureVolumeKeys();
            return Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume));
        }
    }

    // Compatibility aliases for existing callers and serialized UnityEvents.
    public static float MasterVolume => SfxVolume;
    public static bool CameraShakeEnabled => PlayerPrefs.GetInt(CameraShakeEnabledKey, 1) != 0;
    public static bool HasCameraShakePreference => PlayerPrefs.HasKey(CameraShakeEnabledKey);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        volumeKeysInitialized = false;
    }

    public static void ApplySavedVolume() => ApplySavedAudioSettings();

    public static void ApplySavedAudioSettings()
    {
        EnsureVolumeKeys();
        AudioListener.volume = 1f;
        SfxPlayer.SetVolume(SfxVolume);
        BgmPlayer.SetVolume(BgmVolume);
    }

    public static void SetMasterVolume(float value)
    {
        SetSfxVolume(value);
    }

    public static void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);
        EnsureVolumeKeys();
        AudioListener.volume = 1f;
        PlayerPrefs.SetFloat(SfxVolumeKey, value);
        SfxPlayer.SetVolume(value);
    }

    public static void SetBgmVolume(float value)
    {
        value = Mathf.Clamp01(value);
        EnsureVolumeKeys();
        AudioListener.volume = 1f;
        PlayerPrefs.SetFloat(BgmVolumeKey, value);
        BgmPlayer.SetVolume(value);
    }

    public static void SetCameraShakeEnabled(bool value)
    {
        PlayerPrefs.SetInt(CameraShakeEnabledKey, value ? 1 : 0);
        ApplyCameraShake();
    }

    public static void ApplyCameraShake()
    {
        if (CameraShake.Instance != null)
            CameraShake.Instance.SetIntensity(CameraShakeEnabled ? 1f : 0f);
    }

    public static void Save() => PlayerPrefs.Save();

    private static void EnsureVolumeKeys()
    {
        if (volumeKeysInitialized)
            return;

        bool changed = false;
        if (!PlayerPrefs.HasKey(SfxVolumeKey))
        {
            float legacyValue = PlayerPrefs.HasKey(MasterVolumeKey)
                ? Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultSfxVolume))
                : DefaultSfxVolume;
            PlayerPrefs.SetFloat(SfxVolumeKey, legacyValue);
            changed = true;
        }

        if (!PlayerPrefs.HasKey(BgmVolumeKey))
        {
            PlayerPrefs.SetFloat(BgmVolumeKey, DefaultBgmVolume);
            changed = true;
        }

        volumeKeysInitialized = true;
        if (changed)
            PlayerPrefs.Save();
    }
}
