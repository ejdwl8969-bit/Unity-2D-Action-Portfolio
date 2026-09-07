using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerKnockback : MonoBehaviour
{
    [Header("Knockback")]
    [SerializeField, Min(0f)]
    private float knockbackTime = 0.15f;

    private Rigidbody2D rb;
    private PlayerController playerController;

    private Coroutine knockbackCoroutine;

    public bool IsKnockedBack { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
    }

    public void Knockback(
        Vector2 direction,
        float force)
    {
        if (rb == null)
            return;

        if (direction.sqrMagnitude <= 0f)
            return;

        force = Mathf.Max(0f, force);

        if (force <= 0f)
            return;

        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
        }

        knockbackCoroutine =
            StartCoroutine(
                KnockbackRoutine(
                    direction.normalized,
                    force
                )
            );
    }

    private IEnumerator KnockbackRoutine(
        Vector2 direction,
        float force)
    {
        IsKnockedBack = true;

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        rb.linearVelocity = Vector2.zero;

        rb.AddForce(
            direction * force,
            ForceMode2D.Impulse
        );

        yield return new WaitForSeconds(
            knockbackTime
        );

        Recover();
    }

    private void Recover()
    {
        IsKnockedBack = false;

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        knockbackCoroutine = null;
    }

    private void OnDisable()
    {
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = null;
        }

        IsKnockedBack = false;

        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }
}