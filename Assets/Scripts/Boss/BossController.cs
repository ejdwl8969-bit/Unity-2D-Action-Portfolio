using System.Collections;
using UnityEngine;

public class BossController : MonoBehaviour
{
    public enum BossState
    {
        Idle,
        Selecting,
        Attacking,
        Cooldown,
        Dead
    }

    [Header("Boss")]
    [SerializeField] private EnemyHealth bossHealth;

    [Header("Attacks")]
    [SerializeField] private BossAttackBase[] attacks;

    [Header("Phase")]
    [SerializeField, Range(0.1f, 0.9f)]
    private float phase2Threshold = 0.5f;

    [Header("Timing")]
    [SerializeField, Min(0f)]
    private float startDelay = 1f;

    [SerializeField, Min(0f)]
    private float phase1Cooldown = 1.5f;

    [SerializeField, Min(0f)]
    private float phase2Cooldown = 1f;

    private BossState currentState;
    private int currentPhase = 1;
    private int lastAttackIndex = -1;

    public BossState CurrentState => currentState;
    public int CurrentPhase => currentPhase;

    private void Awake()
    {
        if (bossHealth == null)
        {
            bossHealth =
                GetComponent<EnemyHealth>();
        }
    }

    private void Start()
    {
        StartCoroutine(
            FightRoutine()
        );
    }

    private IEnumerator FightRoutine()
    {
        currentState =
            BossState.Idle;

        yield return new WaitForSeconds(
            startDelay
        );

        while (bossHealth != null &&
               !bossHealth.IsDead)
        {
            UpdatePhase();

            BossAttackBase attack =
                SelectAttack();

            if (attack == null)
            {
                yield return null;
                continue;
            }

            currentState =
                BossState.Attacking;

            yield return attack.Execute(
                currentPhase
            );

            if (bossHealth == null ||
                bossHealth.IsDead)
            {
                break;
            }

            currentState =
                BossState.Cooldown;

            float cooldown =
                currentPhase >= 2
                    ? phase2Cooldown
                    : phase1Cooldown;

            yield return new WaitForSeconds(
                cooldown
            );
        }

        currentState =
            BossState.Dead;
    }

    private void UpdatePhase()
    {
        if (currentPhase >= 2)
            return;

        if (bossHealth.GetHPRatio()
            <= phase2Threshold)
        {
            currentPhase = 2;

        }
    }

    private BossAttackBase SelectAttack()
    {
        currentState =
            BossState.Selecting;

        if (attacks == null ||
            attacks.Length == 0)
        {
            return null;
        }

        if (attacks.Length == 1)
        {
            lastAttackIndex = 0;
            return attacks[0];
        }

        int index;

        do
        {
            index = Random.Range(
                0,
                attacks.Length
            );
        }
        while (index == lastAttackIndex);

        lastAttackIndex = index;

        return attacks[index];
    }
}