using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    private const string PrefabResourcePath = "UI/GameOverUI";

    [SerializeField]
    private GameObject gameOverPanel;

    [SerializeField]
    private Button retryButton;

    private PlayerHealth playerHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include) != null)
            return;

        PlayerStatusUI globalUi =
            FindFirstObjectByType<PlayerStatusUI>(FindObjectsInactive.Include);
        Transform gameplayUi = globalUi != null
            ? FindChild(globalUi.transform, "GameplayUI")
            : null;
        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (gameplayUi == null || prefab == null)
        {
            Debug.LogError("GameOverUI: GlobalUI/GameplayUI or Resources/UI/GameOverUI is missing.");
            return;
        }

        Instantiate(prefab, gameplayUi, false);
    }

    private void Awake()
    {
        Hide();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (retryButton != null)
            retryButton.onClick.AddListener(Retry);
        BindPlayer();
    }

    private void Start()
    {
        BindPlayer();
    }

    private void BindPlayer()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= ShowGameOver;

        playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.OnDied += ShowGameOver;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Hide();
        BindPlayer();
    }

    private void ShowGameOver()
    {
        if (gameOverPanel == null ||
            (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        if (HitStopManager.Instance != null)
            HitStopManager.Instance.CancelHitStop();
        if (PauseManager.Instance != null)
            PauseManager.Instance.Resume();

        Time.timeScale = 0f;
        gameOverPanel.SetActive(true);
        gameOverPanel.transform.SetAsLastSibling();
        if (retryButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    public void Retry()
    {
        if (!SceneTransition.CanLoadScene("WaitingRoom"))
            return;

        Time.timeScale = 1f;
        Hide();
        if (RunManager.Instance != null)
            RunManager.Instance.ResetRun();

        SceneTransition.LoadScene("WaitingRoom");
    }

    private void Hide()
    {
        if (retryButton != null && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == retryButton.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (retryButton != null)
            retryButton.onClick.RemoveListener(Retry);
        if (playerHealth != null)
            playerHealth.OnDied -= ShowGameOver;
        playerHealth = null;
        Hide();
    }

    private static Transform FindChild(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
