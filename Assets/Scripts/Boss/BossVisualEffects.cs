using System.Collections;
using UnityEngine;

public class BossVisualEffects : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private SpriteRenderer[] bodyRenderers;

    [Header("Floating")]
    [SerializeField]
    private bool useFloating = true;

    [SerializeField, Min(0f)]
    private float floatAmplitude = 0.12f;

    [SerializeField, Min(0.1f)]
    private float floatDuration = 1.8f;

    [Header("Hit Flash")]
    [SerializeField, Min(1)]
    private int flashCount = 2;

    [SerializeField, Min(0.01f)]
    private float flashInterval = 0.07f;

    [SerializeField, Range(0f, 1f)]
    private float flashAlpha = 0.15f;

    private Vector3 baseLocalPosition;

    private Color[] originalColors;

    private Coroutine flashCoroutine;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        baseLocalPosition =
            visualRoot.localPosition;

        CacheRendererColors();
    }

    private void LateUpdate()
    {
        UpdateFloating();
    }

    private void UpdateFloating()
    {
        if (visualRoot == null)
            return;

        if (!useFloating)
        {
            visualRoot.localPosition =
                baseLocalPosition;

            return;
        }

        float frequency =
            Mathf.PI * 2f / floatDuration;

        float offsetY =
            Mathf.Sin(
                Time.time * frequency
            ) * floatAmplitude;

        visualRoot.localPosition =
            baseLocalPosition
            + Vector3.up * offsetY;
    }

    public void PlayHitFlash()
    {
        if (bodyRenderers == null ||
            bodyRenderers.Length == 0)
        {
            return;
        }

        if (flashCoroutine != null)
        {
            StopCoroutine(
                flashCoroutine
            );

            RestoreColors();
        }

        flashCoroutine =
            StartCoroutine(
                HitFlashRoutine()
            );
    }

    private IEnumerator HitFlashRoutine()
    {
        for (int i = 0;
             i < flashCount;
             i++)
        {
            SetAlpha(
                flashAlpha
            );

            yield return
                new WaitForSeconds(
                    flashInterval
                );

            RestoreColors();

            yield return
                new WaitForSeconds(
                    flashInterval
                );
        }

        RestoreColors();

        flashCoroutine = null;
    }

    private void SetAlpha(
        float alpha)
    {
        for (int i = 0;
             i < bodyRenderers.Length;
             i++)
        {
            if (bodyRenderers[i] == null)
                continue;

            Color color =
                originalColors[i];

            color.a = alpha;

            bodyRenderers[i].color =
                color;
        }
    }

    private void CacheRendererColors()
    {
        if (bodyRenderers == null)
        {
            originalColors =
                new Color[0];

            return;
        }

        originalColors =
            new Color[
                bodyRenderers.Length
            ];

        for (int i = 0;
             i < bodyRenderers.Length;
             i++)
        {
            if (bodyRenderers[i] == null)
            {
                originalColors[i] =
                    Color.white;

                continue;
            }

            originalColors[i] =
                bodyRenderers[i].color;
        }
    }

    private void RestoreColors()
    {
        if (bodyRenderers == null ||
            originalColors == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                bodyRenderers.Length,
                originalColors.Length
            );

        for (int i = 0;
             i < count;
             i++)
        {
            if (bodyRenderers[i] == null)
                continue;

            bodyRenderers[i].color =
                originalColors[i];
        }
    }

    private void OnDisable()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition =
                baseLocalPosition;
        }

        RestoreColors();
    }
}