using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>First-pass UpgradeData balancing tool. Analyze/Preview are read-only; Apply is explicit.</summary>
public sealed class UpgradeBalancePassWindow : EditorWindow
{
    private sealed class Proposal
    {
        public string file, name, meaning; public float expectedValue, proposedValue, currentValue; public int expectedStack, proposedStack, currentStack; public UpgradeData asset; public bool currentMatches, handlerFound;
        public string Status => !handlerFound ? "RUNTIME WARNING" : !currentMatches ? "BASELINE MISMATCH" : "READY";
    }

    private static readonly string[] Files =
    {
        "AttackDamage_C.asset", "CriticalChance_C.asset", "MaxHp_C.asset", "MoveSpeed_C.asset", "ArrowSpeed_C.asset", "DaggerAttackSpeed_C.asset", "SwordThirdHitDamage_C.asset",
        "ArrowDistance_R.asset", "ArrowPierce_R.asset", "CriticalDamage_R.asset", "DaggerEchoChance_R.asset", "WindSlashDamage_R.asset", "WindSlashDistance_R.asset", "AttackDamage_L.asset"
    };

    private readonly List<Proposal> proposals = new List<Proposal>();
    private readonly List<string> warnings = new List<string>();
    private bool analyzed, previewed;
    private Vector2 scroll;
    private string report = "Analyze Current를 먼저 실행하세요.";

