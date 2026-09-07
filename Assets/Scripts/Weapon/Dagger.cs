using System.Collections;
using UnityEngine;

public class Dagger : Weapon
{
    [Header("Attack")]
    [SerializeField] private GameObject attackBoxPrefab;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackBoxLifeTime = 0.1f;

    [Header("Combo")]
    [SerializeField] private float comboResetTime = 0.6f;

    [Header("Damage Multiplier")]
    [SerializeField] private float firstHitMultiplier = 0.7f;
    [SerializeField] private float secondHitMultiplier = 0.75f;
    [SerializeField] private float thirdHitMultiplier = 0.8f;
    [SerializeField] private float fourthHitMultiplier = 1f;

    [Header("Wind - Attack Speed")]
    [SerializeField] private float attackSpeedBonusPerLevel = 0.1f;

    [Header("Wind - Echo Attack")]
    [SerializeField] private float echoDelay = 0.1f;
    [SerializeField] private float baseEchoChance = 0.1f;
    [SerializeField] private float echoChancePerLevel = 0.05f;
    [SerializeField] private float baseEchoDamageMultiplier = 0.35f;
    [SerializeField] private float echoDamageMultiplierPerLevel = 0.05f;
    [SerializeField] private float maxEchoDamageMultiplier = 0.8f;

    [Header("Upgrade Bonus")]
    [SerializeField] private float upgradeAttackSpeedBonus;
    [SerializeField] private float upgradeEchoChance;
    [SerializeField] private float upgradeEchoDamage;

    private int comboCount;
    private float lastComboTime;

    protected override void Attack()
    {
        UpdateCombo();

        DamageData damageData = CreateComboDamageData();
        Vector3 attackPosition = GetAttackPosition();

        SpawnAttackBox(
            attackPosition,
            damageData,
            comboCount
        );
        SfxPlayer.Play(SfxId.DaggerAttack);

        TryCreateEchoAttack(
            attackPosition,
            damageData.Damage
        );

        transform.root.GetComponentInChildren<PlayerAnimationController>()?
            .PlayDaggerAttack(this, comboCount, GetAttackCooldown());
    }

    protected override float GetAttackCooldown()
    {
        float baseCooldown = base.GetAttackCooldown();

        float totalAttackSpeedBonus =
            upgradeAttackSpeedBonus;

        if (IsWindActive)
        {
            totalAttackSpeedBonus +=
                WindLevel * attackSpeedBonusPerLevel;
        }

        return baseCooldown / (1f + totalAttackSpeedBonus);
    }

    private void TryCreateEchoAttack(
        Vector3 attackPosition,
        int originalDamage)
    {
        if (!IsWindActive)
            return;

        float echoChance =
            baseEchoChance
            + WindLevel * echoChancePerLevel
            + upgradeEchoChance;

        echoChance = Mathf.Clamp01(echoChance);

        if (Random.value > echoChance)
            return;

        float echoDamageMultiplier =
            baseEchoDamageMultiplier
            + WindLevel * echoDamageMultiplierPerLevel
            + upgradeEchoDamage;

        echoDamageMultiplier = Mathf.Min(
            echoDamageMultiplier,
            maxEchoDamageMultiplier
        );

        // originalDamage already includes shared, level and combo scaling.
        // Echo applies only its own fraction, so those multipliers stay single-use.
        int echoDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(
                originalDamage * echoDamageMultiplier
            )
        );

        StartCoroutine(
            EchoAttackRoutine(
                attackPosition,
                echoDamage
            )
        );
    }

    private IEnumerator EchoAttackRoutine(
        Vector3 attackPosition,
        int echoDamage)
    {
        yield return new WaitForSeconds(echoDelay);

        DamageData echoDamageData = new DamageData(
            echoDamage,
            false,
            ElementType.None
        );

        // comboCount를 1로 전달해서
        // 강한 피니시 넉백이나 카메라 셰이크가 발생하지 않게 한다.
        SpawnAttackBox(
            attackPosition,
            echoDamageData,
            1
        );
    }

    private void SpawnAttackBox(
        Vector3 position,
        DamageData damageData,
        int attackComboCount)
    {
        GameObject attackBox = Instantiate(
            attackBoxPrefab,
            position,
            Quaternion.identity
        );

        AttackBox attackBoxScript =
            attackBox.GetComponent<AttackBox>();

        if (attackBoxScript != null)
        {
            attackBoxScript.Initialize(
                damageData,
                attackComboCount
            );
        }

        Destroy(attackBox, attackBoxLifeTime);
    }

    private Vector3 GetAttackPosition()
    {
        float directionX = GetFacingDirection();

        Vector3 localAttackPosition =
            attackPoint.localPosition;

        localAttackPosition.x =
            Mathf.Abs(localAttackPosition.x) * directionX;

        return transform.root.TransformPoint(
            localAttackPosition
        );
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

            if (comboCount > 4)
                comboCount = 1;
        }

        lastComboTime = Time.time;
    }

    private DamageData CreateComboDamageData()
    {
        DamageData baseDamageData =
            CreateDamageData();

        float multiplier =
            GetComboDamageMultiplier();

        int finalDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(
                baseDamageData.Damage * multiplier
            )
        );

        return new DamageData(
            finalDamage,
            baseDamageData.IsCritical,
            baseDamageData.Element
        );
    }

    private float GetComboDamageMultiplier()
    {
        switch (comboCount)
        {
            case 2:
                return secondHitMultiplier;

            case 3:
                return thirdHitMultiplier;

            case 4:
                return fourthHitMultiplier;

            default:
                return firstHitMultiplier;
        }
    }

    private float GetFacingDirection()
    {
        PlayerController playerController =
            transform.root.GetComponent<PlayerController>();

        if (playerController != null &&
            !playerController.IsFacingRight)
        {
            return -1f;
        }

        return 1f;
    }

    public void AddDaggerAttackSpeed(float value)
    {
        upgradeAttackSpeedBonus += value;
        upgradeAttackSpeedBonus = Mathf.Max(0f, upgradeAttackSpeedBonus);
    }

    public void AddEchoChance(float value)
    {
        upgradeEchoChance += value;
    }

    public void AddEchoDamage(float value)
    {
        upgradeEchoDamage += value;
    }

}