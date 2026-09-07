using UnityEngine;

public class BossChapterProgress : MonoBehaviour
{
    [SerializeField]
    private int chapterNumber = 1;

    private EnemyHealth bossHealth;

    private void Awake()
    {
        bossHealth =
            GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnDied += OnBossDied;
        }
    }

    private void OnDisable()
    {
        if (bossHealth != null)
        {
            bossHealth.OnDied -= OnBossDied;
        }
    }

    private void OnBossDied()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError(
                "RunManager.Instance°¡ ¾øÀ½!"
            );

            return;
        }

        RunManager.Instance.CompleteChapter(
            chapterNumber
        );
    }
}