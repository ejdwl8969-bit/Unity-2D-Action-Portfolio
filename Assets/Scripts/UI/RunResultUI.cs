using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Presentation only. RunManager owns the statistics, snapshot and time freeze.
public class RunResultUI : MonoBehaviour
{
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text clearTimeValue;
    [SerializeField] private TMP_Text finalLevelValue;
    [SerializeField] private TMP_Text weaponValue;
    [SerializeField] private TMP_Text elementValue;
    [SerializeField] private TMP_Text enemiesDefeatedValue;
    [SerializeField] private TMP_Text damageTakenValue;
    [SerializeField] private Button waitingRoomButton;

    private RunManager boundRun;

    private void Awake()
    {
        Hide();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (waitingRoomButton != null)
        {
            // Preserve existing prefab IDs / scene overrides, but retire the legacy
            // WaitingRoom-only StartNewRun callback in favor of the shared RunManager.
            for (int i = 0; i < waitingRoomButton.onClick.GetPersistentEventCount(); i++)
                waitingRoomButton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            waitingRoomButton.onClick.AddListener(ReturnToWaitingRoom);
        }
        BindRunManager();
    }

    private void Start()
    {
        BindRunManager();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindRunManager();
    }

    private void BindRunManager()
    {
        if (boundRun != RunManager.Instance)
        {
            UnbindRunManager();
            boundRun = RunManager.Instance;
            if (boundRun != null)
            {
                boundRun.RunCleared += Show;
                boundRun.RunReset += Hide;
            }
        }
        ShowCurrentResult();
    }

    public void ShowCurrentResult()
    {
        RunManager run = RunManager.Instance;
        if (run != null && run.IsRunCleared && run.LastResult != null)
            Show(run.LastResult);
        else
            Hide();
    }

    public void Show(RunResultData result)
    {
        if (result == null || resultPanel == null)
            return;
        SetText(clearTimeValue, result.FormattedClearTime);
        SetText(finalLevelValue, result.FinalLevel.ToString());
        SetText(weaponValue, result.Weapon);
        SetText(elementValue, result.Element);
        SetText(enemiesDefeatedValue, result.EnemiesDefeated.ToString());
        SetText(damageTakenValue, result.DamageTaken.ToString());
        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();
        if (waitingRoomButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(waitingRoomButton.gameObject);
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
            label.text = value;
    }

    public void Hide()
    {
        if (waitingRoomButton != null && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == waitingRoomButton.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    public void ReturnToWaitingRoom()
    {
        RunManager run = RunManager.Instance;
        if (run != null && run.IsRunCleared)
            run.ReturnToWaitingRoom();
    }

    private void UnbindRunManager()
    {
        if (boundRun != null)
        {
            boundRun.RunCleared -= Show;
            boundRun.RunReset -= Hide;
        }
        boundRun = null;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindRunManager();
        if (waitingRoomButton != null)
            waitingRoomButton.onClick.RemoveListener(ReturnToWaitingRoom);
        Hide();
    }
}
