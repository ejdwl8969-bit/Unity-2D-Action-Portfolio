using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Lives on the existing persistent GlobalUI, not on the inactive PausePanel.
[DefaultExecutionOrder(-100)]
public class PauseManager : MonoBehaviour
{
    public const string MasterVolumeKey = GameSettings.MasterVolumeKey;
    public const string CameraShakeEnabledKey = GameSettings.CameraShakeEnabledKey;

    public static PauseManager Instance { get; private set; }
    public static bool IsGamePaused => Instance != null && Instance.IsPaused;
    public static bool IsUpgradeSelectionOpen =>
        Instance != null && Instance.levelUpUI != null && Instance.levelUpUI.IsOpen;
    public bool IsPaused { get; private set; }

    [Header("Pause UI (GlobalUI/Canvas/GameplayUI/PausePanel)")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private TMP_Text bgmVolumeValueText;
    [SerializeField] private Toggle cameraShakeToggle;
    [SerializeField] private Button homeButton;

    [Header("Scenes")]
    [SerializeField] private string homeSceneName = "WaitingRoom";
    [SerializeField] private string[] pauseSceneNames =
    {
        "WaitingRoom", "Room1-1", "Room1-2", "Room1-3", "BossRoom1",
        "Room2-1", "Room2-2", "Room2-3", "BossRoom2"
    };

    private PlayerHealth playerHealth;
    private LevelUpUI levelUpUI;
    private GameObject previousSelection;
    private bool sceneSupportsPause;
    private bool uiReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedVolume()
    {
        GameSettings.ApplySavedVolume();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        Instance = this;
        uiReady = pausePanel != null && resumeButton != null && volumeSlider != null &&
                  volumeValueText != null && bgmVolumeSlider != null &&
                  bgmVolumeValueText != null && cameraShakeToggle != null && homeButton != null;
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (!uiReady)
            Debug.LogError("PauseManager: Assign all Pause UI references on GlobalUI.", this);
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        if (uiReady)
        {
            resumeButton.onClick.AddListener(Resume);
            homeButton.onClick.AddListener(ReturnHome);
            volumeSlider.onValueChanged.AddListener(SetMasterVolume);
            bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
            cameraShakeToggle.onValueChanged.AddListener(SetCameraShakeEnabled);
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
            bgmVolumeSlider.minValue = 0f;
            bgmVolumeSlider.maxValue = 1f;
            bgmVolumeSlider.wholeNumbers = false;
        }
        BindScene();
        RefreshSettings();
    }

    private void Update()
    {
        if (SceneTransition.IsTransitioning)
            return;

        if (IsPaused && (HasEnded() || IsUpgradeSelectionOpen))
        {
            ClosePause(!IsUpgradeSelectionOpen);
            return;
        }

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        if (SceneTransition.IsTransitioning || Instance != this || !isActiveAndEnabled || IsPaused || !uiReady ||
            !sceneSupportsPause || playerHealth == null || HasEnded() || IsUpgradeSelectionOpen)
            return;

        // Do not take ownership of time stopped by another modal UI.
        if (Time.timeScale <= 0f &&
            (HitStopManager.Instance == null || !HitStopManager.Instance.IsHitStopping))
            return;

        // A realtime HitStop must not resume the game behind the Pause panel.
        if (HitStopManager.Instance != null)
            HitStopManager.Instance.CancelHitStop();

        IsPaused = true;
        Time.timeScale = 0f;
        RefreshSettings();
        pausePanel.SetActive(true);
        pausePanel.transform.SetAsLastSibling();
        if (EventSystem.current != null)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }
    }

    public void Resume()
    {
        if (Instance != this || !IsPaused)
            return;
        ClosePause(!IsUpgradeSelectionOpen);
    }

    private void ClosePause(bool restoreTime)
    {
        if (!IsPaused)
            return;
        IsPaused = false;
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (restoreTime && !SceneTransition.IsTransitioning && !(RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            Time.timeScale = 1f;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(
                previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        previousSelection = null;
        PlayerPrefs.Save();
    }

    public void ReturnHome()
    {
        if (Instance != this || !IsPaused)
            return;
        if (!SceneTransition.CanLoadScene(homeSceneName))
            return;

        ClosePause(true);
        Time.timeScale = 1f;
        // Same abandon-run policy as GameOverUI.Retry / WaitingRoomProgress.StartNewRun.
        if (RunManager.Instance != null)
            RunManager.Instance.ResetRun();
        SceneTransition.LoadScene(homeSceneName);
    }

    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        GameSettings.SetSfxVolume(value);
        if (volumeValueText != null)
            volumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    public void SetBgmVolume(float value)
    {
        value = Mathf.Clamp01(value);
        GameSettings.SetBgmVolume(value);
        if (bgmVolumeValueText != null)
            bgmVolumeValueText.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    public void SetCameraShakeEnabled(bool value)
    {
        GameSettings.SetCameraShakeEnabled(value);
    }

    private void RefreshSettings()
    {
        ApplySavedVolume();
        bool shakeEnabled = GameSettings.CameraShakeEnabled;
        GameSettings.ApplyCameraShake();
        if (!uiReady)
            return;
        volumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        volumeValueText.text = $"{Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}%";
        bgmVolumeSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
        bgmVolumeValueText.text = $"{Mathf.RoundToInt(GameSettings.BgmVolume * 100f)}%";
        cameraShakeToggle.SetIsOnWithoutNotify(shakeEnabled);
        homeButton.interactable = SceneManager.GetActiveScene().name != homeSceneName;
    }

    private bool HasEnded()
    {
        return (playerHealth != null && playerHealth.IsDead) ||
               (RunManager.Instance != null && RunManager.Instance.IsRunCleared);
    }

    private void BindScene()
    {
        var scene = SceneManager.GetActiveScene();
        sceneSupportsPause = System.Array.IndexOf(pauseSceneNames, scene.name) >= 0;
        playerHealth = null;
        levelUpUI = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (playerHealth == null)
                playerHealth = root.GetComponentInChildren<PlayerHealth>();
            if (levelUpUI == null)
                levelUpUI = root.GetComponentInChildren<LevelUpUI>(true);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            ClosePause(true);
        BindScene();
        RefreshSettings();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && Instance == this)
            PlayerPrefs.Save();
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (uiReady)
        {
            resumeButton.onClick.RemoveListener(Resume);
            homeButton.onClick.RemoveListener(ReturnHome);
            volumeSlider.onValueChanged.RemoveListener(SetMasterVolume);
            bgmVolumeSlider.onValueChanged.RemoveListener(SetBgmVolume);
            cameraShakeToggle.onValueChanged.RemoveListener(SetCameraShakeEnabled);
        }
        ClosePause(!IsUpgradeSelectionOpen);
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;
        ClosePause(!IsUpgradeSelectionOpen);
        Instance = null;
    }
}
