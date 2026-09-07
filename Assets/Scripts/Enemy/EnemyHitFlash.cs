using System.Collections;
using UnityEngine;

public class EnemyHitFlash : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color flashColor = Color.white;

    [SerializeField, Min(0f)]
    private float flashDuration = 0.08f;

    private Color originalColor;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            originalColor =
                spriteRenderer.color;
        }
    }

    public void Flash()
    {
        if (spriteRenderer == null)
            return;

        if (flashCoroutine != null)
        {
            StopCoroutine(
                flashCoroutine
            );
        }

        flashCoroutine =
            StartCoroutine(
                FlashRoutine()
            );
    }

    private IEnumerator FlashRoutine()
    {
        spriteRenderer.color =
            flashColor;

        yield return new WaitForSeconds(
            flashDuration
        );

        RestoreColor();

        flashCoroutine = null;
    }

    private void RestoreColor()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color =
            originalColor;
    }

    private void OnDisable()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(
                flashCoroutine
            );

            flashCoroutine = null;
        }

        RestoreColor();
    }
}