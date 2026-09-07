using UnityEngine;

public class Sword : Weapon
{
    [Header("Attack")]
    [SerializeField] private GameObject attackBoxPrefab;
    [SerializeField] private Transform attackPoint;

    [Header("Combo")]
    [SerializeField] private float comboResetTime = 0.8f;

    [Header("Damage Multiplier")]
    [SerializeField] private float firstHitMultiplier = 1f;
    [SerializeField] private float secondHitMultiplier = 1.1f;
    [SerializeField] private float thirdHitMultiplier = 1.3f;

    [Header("Wind Slash")]
    [SerializeField] private GameObject windSlashPrefab;
    [SerializeField] private Transform windSlashPoint;
    [SerializeField] private float baseWindSlashDistance = 6f;

    [Header("Upgrade Bonus")]
    [SerializeField] private float thirdHitDamageBonus;
    [SerializeField] private float windSlashDamageBonus;
    [SerializeField] private float windSlashDistanceBonus;

    private int comboCount;
    private float lastComboTime;

    protected override void Attack()
    {
        UpdateCombo();

        DamageData damageData = CreateComboDamageData();

        PlayerController playerController =
            transform.root.GetComponent<PlayerController>();

        float directionX = 1f;

        if (playerController != null &&
            playerController.IsFacingRight == false)
        {
            directionX = -1f;
        }

        Vector3 attackPosition = attackPoint.localPosition;
        attackPosition.x = Mathf.Abs(attackPosition.x) * directionX;

        Vector3 worldAttackPosition =
            transform.root.TransformPoint(attackPosition);

        GameObject attackBox = Instantiate(
            attackBoxPrefab,
            worldAttackPosition,
            Quaternion.identity
        );

        attackBox.GetComponent<AttackBox>().Initialize(damageData, comboCount);
        SfxPlayer.Play(SfxId.SwordAttack);

        Destroy(attackBox, 0.15f);


        if (comboCount == 3)
        {
            ApplyWindEffect();
        }
        transform.root.GetComponentInChildren<PlayerAnimationController>()?.PlaySwordAttack(this, comboCount, GetAttackCooldown());
    }

    private void UpdateCombo()
    {
        bool comboExpired =
            Time.time > lastComboTime + comboResetTime;

        if (comboExpired)
        {
            comboCount = 1;
        }
        else
        {
            comboCount++;

            if (comboCount > 3)
            {
                comboCount = 1;
            }
        }

        lastComboTime = Time.time;
    }

    private DamageData CreateComboDamageData()
    {
        DamageData damageData = CreateDamageData();

        float multiplier = GetComboDamageMultiplier();

        int finalDamage = Mathf.RoundToInt(
            damageData.Damage * multiplier
        );

        return new DamageData(
            finalDamage,
            damageData.IsCritical,
            damageData.Element
        );
    }

    private float GetComboDamageMultiplier()
    {
        switch (comboCount)
        {
            case 2:
                return secondHitMultiplier;

            case 3:
                return thirdHitMultiplier + thirdHitDamageBonus;

            default:
                return firstHitMultiplier;
        }
    }

    protected override void ApplyWindEffect()
    {
        if (!IsWindActive || windSlashPrefab == null)
            return;

        PlayerController playerController =
            transform.root.GetComponent<PlayerController>();

        float directionX = 1f;

        if (playerController != null &&
            playerController.IsFacingRight == false)
        {
            directionX = -1f;
        }

        Transform spawnPoint =
            windSlashPoint != null ? windSlashPoint : attackPoint;

        Vector3 localSpawnPosition = spawnPoint.localPosition;
        localSpawnPosition.x =
            Mathf.Abs(localSpawnPosition.x) * directionX;

        Vector3 worldSpawnPosition =
            transform.root.TransformPoint(localSpawnPosition);

        DamageData baseDamageData = CreateDamageData();

        float damageMultiplier = GetWindSlashDamageMultiplier();
        float distance = GetWindSlashDistance();

        // CreateDamageData already includes shared and weapon-level multipliers.
        int windSlashDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(
                baseDamageData.Damage * damageMultiplier
            )
        );

        DamageData windDamageData = new DamageData(
            windSlashDamage,
            baseDamageData.IsCritical,
            ElementType.Wind
        );

        GameObject windSlashObject = Instantiate(
            windSlashPrefab,
            worldSpawnPosition,
            Quaternion.identity
        );

        WindSlash windSlash =
            windSlashObject.GetComponent<WindSlash>();

        windSlash.Initialize(
            windDamageData,
            new Vector2(directionX, 0f),
            distance
        );
        SfxPlayer.Play(SfxId.WindSlash);
    }

    private float GetWindSlashDamageMultiplier()
    {
        float levelMultiplier;

        switch (WindLevel)
        {
            case 1:
                levelMultiplier = 0.7f;
                break;

            case 2:
                levelMultiplier = 0.8f;
                break;

            case 3:
                levelMultiplier = 0.9f;
                break;

            case 4:
                levelMultiplier = 1f;
                break;

            default:
                levelMultiplier = 1.2f;
                break;
        }

        return levelMultiplier + windSlashDamageBonus;
    }


    private float GetWindSlashDistance()
    {
        return baseWindSlashDistance
               + (WindLevel - 1) * 0.5f
               + windSlashDistanceBonus;
    }

    public void AddThirdHitDamage(float value)
    {
        thirdHitDamageBonus += value;
    }

    public void AddWindSlashDamage(float value)
    {
        windSlashDamageBonus += value;
    }


    public void AddWindSlashDistance(float value)
    {
        windSlashDistanceBonus += value;
    }

}
