using System.Collections;
using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private TMP_Text text;

    [Header("Animation")]
    [SerializeField, Min(0f)]
    private float moveUpDistance = 30f;

    [SerializeField, Min(0.01f)]
    private float duration = 0.6f;

    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (text == null)
        {
            text = GetComponentInChildren<TMP_Text>();
        }
    }

    public void Show(
        int value,
        FloatingTextType type)
    {
        if (text == null)
        {
            Destroy(gameObject);
            return;
        }

        SetupText(value, type);

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine =
            StartCoroutine(
                Animate()
            );
    }

    private void SetupText(
        int value,
        FloatingTextType type)
    {
        transform.localScale =
            Vector3.one;

        text.text =
            value.ToString();

        switch (type)
        {
            case FloatingTextType.Critical:
                text.text = $"{value}!";
                text.color = Color.yellow;
                transform.localScale =
                    Vector3.one * 1.4f;
                break;

            case FloatingTextType.Fire:
                text.color =
                    new Color(
                        1f,
                        0.45f,
                        0f
                    );
                break;

            case FloatingTextType.Lightning:
                text.color =
                    Color.yellow;
                break;

            case FloatingTextType.Ice:
                text.color =
                    Color.cyan;
                break;

            case FloatingTextType.Wind:
                text.color =
                    Color.green;
                break;

            case FloatingTextType.Heal:
                text.text = $"+{value}";
                text.color =
                    new Color(
                        0.6f,
                        1f,
                        0.3f
                    );
                break;

            case FloatingTextType.PlayerDamage:
                text.color =
                    Color.red;
                break;

            default:
                text.color =
                    Color.white;
                break;
        }
    }

    private IEnumerator Animate()
    {
        Vector3 startPosition =
            transform.localPosition;

        Vector3 endPosition =
            startPosition +
            Vector3.up * moveUpDistance;

        Color startColor =
            text.color;

        Color endColor =
            new Color(
                startColor.r,
                startColor.g,
                startColor.b,
                0f
            );

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime +=
                Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsedTime / duration
                );

            transform.localPosition =
                Vector3.Lerp(
                    startPosition,
                    endPosition,
                    t
                );

            text.color =
                Color.Lerp(
                    startColor,
                    endColor,
                    t
                );

            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(
                animationCoroutine
            );

            animationCoroutine = null;
        }
    }
}