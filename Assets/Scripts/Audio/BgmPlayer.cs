using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BgmTrack
{
    None,
    Track3,
    Track6
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class BgmPlayer : MonoBehaviour
{
    private static BgmPlayer instance;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip track3;
    [SerializeField] private AudioClip track6;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

    private AudioClip requestedClip;
    private Coroutine fadeCoroutine;
    private float fadeMultiplier = 1f;

    public static BgmPlayer Instance => instance;
    public AudioClip CurrentClip => audioSource != null ? audioSource.clip : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
        if (audioSource == null)
            Debug.LogError("BgmPlayer: Assign the dedicated BGM AudioSource.", this);

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            audioSource.pitch = 1f;
            ApplyVolume(GameSettings.BgmVolume);
        }
    }

    private void OnEnable()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        ResolveAndPlay(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void SetVolume(float value)
    {
        if (instance != null)
            instance.ApplyVolume(value);
    }

    public static BgmTrack ResolveTrack(string sceneName, int currentChapter)
    {
        switch (sceneName)
        {
            case "Title":
            case "Room1-1":
            case "Room1-2":
            case "Room1-3":
            case "BossRoom1":
                return BgmTrack.Track6;

            case "Room2-1":
            case "Room2-2":
            case "Room2-3":
            case "BossRoom2":
                return BgmTrack.Track3;

            case "WaitingRoom":
                return currentChapter >= 2 ? BgmTrack.Track3 : BgmTrack.Track6;

            default:
                return BgmTrack.None;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance == this && mode == LoadSceneMode.Single)
            ResolveAndPlay(scene);
    }

    private void ResolveAndPlay(Scene scene)
    {
        int chapter = RunManager.Instance != null ? RunManager.Instance.CurrentChapter : 1;
        RequestTrack(GetClip(ResolveTrack(scene.name, chapter)));
    }

    private AudioClip GetClip(BgmTrack track)
    {
        switch (track)
        {
            case BgmTrack.Track3:
                return track3;
            case BgmTrack.Track6:
                return track6;
            default:
                return null;
        }
    }

    private void RequestTrack(AudioClip targetClip)
    {
        if (audioSource == null || targetClip == null)
            return;

        if (requestedClip == targetClip &&
            (fadeCoroutine != null || (audioSource.clip == targetClip && audioSource.isPlaying)))
        {
            return;
        }

        requestedClip = targetClip;
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (audioSource.clip == targetClip && audioSource.isPlaying)
        {
            // Adjacent scenes that resolve to the same track keep playback position.
            fadeMultiplier = 1f;
            ApplyVolume(GameSettings.BgmVolume);
            return;
        }

        fadeCoroutine = StartCoroutine(SwitchTrack(targetClip));
    }

    private IEnumerator SwitchTrack(AudioClip targetClip)
    {
        if (audioSource.isPlaying && audioSource.clip != null)
            yield return Fade(fadeMultiplier, 0f);

        audioSource.clip = targetClip;
        audioSource.loop = true;
        fadeMultiplier = 0f;
        ApplyVolume(GameSettings.BgmVolume);
        audioSource.Play();

        yield return Fade(0f, 1f);
        fadeMultiplier = 1f;
        ApplyVolume(GameSettings.BgmVolume);
        fadeCoroutine = null;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeDuration <= 0f)
        {
            fadeMultiplier = to;
            ApplyVolume(GameSettings.BgmVolume);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeMultiplier = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            ApplyVolume(GameSettings.BgmVolume);
            yield return null;
        }

        fadeMultiplier = to;
        ApplyVolume(GameSettings.BgmVolume);
    }

    private void ApplyVolume(float value)
    {
        if (audioSource != null)
            audioSource.volume = Mathf.Clamp01(value) * fadeMultiplier;
    }
}
