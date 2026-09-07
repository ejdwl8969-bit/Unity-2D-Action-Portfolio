using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Guarded UpgradeDatabase registration tool. Scan is read-only; Apply changes only list references.</summary>
public sealed class RegisterUpgradeDataWindow : EditorWindow
{
    private enum ResultKind { Registered, Add, SkipUnimplemented, Broken, Duplicate, Warning }
    private sealed class Row { public UpgradeData asset; public ResultKind kind; public string note; public bool canAdd; }
    private sealed class BrokenRef { public List<UpgradeData> list; public string pool; public int index; public string label; }
    private UpgradeDatabase database;
    private readonly List<Row> rows = new List<Row>();
    private readonly List<string> broken = new List<string>();
    private readonly List<BrokenRef> brokenRefs = new List<BrokenRef>();
    private readonly List<UpgradeData> candidates = new List<UpgradeData>();
    private bool scanned;
    private string report = "Scan has not been run.";
    private Vector2 scroll;

    [MenuItem("Tools/Balance/Register Upgrade Data")]
    private static void Open() => GetWindow<RegisterUpgradeDataWindow>("Register Upgrade Data");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Scan is read-only. Apply Registration changes only UpgradeDatabase list references; UpgradeData values are never edited.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan", GUILayout.Height(26))) Scan();
        using (new EditorGUI.DisabledScope(!scanned || brokenRefs.Count == 0))
            if (GUILayout.Button("Remove Broken References", GUILayout.Height(26))) RemoveBrokenReferences();
        using (new EditorGUI.DisabledScope(!scanned || candidates.Count == 0))
            if (GUILayout.Button("Apply Registration", GUILayout.Height(26))) ApplyRegistration();
        EditorGUILayout.EndHorizontal();
        if (!scanned) EditorGUILayout.HelpBox(report, MessageType.Warning);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (scanned)
        {
            EditorGUILayout.LabelField(report, EditorStyles.boldLabel);
            DrawGroup(ResultKind.Registered, "[REGISTERED]");
            DrawGroup(ResultKind.Add, "[ADD]");
            DrawGroup(ResultKind.SkipUnimplemented, "[SKIP UNIMPLEMENTED]");
            DrawGroup(ResultKind.Duplicate, "[DUPLICATE]");
            DrawGroup(ResultKind.Broken, "[BROKEN]");
            DrawGroup(ResultKind.Warning, "[WARNING]");
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawGroup(ResultKind kind, string title)
    {
        var group = rows.Where(x => x.kind == kind).ToList();
        if (group.Count == 0) return;
        EditorGUILayout.LabelField(title + " " + group.Count, EditorStyles.boldLabel);
        foreach (var r in group)
        {
            string name = r.asset == null ? "<null>" : Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(r.asset));
            string type = r.asset == null ? "" : r.asset.upgradeType + " / " + r.asset.rarity;
            string value = r.asset == null ? "" : "value=" + r.asset.value + ", maxStack=" + r.asset.maxStack;
            EditorGUILayout.LabelField(name + " | " + type + " | " + value + " | " + r.note);
        }
    }

