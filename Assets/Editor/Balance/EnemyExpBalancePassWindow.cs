using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Guarded EXP pass. Analyze/Preview never write; Apply edits only EnemyHealth.expReward.</summary>
public sealed class EnemyExpBalancePassWindow : EditorWindow
{
    private sealed class Entry
    {
        public string label, path, role, chapter; public bool boss, isScene; public int current, expectedCurrent, target; public EnemyHealth health; public bool handler, safe;
        public string Status => !handler ? "RUNTIME WARNING" : !safe ? "UNEXPECTED CURRENT VALUE" : current == target ? "UNCHANGED" : "CHANGE";
    }

    private static readonly (string path, string label, string chapter, string role, int expected, int target)[] Prefabs =
    {
        ("Assets/Prefabs/Enemy_C1.prefab", "Enemy_C1", "C1", "Melee", 5, 5),
        ("Assets/Prefabs/Enemy_Ranged_C1.prefab", "Enemy_Ranged_C1", "C1", "Ranged", 10, 8),
        ("Assets/Prefabs/Enemy_Charge.prefab", "Enemy_Charge", "C1", "Charge", 10, 10),
        ("Assets/Prefabs/Enemy_C2.prefab", "Enemy_C2", "C2", "Melee", 10, 8),
        ("Assets/Prefabs/Enemy_Ranged_C2.prefab", "Enemy_Ranged_C2", "C2", "Ranged", 20, 12),
        ("Assets/Prefabs/Enemy_Charge_C2.prefab", "Enemy_Charge_C2", "C2", "Charge", 20, 15)
    };

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<string> warnings = new List<string>();
    private bool analyzed, previewed;
    private Vector2 scroll;
    private string report = "Analyze Current를 먼저 실행하세요.";

    [MenuItem("Tools/Balance/Enemy EXP Balance Pass 1")]
    private static void Open() => GetWindow<EnemyExpBalancePassWindow>("Enemy EXP Balance Pass 1");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Only EnemyHealth.expReward is eligible for Apply. All other gameplay fields, Prefabs, Scenes and Runtime systems are protected.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analyze Current", GUILayout.Height(26))) AnalyzeCurrent();
        using (new EditorGUI.DisabledScope(!analyzed)) if (GUILayout.Button("Preview Changes", GUILayout.Height(26))) { previewed = true; Repaint(); }
        using (new EditorGUI.DisabledScope(!analyzed || !previewed || !entries.Any(x => !x.boss && x.safe && x.handler && x.current != x.target))) if (GUILayout.Button("Apply Enemy Prefab EXP", GUILayout.Height(26))) ApplyEnemyPrefabExp();
        using (new EditorGUI.DisabledScope(!analyzed || !previewed || !entries.Any(x => x.boss && x.safe && x.handler && x.current != x.target))) if (GUILayout.Button("Apply Current Boss EXP", GUILayout.Height(26))) ApplyCurrentBossExp();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(report, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (analyzed)
        {
            DrawWarnings();
            if (previewed) DrawPreview(); else EditorGUILayout.HelpBox("Preview Changes를 눌러 Before → After를 확인하세요.", MessageType.Warning);
        }
        EditorGUILayout.EndScrollView();
    }

