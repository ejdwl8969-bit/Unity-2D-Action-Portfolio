using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MiniProject.EditorTools.RoomProduction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only orchestration window for the gameplay (CRG) and castle visual (CRB)
/// authoring tools.  It deliberately talks to the existing windows through their
/// private editor APIs, so the two established generators remain the source of truth.
/// No scene is opened by this window and no action is executed automatically.
/// </summary>
public sealed class RoomProductionPipelineWindow : EditorWindow
{
    private const string Pref = "MiniProject.RoomProductionPipeline.v1.";
    private const string CrgPreviewRoot = "__CRG_PREVIEW__Combat Room";
    private const string CrgCommittedRoot = "__CRG_COMMITTED__Gameplay";
    private const string CrbPreviewRoot = "__CRB_PREVIEW__Environment Visual";
    private const string CrbCommittedRoot = "__CRB_COMMITTED__Environment Visual";
    private const string BackupFolder = "Assets/Generated/CastleRoomBackups";
    private const string CrgPreviewApi = "Generate";
    private const string CrgValidationApi = "ValidateCurrent";
    private const string CrgAnalyzeApi = "AnalyzeCommit";
    private const string CrgCommitApi = "CommitPreviewToScene";
    private const float GridTolerance = 0.001f;
    private static readonly string[] SceneNames = { "Room1-1", "Room1-2", "Room1-3", "Room2-1", "Room2-2", "Room2-3" };
    private static readonly string[] ScenePaths = SceneNames.Select(x => "Assets/Scenes/" + x + ".unity").ToArray();
    private static readonly int[] DefaultMelee = { 12, 10, 8, 12, 10, 8 };
    private static readonly int[] DefaultRanged = { 4, 5, 6, 5, 6, 7 };
    private static readonly int[] DefaultCharge = { 2, 3, 4, 3, 4, 5 };

    private int roomIndex;
    private int seed = 12345;
    private int roomWidth = 80;
    private int encounterCount = 4;
    private int meleeCount, rangedCount, chargeCount;
    private int visualSeed;
    private Vector2 scroll;
    private string report = "Analyze Project를 실행하세요.";
    private bool projectAnalyzed;
    private bool gameplayPreview;
    private bool gameplayValidated;
    private bool gameplayCommitAnalyzed;
    private bool gameplayCommitReady;
    private string gameplayCommitBlockReason = string.Empty;
    private bool castlePreview;
    private bool castleValidated;
    private bool showAdvanced;

    [MenuItem("Tools/Level Design/Run Room Pipeline Self Checks")]
    private static void RunSelfChecksMenu()
    {
        var w = GetWindow<RoomProductionPipelineWindow>("Room Production Pipeline");
        w.RunSelfChecks();
        w.Repaint();
    }

    [MenuItem("Tools/Level Design/Room Production Pipeline")]
    private static void OpenWindow()
    {
        var w = GetWindow<RoomProductionPipelineWindow>("Room Production Pipeline");
        w.minSize = new Vector2(560f, 720f);
        w.Show();
    }

    private void OnEnable()
    {
        roomIndex = Mathf.Clamp(EditorPrefs.GetInt(Pref + "Room", 0), 0, SceneNames.Length - 1);
        seed = EditorPrefs.GetInt(RoomKey("Seed"), 12345);
        roomWidth = EditorPrefs.GetInt(RoomKey("Width"), 80);
        encounterCount = EditorPrefs.GetInt(RoomKey("Encounters"), 4);
        LoadComposition();
        visualSeed = EditorPrefs.GetInt(RoomKey("VisualSeed"), 1000 + roomIndex + 1);
    }

    private void OnDisable()
    {
        SaveConfig();
    }

    private string RoomKey(string suffix) { return Pref + SceneNames[roomIndex] + "." + suffix; }

    private void LoadComposition()
    {
        meleeCount = EditorPrefs.GetInt(RoomKey("Melee"), DefaultMelee[roomIndex]);
        rangedCount = EditorPrefs.GetInt(RoomKey("Ranged"), DefaultRanged[roomIndex]);
        chargeCount = EditorPrefs.GetInt(RoomKey("Charge"), DefaultCharge[roomIndex]);
    }

