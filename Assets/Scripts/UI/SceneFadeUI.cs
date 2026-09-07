using System.Collections;
using UnityEngine;

// Presentation only; the existing GlobalUI owns this overlay across scene loads.
public class SceneFadeUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.3f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.3f;

    public bool IsReady => isActiveAndEnabled && fadeGroup != null;

    public void SetImmediateBlack() => SetState(1f, true);
    public void SetImmediateClear() => SetState(0f, false);
    public void BlockInput() => SetState(fadeGroup != null ? fadeGroup.alpha : 1f, true);

    public IEnumerator FadeOut() => FadeTo(1f, fadeOutDuration);
    public IEnumerator FadeIn() => FadeTo(0f, fadeInDuration);

    private IEnumerator FadeTo(float target, float duration)
    {
        if (!IsReady)
            yield break;
        float start = fadeGroup.alpha;
        SetState(start, true);
        float elapsed = 0f;
        while (elapsed < duration && IsReady)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        SetState(target, target > 0f);
    }

    private void SetState(float alpha, bool block)
    {
        if (fadeGroup == null)
            return;
        fadeGroup.alpha = alpha;
        fadeGroup.blocksRaycasts = block;
        fadeGroup.interactable = false;
    }
}