    private void AnalyzeCurrent()
    {
        entries.Clear(); warnings.Clear(); analyzed = true; previewed = false;
        string enemyHealthSource = ReadSource("EnemyHealth.cs");
        foreach (var spec in Prefabs)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(spec.path);
            var health = go == null ? null : go.GetComponentInChildren<EnemyHealth>(true);
            var entry = new Entry { label = spec.label, path = spec.path, chapter = spec.chapter, role = spec.role, expectedCurrent = spec.expected, target = spec.target, health = health, handler = health != null && enemyHealthSource.Contains("expReward") };
            bool aiOk = spec.role == "Melee" ? go != null && go.GetComponentInChildren<EnemyAI>(true) != null : spec.role == "Ranged" ? go != null && go.GetComponentInChildren<RangedEnemyAI>(true) != null : go != null && go.GetComponentInChildren<ChargeEnemyAI>(true) != null;
            if (!aiOk) warnings.Add(spec.label + ": expected " + spec.role + " AI component was not found.");
            entry.current = ReadPrefabExp(spec.path, health);
            entry.safe = entry.current == entry.expectedCurrent;
            if (go == null || health == null) warnings.Add(spec.label + ": EnemyHealth or Prefab is missing.");
            if (!entry.safe) warnings.Add(spec.label + ": [UNEXPECTED CURRENT VALUE] Expected Current=" + entry.expectedCurrent + ", Actual=" + entry.current + ". Auto-apply blocked.");
            entry.handler = entry.handler && aiOk;
            entries.Add(entry);
        }
        AddBossEntry("Assets/Scenes/BossRoom1.unity", "Boss1", 150);
        AddBossEntry("Assets/Scenes/BossRoom2.unity", "Boss2", 180);
        report = "Analyzed " + entries.Count + " entries | Prefab changes=" + entries.Count(x => !x.boss && x.safe && x.current != x.target) + " | Current Boss changes=" + entries.Count(x => x.boss && x.safe && x.handler && x.current != x.target) + " | Warnings=" + warnings.Count;
        Repaint();
    }

    private void AddBossEntry(string scenePath, string label, int target)
    {
        Scene scene = FindLoadedScene(scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            entries.Add(new Entry { label = label, path = scenePath, boss = true, isScene = true, role = "Boss", chapter = label == "Boss1" ? "C1" : "C2", current = -1, expectedCurrent = -1, target = target, handler = false, safe = false });
            warnings.Add("[INFO] " + Path.GetFileNameWithoutExtension(scenePath) + "을 열어야 " + label + " EXP를 적용할 수 있습니다.");
            return;
        }
        EnemyHealth health = FindEnemyHealth(scene);
        int current = ReadHealthExp(health);
        bool found = health != null && current >= 0;
        var e = new Entry { label = label, path = scenePath, boss = true, isScene = true, role = "Boss", chapter = label == "Boss1" ? "C1" : "C2", current = found ? current : -1, expectedCurrent = found ? current : -1, target = target, health = health, handler = found, safe = found && !scene.isDirty };
        if (!found) warnings.Add(label + ": [BOSS EXP NOT SAFE TO APPLY] EnemyHealth.expReward could not be identified safely.");
        else if (scene.isDirty) warnings.Add("[INFO] " + Path.GetFileNameWithoutExtension(scenePath) + " is Dirty. Boss EXP apply is blocked until the Scene is saved or reverted.");
        entries.Add(e);
    }

    private void DrawWarnings()
    {
        if (warnings.Count == 0) EditorGUILayout.HelpBox("No warnings.", MessageType.Info);
        foreach (var w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview Changes (no assets modified)", EditorStyles.boldLabel);
        foreach (var e in entries)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(e.label + " [" + e.Status + "]", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("EXP", e.current < 0 ? "Unavailable  →  " + e.target : e.current + "  →  " + e.target);
            EditorGUILayout.LabelField("Type", e.boss ? "Scene EnemyHealth (Boss)" : e.role + " " + e.chapter + " Prefab");
            if (e.boss && !e.handler) EditorGUILayout.LabelField("Apply", "Open this Boss Scene directly; no automatic Scene loading is performed.");
            if (!e.boss && e.safe) EditorGUILayout.LabelField("Expected Room EXP note", "Uses the same role EXP in Run Balance Tuner; Tuner settings are not changed.");
            EditorGUILayout.EndVertical();
        }
    }

    private void ApplyEnemyPrefabExp()
    {
        if (!analyzed || !previewed) return;
        var eligible = entries.Where(x => !x.boss && x.safe && x.handler && x.current != x.target).ToList();
        foreach (var e in eligible) ApplyPrefabEntry(e);
        AnalyzeCurrent(); previewed = true; report += " | Applied Prefabs=" + eligible.Count;
    }

    private void ApplyCurrentBossExp()
    {
        if (!analyzed || !previewed) return;
        var eligible = entries.Where(x => x.boss && x.safe && x.handler && x.current != x.target).ToList();
        int applied = 0;
        foreach (var e in eligible) if (ApplyLoadedBossEntry(e)) applied++;
        AnalyzeCurrent(); previewed = true; report += " | Applied Current Boss=" + applied;
    }

    private static void ApplyPrefabEntry(Entry e)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(e.path);
        try
        {
            var health = root.GetComponentInChildren<EnemyHealth>(true); if (health == null) return;
            Undo.RecordObject(health, "Apply Enemy EXP Balance");
            var so = new SerializedObject(health); var prop = so.FindProperty("expReward"); if (prop == null) return;
            prop.intValue = e.target; so.ApplyModifiedProperties();
            EditorUtility.SetDirty(health); PrefabUtility.SaveAsPrefabAsset(root, e.path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static bool ApplyLoadedBossEntry(Entry e)
    {
        Scene scene = FindLoadedScene(e.path);
        if (!scene.IsValid() || !scene.isLoaded || scene.isDirty) return false;
        EnemyHealth health = FindEnemyHealth(scene);
        if (health == null) return false;
        Undo.RecordObject(health, "Apply Boss EXP Balance");
        var so = new SerializedObject(health); var prop = so.FindProperty("expReward");
        if (prop == null) return false;
        prop.intValue = e.target; so.ApplyModifiedProperties(); EditorUtility.SetDirty(health); EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static Scene FindLoadedScene(string path)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.path == path) return scene;
        }
        return default;
    }

    private static EnemyHealth FindEnemyHealth(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var health = root.GetComponentInChildren<EnemyHealth>(true);
            if (health != null) return health;
        }
        return null;
    }

    private static int ReadHealthExp(EnemyHealth health)
    {
        if (health == null) return -1;
        var so = new SerializedObject(health); var prop = so.FindProperty("expReward");
        return prop == null ? -1 : prop.intValue;
    }

    private static int ReadPrefabExp(string path, EnemyHealth health)
    {
        return ReadHealthExp(health);
    }

    private static string ReadSource(string file)
    {
        string path = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(file) + " t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileName(p).Equals(file, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(path) ? string.Empty : File.ReadAllText(path);
    }
}
