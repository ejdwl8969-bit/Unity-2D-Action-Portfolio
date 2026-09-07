using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance { get; private set; }

    private Coroutine hitStopCoroutine;
    public bool IsHitStopping => hitStopCoroutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Stop(float duration)
    {
        if (SceneTransition.IsTransitioning || PauseManager.IsGamePaused || PauseManager.IsUpgradeSelectionOpen ||
            (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
        }

        hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;

        // timeScale이 0이어도 흐르는 실제 시간
        yield return new WaitForSecondsRealtime(duration);

        if (!SceneTransition.IsTransitioning && !PauseManager.IsGamePaused && !PauseManager.IsUpgradeSelectionOpen &&
            !(RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            Time.timeScale = 1f;
        hitStopCoroutine = null;
    }

    // Transfers time ownership to Pause without letting the realtime routine resume it.
    public void CancelHitStop()
    {
        if (hitStopCoroutine != null)
            StopCoroutine(hitStopCoroutine);
        hitStopCoroutine = null;
    }

    private void OnDisable()
    {
        // 오브젝트가 꺼졌을 때 게임이 멈춘 채 남는 것 방지
        if (hitStopCoroutine == null)
            return;
        CancelHitStop();
        if (!SceneTransition.IsTransitioning && !PauseManager.IsGamePaused && !PauseManager.IsUpgradeSelectionOpen &&
            !(RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            Time.timeScale = 1f;
    }
}
