using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Scene-local presentation. RunManager and SceneTransition keep their existing lifecycles.
public class TitleMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private TMP_Text bgmVolumeValueText;
    [SerializeField] private Toggle cameraShakeToggle;

    private bool uiReady;
    private bool selectAfterFade;

    private void Awake()
    {
        uiReady = mainMenu != null && settingsPanel != null && startButton != null &&
                  settingsButton != null && quitButton != null && backButton != null &&
                  volumeSlider != null && volumeValueText != null &&
                  bgmVolumeSlider != null && bgmVolumeValueText != null &&
                  cameraShakeToggle != null;
        if (!uiReady)
        {
            Debug.LogError("TitleMenuUI: Assign all menu and settings references.", this);
            return;
        }
        mainMenu.SetActive(true);
        settingsPanel.SetActive(false);
        selectAfterFade = true;
    }

    private void OnEnable()
    {
        if (!uiReady)
            return;
        startButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);
        backButton.onClick.AddListener(CloseSettings);
        volumeSlider.onValueChanged.AddListener(SetVolume);
        bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
        cameraShakeToggle.onValueChanged.AddListener(SetCameraShake);
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.wholeNumbers = false;
        bgmVolumeSlider.minValue = 0f;
        bgmVolumeSlider.maxValue = 1f;
        bgmVolumeSlider.wholeNumbers = false;
        RefreshSettings();
    }

    private void Update()
    {
        if (!uiReady || SceneTransition.IsTransitioning)
            return;
        if (selectAfterFade && EventSystem.current != null)
        {
            Select(startButton.gameObject);
            selectAfterFade = false;
        }
        if (settingsPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseSettings();
    }

    public void StartGame()
    {
        if (!uiReady || SceneTransition.IsTransitioning)
            return;
        RunManager run = RunManager.Instance;
        if (run == null)
        {
            Debug.LogError("TitleMenuUI: RunManager is missing from the Title Scene.", this);
            return;
        }
        GameSettings.Save();
        // Existing API checks the target, restores time, resets this run, then fades to the hub.
        // Chapter 1 -> hub -> Chapter 2 never calls this Title button.
        run.ReturnToWaitingRoom();
    }

    public void OpenSettings()
    {
        if (!uiReady || SceneTransition.IsTransitioning)
            return;
        selectAfterFade = false;
        RefreshSettings();
        mainMenu.SetActive(false);
        settingsPanel.SetActive(true);
        Select(volumeSlider.gameObject);
    }

    public void CloseSettings()
    {
        if (!uiReady || SceneTransition.IsTransitioning)
            return;
        GameSettings.Save();
        settingsPanel.SetActive(false);
        mainMenu.SetActive(true);
        Select(settingsButton.gameObject);
    }

    private void RefreshSettings()
    {
        GameSettings.ApplySavedAudioSettings();
        volumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        bgmVolumeSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
        UpdateVolumeText();
        UpdateBgmVolumeText();
        cameraShakeToggle.SetIsOnWithoutNotify(GameSettings.CameraShakeEnabled);
    }

    private void SetVolume(float value)
    {
        GameSettings.SetSfxVolume(value);
        UpdateVolumeText();
    }

    private void SetBgmVolume(float value)
    {
        GameSettings.SetBgmVolume(value);
        UpdateBgmVolumeText();
    }

    private void UpdateVolumeText() => volumeValueText.text = $"{Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}%";
    private void UpdateBgmVolumeText() => bgmVolumeValueText.text = $"{Mathf.RoundToInt(GameSettings.BgmVolume * 100f)}%";
    private void SetCameraShake(bool enabled) => GameSettings.SetCameraShakeEnabled(enabled);

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }

    public void QuitGame()
    {
        if (!uiReady || SceneTransition.IsTransitioning)
            return;
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            GameSettings.Save();
    }

    private void OnDisable()
    {
        if (!uiReady)
            return;
        startButton.onClick.RemoveListener(StartGame);
        settingsButton.onClick.RemoveListener(OpenSettings);
        quitButton.onClick.RemoveListener(QuitGame);
        backButton.onClick.RemoveListener(CloseSettings);
        volumeSlider.onValueChanged.RemoveListener(SetVolume);
        bgmVolumeSlider.onValueChanged.RemoveListener(SetBgmVolume);
        cameraShakeToggle.onValueChanged.RemoveListener(SetCameraShake);
        GameSettings.Save();
    }
}
