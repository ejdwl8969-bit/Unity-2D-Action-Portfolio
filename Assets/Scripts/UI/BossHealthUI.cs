using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class BossHealthUI : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private EnemyHealth bossHealth;

    [Header("UI")]
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TMP_Text bossNameText;

    [Header("Display")]
    [SerializeField] private string bossName = "BOSS";

    private int ownerSceneHandle;

    private void Awake()
    {
        ownerSceneHandle = gameObject.scene.handle;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        MoveToPersistentCanvasIfNeeded();
    }

    private void Start()
    {
        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }

        if (bossHealth != null)
        {
            bossHealth.OnDied += HandleBossDied;
        }

        UpdateHealthBar();
    }

    private void Update()
    {
        if (bossHealth == null)
            return;

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (hpFillImage == null ||
            bossHealth == null)
        {
            return;
        }

        hpFillImage.fillAmount =
            bossHealth.GetHPRatio();
    }

    private void HandleBossDied()
    {
        gameObject.SetActive(false);
    }

    private void MoveToPersistentCanvasIfNeeded()
    {
        PlayerStatusUI persistentUi = PlayerStatusUI.Instance;
        if (persistentUi == null || transform.IsChildOf(persistentUi.transform))
            return;

        Transform canvas = persistentUi.transform.Find("Canvas");
        if (canvas != null)
        {
            // Move before scene-local GlobalUI deduplication, but retain the owner
            // scene handle so this bar is still destroyed when its BossRoom unloads.
            transform.SetParent(canvas, false);
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (scene.handle == ownerSceneHandle)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;

        if (bossHealth != null)
        {
            bossHealth.OnDied -= HandleBossDied;
        }
    }
}