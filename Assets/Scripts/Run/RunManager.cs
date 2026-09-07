using UnityEngine;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance
    {
        get;
        private set;
    }

    private RunData runData =
        new RunData();

    private bool hasSavedRun;

    private bool hasStartedRun;
    private bool hasFailedRun;
    private PlayerHealth statisticsPlayer;
    public double ElapsedTime { get; private set; }
    public int EnemiesDefeated { get; private set; }
    public int DamageTaken { get; private set; }
    public bool IsRunActive => hasStartedRun && !hasFailedRun && !IsRunCleared;
    public bool HasSelectedElement => runData != null && runData.hasSelectedElement;
    public ElementType SelectedElement => HasSelectedElement
        ? runData.selectedElement
        : ElementType.None;
    public RunResultData LastResult { get; private set; }
    public event System.Action<RunResultData> RunCleared;
    public event System.Action RunReset;


    // ==================================================
    // Unity
    // ==================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(
            gameObject
        );
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;
        SceneManager.sceneLoaded +=
            OnSceneLoaded;
        BindStatisticsPlayer();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -=
            OnSceneLoaded;
        if (statisticsPlayer != null)
            statisticsPlayer.OnDied -= OnPlayerDied;
        if (Instance == this && IsRunCleared && !SceneTransition.IsTransitioning)
            Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Instance == this && IsRunActive && !SceneTransition.IsTransitioning)
            ElapsedTime += Time.deltaTime;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    // ==================================================
    // Scene
    // ==================================================

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (Instance != this)
            return;
        BindStatisticsPlayer();
        if (!hasSavedRun)
            return;

        ApplyRunDataToPlayer();
    }


    // ==================================================
    // Save
    // ==================================================

    public void SaveCurrentRun()
    {
        PlayerLevel playerLevel =
            FindFirstObjectByType<PlayerLevel>();

        PlayerHealth playerHealth =
            FindFirstObjectByType<PlayerHealth>();

        PlayerAttack playerAttack =
            FindFirstObjectByType<PlayerAttack>();

        UpgradeManager upgradeManager =
            FindFirstObjectByType<UpgradeManager>();


        if (playerLevel != null)
        {
            runData.level =
                playerLevel.Level;

            runData.currentExp =
                playerLevel.CurrentExp;

            runData.needExp =
                playerLevel.NeedExp;
        }


        if (playerHealth != null)
        {
            runData.currentHP =
                playerHealth.CurrentHP;

            runData.maxHP =
                playerHealth.MaxHP;
        }


        Weapon weapon =
            playerAttack != null
                ? playerAttack.CurrentWeapon
                : null;

        if (playerAttack != null)
            SaveWeaponLevels(playerAttack);

        if (weapon != null)
        {
            runData.weaponType = playerAttack.CurrentWeaponType;
            SaveWeaponData(
                weapon
            );
        }


        if (upgradeManager != null)
        {
            runData.upgradeStacks =
                upgradeManager.GetUpgradeStacksCopy();
        }


        hasSavedRun = true;
        // ExitDoor is the only caller: its first successful save starts this run.
        // Later room exits (including the Chapter 2 hub exit) must not reset it.
        if (!hasStartedRun && !hasFailedRun && !IsRunCleared)
        {
            hasStartedRun = true;
            BindStatisticsPlayer();
        }
    }


    private void SaveWeaponLevels(PlayerAttack playerAttack)
    {
        runData.swordLevel = playerAttack.GetWeaponLevel(WeaponType.Sword);
        runData.bowLevel = playerAttack.GetWeaponLevel(WeaponType.Bow);
        runData.daggerLevel = playerAttack.GetWeaponLevel(WeaponType.Dagger);
    }

    private void SaveWeaponData(
        Weapon weapon)
    {
        if (weapon.Stat != null)
        {
            runData.weaponDamage =
                weapon.Stat.damage;

            runData.attackCooldown =
                weapon.Stat.attackCooldown;

            runData.criticalChance =
                weapon.Stat.criticalChance;

            runData.criticalDamageMultiplier =
                weapon.Stat
                    .criticalDamageMultiplier;
        }


        if (weapon.Element != null)
        {
            runData.fireLevel =
                weapon.Element.FireLevel;

            runData.lightningLevel =
                weapon.Element.LightningLevel;

            runData.iceLevel =
                weapon.Element.IceLevel;

            runData.windLevel =
                weapon.Element.WindLevel;

            ElementType mainElement = weapon.Element.GetMainElement();
            if (mainElement != ElementType.None)
            {
                runData.selectedElement = mainElement;
                runData.hasSelectedElement = true;
            }
        }
    }


    // ==================================================
    // Load
    // ==================================================

    private void ApplyRunDataToPlayer()
    {
        PlayerLevel playerLevel =
            FindFirstObjectByType<PlayerLevel>();

        PlayerHealth playerHealth =
            FindFirstObjectByType<PlayerHealth>();

        PlayerAttack playerAttack =
            FindFirstObjectByType<PlayerAttack>();

        UpgradeManager upgradeManager =
            FindFirstObjectByType<UpgradeManager>();


        if (playerLevel != null)
        {
            playerLevel.RestoreProgress(
                runData.level,
                runData.currentExp,
                runData.needExp
            );
        }


        if (playerHealth != null)
        {
            playerHealth.RestoreHealth(
                runData.currentHP,
                runData.maxHP
            );
        }


        Weapon weapon = null;
        if (playerAttack != null)
        {
            playerAttack.SetWeaponLevels(
                runData.swordLevel,
                runData.bowLevel,
                runData.daggerLevel
            );
            playerAttack.SetElementLevelsForAllWeapons(
                runData.fireLevel,
                runData.lightningLevel,
                runData.iceLevel,
                runData.windLevel
            );
            playerAttack.SetWeapon(runData.weaponType);
            weapon = playerAttack.CurrentWeapon;
        }

        if (weapon != null)
        {
            ApplyWeaponData(
                weapon
            );
        }


        if (upgradeManager != null)
        {
            upgradeManager.RestoreUpgradeStacks(
                runData.upgradeStacks
            );
        }
    }


    private void ApplyWeaponData(
        Weapon weapon)
    {
        if (weapon.Stat != null)
        {
            weapon.Stat.SetValues(
                runData.weaponDamage,
                runData.attackCooldown,
                runData.criticalChance,
                runData.criticalDamageMultiplier
            );
        }

    }


    public void CommitInitialElement(
        ElementType elementType,
        PlayerAttack playerAttack)
    {
        if (elementType == ElementType.None || HasSelectedElement)
            return;

        runData.selectedElement = elementType;
        runData.hasSelectedElement = true;

        Weapon weapon = playerAttack != null ? playerAttack.CurrentWeapon : null;
        if (weapon != null && weapon.Element != null)
        {
            runData.fireLevel = weapon.Element.FireLevel;
            runData.lightningLevel = weapon.Element.LightningLevel;
            runData.iceLevel = weapon.Element.IceLevel;
            runData.windLevel = weapon.Element.WindLevel;
        }
    }

    // ==================================================
    // Chapter Progress
    // ==================================================

    public int CurrentChapter
    {
        get
        {
            return runData.currentChapter;
        }
    }

    public bool IsRunCleared
    {
        get
        {
            return runData.runCleared;
        }
    }

    public void CompleteChapter(
        int chapterNumber)
    {
        if (runData == null)
        {
            Debug.LogError(
                "RunData가 없습니다."
            );

            return;
        }

        if (IsRunCleared || hasFailedRun)
            return;
        PlayerHealth livePlayer = FindFirstObjectByType<PlayerHealth>();
        if (livePlayer != null && livePlayer.IsDead)
            return;

        // Chapter 1 클리어
        if (chapterNumber == 1)
        {
            runData.currentChapter = 2;
        }

        // Chapter 2 클리어
        else if (chapterNumber == 2)
        {
            CaptureResult();
            runData.currentChapter = 2;
            runData.runCleared = true;
            if (HitStopManager.Instance != null)
                HitStopManager.Instance.CancelHitStop();
            if (PauseManager.Instance != null)
                PauseManager.Instance.Resume();
            Time.timeScale = 0f;
            RunCleared?.Invoke(LastResult);
        }
    }


    // ==================================================
    // New Run
    // ==================================================

    public void ResetRun()
    {
        bool wasCleared = IsRunCleared;
        runData =
            new RunData();

        hasSavedRun = false;
        hasStartedRun = false;
        hasFailedRun = false;
        ElapsedTime = 0;
        EnemiesDefeated = 0;
        DamageTaken = 0;
        LastResult = null;
        if (wasCleared && !SceneTransition.IsTransitioning)
            Time.timeScale = 1f;
        RunReset?.Invoke();
    }

    private void BindStatisticsPlayer()
    {
        if (statisticsPlayer != null)
            statisticsPlayer.OnDied -= OnPlayerDied;
        statisticsPlayer = FindFirstObjectByType<PlayerHealth>();
        if (statisticsPlayer != null)
            statisticsPlayer.OnDied += OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        hasFailedRun = true;
    }

    public void RecordEnemyDefeated()
    {
        if (IsRunActive)
            EnemiesDefeated++;
    }

    public void RecordDamageTaken(int actualHPRemoved)
    {
        if (IsRunActive && actualHPRemoved > 0)
            DamageTaken += actualHPRemoved;
    }

    private void CaptureResult()
    {
        PlayerLevel playerLevel = FindFirstObjectByType<PlayerLevel>();
        PlayerAttack attack = FindFirstObjectByType<PlayerAttack>();
        Weapon weapon = attack != null ? attack.CurrentWeapon : null;
        string weaponName = "None";
        string elementName = "None";
        if (weapon != null)
        {
            // Existing scene weapons have empty display names; use their actual type.
            weaponName = string.IsNullOrWhiteSpace(weapon.WeaponName) || weapon.WeaponName == "Weapon"
                ? weapon.GetType().Name : weapon.WeaponName;
            if (weapon.Element != null)
                elementName = weapon.Element.GetMainElement().ToString();
        }
        LastResult = new RunResultData(ElapsedTime,
            playerLevel != null ? playerLevel.Level : runData.level,
            weaponName, elementName, EnemiesDefeated, DamageTaken);
    }

    public void ReturnToWaitingRoom()
    {
        const string waitingRoom = "WaitingRoom";
        if (!SceneTransition.CanLoadScene(waitingRoom))
            return;
        Time.timeScale = 1f;
        ResetRun();
        SceneTransition.LoadScene(waitingRoom);
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Complete Run With Current Statistics (Play Mode)")]
    private void DebugCompleteRun()
    {
        if (Application.isPlaying && Instance == this)
            CompleteChapter(2);
    }
#endif
}
