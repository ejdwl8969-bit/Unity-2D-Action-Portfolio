using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1)]
    private int maxHP = 100;

    [Header("Damage")]
    [SerializeField, Min(0f)]
    private float invincibleTime = 0.7f;

    [Header("Floating Text")]
    [SerializeField]
    private Transform floatingTextSpawnPoint;

    [SerializeField]
    private FloatingText floatingTextPrefab;

    private int currentHP;
    private bool isInvincible;
    private bool isDead;

    private PlayerHitFlash hitFlash;
    private PlayerKnockback knockback;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public bool IsDead => isDead;

    public event Action OnDied;


    // ==================================================
    // Unity
    // ==================================================

    private void Awake()
    {
        hitFlash =
            GetComponent<PlayerHitFlash>();

        knockback =
            GetComponent<PlayerKnockback>();
    }

    private void Start()
    {
        currentHP = maxHP;
        isDead = false;
    }


    // ==================================================
    // Damage
    // ==================================================

    public void TakeDamage(
        int damage,
        Vector2 knockbackDirection,
        float knockbackForce)
    {
        if (SceneTransition.IsTransitioning || isDead ||
            isInvincible || (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
        {
            return;
        }

        damage =
            Mathf.Max(
                0,
                damage
            );

        if (damage <= 0)
            return;

        int hpBeforeDamage = currentHP;
        currentHP -= damage;

        currentHP =
            Mathf.Max(
                0,
                currentHP
            );


        if (RunManager.Instance != null)
            RunManager.Instance.RecordDamageTaken(hpBeforeDamage - currentHP);

        ShowFloatingText(
            damage,
            FloatingTextType.PlayerDamage
        );


        if (hitFlash != null)
        {
            hitFlash.Flash();
        }


        if (knockback != null &&
            knockbackForce > 0f)
        {
            knockback.Knockback(
                knockbackDirection,
                knockbackForce
            );
        }


        if (currentHP <= 0)
        {
            Die();
            return;
        }


        StartCoroutine(
            InvincibleRoutine()
        );
        GetComponentInChildren<PlayerAnimationController>()?.PlayHit();
    }


    // ==================================================
    // Heal
    // ==================================================

    public void Heal(int amount)
    {
        if (isDead)
            return;

        amount =
            Mathf.Max(
                0,
                amount
            );

        if (amount <= 0)
            return;


        int previousHP =
            currentHP;


        currentHP += amount;

        currentHP =
            Mathf.Min(
                currentHP,
                maxHP
            );


        int actualHeal =
            currentHP -
            previousHP;

        if (actualHeal <= 0)
            return;


        ShowFloatingText(
            actualHeal,
            FloatingTextType.Heal
        );
    }


    public void AddMaxHP(int value)
    {
        if (value <= 0)
            return;

        maxHP += value;
        currentHP += value;
    }


    // ==================================================
    // HP
    // ==================================================

    public float GetHPRatio()
    {
        if (maxHP <= 0)
            return 0f;

        return
            (float)currentHP /
            maxHP;
    }


    // ==================================================
    // Floating Text
    // ==================================================

    private void ShowFloatingText(
        int value,
        FloatingTextType type)
    {
        if (floatingTextPrefab == null ||
            floatingTextSpawnPoint == null)
        {
            return;
        }


        FloatingText floatingText =
            Instantiate(
                floatingTextPrefab,
                floatingTextSpawnPoint
            );


        floatingText.transform.localPosition =
            new Vector3(
                0f,
                25f,
                0f
            );

        floatingText.transform.localScale =
            Vector3.one;


        floatingText.Show(
            value,
            type
        );
    }


    // ==================================================
    // Invincible
    // ==================================================

    private IEnumerator InvincibleRoutine()
    {
        isInvincible = true;

        yield return new WaitForSeconds(
            invincibleTime
        );

        isInvincible = false;
    }


    // ==================================================
    // Death
    // ==================================================

    public void KillByFall()
    {
        // Reuse the normal death event so GameOver and run cleanup stay centralized.
        Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;


        Rigidbody2D rb =
            GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity =
                Vector2.zero;
        }


        PlayerController controller =
            GetComponent<PlayerController>();

        if (controller != null)
        {
            controller.enabled =
                false;
        }


        PlayerAttack attack =
            GetComponent<PlayerAttack>();

        if (attack != null)
        {
            attack.enabled =
                false;
        }


        OnDied?.Invoke();
    }


    // ==================================================
    // Restore
    // ==================================================

    public void RestoreHealth(
        int savedCurrentHP,
        int savedMaxHP)
    {
        maxHP =
            Mathf.Max(
                1,
                savedMaxHP
            );

        currentHP =
            Mathf.Clamp(
                savedCurrentHP,
                0,
                maxHP
            );

        isDead =
            currentHP <= 0;
    }
}