    private void Scan()
    {
        rows.Clear(); broken.Clear(); brokenRefs.Clear(); candidates.Clear(); scanned = true;
        string dbPath = AssetDatabase.FindAssets("t:UpgradeDatabase").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        database = string.IsNullOrEmpty(dbPath) ? null : AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(dbPath);
        if (database == null)
        {
            report = "[ERROR] UpgradeDatabase asset was not found.";
            rows.Add(new Row { kind = ResultKind.Warning, note = report }); return;
        }
        var all = AssetDatabase.FindAssets("t:UpgradeData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<UpgradeData>).Where(x => x != null).ToList();
        var occurrences = new Dictionary<UpgradeData, List<string>>();
        ScanList(database.commonUpgrades, "Common", occurrences); ScanList(database.rareUpgrades, "Rare", occurrences); ScanList(database.legendaryUpgrades, "Legendary", occurrences);
        var handlers = ReadHandlers();
        foreach (var asset in all)
        {
            List<string> refs; occurrences.TryGetValue(asset, out refs); int count = refs == null ? 0 : refs.Count;
            bool implemented = handlers.Contains(asset.upgradeType.ToString());
            bool uncertain = !implemented;
            if (count > 1) rows.Add(new Row { asset = asset, kind = ResultKind.Duplicate, note = "registered " + count + " times: " + string.Join(", ", refs) });
            else if (count == 1) rows.Add(new Row { asset = asset, kind = ResultKind.Registered, note = "Database=" + refs[0] + "; Handler=UpgradeManager.Apply" + asset.upgradeType + (string.IsNullOrEmpty(asset.upgradeName) ? "; MISSING NAME" : "") });
            else if (!implemented) rows.Add(new Row { asset = asset, kind = ResultKind.SkipUnimplemented, note = "Runtime handler not implemented; not eligible for registration." + (string.IsNullOrEmpty(asset.upgradeName) ? " MISSING NAME" : "") });
            else if (uncertain) rows.Add(new Row { asset = asset, kind = ResultKind.Warning, note = "Runtime application is uncertain; manual review required." });
            else { rows.Add(new Row { asset = asset, kind = ResultKind.Add, note = "IMPLEMENTED + UNREGISTERED; Handler=UpgradeManager.Apply" + asset.upgradeType + "; will use " + asset.rarity + " pool." + (string.IsNullOrEmpty(asset.upgradeName) ? " MISSING NAME" : ""), canAdd = true }); candidates.Add(asset); }
        }
        AddBrokenRows(occurrences);
        report = "Database: " + Path.GetFileName(dbPath) + " | Assets: " + all.Count + " | ADD: " + candidates.Count + " | Broken: " + broken.Count + " | Common=" + CountValid(database.commonUpgrades) + " Rare=" + CountValid(database.rareUpgrades) + " Legendary=" + CountValid(database.legendaryUpgrades);
        Repaint();
    }

    private void ScanList(List<UpgradeData> list, string pool, Dictionary<UpgradeData, List<string>> occurrences)
    {
        if (list == null) { broken.Add(pool + " list is null"); return; }
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item == null) { string label = pool + "[" + i + "]"; broken.Add(label + " is a missing reference"); brokenRefs.Add(new BrokenRef { list = list, pool = pool, index = i, label = label }); continue; }
            List<string> refs; if (!occurrences.TryGetValue(item, out refs)) occurrences[item] = refs = new List<string>(); refs.Add(pool + "[" + i + "]");
            if (item.rarity.ToString() != pool) rows.Add(new Row { asset = item, kind = ResultKind.Warning, note = "Rarity mismatch: " + pool + " pool contains " + item.rarity });
        }
    }

    private void AddBrokenRows(Dictionary<UpgradeData, List<string>> occurrences)
    {
        foreach (var b in broken) rows.Add(new Row { kind = ResultKind.Broken, note = b });
    }

    private HashSet<string> ReadHandlers()
    {
        string path = AssetDatabase.FindAssets("UpgradeManager t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        string source = string.IsNullOrEmpty(path) ? string.Empty : File.ReadAllText(path);
        var set = new HashSet<string>(Regex.Matches(source, @"case\s+UpgradeType\.(\w+)").Cast<Match>().Select(x => x.Groups[1].Value));
        if (set.Contains("DaggerAttackSpeed") && !HasMethod("Dagger.cs", "AddDaggerAttackSpeed")) set.Remove("DaggerAttackSpeed");
        if (set.Contains("ArrowSpeed") && !HasMethod("Bow.cs", "AddArrowSpeed")) set.Remove("ArrowSpeed");
        return set;
    }

    private bool HasMethod(string file, string method)
    {
        string path = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(file) + " t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        return !string.IsNullOrEmpty(path) && Regex.IsMatch(File.ReadAllText(path), @"\b" + Regex.Escape(method) + @"\s*\(");
    }

    private static int CountValid(List<UpgradeData> list) { return list == null ? 0 : list.Count(x => x != null); }
    private void RemoveBrokenReferences()
    {
        if (!scanned || database == null || brokenRefs.Count == 0) return;
        foreach (var b in brokenRefs) Debug.Log("[REMOVE BROKEN] " + b.label);
        Undo.RecordObject(database, "Remove Broken Upgrade References");
        foreach (var group in brokenRefs.GroupBy(x => x.list))
            foreach (var b in group.OrderByDescending(x => x.index))
                if (b.index >= 0 && b.index < b.list.Count && b.list[b.index] == null) b.list.RemoveAt(b.index);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Scan();
    }
    private void ApplyRegistration()
    {
        if (!scanned || database == null || candidates.Count == 0) return;
        Undo.RecordObject(database, "Register Upgrade Data");
        foreach (var asset in candidates)
        {
            List<UpgradeData> list = Pool(asset.rarity); if (list == null || list.Contains(asset)) continue;
            list.Add(asset);
        }
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        Scan();
    }

    private List<UpgradeData> Pool(UpgradeRarity rarity)
    {
        switch (rarity)
        {
            case UpgradeRarity.Common: return database.commonUpgrades ?? (database.commonUpgrades = new List<UpgradeData>());
            case UpgradeRarity.Rare: return database.rareUpgrades ?? (database.rareUpgrades = new List<UpgradeData>());
            default: return database.legendaryUpgrades ?? (database.legendaryUpgrades = new List<UpgradeData>());
        }
    }
}
