using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHP = 30;

    [Header("Reward")]
    [SerializeField] private int expReward = 10;
    [SerializeField] private ExpGemSpawner expGemSpawner;

    [Header("Floating Text")]
    [SerializeField] private Transform damageTextSpawnPoint;
    [SerializeField] private FloatingText floatingTextPrefab;

    private int currentHP;
    private bool isDead;

    public bool IsDead => isDead;

    public event Action OnDied;

    private ElementEffectManager elementEffectManager;
    private EnemyHitFlash hitFlash;
    private BossVisualEffects bossVisualEffects;
    private EnemyDeathEffect deathEffect;
    private EnemyHealthBar healthBar;

    private Animator animator;

    private void Awake()
    {
        elementEffectManager =
            GetComponent<ElementEffectManager>();

        hitFlash =
            GetComponent<EnemyHitFlash>();

        bossVisualEffects =
            GetComponent<BossVisualEffects>();

        deathEffect =
            GetComponent<EnemyDeathEffect>();

        healthBar =
            GetComponentInChildren<EnemyHealthBar>();

        animator =
            GetComponent<Animator>();

        if (expGemSpawner == null)
        {
            expGemSpawner =
                FindFirstObjectByType<ExpGemSpawner>();
        }
    }

    private void Start()
    {
        currentHP = maxHP;
        isDead = false;
    }

    public void TakeDamage(DamageData damageData)
    {
        if (SceneTransition.IsTransitioning || isDead || (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        int damage = Mathf.Max(0, damageData.Damage);

        if (damage <= 0)
            return;

        currentHP -= damage;
        currentHP = Mathf.Max(0, currentHP);

        // 속성 효과 적용
        if (elementEffectManager != null)
        {
            elementEffectManager.ApplyElementEffect(damageData);
        }

        // 체력바 표시
        if (healthBar != null)
        {
            healthBar.Show();
        }


        // 피격 연출
        if (bossVisualEffects != null)
        {
            bossVisualEffects.PlayHitFlash();
        }
        else if (hitFlash != null)
        {
            hitFlash.Flash();
        }

        ShowDamageText(damageData);

        if (currentHP <= 0)
        {
            Die();
        }
        else
        {
            if (animator != null)
            {
                animator.SetTrigger("Hit");
            }
        }
    }

    private void ShowDamageText(DamageData damageData)
    {
        if (floatingTextPrefab == null || damageTextSpawnPoint == null)
            return;

        FloatingText floatingText = Instantiate(
            floatingTextPrefab,
            damageTextSpawnPoint
        );

        floatingText.transform.localPosition =
            new Vector3(0f, 25f, 0f);

        floatingText.transform.localScale = Vector3.one;

        FloatingTextType textType =
            GetFloatingTextType(damageData);

        floatingText.Show(
            damageData.Damage,
            textType
        );
    }

    private FloatingTextType GetFloatingTextType(
        DamageData damageData)
    {
        if (damageData.IsCritical)
        {
            return FloatingTextType.Critical;
        }

        switch (damageData.Element)
        {
            case ElementType.Fire:
                return FloatingTextType.Fire;

            case ElementType.Lightning:
                return FloatingTextType.Lightning;

            case ElementType.Ice:
                return FloatingTextType.Ice;

            case ElementType.Wind:
                return FloatingTextType.Wind;

            default:
                return FloatingTextType.Normal;
        }
    }

    public float GetHPRatio()
    {
        if (maxHP <= 0)
            return 0f;

        return (float)currentHP / maxHP;
    }

    public int GetCurrentHP()
    {
        return currentHP;
    }

    public int GetMaxHP()
    {
        return maxHP;
    }

    public void KillByFall()
    {
        // Reuse OnDied so room enemy counts, drops and ExitDoor flow remain intact.
        Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        // Count actual deaths only; Bosses share EnemyHealth but are not normal kills.
        if (RunManager.Instance != null &&
            GetComponentInParent<BossController>() == null &&
            GetComponentInParent<BossChapterProgress>() == null)
            RunManager.Instance.RecordEnemyDefeated();
        if (animator != null)
        {
            animator.SetTrigger("Death");
        }

        OnDied?.Invoke();

        if (expGemSpawner != null)
        {
            expGemSpawner.Spawn(
                transform.position,
                expReward
            );
        }

        EnemyDeathEffect deathEffect =
            GetComponent<EnemyDeathEffect>();

        if (deathEffect != null)
        {
            deathEffect.Play();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
