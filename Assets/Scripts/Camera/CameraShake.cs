using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float intensityMultiplier = 1f;

    private Coroutine shakeCoroutine;
    private Vector3 originalLocalPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        originalLocalPosition = transform.localPosition;
        if (GameSettings.HasCameraShakePreference)
            SetIntensity(GameSettings.CameraShakeEnabled ? 1f : 0f);
    }

    public void Shake(float duration, float strength)
    {
        if (intensityMultiplier <= 0f || SceneTransition.IsTransitioning || PauseManager.IsGamePaused ||
            (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            transform.localPosition = originalLocalPosition;
        }

        shakeCoroutine = StartCoroutine(
            ShakeRoutine(
                duration,
                strength * intensityMultiplier
            )
        );
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (SceneTransition.IsTransitioning || PauseManager.IsGamePaused || (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            {
                yield return null;
                continue;
            }

            float offsetX = Random.Range(-1f, 1f) * strength;
            float offsetY = Random.Range(-1f, 1f) * strength;

            transform.localPosition =
                originalLocalPosition + new Vector3(offsetX, offsetY, 0f);

            elapsedTime += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        shakeCoroutine = null;
    }

    public void SetIntensity(float value)
    {
        intensityMultiplier = Mathf.Clamp01(value);
        if (intensityMultiplier <= 0f && shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
            transform.localPosition = originalLocalPosition;
        }
    }

    private void OnDisable()
    {
        transform.localPosition = originalLocalPosition;
    }
}
