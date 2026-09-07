using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ElementSelectionUI : MonoBehaviour
{
    private const string WaitingRoomSceneName = "WaitingRoom";

    [Header("Panel")]
    [SerializeField] private GameObject panel;

    [Header("Buttons")]
    [SerializeField] private Button fireButton;
    [SerializeField] private Button lightningButton;
    [SerializeField] private Button iceButton;
    [SerializeField] private Button windButton;

    [Header("Existing Element Upgrades")]
    [SerializeField] private UpgradeData fireUpgrade;
    [SerializeField] private UpgradeData lightningUpgrade;
    [SerializeField] private UpgradeData iceUpgrade;
    [SerializeField] private UpgradeData windUpgrade;

    private UpgradeManager upgradeManager;
    private PlayerController blockedController;
    private PlayerAttack blockedAttack;
    private PlayerStatusUI blockedStatusUI;
    private GameObject previousSelection;
    private float previousTimeScale = 1f;
    private bool controllerWasEnabled;
    private bool attackWasEnabled;
    private bool statusWasEnabled;
    private bool isSelectionCommitted;
    private Coroutine showRoutine;

    public bool IsOpen => panel != null && panel.activeInHierarchy;

    private void Awake()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddButtonListeners();
        ScheduleShowIfNeeded();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RemoveButtonListeners();
        Hide(true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Hide(false);
        ScheduleShowIfNeeded();
    }

    private void ScheduleShowIfNeeded()
    {
        if (showRoutine != null)
            StopCoroutine(showRoutine);

        showRoutine = StartCoroutine(ShowWhenReady());
    }

    private IEnumerator ShowWhenReady()
    {
        yield return null;

        for (int frame = 0; frame < 60; frame++)
        {
            if (SceneManager.GetActiveScene().name != WaitingRoomSceneName)
                break;

            RunManager runManager = RunManager.Instance;
            if (runManager != null && runManager.HasSelectedElement)
                break;

            if (!SceneTransition.IsTransitioning &&
                runManager != null &&
                Time.timeScale > 0f)
            {
                upgradeManager = FindFirstObjectByType<UpgradeManager>();
                if (upgradeManager != null && upgradeManager.PlayerAttack != null)
                {
                    Show();
                    break;
                }
            }

            yield return null;
        }

        showRoutine = null;
    }

    private void Show()
    {
        if (panel == null || IsOpen ||
            RunManager.Instance == null ||
            RunManager.Instance.HasSelectedElement)
        {
            return;
        }

        isSelectionCommitted = false;
        SetButtonsInteractable(true);
        BlockGameplayInput();

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        if (EventSystem.current != null && fireButton != null)
        {
            previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(fireButton.gameObject);
        }
    }

    private void SelectFire() => TrySelect(fireUpgrade);
    private void SelectLightning() => TrySelect(lightningUpgrade);
    private void SelectIce() => TrySelect(iceUpgrade);
    private void SelectWind() => TrySelect(windUpgrade);

    private void TrySelect(UpgradeData upgrade)
    {
        if (isSelectionCommitted || upgradeManager == null)
            return;

        isSelectionCommitted = true;
        SetButtonsInteractable(false);

        if (upgradeManager.TryAcquireInitialElement(upgrade))
        {
            Hide(true);
            return;
        }

        isSelectionCommitted = false;
        SetButtonsInteractable(true);
    }

    private void BlockGameplayInput()
    {
        blockedController = FindFirstObjectByType<PlayerController>();
        blockedAttack = blockedController != null
            ? blockedController.GetComponent<PlayerAttack>()
            : FindFirstObjectByType<PlayerAttack>();
        blockedStatusUI = GetComponent<PlayerStatusUI>();

        controllerWasEnabled = blockedController != null && blockedController.enabled;
        attackWasEnabled = blockedAttack != null && blockedAttack.enabled;
        statusWasEnabled = blockedStatusUI != null && blockedStatusUI.enabled;

        if (blockedController != null)
            blockedController.enabled = false;
        if (blockedAttack != null)
            blockedAttack.enabled = false;
        if (blockedStatusUI != null)
            blockedStatusUI.enabled = false;
    }

    private void RestoreGameplayInput()
    {
        if (blockedController != null)
            blockedController.enabled = controllerWasEnabled;
        if (blockedAttack != null)
            blockedAttack.enabled = attackWasEnabled;
        if (blockedStatusUI != null)
            blockedStatusUI.enabled = statusWasEnabled;

        blockedController = null;
        blockedAttack = null;
        blockedStatusUI = null;
    }

    private void Hide(bool restoreTime)
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (panel != null)
            panel.SetActive(false);

        RestoreGameplayInput();

        if (restoreTime &&
            !SceneTransition.IsTransitioning &&
            !PauseManager.IsGamePaused &&
            !(RunManager.Instance != null && RunManager.Instance.IsRunCleared))
        {
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(
                previousSelection != null && previousSelection.activeInHierarchy
                    ? previousSelection
                    : null);
        }

        previousSelection = null;
    }

    private void AddButtonListeners()
    {
        if (fireButton != null)
            fireButton.onClick.AddListener(SelectFire);
        if (lightningButton != null)
            lightningButton.onClick.AddListener(SelectLightning);
        if (iceButton != null)
            iceButton.onClick.AddListener(SelectIce);
        if (windButton != null)
            windButton.onClick.AddListener(SelectWind);
    }

    private void RemoveButtonListeners()
    {
        if (fireButton != null)
            fireButton.onClick.RemoveListener(SelectFire);
        if (lightningButton != null)
            lightningButton.onClick.RemoveListener(SelectLightning);
        if (iceButton != null)
            iceButton.onClick.RemoveListener(SelectIce);
        if (windButton != null)
            windButton.onClick.RemoveListener(SelectWind);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (fireButton != null)
            fireButton.interactable = interactable;
        if (lightningButton != null)
            lightningButton.interactable = interactable;
        if (iceButton != null)
            iceButton.interactable = interactable;
        if (windButton != null)
            windButton.interactable = interactable;
    }
}