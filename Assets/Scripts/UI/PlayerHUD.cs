using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("HP")]
    [SerializeField]
    private Image hpFill;

    [SerializeField]
    private TMP_Text hpText;


    [Header("EXP")]
    [SerializeField]
    private Image expFill;

    [SerializeField]
    private TMP_Text levelText;


    private PlayerHealth playerHealth;
    private PlayerLevel playerLevel;


    // ==================================================
    // Unity
    // ==================================================

    private void OnEnable()
    {
        SceneManager.sceneLoaded +=
            OnSceneLoaded;
        // Re-enabled when the persistent GlobalUI leaves Title; Start runs only once.
        FindPlayer();
        RefreshUI();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -=
            OnSceneLoaded;
    }

    private void Start()
    {
        FindPlayer();
        RefreshUI();
    }

    private void Update()
    {
        RefreshUI();
    }


    // ==================================================
    // Scene
    // ==================================================

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        FindPlayer();
        RefreshUI();
    }


    // ==================================================
    // Player
    // ==================================================

    private void FindPlayer()
    {
        playerHealth =
            FindFirstObjectByType<PlayerHealth>();

        playerLevel =
            FindFirstObjectByType<PlayerLevel>();
    }


    // ==================================================
    // UI
    // ==================================================

    private void RefreshUI()
    {
        UpdateHP();
        UpdateEXP();
        UpdateLevel();
    }


    private void UpdateHP()
    {
        if (playerHealth == null)
            return;

        if (hpFill != null)
        {
            hpFill.fillAmount =
                playerHealth.GetHPRatio();
        }

        if (hpText != null)
        {
            hpText.text =
                $"HP {playerHealth.CurrentHP} / " +
                $"{playerHealth.MaxHP}";
        }
    }


    private void UpdateEXP()
    {
        if (playerLevel == null ||
            expFill == null)
        {
            return;
        }

        if (playerLevel.NeedExp <= 0)
        {
            expFill.fillAmount = 0f;
            return;
        }

        expFill.fillAmount =
            (float)playerLevel.CurrentExp /
            playerLevel.NeedExp;
    }


    private void UpdateLevel()
    {
        if (playerLevel == null ||
            levelText == null)
        {
            return;
        }

        levelText.text =
            $"Lv.{playerLevel.Level}";
    }
}
