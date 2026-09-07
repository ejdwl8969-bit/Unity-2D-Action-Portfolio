using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Uses GlobalUI's existing DontDestroyOnLoad lifecycle; no second persistent root.
[DefaultExecutionOrder(-200)]
public class SceneTransition : MonoBehaviour
{
    [SerializeField] private SceneFadeUI fadeUI;

    private static SceneTransition instance;
    public static bool IsTransitioning => instance != null && instance.transitioning;

    private bool transitioning;
    private bool started;
    private bool loadingScene;
    private float restoreTimeScale = 1f;
    private EventSystem blockedEventSystem;
    private bool previousNavigation;
    private GameObject previousSelection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => instance = null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }
        instance = this;
        if (!HasFadeUI())
            return;
        BeginTransition();
        fadeUI.SetImmediateBlack();
    }

    private void OnEnable()
    {
        if (instance == this)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        started = true;
        if (instance == this && transitioning)
            StartCoroutine(RevealScene());
    }

    // Call before Save/Reset so a repeated door/button cannot repeat those side effects.
    public static bool CanLoadScene(string sceneName)
    {
        if (IsTransitioning)
            return false;
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneTransition: Scene is not in the build: {sceneName}");
            return false;
        }
        if (instance == null || !instance.isActiveAndEnabled || !instance.HasFadeUI())
        {
            Debug.LogError("SceneTransition: GlobalUI / SceneFade is not ready. Scene load was not started.");
            return false;
        }
        return true;
    }

    public static bool LoadScene(string sceneName)
    {
        if (!CanLoadScene(sceneName))
            return false;
        instance.BeginTransition();
        instance.StartCoroutine(instance.LoadWithFade(sceneName));
        return true;
    }

    private bool HasFadeUI() => fadeUI != null && fadeUI.IsReady;

    private void BeginTransition()
    {
        bool hitStopping = HitStopManager.Instance != null && HitStopManager.Instance.IsHitStopping;
        restoreTimeScale = hitStopping ? 1f : Time.timeScale;
        if (HitStopManager.Instance != null)
            HitStopManager.Instance.CancelHitStop();
        transitioning = true;
        Time.timeScale = 0f;
        fadeUI.BlockInput();
        BlockNavigation();
    }

    private IEnumerator LoadWithFade(string sceneName)
    {
        yield return fadeUI.FadeOut();
        if (!HasFadeUI())
        {
            FinishTransition();
            yield break;
        }
        fadeUI.SetImmediateBlack();
        // Render an opaque frame before loading. The persistent overlay never disappears.
        yield return null;
        loadingScene = true;
        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }
        if (operation != null)
        {
            yield return operation;
            restoreTimeScale = 1f;
        }
        loadingScene = false;
        yield return RevealScene();
    }

    private IEnumerator RevealScene()
    {
        if (HasFadeUI())
            fadeUI.SetImmediateBlack();
        // New scene Awake/Start (including duplicate GlobalUI cleanup) runs while black.
        yield return null;
        if (HasFadeUI())
            yield return fadeUI.FadeIn();
        FinishTransition();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != this || mode != LoadSceneMode.Single || !HasFadeUI())
            return;
        if (!transitioning)
            BeginTransition();
        fadeUI.SetImmediateBlack();
        BlockNavigation();
        if (!started || loadingScene)
            return;
        // Also safely reveal an externally loaded scene, without replaying an old request.
        StopAllCoroutines();
        restoreTimeScale = 1f;
        StartCoroutine(RevealScene());
    }

    private void Update()
    {
        if (instance != this || !transitioning)
            return;
        if (!HasFadeUI())
        {
            StopAllCoroutines();
            FinishTransition();
            return;
        }
        BlockNavigation();
    }

    private void BlockNavigation()
    {
        if (blockedEventSystem != EventSystem.current)
        {
            RestoreNavigation();
            blockedEventSystem = EventSystem.current;
            if (blockedEventSystem != null)
            {
                previousNavigation = blockedEventSystem.sendNavigationEvents;
                previousSelection = blockedEventSystem.currentSelectedGameObject;
            }
        }
        if (blockedEventSystem != null)
        {
            blockedEventSystem.sendNavigationEvents = false;
            blockedEventSystem.SetSelectedGameObject(null);
        }
    }

    private void RestoreNavigation()
    {
        if (blockedEventSystem != null)
        {
            blockedEventSystem.sendNavigationEvents = previousNavigation;
            if (previousSelection != null && previousSelection.activeInHierarchy)
                blockedEventSystem.SetSelectedGameObject(previousSelection);
        }
        blockedEventSystem = null;
        previousSelection = null;
    }

    private void FinishTransition()
    {
        loadingScene = false;
        if (fadeUI != null)
            fadeUI.SetImmediateClear();
        RestoreNavigation();
        if (!transitioning)
            return;
        transitioning = false;
        // A new scene may open a modal in Start while the screen is still black.
        bool modalOwnsTime = PauseManager.IsGamePaused || PauseManager.IsUpgradeSelectionOpen ||
                            (RunManager.Instance != null && RunManager.Instance.IsRunCleared);
        Time.timeScale = modalOwnsTime ? 0f : restoreTimeScale;
    }

    private void OnDisable()
    {
        if (instance != this)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAllCoroutines();
        FinishTransition();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
