using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Read-only balance analysis dashboard. It intentionally has no Apply/Save path.
/// </summary>
public sealed class RunBalanceTunerWindow : EditorWindow
{
    private enum Tab { Overview, ExpCurve, Enemies, Upgrades, Warnings }
    private Tab tab;
    private List<EnemyInfo> enemies = new List<EnemyInfo>();
    private List<UpgradeInfo> upgrades = new List<UpgradeInfo>();
    private List<string> warnings = new List<string>();
    private List<SegmentConfig> segments = new List<SegmentConfig>();
    private LevelCurve curve;
    private GemRules gemRules;
    private RunResult run;
    private bool loaded;
    private Vector2 scroll;
    private string upgradeManagerSource = string.Empty;
    private bool useCurrentEnemyExp = true;
    private int c1MeleeExp = 5, c1RangedExp = 10, c1ChargeExp = 10;
    private int c2MeleeExp = 10, c2RangedExp = 20, c2ChargeExp = 20;

    [MenuItem("Tools/Balance/Run Balance Tuner")]
    private static void Open() => GetWindow<RunBalanceTunerWindow>("Run Balance Tuner");

    private void OnEnable()
    {
        BuildDefaultSegments();
        RefreshData();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Read-only analysis tool. Runtime assets, prefabs and scenes are never modified.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Project Data", GUILayout.Height(24))) RefreshData();
        if (GUILayout.Button("Analyze Run", GUILayout.Height(24))) Analyze();
        EditorGUILayout.EndHorizontal();
        tab = (Tab)GUILayout.Toolbar((int)tab, new[] { "Run Overview", "EXP / Level Curve", "Enemy Balance", "Upgrade Balance", "Warnings" });
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (!loaded) EditorGUILayout.HelpBox("Press Refresh Project Data.", MessageType.Warning);
        else if (tab == Tab.Overview) DrawOverview();
        else if (tab == Tab.ExpCurve) DrawExpCurve();
        else if (tab == Tab.Enemies) DrawEnemies();
        else if (tab == Tab.Upgrades) DrawUpgrades();
        else DrawWarnings();
        EditorGUILayout.EndScrollView();
    }

    private void RefreshData()
    {
        warnings.Clear();
        enemies = ReadEnemies();
        upgrades = ReadUpgrades();
        curve = ReadLevelCurve();
        gemRules = ReadGemRules();
        upgradeManagerSource = ReadSource("UpgradeManager.cs");
        if (segments.Count == 0) BuildDefaultSegments();
        loaded = true;
        Analyze();
        Repaint();
    }

    private void Analyze()
    {
        if (!loaded && enemies.Count == 0) return;
        warnings.RemoveAll(x => x.StartsWith("[RUN]", StringComparison.Ordinal));
        run = new RunResult();
        int level = curve.level, exp = curve.currentExp, need = curve.needExp;
        foreach (var s in segments)
        {
            int xp = s.boss ? s.bossXp : RoomXp(s);
            s.expectedXp = xp;
            run.totalXp += xp;
            exp += xp;
            while (exp >= need)
            {
                exp -= need; level++; need += curve.expIncreasePerLevel; run.levelUps++;
            }
            run.rows.Add(new RunRow { segment = s.name, xp = xp, levelAfter = level, threat = RoomThreat(s), gems = s.boss ? -1 : RoomGemCount(s) });
            if (!s.boss && RoomGemCount(s) > 100) warnings.Add("[RUN] " + s.name + ": estimated EXP gem objects exceed 100.");
            else if (!s.boss && RoomGemCount(s) > 50) warnings.Add("[RUN] " + s.name + ": estimated EXP gem objects exceed 50.");
        }
        run.finalLevel = level; run.upgradePicks = run.levelUps; run.finalExp = exp; run.nextNeed = need;
        int targetMin = EditorPrefs.GetInt("BalanceTuner.TargetFinalMin", 10);
        int targetMax = EditorPrefs.GetInt("BalanceTuner.TargetFinalMax", 11);
        if (level < targetMin || level > targetMax) warnings.Add("[RUN] Expected final level is outside the configured target range.");
        foreach (var s in segments)
        {
            int sum = s.melee + s.ranged + s.charge;
            if (!s.boss && sum != s.enemyCount) warnings.Add("[RUN] " + s.name + ": composition does not equal expected enemy count.");
            if (!s.boss && s.expectedXp < s.targetXpMin) warnings.Add("[RUN] " + s.name + ": XP is below target budget.");
            if (!s.boss && s.expectedXp > s.targetXpMax) warnings.Add("[RUN] " + s.name + ": XP is above target budget.");
        }
    }