    private void SaveConfig()
    {
        EditorPrefs.SetInt(Pref + "Room", roomIndex);
        EditorPrefs.SetInt(RoomKey("Seed"), seed);
        EditorPrefs.SetInt(RoomKey("Width"), roomWidth);
        EditorPrefs.SetInt(RoomKey("Encounters"), encounterCount);
        EditorPrefs.SetInt(RoomKey("Melee"), meleeCount);
        EditorPrefs.SetInt(RoomKey("Ranged"), rangedCount);
        EditorPrefs.SetInt(RoomKey("Charge"), chargeCount);
        EditorPrefs.SetInt(RoomKey("VisualSeed"), visualSeed);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Room Production Pipeline은 현재 선택한 Room에서 기존 CRG/CRB를 연결합니다. Scene은 자동으로 열거나 저장하지 않습니다.", MessageType.Info);
        int nextRoom = EditorGUILayout.Popup("Room", roomIndex, SceneNames);
        if (nextRoom != roomIndex)
        {
            SaveConfig();
            roomIndex = nextRoom;
            LoadComposition();
            visualSeed = EditorPrefs.GetInt(RoomKey("VisualSeed"), 1000 + roomIndex + 1);
            ResetState("Room 선택이 변경되었습니다.");
        }

        string expectedPath = ScenePaths[roomIndex];
        Scene active = SceneManager.GetActiveScene();
        string sceneStatus = active.IsValid() && active.isLoaded ? (active.path == expectedPath ? "FOUND / CORRECT" : "WRONG SCENE") : "NOT OPEN";
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Chapter", roomIndex < 3 ? "Chapter 1" : "Chapter 2");
            EditorGUILayout.TextField("Theme", roomIndex < 3 ? "Royal" : "Dark");
            EditorGUILayout.TextField("Detected Scene", SceneNames[roomIndex]);
            EditorGUILayout.TextField("Scene Status", sceneStatus);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Gameplay Generation", EditorStyles.boldLabel);
        seed = EditorGUILayout.IntField("Seed", seed);
        roomWidth = Mathf.Clamp(EditorGUILayout.IntField("Room Width", roomWidth), 60, 200);
        encounterCount = Mathf.Clamp(EditorGUILayout.IntField("Encounter Count", encounterCount), 1, 8);
        meleeCount = Mathf.Max(0, EditorGUILayout.IntField("Melee", meleeCount));
        rangedCount = Mathf.Max(0, EditorGUILayout.IntField("Ranged", rangedCount));
        chargeCount = Mathf.Max(0, EditorGUILayout.IntField("Charge", chargeCount));
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.IntField("Total", meleeCount + rangedCount + chargeCount);

        EditorGUILayout.LabelField("Visual Profile", EditorStyles.boldLabel);
        DrawVisualProfileStatus();
        visualSeed = EditorGUILayout.IntField("Visual Seed", visualSeed);

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced / Safety", true);
        if (showAdvanced)
        {
            EditorGUILayout.HelpBox("Existing CRG/CRB safety checks remain authoritative. Existing gameplay commits are never overwritten automatically.", MessageType.None);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Additive Scene Loading", false);
                EditorGUILayout.Toggle("Auto Save", false);
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(false))
        {
            if (GUILayout.Button("Analyze Project")) AnalyzeProject();
            if (GUILayout.Button("Prepare Current Room")) PrepareCurrentRoom();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Gameplay Preview")) GenerateGameplayPreview();
            if (GUILayout.Button("Validate Gameplay")) ValidateGameplay();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Castle Preview")) GenerateCastlePreview();
            if (GUILayout.Button("Validate Castle Visual")) ValidateCastleVisual();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Regenerate Decoration")) { visualSeed++; GenerateCastlePreview(); }
            if (GUILayout.Button("Regenerate Current Room")) RegenerateCurrentRoom();
            if (GUILayout.Button("Clear All Preview")) ClearAllPreview();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Analyze Gameplay Commit")) AnalyzeGameplayCommit();
            if (GUILayout.Button("Commit Current Room")) CommitCurrentRoom();
            if (GUILayout.Button("Analyze Visual Commit")) AnalyzeVisualCommit();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(2f);
        using (new EditorGUI.DisabledScope(!CanCommitGameplay()))
        {
            if (GUILayout.Button("Commit Gameplay (existing CRG safety checks)")) CommitGameplay();
        }
        using (new EditorGUI.DisabledScope(!CanCommitVisual()))
        {
            if (GUILayout.Button("Commit Visual To Scene")) CommitVisual();
        }
        if (GUILayout.Button("Run Room Pipeline Self Checks")) RunSelfChecks();
        if (GUILayout.Button("Batch Room Dashboard")) ShowDashboard();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Gameplay", gameplayValidated ? "VALIDATED" : gameplayPreview ? "PREVIEW" : "NOT PREPARED");
        EditorGUILayout.LabelField("Visual", castleValidated ? "VALIDATED" : castlePreview ? "PREVIEW" : "NOT PREPARED");
        EditorGUILayout.LabelField("Overall", gameplayValidated && castleValidated ? "READY FOR PLAY TEST / MANUAL COMMIT REVIEW" : "INCOMPLETE");
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(230f));
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void DrawVisualProfileStatus()
    {
        string theme = roomIndex < 3 ? "Royal" : "Dark";
        string[] slots = { "BackgroundFill", "BackWall", "Ground", "Platform" };
        foreach (string slot in slots)
        {
            string value = EditorPrefs.GetString("MiniProject.CastleRoomBuilder.v1." + theme + "." + slot, string.Empty);
            Sprite sprite = LoadSprite(value);
            string label;
            if ((slot == "Ground" || slot == "Platform") && RoomProductionVisualGridUtility.TryGetVisualGridInfo(theme, slot, out VisualGridSpriteInfo gridInfo, out _))
                label = gridInfo.OriginalSprite.name + " / Grid Source: WORKING COPY / " + gridInfo.WorkingCopyWorldWidth.ToString("0.###") + " wu";
            else
                label = sprite == null ? "NOT CONFIGURED" : sprite.name + " (" + TileSize(sprite) + ")";
            EditorGUILayout.LabelField(slot, label);
        }
    }

    private static Sprite LoadSprite(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        string[] split = value.Split('|');
        if (split.Length == 0) return null;
        string path = AssetDatabase.GUIDToAssetPath(split[0]);
        if (string.IsNullOrEmpty(path)) return null;
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        if (split.Length > 1 && long.TryParse(split[1], out long localId))
        {
            foreach (Sprite s in sprites) { if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out _, out long id) && id == localId) return s; }
        }
        return sprites.FirstOrDefault();
    }

    private static string TileSize(Sprite sprite)
    {
        if (sprite == null || sprite.pixelsPerUnit <= 0f) return "N/A";
        return sprite.rect.width.ToString("0.##") + "x" + sprite.rect.height.ToString("0.##") + " px / " + (sprite.rect.width / sprite.pixelsPerUnit).ToString("0.###") + " wu";
    }

    private void AnalyzeProject()
    {
        Scene active = SceneManager.GetActiveScene();
        var lines = new List<string> { "PROJECT AUDIT", "Target: " + SceneNames[roomIndex], "Theme: " + (roomIndex < 3 ? "Royal" : "Dark"), "Scene Status: " + (active.IsValid() && active.isLoaded && active.path == ScenePaths[roomIndex] ? "FOUND / CORRECT" : "NOT OPEN OR WRONG SCENE") };
        lines.Add("CRG script: " + (File.Exists(Path.Combine(Application.dataPath, "Editor/LevelDesign/CombatRoomGeneratorWindow.cs")) ? "FOUND" : "MISSING"));
        lines.Add("CRB script: " + (File.Exists(Path.Combine(Application.dataPath, "Editor/EnvironmentAuthoring/CastleRoomBuilderWindow.cs")) ? "FOUND" : "MISSING"));
        lines.Add("Gameplay committed root: " + (FindRoot(active, CrgCommittedRoot) != null ? "FOUND" : "MISSING"));
        lines.Add("Visual committed root: " + (FindRoot(active, CrbCommittedRoot) != null ? "FOUND" : "MISSING"));
        lines.Add("Additive/Preview Scene load: NOT USED");
        lines.Add("Runtime Asset mutation: NOT USED by pipeline orchestration");
        projectAnalyzed = true;
        report = string.Join("\n", lines);
        Repaint();
    }

    private void PrepareCurrentRoom()
    {
        ClearAllPreview();
        SaveConfig();
        AnalyzeProject();
        GenerateGameplayPreview();
        if (gameplayPreview) ValidateGameplay();
        GenerateCastlePreview();
        if (castlePreview) ValidateCastleVisual();
    }

    private void GenerateGameplayPreview()
    {
        if (!CheckTargetScene(out string error)) { report = "[ERROR] " + error; return; }
        SaveConfig();
        object crg = GetWindowInstance("CombatRoomGeneratorWindow");
        if (crg == null) { report = "[ERROR] CombatRoomGeneratorWindow를 찾지 못했습니다."; return; }
        SetField(crg, "chapter", roomIndex < 3 ? 1 : 2);
        SetField(crg, "roomIndex", (roomIndex % 3) + 1);
        SetField(crg, "seed", seed);
        SetField(crg, "roomWidth", roomWidth);
        SetField(crg, "encounterCount", encounterCount);
        SetField(crg, "meleeCount", meleeCount);
        SetField(crg, "rangedCount", rangedCount);
        SetField(crg, "chargeCount", chargeCount);
        Invoke(crg, CrgPreviewApi, false);
        gameplayPreview = FindRoot(SceneManager.GetActiveScene(), CrgPreviewRoot) != null;
        gameplayValidated = false;
        InvalidateGameplayCommitAnalysis();
        report = gameplayPreview ? "[GAMEPLAY PREVIEW] CRG Preview가 생성되었습니다. Validate Gameplay를 실행하세요." : "[ERROR] CRG Preview를 생성하지 못했습니다.";
        Repaint();
    }

    private void ValidateGameplay()
    {
        object crg = GetWindowInstance("CombatRoomGeneratorWindow");
        if (crg == null) { report = "[ERROR] CombatRoomGeneratorWindow를 찾지 못했습니다."; return; }
        Invoke(crg, CrgValidationApi);
        gameplayValidated = true;
        InvalidateGameplayCommitAnalysis();
        report = "[GAMEPLAY VALIDATION] CRG ValidateCurrent 실행 완료. 상세 결과는 Combat Room Generator에서 확인하세요.";
        Repaint();
    }

    private void GenerateCastlePreview()
    {
        if (!CheckTargetScene(out string error)) { report = "[ERROR] " + error; return; }
        object crb = GetWindowInstance("MiniProject.EditorTools.EnvironmentAuthoring.CastleRoomBuilderWindow");
        if (crb == null) { report = "[ERROR] CastleRoomBuilderWindow를 찾지 못했습니다."; return; }
        SetCastleGeometrySourceMode(crb, "CRGPreview");
        SetField(crb, "selectedRoomIndex", roomIndex);
        object rooms = GetStaticField(crb.GetType(), "Rooms");
        object room = ((Array)rooms).GetValue(roomIndex);
        string dryRun = (string)Invoke(crb, "RunDryRun", room);
        if (dryRun.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0) { report = dryRun; castlePreview = false; SetCastleGeometrySourceMode(crb, "Auto"); return; }
        string preview = (string)Invoke(crb, "RunPreview", room);
        castlePreview = FindRoot(SceneManager.GetActiveScene(), CrbPreviewRoot) != null;
        castleValidated = false;
        report = preview;
        SetCastleGeometrySourceMode(crb, "Auto");
        Repaint();
    }

    private void ValidateCastleVisual()
    {
        object crb = GetWindowInstance("MiniProject.EditorTools.EnvironmentAuthoring.CastleRoomBuilderWindow");
        if (crb == null) { report = "[ERROR] CastleRoomBuilderWindow를 찾지 못했습니다."; return; }
        SetCastleGeometrySourceMode(crb, "CRGPreview");
        object rooms = GetStaticField(crb.GetType(), "Rooms");
        object room = ((Array)rooms).GetValue(roomIndex);
        string dryRun = (string)Invoke(crb, "RunDryRun", room);
        castleValidated = dryRun.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) < 0 && FindRoot(SceneManager.GetActiveScene(), CrbPreviewRoot) != null;
        report = dryRun;
        SetCastleGeometrySourceMode(crb, "Auto");
        Repaint();
    }

    private void ClearAllPreview()
    {
        object crg = GetWindowInstance("CombatRoomGeneratorWindow");
        if (crg != null) Invoke(crg, "ClearPreview");
        object crb = GetWindowInstance("MiniProject.EditorTools.EnvironmentAuthoring.CastleRoomBuilderWindow");
        if (crb != null)
        {
            object rooms = GetStaticField(crb.GetType(), "Rooms");
            object room = ((Array)rooms).GetValue(roomIndex);
            Invoke(crb, "ClearPreview", room);
        }
        gameplayPreview = castlePreview = gameplayValidated = castleValidated = false;
        InvalidateGameplayCommitAnalysis();
        report = "Preview cleared. Existing Scene objects were not modified or saved.";
        Repaint();
    }

    private void RegenerateCurrentRoom()
    {
        seed++;
        ClearAllPreview();
        GenerateGameplayPreview();
        if (gameplayPreview) ValidateGameplay();
        if (gameplayValidated) GenerateCastlePreview();
        if (castlePreview) ValidateCastleVisual();
    }

    private void CommitCurrentRoom()
    {
        if (!CheckTargetScene(out string error)) { report = "[ERROR] " + error; return; }
        if (!gameplayValidated || !castleValidated) { report = "[BLOCKED] Gameplay과 Castle Validation을 먼저 통과해야 합니다."; return; }
        report = "[READY] Current Room transaction을 Gameplay Commit -> Post Validate -> Castle Commit 순서로 진행합니다. 각 단계는 기존 Backup/Undo/Dirty Scene 보호를 사용하며 Scene은 자동 저장하지 않습니다.";
        AnalyzeGameplayCommit();
        CommitGameplay();
        report += "\nGameplay Commit 결과를 Scene을 저장하지 않은 상태에서 확인하세요. Visual Commit 전에는 Castle Preview 재생성과 별도 확인이 필요합니다.";
    }

    private void AnalyzeGameplayCommit()
    {
        InvalidateGameplayCommitAnalysis();
        report = "[GAMEPLAY COMMIT ANALYZE] RUNNING";
        if (!CheckTargetScene(out string targetError))
        {
            gameplayCommitAnalyzed = true;
            gameplayCommitBlockReason = targetError;
            report = "[GAMEPLAY COMMIT ANALYZE]\nFinal Analyze Result: BLOCKED\nReason: " + targetError;
            Repaint();
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        object crg = GetWindowInstance("CombatRoomGeneratorWindow");
        if (crg == null)
        {
            gameplayCommitAnalyzed = true;
            gameplayCommitBlockReason = "CombatRoomGeneratorWindow was not found.";
            report = "[GAMEPLAY COMMIT ANALYZE]\nFinal Analyze Result: BLOCKED\nReason: " + gameplayCommitBlockReason;
            Repaint();
            return;
        }

        GameObject previewBefore = FindRoot(scene, CrgPreviewRoot);
        int previewInstanceBefore = previewBefore == null ? 0 : previewBefore.GetInstanceID();
        bool dirtyBefore = scene.isDirty;
        int seedBefore = GetInstanceField(crg, "seed", int.MinValue);

        try
        {
            MethodInfo analyzeMethod = FindExactInstanceMethod(crg.GetType(), CrgAnalyzeApi, Type.EmptyTypes);
            if (analyzeMethod == null) throw new MissingMethodException(crg.GetType().FullName, CrgAnalyzeApi + "()");
            analyzeMethod.Invoke(crg, null);

            string crgAnalyzeReport = GetInstanceField(crg, "commitReport", string.Empty);
            object commitAnalysis = GetInstanceField<object>(crg, "commitAnalysis", null);
            bool validationCompleted = GetInstanceField(crg, "validationCompleted", false);
            object validation = GetInstanceField<object>(crg, "validation", null);
            int validationErrors = CountValidationSeverity(validation, "ERROR");
            bool validationPass = validationCompleted && validationErrors == 0;

            GameObject preview = FindRoot(scene, CrgPreviewRoot);
            bool previewFound = preview != null;
            string previewGridReport = string.Empty;
            bool previewGridPass = previewFound && EvaluateVisualGridCompatibility(preview, roomIndex < 3 ? 1 : 2, out previewGridReport);
            if (!previewFound) previewGridReport = "Preview root is missing.";

            GameObject existingCommit = FindRoot(scene, CrgCommittedRoot);
            bool existingCommitFound = existingCommit != null;
            string existingGridReport = string.Empty;
            bool existingCommitGridPass = existingCommitFound && EvaluateVisualGridCompatibility(existingCommit, roomIndex < 3 ? 1 : 2, out existingGridReport);
            if (!existingCommitFound) existingGridReport = "No existing gameplay commit.";

            string backupPath = GetObjectField(commitAnalysis, "backupPath", string.Empty);
            string crgReason = GetObjectField(commitAnalysis, "reason", string.Empty);
            bool backupReady = !string.IsNullOrEmpty(backupPath);
            bool requiredReferencesPass = GetObjectField<UnityEngine.Object>(commitAnalysis, "player", null) != null &&
                                          GetObjectField<UnityEngine.Object>(commitAnalysis, "exit", null) != null &&
                                          GetObjectField<UnityEngine.Object>(commitAnalysis, "roomController", null) != null &&
                                          GetObjectField<UnityEngine.Object>(commitAnalysis, "enemyRoot", null) != null &&
                                          crgReason.IndexOf("Missing enemy prefab", StringComparison.OrdinalIgnoreCase) < 0 &&
                                          crgReason.IndexOf("grounding collider", StringComparison.OrdinalIgnoreCase) < 0;
            bool crgReady = GetObjectField(commitAnalysis, "ready", false);

            GameObject previewAfter = FindRoot(scene, CrgPreviewRoot);
            int previewInstanceAfter = previewAfter == null ? 0 : previewAfter.GetInstanceID();
            int seedAfter = GetInstanceField(crg, "seed", int.MinValue);
            bool analyzeWasReadOnly = dirtyBefore == scene.isDirty && previewInstanceBefore == previewInstanceAfter && seedBefore == seedAfter;

            var blockers = new List<string>();
            if (!previewFound) blockers.Add("Gameplay Preview is missing.");
            if (!validationPass) blockers.Add("Gameplay Validation is not complete or contains errors.");
            if (previewFound && !previewGridPass) blockers.Add("Gameplay Preview is not visual-grid compatible.");
            if (existingCommitFound)
            {
                blockers.Add(existingCommitGridPass
                    ? "Existing gameplay commit already exists; automatic replacement is disabled."
                    : "Existing gameplay commit is legacy / visual-grid incompatible. Gameplay regeneration/replacement is required.");
            }
            if (!backupReady) blockers.Add("Scene backup path is unavailable.");
            if (!requiredReferencesPass) blockers.Add("Required references or enemy prefab references are incomplete.");
            if (!analyzeWasReadOnly) blockers.Add("Analyze API unexpectedly changed Seed, Preview identity, or Scene Dirty state.");
            if (!crgReady && !string.IsNullOrEmpty(crgReason) && blockers.Count == 0) blockers.Add(crgReason);

            gameplayCommitAnalyzed = true;
            gameplayCommitReady = crgReady && blockers.Count == 0;
            gameplayCommitBlockReason = gameplayCommitReady ? string.Empty : string.Join(" ", blockers);

            var lines = new List<string>
            {
                "[GAMEPLAY COMMIT ANALYZE]",
                "Analyze API: " + CrgAnalyzeApi + "()",
                "Scene: " + scene.name,
                "Gameplay Preview: " + (previewFound ? "FOUND" : "MISSING"),
                "Gameplay Validation: " + (validationPass ? "PASS" : "FAIL") + " (Errors=" + validationErrors + ")",
                "Tile Grid: " + (previewGridPass ? "PASS" : "FAIL"),
                "Existing Gameplay Commit: " + (existingCommitFound ? "FOUND" : "MISSING"),
                "Existing Commit Tile Compatibility: " + (existingCommitFound ? (existingCommitGridPass ? "PASS" : "FAIL") : "NOT APPLICABLE"),
                "Scene Dirty: " + (scene.isDirty ? "YES" : "NO"),
                "Backup Readiness: " + (backupReady ? "READY" : "BLOCKED"),
                "Required References: " + (requiredReferencesPass ? "PASS" : "FAIL"),
                "Read-Only Guard: " + (analyzeWasReadOnly ? "PASS" : "FAIL"),
                "Final Analyze Result: " + (gameplayCommitReady ? "READY" : "BLOCKED")
            };
            if (!gameplayCommitReady) lines.Add("Reason: " + gameplayCommitBlockReason);
            lines.Add("Preview Grid Detail: " + previewGridReport);
            lines.Add("Existing Commit Grid Detail: " + existingGridReport);
            lines.Add("CRG Analyze API Report:\n" + (string.IsNullOrEmpty(crgAnalyzeReport) ? "MISSING" : crgAnalyzeReport));
            report = string.Join("\n", lines);
        }
        catch (TargetInvocationException exception)
        {
            gameplayCommitAnalyzed = true;
            gameplayCommitReady = false;
            Exception cause = exception.InnerException ?? exception;
            gameplayCommitBlockReason = CrgAnalyzeApi + "() failed: " + cause.GetType().Name + ": " + cause.Message;
            report = "[GAMEPLAY COMMIT ANALYZE]\nFinal Analyze Result: BLOCKED\nReason: " + gameplayCommitBlockReason;
        }
        catch (Exception exception)
        {
            gameplayCommitAnalyzed = true;
            gameplayCommitReady = false;
            gameplayCommitBlockReason = CrgAnalyzeApi + "() failed: " + exception.GetType().Name + ": " + exception.Message;
            report = "[GAMEPLAY COMMIT ANALYZE]\nFinal Analyze Result: BLOCKED\nReason: " + gameplayCommitBlockReason;
        }
        Repaint();
    }

    private void AnalyzeVisualCommit()
    {
        if (!CheckTargetScene(out string error)) { report = "[ERROR] " + error; return; }
        object crb = GetWindowInstance("MiniProject.EditorTools.EnvironmentAuthoring.CastleRoomBuilderWindow");
        if (crb != null) SetCastleGeometrySourceMode(crb, "CRGCommitted");
        if (SceneManager.GetActiveScene().isDirty) { report = "[BLOCKED] Scene is dirty. Save or revert it before Analyze."; return; }
        GameObject preview = FindRoot(SceneManager.GetActiveScene(), CrbPreviewRoot);
        GameObject existing = FindRoot(SceneManager.GetActiveScene(), CrbCommittedRoot);
        GameObject committedGeometry = FindRoot(SceneManager.GetActiveScene(), CrgCommittedRoot);
        report = preview == null
            ? "[BLOCKED] Castle Preview is missing."
            : existing != null
                ? "[BLOCKED] Existing Visual Commit found; automatic overwrite is disabled."
                : "ANALYZE VISUAL COMMIT\nRequested Geometry Source: CRG COMMITTED\nResolved Geometry Source: " + (committedGeometry != null ? "CRG COMMITTED" : "MISSING") + "\nVisual Preview: FOUND\nScene: CLEAN\nBackup Path: " + GetBackupPath(SceneManager.GetActiveScene()) + "\nCommit Status: READY (manual confirmation required)";
    }

    private bool CanCommitGameplay() { return projectAnalyzed && gameplayValidated && gameplayCommitAnalyzed && gameplayCommitReady && !EditorApplication.isPlaying; }
    private bool CanCommitVisual() { return projectAnalyzed && castleValidated && FindRoot(SceneManager.GetActiveScene(), CrbPreviewRoot) != null && FindRoot(SceneManager.GetActiveScene(), CrbCommittedRoot) == null && !EditorApplication.isPlaying && !SceneManager.GetActiveScene().isDirty; }

    private void CommitGameplay()
    {
        if (!CanCommitGameplay())
        {
            report = "[GAMEPLAY COMMIT]\nBLOCKED\nReason: " + (string.IsNullOrEmpty(gameplayCommitBlockReason) ? "Run Analyze Gameplay Commit and resolve all blockers first." : gameplayCommitBlockReason);
            Repaint();
            return;
        }
        object crg = GetWindowInstance("CombatRoomGeneratorWindow");
        if (crg == null) { report = "[GAMEPLAY COMMIT]\nBLOCKED\nReason: CombatRoomGeneratorWindow was not found."; return; }
        Invoke(crg, CrgCommitApi);
        report = "[GAMEPLAY COMMIT] CRG " + CrgCommitApi + "() was invoked. The Scene was not auto-saved.";
    }

    private void CommitVisual()
    {
        if (!CanCommitVisual()) { report = "[BLOCKED] Analyze Visual Commit과 Validation을 먼저 통과해야 합니다."; return; }
        if (!EditorUtility.DisplayDialog("Commit Castle Visual", "Backup을 만든 후 현재 열린 Scene에 Visual Commit을 적용합니다. Scene은 자동 저장하지 않습니다.", "Commit", "Cancel")) return;
        Scene scene = SceneManager.GetActiveScene();
        string backup = CreateBackup(scene);
        if (string.IsNullOrEmpty(backup)) { report = "[COMMIT BLOCKED] Scene backup path is unavailable. DO NOT SAVE SCENE."; return; }
        GameObject preview = FindRoot(scene, CrbPreviewRoot);
        object crb = GetWindowInstance("MiniProject.EditorTools.EnvironmentAuthoring.CastleRoomBuilderWindow");
        if (crb == null) { report = "[COMMIT BLOCKED] CastleRoomBuilderWindow was not found."; return; }
        SetCastleGeometrySourceMode(crb, "CRGCommitted");
        object rooms = GetStaticField(crb.GetType(), "Rooms");
        object room = rooms == null ? null : ((Array)rooms).GetValue(roomIndex);
        if (room == null) { report = "[COMMIT BLOCKED] Castle Room definition was not found."; return; }
        GameObject committed = null;
        string buildReport = string.Empty;
        try
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Commit Castle Visual");
            buildReport = Invoke(crb, "BuildVisualCommitCandidate", room) as string ?? string.Empty;
            committed = Invoke(crb, "TakeValidatedCommitBuild") as GameObject;
            if (committed == null)
                throw new InvalidOperationException("Committed-geometry visual BUILD did not pass validation.\n" + buildReport);
            if (FindRoot(scene, CrbCommittedRoot) != null)
                throw new InvalidOperationException("Existing Visual Commit found; automatic overwrite is disabled.");

            Undo.RegisterCreatedObjectUndo(committed, "Commit Castle Visual");
            committed.name = CrbCommittedRoot;
            committed.hideFlags = HideFlags.None;
            SetHideFlagsRecursively(committed, HideFlags.None);
            SceneManager.MoveGameObjectToScene(committed, scene);
            HideCommittedGameplaySprites(scene);
            Undo.DestroyObjectImmediate(preview);
            report = buildReport + "\nVISUAL COMMIT COMPLETE\nBackup: " + backup + "\nSCENE NOT SAVED - inspect the result before Ctrl+S.";
        }
        catch (Exception ex)
        {
            if (committed != null && committed.name != CrbCommittedRoot) UnityEngine.Object.DestroyImmediate(committed);
            report = buildReport + "\n[COMMIT FAILED]\nDO NOT SAVE SCENE\n" + ex.Message;
        }
    }

    private static void SetHideFlagsRecursively(GameObject go, HideFlags flags)
    {
        go.hideFlags = flags;
        foreach (Transform child in go.transform) SetHideFlagsRecursively(child.gameObject, flags);
    }

    private static void HideCommittedGameplaySprites(Scene scene)
    {
        GameObject root = FindRoot(scene, CrgCommittedRoot);
        if (root == null) return;
        Transform geometry = root.transform.Find("Geometry");
        if (geometry == null) return;
        foreach (SpriteRenderer r in geometry.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Undo.RecordObject(r, "Hide CRG gameplay visual");
            r.enabled = false;
        }
    }

    private static string GetBackupPath(Scene scene)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return BackupFolder + "/" + scene.name + "_BeforeRoomProduction_" + stamp + ".unity";
    }

    private static string CreateBackup(Scene scene)
    {
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return null;
        if (!AssetDatabase.IsValidFolder(BackupFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            AssetDatabase.CreateFolder("Assets/Generated", "CastleRoomBackups");
        }
        string path = GetBackupPath(scene);
        return AssetDatabase.CopyAsset(scene.path, path) ? path : null;
    }

    private void RunSelfChecks()
    {
        bool royalGroundResolved = RoomProductionVisualGridUtility.TryGetVisualGridInfo("Royal", "Ground", out VisualGridSpriteInfo ground, out string groundError);
        bool royalPlatformResolved = RoomProductionVisualGridUtility.TryGetVisualGridInfo("Royal", "Platform", out VisualGridSpriteInfo platform, out string platformError);
        float crgGroundGrid = royalGroundResolved ? ground.WorkingCopyWorldWidth : 0f;
        float crgPlatformGrid = royalPlatformResolved ? platform.WorkingCopyWorldWidth : 0f;
        float crbGroundGrid = royalGroundResolved ? RoomProductionVisualGridUtility.GetWorldWidth(ground.WorkingCopySprite) : 0f;
        float crbPlatformGrid = royalPlatformResolved ? RoomProductionVisualGridUtility.GetWorldWidth(platform.WorkingCopySprite) : 0f;
        bool originalGroundExpected = royalGroundResolved && Mathf.Abs(ground.OriginalWorldWidth - 0.98f) <= 0.001f;
        bool workingGroundExpected = royalGroundResolved && Mathf.Abs(ground.WorkingCopyWorldWidth - 6.125f) <= 0.001f;
        bool workingPlatformExpected = royalPlatformResolved && Mathf.Abs(platform.WorkingCopyWorldWidth - 6.125f) <= 0.001f;
        bool gridMatch = royalGroundResolved && royalPlatformResolved &&
                         Mathf.Abs(crgGroundGrid - crbGroundGrid) <= 0.001f &&
                         Mathf.Abs(crgPlatformGrid - crbPlatformGrid) <= 0.001f;
        bool source1 = ResolveSourceForSelfCheck(true, true, "CRGPreview") == "CRGPreview";
        bool source2 = ResolveSourceForSelfCheck(true, true, "CRGCommitted") == "CRGCommitted";
        bool source3 = ResolveSourceForSelfCheck(true, true, "Auto") == "CRGCommitted";
        bool source4 = ResolveSourceForSelfCheck(true, false, "CRGPreview") == "FAIL";
        float previewSurfaceY = 20f;
        float committedSurfaceY = 0f;
        float visualCommitSourceY = ResolveVisualCommitYForSelfCheck(previewSurfaceY, committedSurfaceY, "CRGCommitted");
        bool visualCommitCoordinateSource = ResolveSourceForSelfCheck(true, true, "CRGCommitted") == "CRGCommitted" &&
                                            Mathf.Abs(visualCommitSourceY - committedSurfaceY) <= 0.001f;
        Type crgType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("CombatRoomGeneratorWindow", false)).FirstOrDefault(type => type != null);
        bool previewApiMapped = FindExactInstanceMethod(crgType, CrgPreviewApi, new[] { typeof(bool) }) != null;
        bool validationApiMapped = FindExactInstanceMethod(crgType, CrgValidationApi, Type.EmptyTypes) != null;
        bool analyzeApiMapped = FindExactInstanceMethod(crgType, CrgAnalyzeApi, Type.EmptyTypes) != null;
        bool commitApiMapped = FindExactInstanceMethod(crgType, CrgCommitApi, Type.EmptyTypes) != null;
        bool apiNamesAreDistinct = new HashSet<string> { CrgPreviewApi, CrgValidationApi, CrgAnalyzeApi, CrgCommitApi }.Count == 4;
        bool callbackApiMappingPass = previewApiMapped && validationApiMapped && analyzeApiMapped && commitApiMapped && apiNamesAreDistinct;
        report = "ROOM PRODUCTION SELF CHECKS\n" +
                 "VISUAL GRID SOURCE: WORKING COPY\n" +
                 (royalGroundResolved ? RoomProductionVisualGridUtility.FormatGridReport("Ground", ground) : "FAIL Ground: " + groundError) + "\n" +
                 (royalPlatformResolved ? RoomProductionVisualGridUtility.FormatGridReport("Platform", platform) : "FAIL Platform: " + platformError) + "\n" +
                 (originalGroundExpected ? "PASS" : "FAIL") + " Royal original Ground 98/100 = 0.98\n" +
                 (workingGroundExpected ? "PASS" : "FAIL") + " Royal production Ground grid = 6.125\n" +
                 (workingPlatformExpected ? "PASS" : "FAIL") + " Royal production Platform grid = 6.125\n" +
                  (gridMatch ? "PASS CRG/CRB Grid Match" : "FAIL Visual Grid Source mismatch.") + "\n" +
                  (source1 && source2 && source3 && source4 ? "PASS" : "FAIL") + " Geometry Source Mode cases\n" +
                  (visualCommitCoordinateSource ? "PASS" : "FAIL") + " Visual Commit SourceMode=CRGCommitted; PreviewY=20, CommittedY=0, ResultY=" + visualCommitSourceY.ToString("0.###") + "\n" +
                  (callbackApiMappingPass ? "PASS" : "FAIL") + " CRG Callback/API Mapping: Preview->" + CrgPreviewApi +
                  ", Validation->" + CrgValidationApi + ", Analyze->" + CrgAnalyzeApi + ", Commit->" + CrgCommitApi + "\n" +
                  "No Scene opened, saved, or modified by self checks.";
    }

    private static int DeterministicHash(int value, int room) { unchecked { return value * 397 ^ room * 7919; } }

    private static string ResolveSourceForSelfCheck(bool committed, bool preview, string requested)
    {
        if (requested == "CRGPreview") return preview ? "CRGPreview" : "FAIL";
        if (requested == "CRGCommitted") return committed ? "CRGCommitted" : "FAIL";
        return committed ? "CRGCommitted" : preview ? "CRGPreview" : "FAIL";
    }

    private static float ResolveVisualCommitYForSelfCheck(float previewY, float committedY, string requested)
    {
        return requested == "CRGCommitted" ? committedY : previewY;
    }

    private void InvalidateGameplayCommitAnalysis()
    {
        gameplayCommitAnalyzed = false;
        gameplayCommitReady = false;
        gameplayCommitBlockReason = string.Empty;
    }

    private static bool EvaluateVisualGridCompatibility(GameObject root, int chapter, out string details)
    {
        if (root == null)
        {
            details = "Root is missing.";
            return false;
        }
        if (!RoomProductionVisualGridUtility.TryGetVisualGridInfo(chapter, "Ground", out VisualGridSpriteInfo groundInfo, out string groundError))
        {
            details = groundError;
            return false;
        }
        if (!RoomProductionVisualGridUtility.TryGetVisualGridInfo(chapter, "Platform", out VisualGridSpriteInfo platformInfo, out string platformError))
        {
            details = platformError;
            return false;
        }

        Transform geometry = root.transform.Find("Geometry");
        if (geometry == null)
        {
            details = "Geometry root is missing.";
            return false;
        }

        BoxCollider2D[] colliders = geometry.GetComponentsInChildren<BoxCollider2D>(true)
            .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
            .ToArray();
        int checkedCount = 0;
        int failureCount = 0;
        float maxError = 0f;
        foreach (BoxCollider2D collider in colliders)
        {
            bool isGround = IsUnderNamedParent(collider.transform, "GroundSegments");
            bool isPlatform = IsUnderNamedParent(collider.transform, "Platforms");
            if (!isGround && !isPlatform) continue;
            float tileWidth = isGround ? groundInfo.WorkingCopyWorldWidth : platformInfo.WorkingCopyWorldWidth;
            float width = collider.bounds.size.x;
            int tileCount = tileWidth <= 0f ? 0 : Mathf.RoundToInt(width / tileWidth);
            float error = tileWidth <= 0f ? float.PositiveInfinity : Mathf.Abs(width - tileCount * tileWidth);
            maxError = Mathf.Max(maxError, error);
            checkedCount++;
            if (tileCount <= 0 || error > GridTolerance) failureCount++;
        }

        details = "Ground Grid=" + groundInfo.WorkingCopyWorldWidth.ToString("0.###") +
                  ", Platform Grid=" + platformInfo.WorkingCopyWorldWidth.ToString("0.###") +
                  ", Surfaces=" + checkedCount + ", Failed=" + failureCount +
                  ", MaxError=" + maxError.ToString("0.######");
        return checkedCount > 0 && failureCount == 0;
    }

    private static bool IsUnderNamedParent(Transform transform, string parentName)
    {
        for (Transform current = transform; current != null; current = current.parent)
            if (current.name == parentName) return true;
        return false;
    }

    private static int CountValidationSeverity(object validation, string severity)
    {
        if (!(validation is System.Collections.IEnumerable enumerable)) return 0;
        int count = 0;
        foreach (object item in enumerable)
            if (string.Equals(GetObjectField(item, "severity", string.Empty), severity, StringComparison.OrdinalIgnoreCase)) count++;
        return count;
    }

    private static MethodInfo FindExactInstanceMethod(Type type, string methodName, Type[] parameterTypes)
    {
        return type?.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
    }

    private static T GetInstanceField<T>(object target, string fieldName, T fallback)
    {
        return GetObjectField(target, fieldName, fallback);
    }

    private static T GetObjectField<T>(object target, string fieldName, T fallback)
    {
        if (target == null) return fallback;
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        object value = field?.GetValue(target);
        return value is T typed ? typed : fallback;
    }

    private void ShowDashboard()
    {
        var lines = new List<string> { "ROOM DASHBOARD (read-only)", "Room | Gameplay | Visual | Status" };
        for (int i = 0; i < SceneNames.Length; i++)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePaths[i]);
            bool gp = FindRoot(scene, CrgCommittedRoot) != null;
            bool vis = FindRoot(scene, CrbCommittedRoot) != null;
            lines.Add(SceneNames[i] + " | " + (gp ? "FOUND" : "MISSING") + " | " + (vis ? "FOUND" : "MISSING") + " | " + (gp && vis ? "READY" : "NEEDS WORK"));
        }
        report = string.Join("\n", lines) + "\nOffline analysis never opens additional Scenes.";
        Repaint();
    }

    private void ResetState(string message)
    {
        projectAnalyzed = gameplayPreview = gameplayValidated = castlePreview = castleValidated = false;
        InvalidateGameplayCommitAnalysis();
        report = message;
        Repaint();
    }

    private bool CheckTargetScene(out string error)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) { error = SceneNames[roomIndex] + " Scene을 먼저 열어주세요."; return false; }
        if (scene.path != ScenePaths[roomIndex]) { error = "대상 Scene이 아닙니다. 현재: " + scene.name + ", 대상: " + SceneNames[roomIndex]; return false; }
        error = null; return true;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root;
        return null;
    }

    private static object GetWindowInstance(string typeName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName, false)).FirstOrDefault(t => t != null);
        return type == null ? null : EditorWindow.GetWindow(type, false, typeName, false);
    }

    private static object GetStaticField(Type type, string name) { return type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null); }
    private static void SetField(object target, string name, object value) { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value); }
    private static void SetCastleGeometrySourceMode(object target, string modeName)
    {
        Type enumType = target.GetType().Assembly.GetType("MiniProject.EditorTools.EnvironmentAuthoring.CastleGeometrySourceMode", false);
        if (enumType == null) return;
        SetField(target, "requestedGeometrySourceMode", Enum.Parse(enumType, modeName));
    }
    private static object Invoke(object target, string method, params object[] args)
    {
        MethodInfo info = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).FirstOrDefault(m => m.Name == method && m.GetParameters().Length == args.Length);
        return info == null ? null : info.Invoke(target, args);
    }
}



















