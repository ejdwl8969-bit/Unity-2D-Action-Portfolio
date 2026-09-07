using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerStatusUI : MonoBehaviour
{
    public static PlayerStatusUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject statusPanel;
    [SerializeField] private GameObject gameplayUIRoot;

    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text playerStatusText;
    [SerializeField] private TMP_Text weaponText;
    [SerializeField] private TMP_Text elementText;

    private PlayerLevel playerLevel;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private PlayerAttack playerAttack;

    private void Awake()
    {
        // Scene마다 GlobalUI를 실수로 넣어도
        // 하나만 유지
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);
        ApplySceneVisibility();

        if (statusPanel != null)
        {
            statusPanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        FindPlayer();
        RefreshUI();
    }

    private void Update()
    {
        if (gameplayUIRoot != null && !gameplayUIRoot.activeInHierarchy)
            return;

        if (SceneTransition.IsTransitioning || PauseManager.IsGamePaused || (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.sKey.wasPressedThisFrame)
        {
            ToggleStatusPanel();
        }
    }

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        ApplySceneVisibility();
        // 새 Scene에는 새 Player가 있으므로
        // 다시 찾아준다.
        FindPlayer();

        // 맵 이동 시 Status 창은 닫기
        if (statusPanel != null)
        {
            statusPanel.SetActive(false);
        }
    }

    private void ApplySceneVisibility()
    {
        // Fade is a sibling outside this container and remains active in Title.
        if (gameplayUIRoot != null)
            gameplayUIRoot.SetActive(SceneManager.GetActiveScene().name != "Title");
    }

    private void ToggleStatusPanel()
    {
        if (statusPanel == null)
            return;

        bool newState =
            !statusPanel.activeSelf;

        statusPanel.SetActive(newState);

        if (newState)
        {
            FindPlayer();
            RefreshUI();
        }
    }

    private void FindPlayer()
    {
        playerLevel =
            FindFirstObjectByType<PlayerLevel>();

        playerHealth =
            FindFirstObjectByType<PlayerHealth>();

        playerController =
            FindFirstObjectByType<PlayerController>();

        playerAttack =
            FindFirstObjectByType<PlayerAttack>();
    }

    private void RefreshUI()
    {
        UpdateTitle();
        UpdatePlayerStatus();
        UpdateWeaponStatus();
        UpdateElementStatus();
    }

    private void UpdateTitle()
    {
        if (titleText != null)
        {
            titleText.text =
                "CURRENT STATUS";
        }
    }

    private void UpdatePlayerStatus()
    {
        if (playerStatusText == null)
            return;

        if (playerLevel == null ||
            playerHealth == null ||
            playerController == null)
        {
            playerStatusText.text =
                "Data Not Found";

            return;
        }

        playerStatusText.text =
            $"{playerLevel.Level}\n" +
            $"{playerHealth.CurrentHP} / {playerHealth.MaxHP}\n" +
            $"{playerLevel.CurrentExp} / {playerLevel.NeedExp}\n" +
            $"{playerController.moveSpeed:F1}";
    }

    private void UpdateWeaponStatus()
    {
        if (weaponText == null)
            return;

        if (playerAttack == null ||
            playerAttack.CurrentWeapon == null)
        {
            weaponText.text =
                "NONE";

            return;
        }

        Weapon weapon =
            playerAttack.CurrentWeapon;

        WeaponStat stat =
            weapon.Stat;

        weaponText.text =
            $"{weapon.WeaponName} Lv.{weapon.Level}\n" +
            $"{weapon.GetBaseDamage()}\n" +
            $"{stat.attackCooldown:F2}s\n" +
            $"{stat.criticalChance:F1}%\n" +
            $"{stat.criticalDamageMultiplier * 100f:F0}%";
    }

    private void UpdateElementStatus()
    {
        if (elementText == null)
            return;

        if (playerAttack == null ||
            playerAttack.CurrentWeapon == null ||
            playerAttack.CurrentWeapon.Element == null)
        {
            elementText.text =
                "NONE";

            return;
        }

        WeaponElement element =
            playerAttack.CurrentWeapon.Element;

        elementText.text =
            $"Lv.{element.FireLevel}\n" +
            $"Lv.{element.LightningLevel}\n" +
            $"Lv.{element.IceLevel}\n" +
            $"Lv.{element.WindLevel}";
    }
}