    [MenuItem("Tools/Balance/Upgrade Balance Pass 1")]
    private static void Open() => GetWindow<UpgradeBalancePassWindow>("Upgrade Balance Pass 1");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("This tool changes only upgradeName, value and maxStack after explicit Apply. Database membership, Type, Rarity, Icon, Scenes and Prefabs are protected.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analyze Current", GUILayout.Height(26))) AnalyzeCurrent();
        using (new EditorGUI.DisabledScope(!analyzed)) if (GUILayout.Button("Preview Changes", GUILayout.Height(26))) { previewed = true; Repaint(); }
        using (new EditorGUI.DisabledScope(!analyzed || !previewed)) if (GUILayout.Button("Apply Balance Pass", GUILayout.Height(26))) ApplyBalancePass();
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
        proposals.Clear(); warnings.Clear(); analyzed = true; previewed = false;
        string manager = ReadSource("UpgradeManager.cs");
        foreach (var file in Files)
        {
            Proposal p = MakeProposal(file); p.asset = FindAsset(file); p.handlerFound = p.asset != null && Regex.IsMatch(manager, @"case\s+UpgradeType\." + Regex.Escape(p.asset.upgradeType.ToString()) + @"\s*:");
            if (p.asset == null) { warnings.Add("Missing UpgradeData: " + file); proposals.Add(p); continue; }
            p.currentValue = p.asset.value; p.currentStack = p.asset.maxStack;
            p.currentMatches = Mathf.Abs(p.currentValue - p.expectedValue) < 0.0001f && p.currentStack == p.expectedStack;
            if (!p.currentMatches) warnings.Add(file + ": expected baseline Value=" + p.expectedValue + ", MaxStack=" + p.expectedStack + "; actual Value=" + p.currentValue + ", MaxStack=" + p.currentStack + ". Auto-apply blocked for this asset.");
            if (!p.handlerFound) warnings.Add(file + ": Runtime Handler was not found; manual review required.");
            if (string.IsNullOrEmpty(p.asset.upgradeName)) warnings.Add(file + ": MISSING NAME (will be filled only by explicit Apply).");
            proposals.Add(p);
        }
        report = "Analyzed " + proposals.Count + " UpgradeData assets | Ready=" + proposals.Count(x => x.currentMatches && x.handlerFound) + " | Warnings=" + warnings.Count;
        Repaint();
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview Changes (no assets modified)", EditorStyles.boldLabel);
        foreach (var p in proposals)
        {
            string file = p.file.Replace(".asset", "");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(file + "  [" + p.Status + "]", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Runtime meaning", p.meaning);
            EditorGUILayout.LabelField("Name", Show(p.asset == null ? "<missing>" : p.asset.upgradeName) + "  →  " + p.name);
            EditorGUILayout.LabelField("Value", p.currentValue + "  →  " + p.proposedValue);
            EditorGUILayout.LabelField("Max Stack", p.currentStack + "  →  " + p.proposedStack);
            EditorGUILayout.LabelField("Power estimate", Power(p, false) + "  →  " + Power(p, true));
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawWarnings()
    {
        if (warnings.Count == 0) EditorGUILayout.HelpBox("No warnings.", MessageType.Info);
        foreach (var w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
    }

    private void ApplyBalancePass()
    {
        if (!analyzed || !previewed) return;
        var eligible = proposals.Where(x => x.asset != null && x.currentMatches && x.handlerFound).ToList();
        if (eligible.Count == 0) { warnings.Add("No eligible UpgradeData assets to apply."); return; }
        Undo.RecordObjects(eligible.Select(x => (UnityEngine.Object)x.asset).ToArray(), "Apply Upgrade Balance Pass 1");
        foreach (var p in eligible) { p.asset.upgradeName = p.name; p.asset.value = p.proposedValue; p.asset.maxStack = p.proposedStack; }
        foreach (var p in eligible) EditorUtility.SetDirty(p.asset);
        AssetDatabase.SaveAssets();
        AnalyzeCurrent();
        previewed = true;
        report += " | Applied=" + eligible.Count;
    }

    private Proposal MakeProposal(string file)
    {
        var p = new Proposal { file = file, name = "", expectedStack = 10, proposedStack = 1, expectedValue = 0, proposedValue = 0, meaning = "Unknown" };
        switch (file)
        {
            case "AttackDamage_C.asset": p.name = "공격력 증가"; p.expectedValue = 2; p.proposedValue = 1; p.expectedStack = 10; p.proposedStack = 5; p.meaning = "Flat weapon damage (+1 per stack)."; break;
            case "CriticalChance_C.asset":
                p.name = "치명타 확률 증가";
                p.expectedValue = 20;
                p.proposedValue = 20;
                p.expectedStack = 5;
                p.proposedStack = 5;
                p.meaning = "Percentage points in the 0-100 runtime unit (+20%p).";
                break;
            case "MaxHp_C.asset": p.name = "최대 체력 증가"; p.expectedValue = 20; p.proposedValue = 15; p.expectedStack = 10; p.proposedStack = 4; p.meaning = "Flat Max HP (+15 per stack)."; break;
            case "MoveSpeed_C.asset": p.name = "이동속도 증가"; p.expectedValue = 1; p.proposedValue = 0.5f; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Flat MoveSpeed (+0.5 per stack)."; break;
            case "ArrowSpeed_C.asset": p.name = "화살 속도 증가"; p.expectedValue = 1; p.proposedValue = 1; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Bow arrow speed via AddArrowSpeed."; break;
            case "DaggerAttackSpeed_C.asset": p.name = "단검 공격속도 증가"; p.expectedValue = .05f; p.proposedValue = .05f; p.expectedStack = 10; p.proposedStack = 4; p.meaning = "Dagger attack-speed bonus multiplier component (+0.05)."; break;
            case "SwordThirdHitDamage_C.asset": p.name = "검 3타 피해 증가"; p.expectedValue = .1f; p.proposedValue = .1f; p.expectedStack = 10; p.proposedStack = 4; p.meaning = "Third-hit damage multiplier increase."; break;
            case "ArrowDistance_R.asset": p.name = "화살 사거리 증가"; p.expectedValue = 2; p.proposedValue = 1.5f; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Bow arrow distance."; break;
            case "ArrowPierce_R.asset": p.name = "화살 관통 증가"; p.expectedValue = 2; p.proposedValue = 1; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Additional pierce targets (+1)."; break;
            case "CriticalDamage_R.asset": p.name = "치명타 피해 증가"; p.expectedValue = .4f; p.proposedValue = .3f; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Critical multiplier increase."; break;
            case "DaggerEchoChance_R.asset": p.name = "단검 추가 공격 확률 증가"; p.expectedValue = .1f; p.proposedValue = .1f; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Chance in 0-1 unit (+0.1 = 10%)."; break;
            case "WindSlashDamage_R.asset": p.name = "바람참 피해 증가"; p.expectedValue = .3f; p.proposedValue = .2f; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Wind slash damage multiplier component."; break;
            case "WindSlashDistance_R.asset": p.name = "바람참 사거리 증가"; p.expectedValue = 1; p.proposedValue = 1; p.expectedStack = 10; p.proposedStack = 3; p.meaning = "Wind slash distance."; break;
            case "AttackDamage_L.asset": p.name = "공격력 대폭 증가"; p.expectedValue = 50; p.proposedValue = 10; p.expectedStack = 10; p.proposedStack = 1; p.meaning = "Flat weapon damage (+10; about +100% from base 10)."; break;
        }
        return p;
    }

    private static string Power(Proposal p, bool proposed)
    {
        float v = proposed ? p.proposedValue : p.currentValue; int stacks = proposed ? p.proposedStack : p.currentStack;
        if (p.file.Contains("AttackDamage")) return "+" + (v * stacks / 10f * 100f).ToString("0.#") + "% damage max";
        if (p.file.Contains("MoveSpeed")) return "+" + (v * stacks / 7f * 100f).ToString("0.#") + "% move max";
        if (p.file.Contains("MaxHp")) return "+" + (v * stacks / 100f * 100f).ToString("0.#") + "% HP max";
        if (p.file.Contains("CriticalChance")) return "+" + (v * stacks).ToString("0.#") + "%p max";
        return "stack total " + (v * stacks).ToString("0.##");
    }

    private static string Show(string value) => string.IsNullOrEmpty(value) ? "<empty>" : value;
    private static UpgradeData FindAsset(string file) => AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(file) + " t:UpgradeData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UpgradeData>).FirstOrDefault();
    private static string ReadSource(string file) { string path = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(file) + " t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileName(p).Equals(file, StringComparison.OrdinalIgnoreCase)); return string.IsNullOrEmpty(path) ? string.Empty : File.ReadAllText(path); }
}