    private void DrawOverview()
    {
        EditorGUILayout.LabelField("Run Overview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Expected Final Level: {run.finalLevel}    Upgrade Picks: {run.upgradePicks}    Total XP: {run.totalXp}", EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorPrefs.SetInt("BalanceTuner.TargetFinalMin", EditorGUILayout.IntField("Final Level Min", EditorPrefs.GetInt("BalanceTuner.TargetFinalMin", 10)));
        EditorPrefs.SetInt("BalanceTuner.TargetFinalMax", EditorGUILayout.IntField("Final Level Max", EditorPrefs.GetInt("BalanceTuner.TargetFinalMax", 11)));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("EXP Simulation Override", EditorStyles.boldLabel);
        useCurrentEnemyExp = EditorGUILayout.Toggle("Use Current Enemy EXP Values", useCurrentEnemyExp);
        EditorGUILayout.HelpBox(useCurrentEnemyExp ? "Analyze Run uses expReward read from the current Enemy Prefabs." : "Analyze Run uses the Simulation EXP values below only. Prefab expReward values remain unchanged.", MessageType.Info);
        EditorGUILayout.LabelField("Current Prefab EXP (read-only)", string.Join(", ", enemies.Select(e => e.chapter + " " + e.role + "=" + Mathf.RoundToInt(e.exp))));
        EditorGUILayout.BeginHorizontal();
        c1MeleeExp = EditorGUILayout.IntField("C1 Melee EXP", c1MeleeExp);
        c1RangedExp = EditorGUILayout.IntField("C1 Ranged EXP", c1RangedExp);
        c1ChargeExp = EditorGUILayout.IntField("C1 Charge EXP", c1ChargeExp);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        c2MeleeExp = EditorGUILayout.IntField("C2 Melee EXP", c2MeleeExp);
        c2RangedExp = EditorGUILayout.IntField("C2 Ranged EXP", c2RangedExp);
        c2ChargeExp = EditorGUILayout.IntField("C2 Charge EXP", c2ChargeExp);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("EXP source for next analysis", useCurrentEnemyExp ? "Current Prefab expReward" : "Simulation Override (EditorPrefs only)");
        foreach (var s in segments)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(s.name + (s.boss ? " (Boss XP input)" : ""), EditorStyles.boldLabel);
            if (s.boss) s.bossXp = EditorGUILayout.IntField("Boss XP", s.bossXp);
            else
            {
                s.enemyCount = EditorGUILayout.IntField("Expected Enemy Count", s.enemyCount);
                EditorGUILayout.BeginHorizontal();
                s.melee = EditorGUILayout.IntField("Melee", s.melee);
                s.ranged = EditorGUILayout.IntField("Ranged", s.ranged);
                s.charge = EditorGUILayout.IntField("Charge", s.charge);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                s.targetXpMin = EditorGUILayout.IntField("Target XP Min", s.targetXpMin);
                s.targetXpMax = EditorGUILayout.IntField("Target XP Max", s.targetXpMax);
                s.encounters = EditorGUILayout.IntField("Encounters", s.encounters);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Expected XP", s.expectedXp.ToString());
            EditorGUILayout.EndVertical();
        }
        if (GUI.changed) { SaveSegmentPrefs(); SaveExpPrefs(); Analyze(); }
    }

    private void DrawExpCurve()
    {
        EditorGUILayout.LabelField("EXP / Level Curve", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Level", curve.level.ToString());
        EditorGUILayout.LabelField("Current EXP", curve.currentExp.ToString());
        EditorGUILayout.LabelField("Required EXP", curve.needExp.ToString());
        EditorGUILayout.LabelField("Increase Per Level", curve.expIncreasePerLevel.ToString());
        EditorGUILayout.LabelField("Simulation Result", $"Level {run.finalLevel}, EXP {run.finalExp}/{run.nextNeed}, Level-ups {run.levelUps}");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("EXP Gem Spawner (source-derived)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Max Gem Count", gemRules.maxGemCount.ToString());
        EditorGUILayout.LabelField("Rules", string.Join(", ", gemRules.rules.Select(x => x.threshold + "→" + x.value)));
        foreach (var r in run.rows)
            EditorGUILayout.LabelField(r.segment, r.gems < 0 ? "Boss gem count: UNKNOWN (boss is scene-only)" : "Estimated gems: " + r.gems);
    }

    private void DrawEnemies()
    {
        EditorGUILayout.LabelField("Enemy Balance (actual prefab values)", EditorStyles.boldLabel);
        string[] heads = { "Enemy", "HP", "EXP", "Damage", "Move", "Cooldown", "Detect", "Projectile", "Charge", "Threat", "XP/Threat" };
        EditorGUILayout.BeginHorizontal(); foreach (var h in heads) EditorGUILayout.LabelField(h, GUILayout.Width(78)); EditorGUILayout.EndHorizontal();
        foreach (var e in enemies)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(e.name, GUILayout.Width(120));
            EditorGUILayout.LabelField(F(e.hp)); EditorGUILayout.LabelField(F(e.exp)); EditorGUILayout.LabelField(F(e.damage)); EditorGUILayout.LabelField(F(e.moveSpeed)); EditorGUILayout.LabelField(F(e.attackCooldown)); EditorGUILayout.LabelField(F(e.detectRange)); EditorGUILayout.LabelField(F(e.projectileSpeed)); EditorGUILayout.LabelField(F(e.chargeSpeed)); EditorGUILayout.LabelField(F(e.threatWeight));
            EditorGUILayout.LabelField(e.threatWeight > 0 ? F(e.exp / e.threatWeight) : "-");
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recommended threat weights are shown as role/chapter defaults; no values are written.");
    }

    private void DrawUpgrades()
    {
        EditorGUILayout.LabelField("Upgrade Balance / Connectivity (read-only)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"UpgradeData assets: {upgrades.Count}");
        string[] heads = { "Name", "Type", "Rarity", "Value", "Max", "DB", "Manager", "Status", "Single", "Max Stack" };
        EditorGUILayout.BeginHorizontal(); foreach (var h in heads) EditorGUILayout.LabelField(h, GUILayout.Width(90)); EditorGUILayout.EndHorizontal();
        foreach (var u in upgrades)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(u.name) ? "<empty>" : u.name, GUILayout.Width(130)); EditorGUILayout.LabelField(u.type, GUILayout.Width(110)); EditorGUILayout.LabelField(u.rarity, GUILayout.Width(75)); EditorGUILayout.LabelField(F(u.value), GUILayout.Width(55)); EditorGUILayout.LabelField(u.maxStack.ToString(), GUILayout.Width(45)); EditorGUILayout.LabelField(u.registered ? "YES" : "NO", GUILayout.Width(45)); EditorGUILayout.LabelField(u.implemented ? "YES" : "NO", GUILayout.Width(60)); EditorGUILayout.LabelField(u.status, GUILayout.Width(125)); EditorGUILayout.LabelField(u.singlePower, GUILayout.Width(80)); EditorGUILayout.LabelField(u.maxPower, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawWarnings()
    {
        EditorGUILayout.LabelField("Warnings Dashboard", EditorStyles.boldLabel);
        if (warnings.Count == 0) EditorGUILayout.HelpBox("No warnings detected.", MessageType.Info);
        foreach (var w in warnings) EditorGUILayout.HelpBox(w, w.Contains("ERROR") ? MessageType.Error : MessageType.Warning);
        EditorGUILayout.HelpBox("Boss1/Boss2 are scene-only in this project; boss stats and gem composition require manual scene inspection. This tool never opens or saves scenes.", MessageType.Info);
    }

    private List<EnemyInfo> ReadEnemies()
    {
        var list = new List<EnemyInfo>();
        string[] paths = {
            "Assets/Prefabs/Enemy_C1.prefab", "Assets/Prefabs/Enemy_Ranged_C1.prefab", "Assets/Prefabs/Enemy_Charge.prefab",
            "Assets/Prefabs/Enemy_C2.prefab", "Assets/Prefabs/Enemy_Ranged_C2.prefab", "Assets/Prefabs/Enemy_Charge_C2.prefab" };
        foreach (var path in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (go == null) { warnings.Add("[DATA] Missing enemy prefab: " + path); continue; }
            var h = go.GetComponentInChildren<EnemyHealth>(true); var a = go.GetComponentInChildren<EnemyAttack>(true); var ai = go.GetComponentInChildren<EnemyAI>(true); var r = go.GetComponentInChildren<RangedEnemyAI>(true); var c = go.GetComponentInChildren<ChargeEnemyAI>(true);
            string chapter = path.Contains("C2") ? "C2" : "C1"; string role = r != null ? "Ranged" : c != null ? "Charge" : "Melee";
            var e = new EnemyInfo { name = Path.GetFileNameWithoutExtension(path), chapter = chapter, role = role, path = path, hp = Num(h, "maxHP"), exp = Num(h, "expReward"), damage = r != null ? Num(r, "projectileDamage") : Num(a, "damage"), moveSpeed = r != null ? Num(r, "moveSpeed") : c != null ? Num(c, "moveSpeed") : Num(ai, "moveSpeed"), attackCooldown = r != null ? Num(r, "attackCooldown") : Num(a, "attackCooldown"), detectRange = r != null ? Num(r, "detectRange") : c != null ? Num(c, "detectRange") : Num(ai, "detectRange"), projectileSpeed = r != null ? Num(r, "projectileSpeed") : 0, chargeSpeed = c != null ? Num(c, "chargeSpeed") : 0 };
            e.threatWeight = chapter == "C1" ? (role == "Melee" ? 1f : role == "Ranged" ? 1.2f : 1.5f) : (role == "Melee" ? 1f : role == "Ranged" ? 1.4f : 1.8f); list.Add(e);
        }
        return list;
    }

    private List<UpgradeInfo> ReadUpgrades()
    {
        var all = AssetDatabase.FindAssets("t:UpgradeData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UpgradeData>).Where(x => x != null).ToList();
        var db = AssetDatabase.FindAssets("t:UpgradeDatabase").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UpgradeDatabase>).FirstOrDefault();
        var registered = new HashSet<UpgradeData>(); int nullRefs = 0;
        if (db != null)
        {
            foreach (var x in db.commonUpgrades ?? new List<UpgradeData>()) { if (x == null) nullRefs++; else registered.Add(x); }
            foreach (var x in db.rareUpgrades ?? new List<UpgradeData>()) { if (x == null) nullRefs++; else registered.Add(x); }
            foreach (var x in db.legendaryUpgrades ?? new List<UpgradeData>()) { if (x == null) nullRefs++; else registered.Add(x); }
        }
        else warnings.Add("[DATA] UpgradeDatabase asset not found.");
        if (nullRefs > 0) warnings.Add("[DATA] UpgradeDatabase contains " + nullRefs + " broken/null references.");
        var implemented = new HashSet<string>(Regex.Matches(upgradeManagerSource, @"case\s+UpgradeType\.(\w+)").Cast<Match>().Select(m => m.Groups[1].Value));
        var result = new List<UpgradeInfo>();
        foreach (var x in all)
        {
            var u = new UpgradeInfo { asset = x, name = x.upgradeName, type = x.upgradeType.ToString(), rarity = x.rarity.ToString(), value = x.value, maxStack = x.maxStack, registered = registered.Contains(x), implemented = implemented.Contains(x.upgradeType.ToString()) };
            u.status = !u.registered ? "UNREGISTERED" : string.IsNullOrEmpty(u.name) ? "MISSING NAME" : !u.implemented ? "UNIMPLEMENTED" : float.IsNaN(u.value) || float.IsInfinity(u.value) ? "INVALID VALUE" : "OK";
            u.singlePower = PowerText(u); u.maxPower = u.maxStack > 0 ? PowerText(u, u.maxStack) : "UNKNOWN"; result.Add(u);
        }
        string[] missing = { "BurnDamage", "BurnDuration", "ShockChance", "ShockDamage", "ShockStunDuration", "IceTickDamage", "IceDuration", "IceSlowAmount" };
        foreach (var t in missing) if (!all.Any(x => x.upgradeType.ToString() == t)) result.Add(new UpgradeInfo { name = "<NO ASSET>", type = t, rarity = "-", status = "NO ASSET / UNIMPLEMENTED", singlePower = "-", maxPower = "-" });
        return result;
    }

    private string PowerText(UpgradeInfo u, int stacks = 1)
    {
        float v = u.value * stacks;
        if (u.type == "CriticalChance") return "+" + v.ToString("0.##") + "%p";
        if (u.type == "AttackDamage") return (v / 10f * 100f).ToString("0.#") + "% dmg";
        if (u.type == "MoveSpeed") return (v / 7f * 100f).ToString("0.#") + "% move";
        if (u.type == "MaxHP") return (v / 100f * 100f).ToString("0.#") + "% HP";
        return v.ToString("0.##");
    }

    private LevelCurve ReadLevelCurve()
    {
        string s = ReadSource("PlayerLevel.cs");
        return new LevelCurve { level = IntMatch(s, "level\\s*=\\s*(\\d+)", 1), currentExp = IntMatch(s, "currentExp\\s*=\\s*(\\d+)", 0), needExp = IntMatch(s, "needExp\\s*=\\s*(\\d+)", 30), expIncreasePerLevel = IntMatch(s, "expIncreasePerLevel\\s*=\\s*(\\d+)", 20) };
    }

    private GemRules ReadGemRules()
    {
        string s = ReadSource("ExpGemSpawner.cs"); var g = new GemRules { maxGemCount = IntMatch(s, "maxGemCount\\s*=\\s*(\\d+)", 10) };
        g.rules.Add(new GemRule { threshold = 20, value = 2 }); g.rules.Add(new GemRule { threshold = 40, value = 5 }); g.rules.Add(new GemRule { threshold = 100, value = 10 }); g.rules.Add(new GemRule { threshold = int.MaxValue, value = 50 });
        if (string.IsNullOrEmpty(s)) warnings.Add("[DATA] ExpGemSpawner.cs source not found; using documented fallback rules."); return g;
    }

    private void BuildDefaultSegments()
    {
        segments.Clear(); string[] names = { "C1 Room1", "C1 Room2", "C1 Room3", "C1 Boss1", "C2 Room1", "C2 Room2", "C2 Room3", "C2 Boss2" };
        for (int i = 0; i < names.Length; i++)
        {
            bool boss = names[i].Contains("Boss"); var s = new SegmentConfig { name = names[i], boss = boss, enemyCount = boss ? 0 : 18, melee = boss ? 0 : 10, ranged = boss ? 0 : 5, charge = boss ? 0 : 3, bossXp = 100, targetXpMin = 120, targetXpMax = 240, encounters = 4 };
            LoadSegmentPrefs(s); segments.Add(s);
        }
    }

    private void LoadSegmentPrefs(SegmentConfig s) { string k = "BalanceTuner." + s.name.Replace(" ", "_"); s.enemyCount = EditorPrefs.GetInt(k + ".Count", s.enemyCount); s.melee = EditorPrefs.GetInt(k + ".Melee", s.melee); s.ranged = EditorPrefs.GetInt(k + ".Ranged", s.ranged); s.charge = EditorPrefs.GetInt(k + ".Charge", s.charge); s.bossXp = EditorPrefs.GetInt(k + ".BossXP", s.bossXp); }
    private void LoadExpPrefs() { useCurrentEnemyExp = EditorPrefs.GetBool("BalanceTuner.UseCurrentEnemyExp", true); c1MeleeExp = EditorPrefs.GetInt("BalanceTuner.C1MeleeExp", c1MeleeExp); c1RangedExp = EditorPrefs.GetInt("BalanceTuner.C1RangedExp", c1RangedExp); c1ChargeExp = EditorPrefs.GetInt("BalanceTuner.C1ChargeExp", c1ChargeExp); c2MeleeExp = EditorPrefs.GetInt("BalanceTuner.C2MeleeExp", c2MeleeExp); c2RangedExp = EditorPrefs.GetInt("BalanceTuner.C2RangedExp", c2RangedExp); c2ChargeExp = EditorPrefs.GetInt("BalanceTuner.C2ChargeExp", c2ChargeExp); }
    private void SaveExpPrefs() { EditorPrefs.SetBool("BalanceTuner.UseCurrentEnemyExp", useCurrentEnemyExp); EditorPrefs.SetInt("BalanceTuner.C1MeleeExp", c1MeleeExp); EditorPrefs.SetInt("BalanceTuner.C1RangedExp", c1RangedExp); EditorPrefs.SetInt("BalanceTuner.C1ChargeExp", c1ChargeExp); EditorPrefs.SetInt("BalanceTuner.C2MeleeExp", c2MeleeExp); EditorPrefs.SetInt("BalanceTuner.C2RangedExp", c2RangedExp); EditorPrefs.SetInt("BalanceTuner.C2ChargeExp", c2ChargeExp); }
    private void SaveSegmentPrefs() { foreach (var s in segments) { string k = "BalanceTuner." + s.name.Replace(" ", "_"); EditorPrefs.SetInt(k + ".Count", s.enemyCount); EditorPrefs.SetInt(k + ".Melee", s.melee); EditorPrefs.SetInt(k + ".Ranged", s.ranged); EditorPrefs.SetInt(k + ".Charge", s.charge); EditorPrefs.SetInt(k + ".BossXP", s.bossXp); } }

    private int RoomXp(SegmentConfig s) => CountXp(s, "Melee") + CountXp(s, "Ranged") + CountXp(s, "Charge");
    private int CountXp(SegmentConfig s, string role) { int n = role == "Melee" ? s.melee : role == "Ranged" ? s.ranged : s.charge; string ch = s.name.StartsWith("C2") ? "C2" : "C1"; return n * Mathf.RoundToInt(EffectiveExp(ch, role)); }
    private float EffectiveExp(string chapter, string role)
    {
        if (useCurrentEnemyExp)
        {
            var e = enemies.FirstOrDefault(x => x.chapter == chapter && x.role == role);
            return e == null ? 0 : e.exp;
        }
        if (chapter == "C1") return role == "Melee" ? c1MeleeExp : role == "Ranged" ? c1RangedExp : c1ChargeExp;
        return role == "Melee" ? c2MeleeExp : role == "Ranged" ? c2RangedExp : c2ChargeExp;
    }
    private float RoomThreat(SegmentConfig s) { string ch = s.name.StartsWith("C2") ? "C2" : "C1"; return s.melee * Weight(ch, "Melee") + s.ranged * Weight(ch, "Ranged") + s.charge * Weight(ch, "Charge"); }
    private float Weight(string ch, string role) => ch == "C1" ? (role == "Melee" ? 1f : role == "Ranged" ? 1.2f : 1.5f) : (role == "Melee" ? 1f : role == "Ranged" ? 1.4f : 1.8f);
    private int RoomGemCount(SegmentConfig s) { int total = 0; string ch = s.name.StartsWith("C2") ? "C2" : "C1"; foreach (var role in new[] { "Melee", "Ranged", "Charge" }) { int n = role == "Melee" ? s.melee : role == "Ranged" ? s.ranged : s.charge; total += n * GemCountForExp(Mathf.RoundToInt(EffectiveExp(ch, role))); } return total; }
    private int GemCountForExp(int xp) { int value = xp <= 20 ? 2 : xp <= 40 ? 5 : xp <= 100 ? 10 : 50; return Mathf.Max(1, Mathf.CeilToInt((float)xp / value)); }

    private string ReadSource(string file) { string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(file) + " t:MonoScript"); string path = guids.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileName(p).Equals(file, StringComparison.OrdinalIgnoreCase)); return string.IsNullOrEmpty(path) ? string.Empty : File.ReadAllText(path); }
    private static int IntMatch(string s, string pattern, int fallback) { var m = Regex.Match(s ?? string.Empty, pattern); return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : fallback; }
    private static float Num(UnityEngine.Object o, string field) { if (o == null) return 0; var so = new SerializedObject(o); var p = so.FindProperty(field); if (p == null) return 0; if (p.propertyType == SerializedPropertyType.Integer) return p.intValue; return p.floatValue; }
    private static string F(float v) => v == 0 ? "-" : v.ToString("0.##");

    [Serializable] private sealed class EnemyInfo { public string name, chapter, role, path; public float hp, exp, damage, moveSpeed, attackCooldown, detectRange, projectileSpeed, chargeSpeed, threatWeight; }
    [Serializable] private sealed class UpgradeInfo { public UpgradeData asset; public string name, type, rarity, status, singlePower, maxPower; public float value; public int maxStack; public bool registered, implemented; }
    [Serializable] private sealed class SegmentConfig { public string name; public bool boss; public int enemyCount, melee, ranged, charge, bossXp, targetXpMin, targetXpMax, encounters, expectedXp; }
    private sealed class LevelCurve { public int level, currentExp, needExp, expIncreasePerLevel; }
    private sealed class GemRules { public int maxGemCount; public List<GemRule> rules = new List<GemRule>(); }
    private sealed class GemRule { public int threshold, value; }
    private sealed class RunResult { public int totalXp, finalLevel, finalExp, nextNeed, levelUps, upgradePicks; public List<RunRow> rows = new List<RunRow>(); }
    private sealed class RunRow { public string segment; public int xp, levelAfter, gems; public float threat; }
}
