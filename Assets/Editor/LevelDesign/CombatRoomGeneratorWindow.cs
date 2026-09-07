using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MiniProject.EditorTools.RoomProduction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Non-destructive first-pass combat-room layout previewer.
/// It creates only DontSave preview objects below __CRG_PREVIEW__Combat Room.
/// No Scene, Prefab, Runtime Asset or Castle Visual is written by this tool.
/// </summary>
public sealed class CombatRoomGeneratorWindow : EditorWindow
{
    private const string PrefKey = "CRG_";
    private const string PreviewRootName = "__CRG_PREVIEW__Combat Room";
    private const float DefaultCameraWidth = 17.8f;
    private const float DefaultCameraHeight = 10f;
    private const float DefaultMinimumEnemyGap = 0.6f;
    private const float DefaultMinimumHeadroom = 3.25f;
    private const float DefaultMeaningfulXOverlap = 1.0f;
    private const float PlayerColliderFallbackWidth = 1f;

    private enum Role { Melee, Ranged, Charge }

    private sealed class Surface
    {
        public string name, surfaceType;
        public float desiredWidth;
        public int tileCount;
        public float minX, maxX, y;
        public float originalMinX, originalMaxX;
        public bool critical;
        public int encounterIndex;
        public Surface(string name, float minX, float maxX, float y, bool critical, string surfaceType = "Ground", int encounterIndex = -1)
        { this.name = name; this.minX = minX; this.maxX = maxX; originalMinX = minX; originalMaxX = maxX; this.y = y; this.critical = critical; this.surfaceType = surfaceType; this.encounterIndex = encounterIndex; }
        public float Width { get { return maxX - minX; } }
        public float OriginalWidth { get { return originalMaxX - originalMinX; } }
    }    private sealed class Marker
    {
        public Role role;
        public int encounter;
        public Vector2 position;
        public float xp, threat, halfWidth, colliderBottomOffset, surfaceY;
        public string prefabPath, groundingColliderPath, groundingColliderType;
        public int ignoredTriggerCount;
        public Sprite sprite;
    }

    private sealed class GroundingColliderInfo
    {
        public Collider2D collider;
        public string path, type;
        public Vector2 offset, size;
        public float rootBottomOffset;
        public int ignoredTriggerCount;
    }

    private sealed class Encounter
    {
        public int index;
        public float minX, maxX;
        public float originalMinX, originalMaxX;
        public readonly List<Marker> markers = new List<Marker>();
        public float Threat { get { return markers.Sum(x => x.threat); } }
        public float XP { get { return markers.Sum(x => x.xp); } }
    }

    private sealed class GroundGapSemantic
    {
        public string previousSurface, nextSurface;
        public float originalGap, finalGap;
        public bool intentional;
    }

    private sealed class Layout
    {
        public int baseSeed, attempt, effectiveSeed;
        public float requestedWidth, width;
        public string macroArchetype;
        public readonly List<string> segmentSequence = new List<string>();
        public readonly List<Surface> surfaces = new List<Surface>();
        public readonly List<Encounter> encounters = new List<Encounter>();
        public readonly List<Marker> markers = new List<Marker>();
        public readonly List<GroundGapSemantic> groundGaps = new List<GroundGapSemantic>();
        public readonly List<string> generationErrors = new List<string>();
        public readonly List<string> retrySummary = new List<string>();
        public readonly List<string> generationWarnings = new List<string>();
        public int headroomErrors, headroomWarnings, blockedLowerPaths, combatSurfacesWithSafeHeadroom;
        public int ceilingOverlapRetries;
        public float lowestValidHeadroom = float.PositiveInfinity;
        public float groundTileWidth, platformTileWidth, maxQuantizationDelta;
        public VisualGridSpriteInfo groundGridInfo, platformGridInfo;
    }    private sealed class HeadroomResult
    {
        public readonly List<Message> messages = new List<Message>();
        public int errors, warnings, blockedLowerPaths, safeCombatSurfaces;
        public float lowestValidHeadroom = float.PositiveInfinity;
        public float groundTileWidth, platformTileWidth, maxQuantizationDelta;
    }

    private sealed class CommitAnalysis
    {
        public bool ready;
        public string scenePath, expectedScene, currentScene, backupPath, reason;
        public Transform player, exit, enemyRoot, roomController;
        public GameObject groundReference, platformReference;
        public readonly List<GameObject> prototypeGeometry = new List<GameObject>();
        public readonly List<GameObject> prototypeEnemies = new List<GameObject>();
        public readonly List<string> warnings = new List<string>();
    }

    private sealed class ReplaceAnalysis
    {
        public bool ready;
        public string reason, backupPath;
        public Scene scene;
        public GameObject existingRoot;
        public CommitAnalysis buildInputs;
        public readonly List<string> blockers = new List<string>();
    }

    private sealed class ReplacementPlacementPlan
    {
        public Surface startSurface, exitSurface;
        public BoxCollider2D startCollider, exitSurfaceCollider;
        public Collider2D exitCollider;
        public SpriteRenderer exitRenderer;
        public float startTop, exitTop;
        public Vector3 playerTarget, exitTarget;
    }

    private sealed class Message
    {
        public string severity, text;
        public Message(string severity, string text) { this.severity = severity; this.text = text; }
    }

    private int chapter = 1;
    private int roomIndex = 1;
    private int seed = 12345;
    private int roomWidth = 80;
    private int encounterCount = 4;
    private int meleeCount = 10;
    private int rangedCount = 5;
    private int chargeCount = 3;
    private Vector2 previewOrigin = new Vector2(0f, 20f);
    private bool advanced;
    private bool showHeightGuides;
    private bool showEnemyColliderDebug;
    private float minimumEnemyGap = DefaultMinimumEnemyGap;
    private float minimumHeadroom = DefaultMinimumHeadroom;
    private float meaningfulXOverlap = DefaultMeaningfulXOverlap;
    private float startSafeZone = 7f;
    private float exitSafeZone = 4f;
    private int maxAttempts = 20;
    private Vector2 scroll;
    private Layout layout;
    private List<Message> validation = new List<Message>();
    private string report = "Generate Preview를 실행하세요.";
    private bool validationCompleted;
    private CommitAnalysis commitAnalysis;
    private ReplaceAnalysis replaceAnalysis;
    private string lastBackupPath;
    private string commitReport = string.Empty;

    private static readonly string[] C1PrefabPaths =
    {
        "Assets/Prefabs/Enemy_C1.prefab",
        "Assets/Prefabs/Enemy_Ranged_C1.prefab",
        "Assets/Prefabs/Enemy_Charge.prefab"
    };
    private static readonly string[] C2PrefabPaths =
    {
        "Assets/Prefabs/Enemy_C2.prefab",
        "Assets/Prefabs/Enemy_Ranged_C2.prefab",
        "Assets/Prefabs/Enemy_Charge_C2.prefab"
    };

    [MenuItem("Tools/Level Design/Combat Room Generator")]
    private static void Open() { GetWindow<CombatRoomGeneratorWindow>("Combat Room Generator"); }

    private void OnEnable()
    {
        chapter = EditorPrefs.GetInt(PrefKey + "Chapter", chapter);
        roomIndex = EditorPrefs.GetInt(PrefKey + "Room", roomIndex);
        seed = EditorPrefs.GetInt(PrefKey + "Seed", seed);
        roomWidth = EditorPrefs.GetInt(PrefKey + "Width", roomWidth);
        meleeCount = EditorPrefs.GetInt(PrefKey + "Melee", meleeCount);
        rangedCount = EditorPrefs.GetInt(PrefKey + "Ranged", rangedCount);
        chargeCount = EditorPrefs.GetInt(PrefKey + "Charge", chargeCount);
        showHeightGuides = EditorPrefs.GetBool(PrefKey + "HeightGuides", showHeightGuides);
        showEnemyColliderDebug = EditorPrefs.GetBool(PrefKey + "EnemyColliderDebug", showEnemyColliderDebug);
        minimumEnemyGap = EditorPrefs.GetFloat(PrefKey + "EnemyGap", minimumEnemyGap);
        minimumHeadroom = EditorPrefs.GetFloat(PrefKey + "Headroom", minimumHeadroom);
        meaningfulXOverlap = EditorPrefs.GetFloat(PrefKey + "XOverlap", meaningfulXOverlap);
    }

    private void OnDisable()
    {
        EditorPrefs.SetInt(PrefKey + "Chapter", chapter);
        EditorPrefs.SetInt(PrefKey + "Room", roomIndex);
        EditorPrefs.SetInt(PrefKey + "Seed", seed);
        EditorPrefs.SetInt(PrefKey + "Width", roomWidth);
        EditorPrefs.SetInt(PrefKey + "Melee", meleeCount);
        EditorPrefs.SetInt(PrefKey + "Ranged", rangedCount);
        EditorPrefs.SetInt(PrefKey + "Charge", chargeCount);
        EditorPrefs.SetBool(PrefKey + "HeightGuides", showHeightGuides);
        EditorPrefs.SetBool(PrefKey + "EnemyColliderDebug", showEnemyColliderDebug);
        EditorPrefs.SetFloat(PrefKey + "EnemyGap", minimumEnemyGap);
        EditorPrefs.SetFloat(PrefKey + "Headroom", minimumHeadroom);
        EditorPrefs.SetFloat(PrefKey + "XOverlap", meaningfulXOverlap);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Preview + Safe Commit generator. Commit requires Analyze Commit, ERROR=0, correct clean Room Scene, and creates a backup before any Scene change. Scene is never auto-saved.", MessageType.Info);

        chapter = EditorGUILayout.Popup("Chapter", chapter - 1, new[] { "Chapter 1", "Chapter 2" }) + 1;
        roomIndex = EditorGUILayout.Popup("Room", roomIndex - 1, new[] { "Room 1", "Room 2", "Room 3" }) + 1;
        seed = EditorGUILayout.IntField("Seed", seed);
        roomWidth = Mathf.Clamp(EditorGUILayout.IntSlider("Room Width", roomWidth, 60, 100), 60, 100);
        encounterCount = Mathf.Clamp(EditorGUILayout.IntSlider("Encounter Count", encounterCount, 1, 8), 1, 8);

        EditorGUILayout.LabelField("Enemy Composition", EditorStyles.boldLabel);
        meleeCount = Mathf.Max(0, EditorGUILayout.IntField("Melee Count", meleeCount));
        rangedCount = Mathf.Max(0, EditorGUILayout.IntField("Ranged Count", rangedCount));
        chargeCount = Mathf.Max(0, EditorGUILayout.IntField("Charge Count", chargeCount));
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.IntField("Total Enemy Count", meleeCount + rangedCount + chargeCount);

        EditorGUILayout.LabelField("Preview Origin", EditorStyles.boldLabel);
        previewOrigin = EditorGUILayout.Vector2Field("Origin", previewOrigin);

        advanced = EditorGUILayout.Foldout(advanced, "Advanced Settings", true);
        if (advanced)
        {
            startSafeZone = Mathf.Max(0f, EditorGUILayout.FloatField("Start Safe Zone", startSafeZone));
            exitSafeZone = Mathf.Max(0f, EditorGUILayout.FloatField("Exit Safe Zone", exitSafeZone));
            maxAttempts = Mathf.Clamp(EditorGUILayout.IntField("Max Generation Attempts", maxAttempts), 1, 100);
            showHeightGuides = EditorGUILayout.Toggle("Show Height Guides", showHeightGuides);
            showEnemyColliderDebug = EditorGUILayout.Toggle("Debug Enemy Collider Alignment", showEnemyColliderDebug);
            minimumEnemyGap = Mathf.Clamp(EditorGUILayout.FloatField("Minimum Enemy Gap", minimumEnemyGap), 0.4f, 1.0f);
            minimumHeadroom = Mathf.Clamp(EditorGUILayout.FloatField("Minimum Headroom", minimumHeadroom), 3.0f, 3.5f);
            meaningfulXOverlap = Mathf.Clamp(EditorGUILayout.FloatField("Meaningful X Overlap", meaningfulXOverlap), 1.0f, 1.5f);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate Preview", GUILayout.Height(26))) Generate(false);
        if (GUILayout.Button("Regenerate", GUILayout.Height(26))) { seed++; Generate(false); }
        if (GUILayout.Button("Validate", GUILayout.Height(26))) ValidateCurrent();
        if (GUILayout.Button("Clear Preview", GUILayout.Height(26))) ClearPreview();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analyze Commit", GUILayout.Height(24))) AnalyzeCommit();
        using (new EditorGUI.DisabledScope(commitAnalysis == null || !commitAnalysis.ready || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Commit Preview To Scene", GUILayout.Height(24))) CommitPreviewToScene();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Analyze Replace Existing Commit", GUILayout.Height(24))) AnalyzeReplaceExistingCommit();
        using (new EditorGUI.DisabledScope(replaceAnalysis == null || !replaceAnalysis.ready || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Replace Existing Gameplay Commit", GUILayout.Height(24))) ReplaceExistingGameplayCommit();
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(commitReport)) EditorGUILayout.HelpBox(commitReport, commitReport.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) >= 0 || commitReport.IndexOf("BLOCKED", StringComparison.OrdinalIgnoreCase) >= 0 ? MessageType.Error : MessageType.Info);
        EditorGUILayout.LabelField(report, EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawReport();
        EditorGUILayout.EndScrollView();
    }

    private void DrawReport()
    {
        if (layout == null) { if (HasPreviewRoot()) EditorGUILayout.HelpBox("[ERROR] Preview exists but LayoutData is missing.", MessageType.Error); return; }
        if (layout.markers == null || layout.encounters == null) { EditorGUILayout.HelpBox("[ERROR] Layout collections are missing.", MessageType.Error); return; }
        EditorGUILayout.LabelField("Layout State", "READY");
        EditorGUILayout.LabelField("Surfaces", layout.surfaces == null ? "0" : layout.surfaces.Count.ToString());
        EditorGUILayout.LabelField("Encounters", layout.encounters.Count.ToString());
        EditorGUILayout.LabelField("Macro Archetype", layout.macroArchetype);
        EditorGUILayout.LabelField("Ground Segments", layout.surfaces.Count(s => s.critical && s.surfaceType != "Bridge").ToString());
        EditorGUILayout.LabelField("Platforms", layout.surfaces.Count(s => !s.critical).ToString());
        EditorGUILayout.LabelField("Gaps", CountCriticalGaps(layout).ToString());
        var criticalSurfaces = layout.surfaces.Where(s => s.critical && s.surfaceType != "Bridge").ToList();
        var allSurfaces = layout.surfaces.ToList();
        EditorGUILayout.LabelField("Height Levels", string.Join(" / ", criticalSurfaces.Select(s => s.y).Distinct().OrderBy(y => y).Select(y => y.ToString("0.##"))));
        if (criticalSurfaces.Count > 0)
        {
            float lowestCritical = criticalSurfaces.Min(s => s.y);
            float highestCritical = criticalSurfaces.Max(s => s.y);
            EditorGUILayout.LabelField("Lowest Critical Y", lowestCritical.ToString("0.##"));
            EditorGUILayout.LabelField("Highest Critical Y", highestCritical.ToString("0.##"));
            EditorGUILayout.LabelField("Start / Exit Critical Y", criticalSurfaces.OrderBy(s => s.minX).First().y.ToString("0.##") + " / " + criticalSurfaces.OrderBy(s => s.minX).Last().y.ToString("0.##"));
            EditorGUILayout.LabelField("Critical Vertical Span", (highestCritical - lowestCritical).ToString("0.##"));
        }
        if (allSurfaces.Count > 0)
        {
            EditorGUILayout.LabelField("Lowest Any Surface Y", allSurfaces.Min(s => s.y).ToString("0.##"));
            EditorGUILayout.LabelField("Highest Any Surface Y", allSurfaces.Max(s => s.y).ToString("0.##"));
            EditorGUILayout.LabelField("Total Vertical Span", (allSurfaces.Max(s => s.y) - allSurfaces.Min(s => s.y)).ToString("0.##"));
        }
        float cameraHeight = ReadCameraWorldHeight();
        EditorGUILayout.LabelField("Camera Vertical World Height", cameraHeight.ToString("0.##"));
        EditorGUILayout.LabelField("Camera Vertical Compatibility", VerticalCompatibility(layout, cameraHeight));
        EditorGUILayout.LabelField("Camera Y Follow", ReadCameraYFollowStatus());
        EditorGUILayout.LabelField("Headroom Errors", layout.headroomErrors.ToString());
        EditorGUILayout.LabelField("Headroom Warnings", layout.headroomWarnings.ToString());
        EditorGUILayout.LabelField("Lowest Valid Headroom", float.IsPositiveInfinity(layout.lowestValidHeadroom) ? "N/A" : layout.lowestValidHeadroom.ToString("0.##"));
        EditorGUILayout.LabelField("Combat Surfaces With Safe Headroom", layout.combatSurfacesWithSafeHeadroom.ToString());
        EditorGUILayout.LabelField("Blocked Lower Paths", layout.blockedLowerPaths.ToString());
        EditorGUILayout.LabelField("Ceiling Overlap Retries", layout.ceilingOverlapRetries.ToString());
        EditorGUILayout.LabelField("Segments", string.Join(" → ", layout.segmentSequence));
        if (layout.retrySummary != null && layout.retrySummary.Count > 0) EditorGUILayout.LabelField("Retry Summary", string.Join(" | ", layout.retrySummary));
        EditorGUILayout.LabelField("Layout", "Chapter " + chapter + " / Room " + roomIndex + " | Base Seed " + layout.baseSeed + " | Attempt " + layout.attempt + " | Effective Seed " + layout.effectiveSeed);
        EditorGUILayout.LabelField("Enemy Count", layout.markers.Count.ToString());
        EditorGUILayout.LabelField("Expected XP", layout.markers.Sum(x => x.xp).ToString("0.##"));
        EditorGUILayout.LabelField("Total Threat", layout.markers.Sum(x => x.threat).ToString("0.##"));
        foreach (var e in layout.encounters)
            EditorGUILayout.LabelField("Encounter " + (e.index + 1), "Enemies=" + e.markers.Count + "  Threat=" + e.Threat.ToString("0.##") + "  XP=" + e.XP.ToString("0.##"));
        int errorCount = validation == null ? 0 : validation.Count(x => x.severity == "ERROR");
        int warningCount = validation == null ? 0 : validation.Count(x => x.severity == "WARNING");
        EditorGUILayout.LabelField("Commit Readiness", errorCount == 0 ? (warningCount == 0 ? "READY FOR MANUAL REVIEW" : "READY WITH WARNINGS") : "BLOCKED BY ERRORS");
        if (validation != null && validation.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            foreach (var m in validation)
                EditorGUILayout.HelpBox("[" + m.severity + "] " + m.text, m.severity == "ERROR" ? MessageType.Error : m.severity == "WARNING" ? MessageType.Warning : MessageType.Info);
        }
    }

    private void Generate(bool keepExisting)
    {
        validationCompleted = false;
        commitAnalysis = null;
        replaceAnalysis = null;
        commitReport = string.Empty;
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded)
        {
            report = "[ERROR] 대상 Gameplay Scene을 먼저 열어주세요.";
            layout = null; Repaint(); return;
        }
        if (!keepExisting) ClearPreview();

        Layout successful = null;
        Layout last = null;
        var retrySummary = new List<string>();
        int ceilingOverlapRetries = 0;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Layout candidate = BuildLayout(seed, attempt);
            last = candidate;
            List<Message> check = ValidateLayout(candidate, false);
            int errors = check.Count(x => x.severity == "ERROR");
            if (check.Any(x => x.severity == "ERROR" && (x.text.IndexOf("headroom", StringComparison.OrdinalIgnoreCase) >= 0 || x.text.IndexOf("ceiling", StringComparison.OrdinalIgnoreCase) >= 0 || x.text.IndexOf("upper route", StringComparison.OrdinalIgnoreCase) >= 0))) ceilingOverlapRetries++;
            if (errors == 0) { retrySummary.Add("Attempt " + attempt + ": SUCCESS"); candidate.retrySummary.AddRange(retrySummary); successful = candidate; break; }
            retrySummary.Add("Attempt " + attempt + ": " + errors + " error(s)");
        }
        layout = successful ?? last;
        if (layout != null) layout.ceilingOverlapRetries = ceilingOverlapRetries;
        if (layout != null && layout.retrySummary.Count == 0) layout.retrySummary.AddRange(retrySummary);
        if (layout == null)
        {
            validation = new List<Message> { new Message("ERROR", "No generated layout to validate.") };
            report = "[GENERATION FAILED] No layout was produced."; Repaint(); return;
        }
        CreatePreviewObjects(active, layout);
        validation = ValidateLayout(layout, true);
        report = validation.Any(x => x.severity == "ERROR") ? "[GENERATION FAILED] Preview created with validation errors." : "Preview generated successfully.";
        Repaint();
    }

    private static readonly string[] MacroArchetypes = { "Flat With Variations", "Ascending", "Descending", "Valley", "Hill", "Alternating Levels", "Split Ground", "Platform Heavy" };

    private Layout BuildLayout(int baseSeed, int attempt)
    {
        int effectiveSeed = unchecked(baseSeed + attempt * 7919 + chapter * 101 + roomIndex * 1009);
        var rng = new System.Random(effectiveSeed);
        var result = new Layout { baseSeed = baseSeed, attempt = attempt, effectiveSeed = effectiveSeed, requestedWidth = roomWidth, width = roomWidth };
        if (RoomProductionVisualGridUtility.TryGetVisualGridInfo(chapter, "Ground", out VisualGridSpriteInfo groundGridInfo, out string groundGridError))
        {
            result.groundGridInfo = groundGridInfo;
            result.groundTileWidth = groundGridInfo.WorkingCopyWorldWidth;
        }
        else result.generationWarnings.Add("Ground visual grid: " + groundGridError);

        if (RoomProductionVisualGridUtility.TryGetVisualGridInfo(chapter, "Platform", out VisualGridSpriteInfo platformGridInfo, out string platformGridError))
        {
            result.platformGridInfo = platformGridInfo;
            result.platformTileWidth = platformGridInfo.WorkingCopyWorldWidth;
        }
        else result.generationWarnings.Add("Platform visual grid: " + platformGridError);

        if (result.groundTileWidth <= 0f || result.platformTileWidth <= 0f)
            result.generationWarnings.Add("Production visual grid requires normalized Castle working-copy Sprites.");
        result.macroArchetype = MacroArchetypes[rng.Next(MacroArchetypes.Length)];

        float startX = Mathf.Clamp(startSafeZone + 1f, 5f, roomWidth * 0.2f);
        float endX = Mathf.Clamp(roomWidth - exitSafeZone - 1f, roomWidth * 0.8f, roomWidth - 5f);
        float encounterSpan = Mathf.Max(8f, (endX - startX) / encounterCount);
        for (int i = 0; i < encounterCount; i++)
        {
            float center = startX + encounterSpan * (i + 0.5f);
            // Keep encounter bands narrower than a camera-width window to preserve traversal margins.
            float width = Mathf.Clamp(10f + rng.Next(0, 4), 10f, Mathf.Min(14f, encounterSpan - 1f));
            float min = center - width * 0.5f;
            float max = center + width * 0.5f;
            result.encounters.Add(new Encounter { index = i, minX = min, maxX = max, originalMinX = min, originalMaxX = max });
        }

        int segmentCount = 6 + rng.Next(0, 2);
        int desiredGaps = result.macroArchetype == "Split Ground" ? 2 : (result.macroArchetype == "Platform Heavy" ? 1 : rng.Next(0, 3));
        var gapAfter = new HashSet<int>();
        while (gapAfter.Count < desiredGaps && segmentCount > 3) gapAfter.Add(rng.Next(1, segmentCount - 1));
        var gapLengths = new float[segmentCount];
        foreach (int gapIndex in gapAfter.OrderBy(x => x))
            gapLengths[gapIndex] = (float)(1.5 + rng.NextDouble() * 2.0);
        float gapTotal = gapLengths.Sum();
        float sectionTotal = Mathf.Max(40f, roomWidth - gapTotal);
        float average = sectionTotal / segmentCount;
        float semanticCursor = 0f;
        var semanticGrounds = new List<Surface>();
        var semanticBridges = new List<Surface>();
        for (int i = 0; i < segmentCount; i++)
        {
            float gapAfterLength = gapLengths[i];
            float reservedFutureGaps = 0f;
            for (int g = i; g < gapLengths.Length; g++) reservedFutureGaps += gapLengths[g];
            float remaining = roomWidth - semanticCursor;
            float maximumLength = remaining - reservedFutureGaps - 9f * (segmentCount - i - 1);
            float desiredLength = i == segmentCount - 1
                ? remaining
                : Mathf.Clamp(average + (float)(rng.NextDouble() * 2.0 - 1.0), 9f, Mathf.Max(9f, maximumLength));
            float y = GroundLevelFor(result.macroArchetype, i, segmentCount);
            string type = Mathf.Abs(y) > 0.01f ? "Step" : "Ground";
            var groundSurface = new Surface("Ground_" + (i + 1).ToString("00"), semanticCursor, semanticCursor + desiredLength, y, true, type);
            groundSurface.desiredWidth = desiredLength;
            result.surfaces.Add(groundSurface);
            semanticGrounds.Add(groundSurface);
            result.segmentSequence.Add(i == 0 ? "SafeStart" : i == segmentCount - 1 ? "ExitSafe" : type == "Step" ? (y > 0f ? "StepUp" : "StepDown") : "GroundSection");
            semanticCursor += desiredLength;
            if (gapAfter.Contains(i))
            {
                bool bridge = gapAfterLength >= 3.0f && (result.macroArchetype == "Platform Heavy" || rng.NextDouble() < 0.45);
                if (bridge)
                {
                    float bridgeGap = gapAfterLength;
                    int bridgeCount = bridgeGap >= 3.4f ? 2 : 1;
                    float bridgeWidth = bridgeCount == 1 ? Mathf.Min(2.5f, bridgeGap - 0.5f) : Mathf.Min(2f, (bridgeGap - 0.5f) / bridgeCount);
                    float bridgeStart = semanticCursor + 0.25f;
                    for (int b = 0; b < bridgeCount; b++)
                    {
                        float bx = bridgeStart + b * (bridgeGap - bridgeWidth) / Mathf.Max(1, bridgeCount - 1);
                        semanticBridges.Add(new Surface("Bridge_" + i.ToString("00") + "_" + b.ToString("00"), bx, bx + bridgeWidth, y, true, "Bridge"));
                    }
                    result.segmentSequence.Add("PlatformBridge");
                }
                else result.segmentSequence.Add("ShortGap");
                semanticCursor += gapAfterLength;
            }
        }

        ReflowGroundChain(result, semanticGrounds);
        ReflowSemanticBridges(semanticBridges, semanticGrounds);
        result.surfaces.AddRange(semanticBridges);
        ReflowEncounters(result, semanticGrounds);
        foreach (Surface ground in semanticGrounds)
            ground.encounterIndex = NearestEncounter(result.encounters, (ground.minX + ground.maxX) * 0.5f);
        foreach (Surface bridge in semanticBridges)
            bridge.encounterIndex = NearestEncounter(result.encounters, (bridge.minX + bridge.maxX) * 0.5f);

        var roles = new List<Role>();
        for (int i = 0; i < meleeCount; i++) roles.Add(Role.Melee);
        for (int i = 0; i < rangedCount; i++) roles.Add(Role.Ranged);
        for (int i = 0; i < chargeCount; i++) roles.Add(Role.Charge);
        Shuffle(roles, rng);
        int[] meleeByEncounter = new int[encounterCount];
        int[] rangedByEncounter = new int[encounterCount];
        int[] chargeByEncounter = new int[encounterCount];
        for (int i = 0; i < roles.Count; i++)
        {
            int encounterIndex = i % encounterCount;
            if (roles[i] == Role.Melee) meleeByEncounter[encounterIndex]++;
            else if (roles[i] == Role.Ranged) rangedByEncounter[encounterIndex]++;
            else chargeByEncounter[encounterIndex]++;
        }

        // Build encounter geometry from its required composition before creating slots.
        for (int i = 0; i < result.encounters.Count; i++)
        {
            Encounter encounter = result.encounters[i];
            bool needsPerch = rangedByEncounter[i] > 0 || result.macroArchetype == "Platform Heavy" || result.macroArchetype == "Hill";
            if (needsPerch)
            {
                Surface ground = result.surfaces.Where(s => s.critical && s.surfaceType != "Bridge" && s.minX <= (encounter.minX + encounter.maxX) * 0.5f && s.maxX >= (encounter.minX + encounter.maxX) * 0.5f).OrderByDescending(s => s.Width).FirstOrDefault();
                float baseY = ground == null ? 0f : ground.y;
                // Leave a comfortable clearance over the combat ground; this is visual preview geometry only.
                float perchY = baseY + 3.5f;
                bool walkway = result.macroArchetype == "Platform Heavy" || result.macroArchetype == "Hill";
                float perchWidth = walkway ? Mathf.Clamp(8f + rangedByEncounter[i] * 1.5f, 8f, 16f) : Mathf.Max(6f, 4f + rangedByEncounter[i] * 2f);
                float perchCenter = (encounter.minX + encounter.maxX) * 0.5f + ((i % 2 == 0) ? -1.2f : 1.2f);
                float min = Mathf.Max(encounter.minX + 0.5f, perchCenter - perchWidth * 0.5f);
                float max = Mathf.Min(encounter.maxX - 0.5f, perchCenter + perchWidth * 0.5f);
                if (max - min >= (walkway ? 8f : 6f)) result.surfaces.Add(new Surface((walkway ? "UpperWalkway_" : "CombatPlatform_") + (i + 1).ToString("00"), min, max, perchY, false, walkway ? "UpperWalkway" : "CombatPlatform", i));
                if ((result.macroArchetype == "Platform Heavy" || result.macroArchetype == "Split Ground") && i == 1)
                {
                    float routeWidth = Mathf.Min(10f, encounter.maxX - encounter.minX - 1f);
                    float routeMin = encounter.minX + (encounter.maxX - encounter.minX - routeWidth) * 0.5f;
                    if (routeWidth >= 8f) result.surfaces.Add(new Surface("OptionalUpperRoute_" + (i + 1).ToString("00"), routeMin, routeMin + routeWidth, perchY + 1.5f, false, "OptionalRoute", i));
                }
            }
        }

        foreach (Surface surface in result.surfaces.Where(s => !s.critical).ToList())
        {
            Surface parentGround = FindGroundAnchor(semanticGrounds, (surface.minX + surface.maxX) * 0.5f);
            QuantizePlatformAroundAnchor(surface, parentGround, result);
        }

        HeadroomResult headroom = EvaluateHeadroom(result);
        result.headroomErrors = headroom.errors;
        result.headroomWarnings = headroom.warnings;
        result.blockedLowerPaths = headroom.blockedLowerPaths;
        result.combatSurfacesWithSafeHeadroom = headroom.safeCombatSurfaces;
        result.lowestValidHeadroom = headroom.lowestValidHeadroom;
        foreach (var message in headroom.messages)
        {
            if (message.severity == "ERROR") result.generationErrors.Add(message.text);
            else if (message.severity == "WARNING") result.generationWarnings.Add(message.text);
        }

        string[] paths = chapter == 1 ? C1PrefabPaths : C2PrefabPaths;
        int[] encounterRoleIndex = new int[encounterCount];        for (int i = 0; i < roles.Count; i++)
        {
            Role role = roles[i];
            int encounterIndex = i % encounterCount;
            Encounter encounter = result.encounters[encounterIndex];
            int localIndex = encounterRoleIndex[encounterIndex]++;
            Surface surface = role == Role.Ranged ? result.surfaces.Where(s => !s.critical && s.encounterIndex == encounterIndex).OrderByDescending(s => s.Width).FirstOrDefault() : null;
            if (surface == null) surface = result.surfaces.Where(s => s.critical && s.surfaceType != "Bridge" && s.encounterIndex == encounterIndex && s.Width >= 5f).OrderByDescending(s => s.Width).FirstOrDefault();
            if (surface == null) surface = result.surfaces.First(s => s.critical && s.surfaceType != "Bridge");
            int encounterTotal = Mathf.Max(1, CountInEncounter(roles, encounterIndex, encounterCount));
            float usableMin = surface.minX + 0.75f;
            float usableMax = surface.maxX - 0.75f;
            float t = encounterTotal <= 1 ? 0.5f : localIndex / (float)(encounterTotal - 1);
            float x = Mathf.Lerp(usableMin, usableMax, t);
            string prefabPath = paths[(int)role];
            GroundingColliderInfo groundingInfo;
            string groundingError;
            if (!TryGetPrefabGroundingColliderInfo(prefabPath, out groundingInfo, out groundingError))
                result.generationErrors.Add(role + " grounding collider: " + groundingError);
            float halfWidth = groundingInfo == null ? PlayerColliderFallbackWidth * 0.5f : Mathf.Max(0.1f, groundingInfo.collider.bounds.size.x * 0.5f);
            float colliderBottomOffset = groundingInfo == null ? 0.75f : groundingInfo.rootBottomOffset;
            var marker = new Marker { role = role, encounter = encounterIndex, position = new Vector2(x, surface.y + colliderBottomOffset), surfaceY = surface.y, colliderBottomOffset = colliderBottomOffset, prefabPath = prefabPath, groundingColliderPath = groundingInfo == null ? "(unavailable)" : groundingInfo.path, groundingColliderType = groundingInfo == null ? "(unavailable)" : groundingInfo.type, ignoredTriggerCount = groundingInfo == null ? 0 : groundingInfo.ignoredTriggerCount, sprite = LoadRoleSprite(prefabPath), xp = ReadPrefabExp(prefabPath), threat = role == Role.Melee ? 1f : role == Role.Ranged ? 1.3f : 1.65f, halfWidth = halfWidth };
            encounter.markers.Add(marker); result.markers.Add(marker);
        }
        return result;
    }

    private static void ReflowGroundChain(Layout layoutData, List<Surface> grounds)
    {
        if (layoutData == null || grounds == null || grounds.Count == 0) return;
        grounds.Sort((a, b) => a.originalMinX.CompareTo(b.originalMinX));
        layoutData.groundGaps.Clear();

        float cursor = grounds[0].originalMinX;
        Surface previous = null;
        foreach (Surface surface in grounds)
        {
            float originalGap = previous == null ? 0f : surface.originalMinX - previous.originalMaxX;
            if (Mathf.Abs(originalGap) <= 0.001f) originalGap = 0f;
            if (previous != null) cursor += Mathf.Max(0f, originalGap);

            float finalWidth = layoutData.groundTileWidth > 0f
                ? QuantizeWidth(surface.desiredWidth, layoutData.groundTileWidth, 9f, true, layoutData)
                : surface.desiredWidth;
            surface.minX = cursor;
            surface.maxX = cursor + finalWidth;
            surface.tileCount = layoutData.groundTileWidth > 0f ? Mathf.RoundToInt(finalWidth / layoutData.groundTileWidth) : 0;

            if (previous != null)
            {
                layoutData.groundGaps.Add(new GroundGapSemantic
                {
                    previousSurface = previous.name,
                    nextSurface = surface.name,
                    originalGap = Mathf.Max(0f, originalGap),
                    finalGap = Mathf.Max(0f, surface.minX - previous.maxX),
                    intentional = originalGap > 0.001f
                });
            }

            cursor = surface.maxX;
            previous = surface;
        }

        layoutData.width = grounds[grounds.Count - 1].maxX;
    }

    private static void ReflowSemanticBridges(List<Surface> bridges, List<Surface> grounds)
    {
        if (bridges == null || grounds == null || grounds.Count < 2) return;
        foreach (Surface bridge in bridges)
        {
            float originalCenter = (bridge.originalMinX + bridge.originalMaxX) * 0.5f;
            Surface previous = grounds.Where(g => g.originalMaxX <= originalCenter + 0.001f).OrderByDescending(g => g.originalMaxX).FirstOrDefault();
            if (previous == null) continue;
            float offsetInsideGap = bridge.originalMinX - previous.originalMaxX;
            float width = bridge.OriginalWidth;
            bridge.minX = previous.maxX + offsetInsideGap;
            bridge.maxX = bridge.minX + width;
            bridge.desiredWidth = width;
        }
    }

    private static void ReflowEncounters(Layout layoutData, List<Surface> grounds)
    {
        if (layoutData == null || grounds == null || grounds.Count == 0) return;
        foreach (Encounter encounter in layoutData.encounters.OrderBy(e => e.index))
        {
            float originalCenter = (encounter.originalMinX + encounter.originalMaxX) * 0.5f;
            float originalWidth = encounter.originalMaxX - encounter.originalMinX;
            Surface anchor = grounds.FirstOrDefault(g => originalCenter >= g.originalMinX - 0.001f && originalCenter <= g.originalMaxX + 0.001f);
            if (anchor == null)
                anchor = grounds.OrderBy(g => Mathf.Abs((g.originalMinX + g.originalMaxX) * 0.5f - originalCenter)).First();

            float normalized = anchor.OriginalWidth <= 0.001f ? 0.5f : Mathf.InverseLerp(anchor.originalMinX, anchor.originalMaxX, originalCenter);
            float newCenter = Mathf.Lerp(anchor.minX, anchor.maxX, normalized);
            float widthScale = anchor.OriginalWidth <= 0.001f ? 1f : anchor.Width / anchor.OriginalWidth;
            float newWidth = Mathf.Max(1f, originalWidth * widthScale);
            float half = newWidth * 0.5f;
            newCenter = Mathf.Clamp(newCenter, half, Mathf.Max(half, layoutData.width - half));
            encounter.minX = newCenter - half;
            encounter.maxX = newCenter + half;
        }
    }

    private static Surface FindGroundAnchor(List<Surface> grounds, float x)
    {
        if (grounds == null || grounds.Count == 0) return null;
        Surface containing = grounds.FirstOrDefault(g => x >= g.minX - 0.001f && x <= g.maxX + 0.001f);
        return containing ?? grounds.OrderBy(g => Mathf.Abs((g.minX + g.maxX) * 0.5f - x)).First();
    }

    private static void QuantizePlatformAroundAnchor(Surface surface, Surface parentGround, Layout layoutData)
    {
        if (surface == null || layoutData == null) return;
        surface.desiredWidth = surface.OriginalWidth;
        float finalWidth = layoutData.platformTileWidth > 0f
            ? QuantizeWidth(surface.desiredWidth, layoutData.platformTileWidth, 6f, true, layoutData)
            : surface.desiredWidth;
        float center = (surface.originalMinX + surface.originalMaxX) * 0.5f;
        if (parentGround != null)
        {
            float normalized = parentGround.Width <= 0.001f ? 0.5f : Mathf.InverseLerp(parentGround.minX, parentGround.maxX, center);
            center = Mathf.Lerp(parentGround.minX, parentGround.maxX, normalized);
            if (finalWidth <= parentGround.Width + 0.001f)
                center = Mathf.Clamp(center, parentGround.minX + finalWidth * 0.5f, parentGround.maxX - finalWidth * 0.5f);
            else
                center = (parentGround.minX + parentGround.maxX) * 0.5f;
        }
        surface.minX = center - finalWidth * 0.5f;
        surface.maxX = center + finalWidth * 0.5f;
        surface.tileCount = layoutData.platformTileWidth > 0f ? Mathf.RoundToInt(finalWidth / layoutData.platformTileWidth) : 0;
    }

    private static float QuantizeWidth(float desiredWidth, float tileWidth, float minimumWidth, bool ceil, Layout layout)
    {
        if (tileWidth <= 0f) return desiredWidth;
        float units = Mathf.Max(minimumWidth, desiredWidth) / tileWidth;
        float quantized = (ceil ? Mathf.Ceil(units) : Mathf.Round(units)) * tileWidth;
        if (layout != null) layout.maxQuantizationDelta = Mathf.Max(layout.maxQuantizationDelta, Mathf.Abs(quantized - desiredWidth));
        return quantized;
    }
    private static float GroundLevelFor(string archetype, int index, int count)
    {
        // Constrained 1.5-unit grid; adjacent critical surfaces stay within a reachable delta.
        int i = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        int last = Mathf.Max(0, count - 1);
        switch (archetype)
        {
            case "Ascending":
                if (i == 0) return 0f;
                if (i == last) return 3f;
                return i < count * 0.5f ? 1.5f : 3f;
            case "Descending":
                if (i == 0) return 3f;
                if (i == last) return 0f;
                return i < count * 0.5f ? 3f : (i < count * 0.8f ? 1.5f : 0f);
            case "Valley":
                if (i == 0 || i == last) return 0f;
                if (i == count / 2) return -1.5f;
                return (i == count / 2 - 1 || i == count / 2 + 1) ? 0f : 1.5f;
            case "Hill":
                if (i == 0 || i == last) return 0f;
                if (i == count / 2 || i == count / 2 - 1) return 3f;
                return 1.5f;
            case "Alternating Levels":
                if (i == 0 || i == last) return 0f;
                return i % 2 == 1 ? 1.5f : -1.5f;
            case "Split Ground":
                if (i < count / 3) return 0f;
                if (i < count * 0.66f) return 1.5f;
                return i == last ? 0f : -1.5f;
            case "Platform Heavy":
                if (i == 0 || i == last) return 0f;
                return i % 3 == 1 ? 3f : 1.5f;
            default:
                if (i == 0 || i == last) return 0f;
                return i % 3 == 1 ? 1.5f : 0f;
        }
    }

    private static int NearestEncounter(List<Encounter> encounters, float x)
    {
        int best = 0; float distance = float.MaxValue;
        for (int i = 0; i < encounters.Count; i++) { float d = Mathf.Abs((encounters[i].minX + encounters[i].maxX) * 0.5f - x); if (d < distance) { distance = d; best = i; } }
        return best;
    }
    private static int CountInEncounter(List<Role> roles, int encounterIndex, int encounterCount)
    {
        int count = 0;
        for (int i = encounterIndex; i < roles.Count; i += encounterCount) count++;
        return count;
    }

    private static void Shuffle<T>(IList<T> values, System.Random rng)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1); T temp = values[i]; values[i] = values[j]; values[j] = temp;
        }
    }

    private void CreatePreviewObjects(Scene scene, Layout data)
    {
        GameObject root = NewPreviewObject(PreviewRootName, null);
        root.transform.position = previewOrigin;
        Transform geometry = NewPreviewObject("Geometry", root.transform).transform;
        Transform groundSegments = NewPreviewObject("GroundSegments", geometry).transform;
        Transform platforms = NewPreviewObject("Platforms", geometry).transform;
        Transform walls = NewPreviewObject("Walls", geometry).transform;
        Transform zones = NewPreviewObject("EncounterZones", root.transform).transform;
        Transform markerRoot = NewPreviewObject("EnemyMarkers", root.transform).transform;
        Transform meleeRoot = NewPreviewObject("Melee", markerRoot).transform;
        Transform rangedRoot = NewPreviewObject("Ranged", markerRoot).transform;
        Transform chargeRoot = NewPreviewObject("Charge", markerRoot).transform;
        Transform debug = NewPreviewObject("Debug", root.transform).transform;

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Assets/Square.png");
        foreach (var s in data.surfaces)
        {
            bool ground = s.critical;
            Transform parent = ground ? groundSegments : platforms;
            float thickness = ground ? 1f : 0.5f;
            float centerY = s.y - thickness * 0.5f;
            Color color = ground ? new Color(0.25f, 0.45f, 0.75f, 0.22f) : new Color(0.25f, 0.8f, 0.45f, 0.28f);
            CreateGeometry(s.name, parent, square, new Vector2((s.minX + s.maxX) * 0.5f, centerY), new Vector2(s.Width, thickness), color);
            if (s.surfaceType != "Bridge") CreateSurfaceGridLabel(s, parent, ground);
        }        foreach (var e in data.encounters)
            CreateZone(e, zones, square);
        foreach (var marker in data.markers)
        {
            Transform parent = marker.role == Role.Melee ? meleeRoot : marker.role == Role.Ranged ? rangedRoot : chargeRoot;
            GameObject go = NewPreviewObject(marker.role.ToString()[0].ToString(), parent);
            go.transform.localPosition = marker.position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = marker.sprite;
            sr.color = marker.role == Role.Melee ? new Color(1f, 0.35f, 0.35f, 0.9f) : marker.role == Role.Ranged ? new Color(0.35f, 0.8f, 1f, 0.9f) : new Color(1f, 0.65f, 0.25f, 0.9f);
            sr.sortingOrder = 20;
            var label = new GameObject("Label_" + marker.role); label.hideFlags = PreviewHideFlags; label.transform.SetParent(go.transform, false); label.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var tm = label.AddComponent<TextMesh>(); tm.text = marker.role.ToString()[0].ToString(); tm.characterSize = 0.18f; tm.fontSize = 32; tm.anchor = TextAnchor.MiddleCenter; tm.color = Color.white;
        }
        var criticalForMarkers = data.surfaces.Where(s => s.critical && s.surfaceType != "Bridge").OrderBy(s => s.minX).ToList();
        float startMarkerY = criticalForMarkers.Count == 0 ? 1f : criticalForMarkers.First().y + 1f;
        float exitMarkerY = criticalForMarkers.Count == 0 ? 1f : criticalForMarkers.Last().y + 1f;
        CreateMarker("PlayerSpawnMarker", root.transform, "START", new Vector2(1f, startMarkerY), Color.green);
        CreateMarker("ExitMarker", root.transform, "EXIT", new Vector2(data.width - 1f, exitMarkerY), Color.yellow);
        if (showHeightGuides) CreateHeightGuides(debug, data);
        // Walls is intentionally empty in Phase 1; it remains a stable hierarchy slot for later geometry work.
        if (walls == null || groundSegments == null || scene != SceneManager.GetActiveScene()) { }
    }

    private static readonly HideFlags PreviewHideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

    private static GameObject NewPreviewObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name); go.hideFlags = PreviewHideFlags;
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static void CreateGeometry(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = NewPreviewObject(name, parent); go.transform.localPosition = position; go.transform.localScale = size;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingOrder = -10;
        var collider = go.AddComponent<BoxCollider2D>(); collider.size = Vector2.one; collider.isTrigger = false;
    }

    private static void CreateSurfaceGridLabel(Surface surface, Transform parent, bool ground)
    {
        GameObject labelObject = NewPreviewObject("GridLabel_" + surface.name, parent);
        labelObject.transform.localPosition = new Vector3((surface.minX + surface.maxX) * 0.5f, surface.y + (ground ? 0.35f : 0.25f), 0f);
        var label = labelObject.AddComponent<TextMesh>();
        label.text = surface.name + " [" + surface.tileCount + " tiles] E" + (surface.encounterIndex + 1);
        label.characterSize = 0.11f;
        label.fontSize = 24;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = ground ? new Color(0.7f, 0.85f, 1f, 0.9f) : new Color(0.65f, 1f, 0.75f, 0.9f);
    }

    private static void CreateHeightGuides(Transform parent, Layout data)
    {
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Assets/Square.png");
        if (square == null || data == null) return;
        float[] levels = { -3f, -1.5f, 0f, 1.5f, 3f };
        foreach (float level in levels)
        {
            GameObject line = NewPreviewObject("HeightGuide_" + level.ToString("0.##"), parent);
            line.transform.localPosition = new Vector3(data.width * 0.5f, level, 0f);
            line.transform.localScale = new Vector3(data.width, 0.02f, 1f);
            var sr = line.AddComponent<SpriteRenderer>();
            sr.sprite = square; sr.color = new Color(0.3f, 0.8f, 1f, 0.12f); sr.sortingOrder = -30;
            var label = NewPreviewObject("Label", line.transform);
            label.transform.localPosition = new Vector3(-data.width * 0.5f + 0.5f, 0.25f, 0f);
            var tm = label.AddComponent<TextMesh>(); tm.text = "Y " + level.ToString("+0.##;-0.##;0"); tm.characterSize = 0.14f; tm.fontSize = 24; tm.color = new Color(0.3f, 0.8f, 1f, 0.7f);
        }
    }

    private static void CreateZone(Encounter encounter, Transform parent, Sprite sprite)
    {
        GameObject go = NewPreviewObject("Encounter_" + (encounter.index + 1).ToString("00"), parent);
        go.transform.localPosition = new Vector3((encounter.minX + encounter.maxX) * 0.5f, 2.1f, 0f);
        go.transform.localScale = new Vector3(encounter.maxX - encounter.minX, 4.2f, 1f);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = new Color(0.95f, 0.85f, 0.2f, 0.012f); sr.sortingOrder = -5;
        var label = new GameObject("Label"); label.hideFlags = PreviewHideFlags; label.transform.SetParent(go.transform, false); label.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        var tm = label.AddComponent<TextMesh>(); tm.text = "E" + (encounter.index + 1); tm.characterSize = 0.16f; tm.fontSize = 28; tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(1f, 0.9f, 0.35f, 0.9f);
    }

    private static void CreateMarker(string name, Transform parent, string text, Vector2 position, Color color)
    {
        GameObject go = NewPreviewObject(name, parent); go.transform.localPosition = position;
        var label = go.AddComponent<TextMesh>(); label.text = text; label.characterSize = 0.22f; label.fontSize = 40; label.anchor = TextAnchor.MiddleCenter; label.color = color;
    }

    private static int CountCriticalGaps(Layout data)
    {
        if (data == null || data.surfaces == null) return 0;
        var ordered = data.surfaces.Where(s => s.critical && s.surfaceType != "Bridge").OrderBy(s => s.minX).ToList();
        int count = 0;
        for (int i = 1; i < ordered.Count; i++) if (ordered[i].minX - ordered[i - 1].maxX > 0.01f) count++;
        return count;
    }

    private static bool TryGetCommittedSurfaceXBounds(string surfaceName, out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded) return false;
        GameObject committedRoot = FindSceneRoot(active, CommittedRootName);
        if (committedRoot == null) return false;
        BoxCollider2D collider = committedRoot.GetComponentsInChildren<BoxCollider2D>(true)
            .FirstOrDefault(c => c != null && c.enabled && !c.isTrigger && c.gameObject.name == surfaceName);
        if (collider == null) return false;
        minX = collider.bounds.min.x;
        maxX = collider.bounds.max.x;
        return true;
    }
    private bool HasPreviewRoot()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded) return false;
        foreach (var root in active.GetRootGameObjects())
            if (root.name == PreviewRootName) return true;
        return false;
    }
    private void ClearPreview()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded)
        {
            GameObject[] roots = active.GetRootGameObjects();
            foreach (var go in roots)
                if (go.name == PreviewRootName) DestroyImmediate(go);
        }
        layout = null; validation = new List<Message>(); validationCompleted = false; commitAnalysis = null; replaceAnalysis = null; commitReport = string.Empty; report = "Preview cleared. Existing Scene objects were not touched."; Repaint();
    }

    private void ValidateCurrent()
    {
        if (layout == null) { validation = new List<Message> { new Message("INFO", "Generate Preview first.") }; report = "[INFO] 먼저 Generate Preview를 실행하세요."; Repaint(); return; }
        validation = ValidateLayout(layout, true);
        validationCompleted = true;
        commitAnalysis = null;
        replaceAnalysis = null;
        report = validation.Any(x => x.severity == "ERROR") ? "Validation failed." : "Validation completed.";
        Repaint();
    }

    private List<Message> ValidateLayout(Layout data, bool includeInfo)
    {
        var result = new List<Message>();
        if (data == null)
        {
            result.Add(new Message("INFO", "No generated layout to validate."));
            return result;
        }
        if (data.surfaces == null || data.surfaces.Count == 0)
        {
            result.Add(new Message("ERROR", "Critical path missing: no surfaces were generated."));
            return result;
        }
        if (data.encounters == null)
        {
            result.Add(new Message("ERROR", "Encounter data missing."));
            return result;
        }
        if (data.markers == null)
        {
            result.Add(new Message("ERROR", "Enemy marker data missing."));
            return result;
        }
        if (data.generationErrors != null) foreach (var error in data.generationErrors) result.Add(new Message("ERROR", error));
        if (data.generationWarnings != null) foreach (var warning in data.generationWarnings) result.Add(new Message("WARNING", warning));
        if (includeInfo)
        {
            result.Add(new Message("INFO", "Visual Grid Quantization: ON"));
            result.Add(new Message("INFO", "VISUAL GRID SOURCE: WORKING COPY"));
            if (data.groundGridInfo.OriginalSprite != null && data.groundGridInfo.WorkingCopySprite != null)
                result.Add(new Message("INFO", RoomProductionVisualGridUtility.FormatGridReport("Ground", data.groundGridInfo)));
            if (data.platformGridInfo.OriginalSprite != null && data.platformGridInfo.WorkingCopySprite != null)
                result.Add(new Message("INFO", RoomProductionVisualGridUtility.FormatGridReport("Platform", data.platformGridInfo)));

            bool resolvedGround = RoomProductionVisualGridUtility.TryGetVisualGridInfo(chapter, "Ground", out VisualGridSpriteInfo currentGroundGrid, out _);
            bool resolvedPlatform = RoomProductionVisualGridUtility.TryGetVisualGridInfo(chapter, "Platform", out VisualGridSpriteInfo currentPlatformGrid, out _);
            bool gridMatch = resolvedGround && resolvedPlatform &&
                             Mathf.Abs(data.groundTileWidth - currentGroundGrid.WorkingCopyWorldWidth) <= 0.001f &&
                             Mathf.Abs(data.platformTileWidth - currentPlatformGrid.WorkingCopyWorldWidth) <= 0.001f;
            result.Add(new Message(gridMatch ? "INFO" : "ERROR", gridMatch ? "CRG/CRB Grid Match: PASS" : "Visual Grid Source mismatch."));
            result.Add(new Message("INFO", "Visual Grid: Ground Tile Width=" + data.groundTileWidth.ToString("0.###") + ", Platform Tile Width=" + data.platformTileWidth.ToString("0.###") + ", Max Quantization Delta=" + data.maxQuantizationDelta.ToString("0.###")));
            result.Add(new Message("INFO", "Room Width: Requested=" + data.requestedWidth.ToString("0.###") + " Final Reflow=" + data.width.ToString("0.###") + " Delta=" + (data.width - data.requestedWidth).ToString("+0.###;-0.###;0")));
            foreach (var surface in data.surfaces)
            {
                float grid = surface.critical ? data.groundTileWidth : data.platformTileWidth;
                if (grid > 0f)
                {
                    string category = surface.critical && surface.surfaceType != "Bridge" ? "GROUND" : surface.critical ? "BRIDGE" : "PLATFORM";
                    string legacy = string.Empty;
                    if (TryGetCommittedSurfaceXBounds(surface.name, out float committedMin, out float committedMax))
                    {
                        float committedWidth = committedMax - committedMin;
                        float widthOnly = Mathf.Ceil(Mathf.Max(surface.critical ? 9f : 6f, committedWidth) / grid) * grid;
                        legacy = " LegacyCommitted=" + committedMin.ToString("0.###") + ".." + committedMax.ToString("0.###") + " (" + committedWidth.ToString("0.###") + ")" +
                                 " PreviousWidthOnlyProjection=" + committedMin.ToString("0.###") + ".." + (committedMin + widthOnly).ToString("0.###");
                    }
                    result.Add(new Message("INFO", "[" + category + "] " + surface.name + legacy +
                        " Original=" + surface.originalMinX.ToString("0.###") + ".." + surface.originalMaxX.ToString("0.###") + " (" + surface.OriginalWidth.ToString("0.###") + ")" +
                        " ReflowQuantized=" + surface.minX.ToString("0.###") + ".." + surface.maxX.ToString("0.###") + " (" + surface.Width.ToString("0.###") + ")" +
                        " Tiles=" + Mathf.RoundToInt(surface.Width / grid) + " Delta=" + (surface.Width - surface.desiredWidth).ToString("+0.###;-0.###;0")));
                }
            }
            foreach (Encounter encounter in data.encounters.OrderBy(e => e.index))
                result.Add(new Message("INFO", "[ENCOUNTER] E" + (encounter.index + 1) +
                    " Original=" + encounter.originalMinX.ToString("0.###") + ".." + encounter.originalMaxX.ToString("0.###") +
                    " Reflow=" + encounter.minX.ToString("0.###") + ".." + encounter.maxX.ToString("0.###")));
            foreach (GroundGapSemantic gap in data.groundGaps)
                result.Add(new Message("INFO", "[GAP] " + gap.previousSurface + " -> " + gap.nextSurface + " Original=" + gap.originalGap.ToString("0.###") + " Final=" + gap.finalGap.ToString("0.###") + " Semantic=" + (gap.intentional ? "INTENTIONAL" : "CONTINUOUS")));
        }
        if (data.requestedWidth < 60f || data.requestedWidth > 100f) result.Add(new Message("ERROR", "Requested Room Width must remain within 60-100."));
        foreach (var surface in data.surfaces)
        {
            float grid = surface.critical ? data.groundTileWidth : data.platformTileWidth;
            if (grid <= 0f) { result.Add(new Message("ERROR", surface.name + " requires a configured visual tile grid.")); continue; }
            float remainder = Mathf.Abs(surface.Width - Mathf.Round(surface.Width / grid) * grid);
            if (remainder > 0.001f) result.Add(new Message("ERROR", (surface.critical ? "Ground" : "Platform") + " width is not compatible with visual tile grid: " + surface.name));
        }
        var criticalSurfaces = data.surfaces.Where(s => s.critical && s.surfaceType != "Bridge").OrderBy(s => s.minX).ToList();
        foreach (GroundGapSemantic gap in data.groundGaps)
        {
            if (!gap.intentional && gap.finalGap > 0.001f)
                result.Add(new Message("ERROR", "Continuous Ground chain separated after quantization: " + gap.previousSurface + " -> " + gap.nextSurface + "."));
            if (gap.intentional && Mathf.Abs(gap.finalGap - gap.originalGap) > 0.001f)
                result.Add(new Message("ERROR", "Intentional gameplay gap changed during quantization: " + gap.previousSurface + " -> " + gap.nextSurface + "."));
        }
        if (criticalSurfaces.Count > 0)
        {
            if (Mathf.Abs(criticalSurfaces.First().minX) > 0.001f) result.Add(new Message("ERROR", "Start Ground moved away from room origin during reflow."));
            if (Mathf.Abs(criticalSurfaces.Last().maxX - data.width) > 0.001f) result.Add(new Message("ERROR", "Exit Ground does not terminate at final reflow room width."));
        }
        for (int i = 0; i < data.encounters.Count; i++)
        {
            Encounter encounter = data.encounters[i];
            float center = (encounter.minX + encounter.maxX) * 0.5f;
            if (encounter.minX < -0.001f || encounter.maxX > data.width + 0.001f)
                result.Add(new Message("ERROR", "Encounter E" + (encounter.index + 1) + " left final room bounds after reflow."));
            if (i > 0 && center <= (data.encounters[i - 1].minX + data.encounters[i - 1].maxX) * 0.5f)
                result.Add(new Message("ERROR", "Encounter order is invalid after reflow: E" + i + " !< E" + (i + 1) + "."));
        }
        foreach (Surface platform in data.surfaces.Where(s => !s.critical && s.encounterIndex >= 0))
        {
            Encounter encounter = data.encounters.FirstOrDefault(e => e.index == platform.encounterIndex);
            float center = (platform.minX + platform.maxX) * 0.5f;
            if (encounter == null || center < encounter.minX - 0.001f || center > encounter.maxX + 0.001f)
                result.Add(new Message("ERROR", platform.name + " center left its parent Encounter section after reflow."));
        }
        float criticalSpan = criticalSurfaces.Count == 0 ? 0f : criticalSurfaces.Max(s => s.y) - criticalSurfaces.Min(s => s.y);
        float totalSpan = data.surfaces.Count == 0 ? 0f : data.surfaces.Max(s => s.y) - data.surfaces.Min(s => s.y);
        float cameraHeight = ReadCameraWorldHeight();
        if (data.width >= 70f && criticalSpan < 1.5f) result.Add(new Message("WARNING", "Layout has insufficient vertical variation."));
        if (!string.Equals(data.macroArchetype, "Flat With Variations", StringComparison.Ordinal) && criticalSpan < 3f) result.Add(new Message("WARNING", "Macro archetype does not express enough vertical structure."));
        if (totalSpan > cameraHeight * 0.9f) result.Add(new Message("WARNING", "Vertical layout may exceed camera framing."));
        if (!string.Equals(data.macroArchetype, "Flat With Variations", StringComparison.Ordinal) && criticalSurfaces.Any(s => s.Width >= data.width * 0.9f)) result.Add(new Message("WARNING", "Layout lacks structural variation."));
        ValidateArchetypeIdentity(data, criticalSurfaces, result);
        if (data.markers.Count != meleeCount + rangedCount + chargeCount) result.Add(new Message("ERROR", "Enemy total does not match composition."));
        if (data.markers.Count(x => x.role == Role.Melee) != meleeCount) result.Add(new Message("ERROR", "Melee count mismatch."));
        if (data.markers.Count(x => x.role == Role.Ranged) != rangedCount) result.Add(new Message("ERROR", "Ranged count mismatch."));
        if (data.markers.Count(x => x.role == Role.Charge) != chargeCount) result.Add(new Message("ERROR", "Charge count mismatch."));

        float playerWidth = ReadPlayerColliderWidth();
        var critical = data.surfaces.Where(x => x.critical).OrderBy(x => x.minX).ToList();
        for (int i = 1; i < critical.Count; i++)
        {
            Surface a = critical[i - 1], b = critical[i];
            float vertical = Mathf.Abs(b.y - a.y);
            float gap = Mathf.Max(0f, Mathf.Max(a.minX - b.maxX, b.minX - a.maxX));
            if (vertical >= 4f || gap >= 5f) result.Add(new Message("ERROR", "Critical Path surface exceeds Player reach (vertical=" + vertical.ToString("0.##") + ", gap=" + gap.ToString("0.##") + ")."));
            else if (vertical > 3f || gap > 4f) result.Add(new Message("WARNING", "Critical Path segment depends heavily on double jump."));
            if (b.Width < playerWidth + 0.5f) result.Add(new Message("ERROR", b.name + " landing width is too narrow."));
        }
        foreach (var m in data.markers)
        {
            if (m.position.x < startSafeZone) result.Add(new Message("ERROR", "Enemy violates Start Safe Zone."));
            if (data.width - m.position.x < exitSafeZone) result.Add(new Message("ERROR", "Enemy violates Exit Safe Zone."));
            if (m.role == Role.Melee && !data.surfaces.Any(s => s.critical && s.surfaceType != "Bridge" && s.minX <= m.position.x && s.maxX >= m.position.x && s.Width >= 5f)) result.Add(new Message("ERROR", "Melee is not on a broad ground surface."));
            if (m.role == Role.Ranged)
            {
                Surface perch = data.surfaces.FirstOrDefault(s => s.y > 1f && s.minX <= m.position.x && s.maxX >= m.position.x);
                if (perch == null || perch.Width < 6f) result.Add(new Message("ERROR", "Ranged marker lacks a usable perch."));
            }
            if (m.role == Role.Charge)
            {
                float minimum = chapter == 1 ? 6.5f : 7.5f;
                if (!data.surfaces.Any(s => s.critical && s.surfaceType != "Bridge" && s.Width >= minimum && s.minX <= m.position.x && s.maxX >= m.position.x)) result.Add(new Message("ERROR", "Charge has insufficient runway."));
            }
            Surface markerSupport = FindSupportSurface(data, m);
            float markerColliderBottom = m.position.y - m.colliderBottomOffset;
            if (data.surfaces.Any(s => !ReferenceEquals(s, markerSupport) && s.minX <= m.position.x && s.maxX >= m.position.x && markerColliderBottom > s.y - 0.5f && markerColliderBottom <= s.y + 0.05f))
                result.Add(new Message("ERROR", "Enemy marker intersects a geometry collider."));
            if (showEnemyColliderDebug)
            {
                Surface support = markerSupport;
                if (support != null)
                {
                    float colliderBottom = m.position.y - m.colliderBottomOffset;
                    float mismatch = Mathf.Abs(colliderBottom - support.y);
                    result.Add(new Message("INFO", m.role + " Prefab: " + m.prefabPath + " | Grounding Collider: " + m.groundingColliderType + " (" + m.groundingColliderPath + ") | Surface Top: " + support.y.ToString("0.###") + " | Collider Bottom: " + colliderBottom.ToString("0.###") + " | Delta: " + (support.y - colliderBottom).ToString("0.###") + " | Error: " + mismatch.ToString("0.####") + " | Ignored Trigger Colliders: " + m.ignoredTriggerCount));
                    if (mismatch > 0.001f) result.Add(new Message("WARNING", "Enemy collider bottom mismatch with surface top (" + m.role + ", error=" + mismatch.ToString("0.####") + ")."));
                }
            }
        }
        var ordered = data.markers.OrderBy(x => x.position.x).ToList();
        for (int i = 1; i < ordered.Count; i++)
        {
            float requiredCenterDistance = ordered[i].halfWidth + ordered[i - 1].halfWidth + minimumEnemyGap;
            if (ordered[i].position.x - ordered[i - 1].position.x < requiredCenterDistance) result.Add(new Message("WARNING", "Enemy markers are closer than collider width + minimum gap."));
        }
        float cameraWidth = ReadCameraWorldWidth();
        for (int i = 0, j = 0; i < ordered.Count; i++)
        {
            while (ordered[i].position.x - ordered[j].position.x > cameraWidth) j++;
            if (i - j + 1 > 6) { result.Add(new Message("WARNING", "More than 6 enemies occupy one camera-width window.")); break; }
        }
        for (int i = 0; i < data.surfaces.Count; i++) for (int j = i + 1; j < data.surfaces.Count; j++)
            if (data.surfaces[i].y > 0f && data.surfaces[j].y > 0f && data.surfaces[i].minX < data.surfaces[j].maxX && data.surfaces[j].minX < data.surfaces[i].maxX && Mathf.Abs(data.surfaces[i].y - data.surfaces[j].y) < 0.5f)
                result.Add(new Message("ERROR", "Platform surfaces overlap."));
        if (includeInfo)
        {
            result.Insert(0, new Message("INFO", "Player basis: MoveSpeed=" + ReadPlayerMoveSpeed().ToString("0.##") + ", JumpForce=" + ReadPlayerJumpForce().ToString("0.##") + ", DoubleJump=" + ReadPlayerJumpCount() + "."));
            if (!result.Any(m => m.severity == "ERROR" && m.text.IndexOf("Encounter", StringComparison.OrdinalIgnoreCase) >= 0)) result.Add(new Message("INFO", "Encounter Order / Section Reflow: PASS"));
            if (!result.Any(m => m.severity == "ERROR" && m.text.IndexOf("gap", StringComparison.OrdinalIgnoreCase) >= 0)) result.Add(new Message("INFO", "Ground Chain / Intentional Gap Semantics: PASS"));
            if (!result.Any(m => m.severity == "ERROR" && m.text.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0)) result.Add(new Message("INFO", "Enemy Relative Placement / Grounding: PASS"));
            result.Add(new Message("INFO", "Camera width used for density validation: " + cameraWidth.ToString("0.##") + "."));
            result.Add(new Message("INFO", "XP/Threat report uses current EnemyHealth.expReward values; no EXP asset is modified."));
        }
        return result;
    }

    private HeadroomResult EvaluateHeadroom(Layout data)
    {
        var output = new HeadroomResult();
        if (data == null || data.surfaces == null) return output;
        var traversable = data.surfaces.Where(s => s.critical || s.encounterIndex >= 0).ToList();
        foreach (var lower in traversable)
        {
            bool safe = true;
            foreach (var upper in data.surfaces)
            {
                if (ReferenceEquals(lower, upper) || upper.y <= lower.y) continue;
                float overlap = Mathf.Min(lower.maxX, upper.maxX) - Mathf.Max(lower.minX, upper.minX);
                if (overlap < meaningfulXOverlap) continue;
                float clearance = upper.y - lower.y;
                if (clearance >= minimumHeadroom) output.lowestValidHeadroom = Mathf.Min(output.lowestValidHeadroom, clearance);
                if (clearance < minimumHeadroom)
                {
                    safe = false;
                    output.errors++;
                    output.blockedLowerPaths++;
                    bool routeBlocks = (upper.surfaceType == "UpperWalkway" || upper.surfaceType == "OptionalRoute" || upper.surfaceType == "CombatPlatform") && lower.critical;
                    bool startBlocked = lower.minX <= startSafeZone && upper.minX <= startSafeZone;
                    bool exitBlocked = lower.maxX >= data.width - exitSafeZone && upper.maxX >= data.width - exitSafeZone;
                    string text;
                    if (startBlocked) text = "Start path has insufficient headroom.";
                    else if (exitBlocked) text = "Exit path has insufficient headroom.";
                    else if (routeBlocks) text = "Upper route blocks lower traversal.";
                    else if (lower.encounterIndex >= 0) text = "Encounter surface lacks headroom.";
                    else text = "Low ceiling above traversable surface.";
                    output.messages.Add(new Message("ERROR", text));
                    continue;
                }
                if (clearance < minimumHeadroom + 0.5f)
                {
                    output.warnings++;
                    if (upper.surfaceType == "UpperWalkway" || upper.surfaceType == "OptionalRoute" || upper.surfaceType == "CombatPlatform")
                        output.messages.Add(new Message("WARNING", "Upper route overlaps lower path but remains barely valid."));
                    else
                        output.messages.Add(new Message("WARNING", "Vertical clearance is near minimum threshold."));
                }
            }
            if (safe) output.safeCombatSurfaces++;
        }
        return output;
    }

    private static void ValidateArchetypeIdentity(Layout data, List<Surface> critical, List<Message> result)
    {
        if (critical == null || critical.Count < 3 || string.Equals(data.macroArchetype, "Flat With Variations", StringComparison.Ordinal)) return;
        float start = critical.First().y;
        float end = critical.Last().y;
        bool weak = false;
        if (data.macroArchetype == "Ascending") weak = end <= start;
        else if (data.macroArchetype == "Descending") weak = end >= start;
        else if (data.macroArchetype == "Valley") weak = critical.Skip(1).Take(critical.Count - 2).Min(s => s.y) >= Mathf.Min(start, end);
        else if (data.macroArchetype == "Hill") weak = critical.Skip(1).Take(critical.Count - 2).Max(s => s.y) <= Mathf.Max(start, end);
        else if (data.macroArchetype == "Alternating Levels")
        {
            int changes = 0;
            float previousDelta = 0f;
            for (int i = 1; i < critical.Count; i++)
            {
                float delta = Mathf.Sign(critical[i].y - critical[i - 1].y);
                if (Mathf.Abs(delta) > 0f && Mathf.Abs(previousDelta) > 0f && delta != previousDelta) changes++;
                if (Mathf.Abs(delta) > 0f) previousDelta = delta;
            }
            weak = changes < 2;
        }
        if (weak) result.Add(new Message("WARNING", "Macro archetype identity is weak."));
    }

    private static GameObject FindPrefab(string contains)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in guids.OrderBy(x => x, StringComparer.Ordinal))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0 && path.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) < 0)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (go != null && go.GetComponentInChildren<PlayerController>(true) != null) return go;
            }
        }
        return null;
    }

    private static PlayerController FindLoadedPlayerController()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
            foreach (var root in scene.GetRootGameObjects())
            {
                var player = root.GetComponentInChildren<PlayerController>(true);
                if (player != null) return player;
            }
        GameObject prefab = FindPrefab("Player");
        return prefab == null ? null : prefab.GetComponentInChildren<PlayerController>(true);
    }

    private static float ReadPlayerMoveSpeed()
    {
        var pc = FindLoadedPlayerController(); if (pc == null) return 7f;
        var prop = new SerializedObject(pc).FindProperty("moveSpeed"); return prop == null ? 7f : prop.floatValue;
    }
    private static float ReadPlayerJumpForce()
    {
        var pc = FindLoadedPlayerController(); if (pc == null) return 7f;
        var prop = new SerializedObject(pc).FindProperty("jumpForce"); return prop == null ? 7f : prop.floatValue;
    }
    private static int ReadPlayerJumpCount()
    {
        var pc = FindLoadedPlayerController(); if (pc == null) return 2;
        var prop = new SerializedObject(pc).FindProperty("maxJumpCount"); return prop == null ? 2 : prop.intValue;
    }
    private static float ReadPlayerColliderWidth()
    {
        var pc = FindLoadedPlayerController();
        var col = pc == null ? null : pc.GetComponentInChildren<Collider2D>(true);
        return col == null ? PlayerColliderFallbackWidth : col.bounds.size.x;
    }
    private static string ReadCameraYFollowStatus()
    {
        CameraFollow follow = UnityEngine.Object.FindObjectsByType<CameraFollow>(FindObjectsSortMode.None).FirstOrDefault();
        if (follow == null) return "NOT FOUND";
        return follow.target == null ? "PRESENT (target missing)" : "FOLLOWING X/Y (no clamp)";
    }

    private static float ReadCameraWorldHeight()
    {
        Camera camera = Camera.main;
        if (camera == null) camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault();
        return camera == null || !camera.orthographic ? DefaultCameraHeight : camera.orthographicSize * 2f;
    }

    private static string VerticalCompatibility(Layout data, float cameraHeight)
    {
        if (data == null || data.surfaces == null || data.surfaces.Count == 0) return "UNKNOWN";
        float span = data.surfaces.Max(s => s.y) - data.surfaces.Min(s => s.y);
        return span <= cameraHeight * 0.9f ? "OK" : "WARNING: Generated vertical span may exceed current camera framing.";
    }

    private static float ReadCameraWorldWidth()
    {
        Camera camera = Camera.main;
        if (camera == null) camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).FirstOrDefault();
        if (camera == null || !camera.orthographic) return DefaultCameraWidth;
        float aspect = camera.aspect > 0.01f ? camera.aspect : 16f / 9f;
        return camera.orthographicSize * 2f * aspect;
    }

    private static string GetTransformPath(Transform root, Transform target)
    {
        if (target == null) return "(none)";
        List<string> names = new List<string>();
        for (Transform current = target; current != null; current = current.parent)
        {
            names.Add(current.name);
            if (current == root) break;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static bool TryGetGroundingColliderInfo(GameObject root, out GroundingColliderInfo info, out string error)
    {
        info = null;
        error = string.Empty;
        if (root == null) { error = "Prefab root is missing."; return false; }
        Collider2D[] all = root.GetComponentsInChildren<Collider2D>(true);
        List<Collider2D> candidates = all.Where(c => c != null && c.enabled && !c.isTrigger).ToList();
        int ignoredTriggers = all.Count(c => c != null && c.enabled && c.isTrigger);
        if (candidates.Count == 0) { error = "No enabled non-trigger Collider2D found."; return false; }
        Collider2D selected = null;
        List<Collider2D> rootCandidates = candidates.Where(c => c.gameObject == root).ToList();
        if (rootCandidates.Count == 1) selected = rootCandidates[0];
        else if (rootCandidates.Count > 1) { error = "[GROUNDING COLLIDER AMBIGUOUS] Multiple root non-trigger colliders."; return false; }
        else
        {
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            List<Collider2D> bodyCandidates = body == null ? new List<Collider2D>() : candidates.Where(c => c.attachedRigidbody == body).ToList();
            if (bodyCandidates.Count == 1) selected = bodyCandidates[0];
            else if (bodyCandidates.Count > 1) { error = "[GROUNDING COLLIDER AMBIGUOUS] Multiple non-trigger colliders share the root Rigidbody2D."; return false; }
            else if (candidates.Count == 1) selected = candidates[0];
            else { error = "[GROUNDING COLLIDER AMBIGUOUS] Multiple non-trigger colliders have no unique body."; return false; }
        }
        info = new GroundingColliderInfo();
        info.collider = selected;
        info.type = selected.GetType().Name;
        info.path = GetTransformPath(root.transform, selected.transform);
        BoxCollider2D box = selected as BoxCollider2D;
        info.offset = box == null ? Vector2.zero : box.offset;
        info.size = box == null ? selected.bounds.size : box.size;
        info.rootBottomOffset = -(selected.bounds.min.y - root.transform.position.y);
        info.ignoredTriggerCount = ignoredTriggers;
        return true;
    }

    private static bool TryGetPrefabGroundingColliderInfo(string prefabPath, out GroundingColliderInfo info, out string error)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return TryGetGroundingColliderInfo(prefab, out info, out error);
    }

    private static bool TryGetGameplayCollisionBounds(GameObject root, out Bounds combined)
    {
        combined = new Bounds();
        if (root == null) return false;
        bool hasBounds = false;
        foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider == null || !collider.enabled) continue;
            if (!hasBounds) { combined = collider.bounds; hasBounds = true; }
            else combined.Encapsulate(collider.bounds);
        }
        return hasBounds;
    }
    private static Sprite LoadRoleSprite(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return null;
        var renderer = prefab.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(x => x.sprite != null);
        return renderer == null ? null : renderer.sprite;
    }

    private static int ReadPrefabExp(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return 0;
        EnemyHealth health = prefab.GetComponentInChildren<EnemyHealth>(true);
        if (health == null) return 0;
        var prop = new SerializedObject(health).FindProperty("expReward"); return prop == null ? 0 : prop.intValue;
    }
    private const string CommittedRootName = "__CRG_COMMITTED__Gameplay";
    private const string ReplacementBuildRootName = "__CRG_BUILD__Gameplay";
    private const string CastleCommittedRootName = "__CRB_COMMITTED__Environment Visual";
    private const string BackupFolderPath = "Assets/Generated/CombatRoomBackups";

    private void AnalyzeCommit()
    {
        commitAnalysis = new CommitAnalysis();
        commitReport = string.Empty;
        Scene active = SceneManager.GetActiveScene();
        commitAnalysis.scenePath = active.IsValid() ? active.path : string.Empty;
        commitAnalysis.currentScene = active.IsValid() ? active.name : "(none)";
        commitAnalysis.expectedScene = "Room" + chapter + "-" + roomIndex;
        var blockers = new List<string>();

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            blockers.Add("Play Mode is active.");
        if (!active.IsValid() || !active.isLoaded)
            blockers.Add("Target Room Scene is not loaded.");
        else if (active.name != commitAnalysis.expectedScene)
            blockers.Add("Selected room does not match open scene. Expected: " + commitAnalysis.expectedScene + ", Current: " + active.name + ".");
        else
        {
            string expectedPath = "Assets/Scenes/" + commitAnalysis.expectedScene + ".unity";
            if (active.path != expectedPath || !File.Exists(AssetPathToAbsolute(expectedPath))) blockers.Add("Selected room Scene path is not the expected project asset: " + expectedPath + ".");
        }
        if (active.IsValid() && string.IsNullOrEmpty(active.path))
            blockers.Add("Current Scene is not a saved Scene Asset.");
        if (active.IsValid() && active.isDirty)
            blockers.Add("Save or revert your current Scene changes before committing.");
        if (layout == null)
            blockers.Add("Generate Preview first.");
        if (!validationCompleted)
            blockers.Add("Run Validate before Analyze Commit.");
        if (validation != null && validation.Any(x => x.severity == "ERROR"))
            blockers.Add("Validation contains one or more errors.");
        if (FindSceneRoot(active, CommittedRootName) != null)
            blockers.Add("A previous CRG commit already exists.");

        PlayerController player = FindSceneComponent<PlayerController>(active);
        ExitDoor exit = FindSceneComponent<ExitDoor>(active);
        RoomController controller = FindSceneComponent<RoomController>(active);
        commitAnalysis.player = player == null ? null : player.transform;
        commitAnalysis.exit = exit == null ? null : exit.transform;
        commitAnalysis.roomController = controller == null ? null : controller.transform;
        if (commitAnalysis.player == null) blockers.Add("Player not found.");
        if (commitAnalysis.exit == null) blockers.Add("ExitDoor not found.");
        if (controller == null) blockers.Add("RoomController not found.");

        if (controller != null)
        {
            commitAnalysis.enemyRoot = ReadEnemyRoot(controller);
            if (commitAnalysis.enemyRoot == null) blockers.Add("RoomController enemyRoot is missing.");
        }

        commitAnalysis.groundReference = FindGroundReference(active);
        commitAnalysis.platformReference = FindPlatformReference(active, commitAnalysis.groundReference);
        if (commitAnalysis.groundReference == null) blockers.Add("Unable to safely identify prototype geometry.");
        commitAnalysis.prototypeGeometry.AddRange(CollectPrototypeGeometry(active));
        if (commitAnalysis.prototypeGeometry.Count == 0) blockers.Add("No removable prototype geometry was safely identified.");
        if (commitAnalysis.enemyRoot != null)
            commitAnalysis.prototypeEnemies.AddRange(CollectPrototypeEnemies(commitAnalysis.enemyRoot));
        if (commitAnalysis.prototypeEnemies.Count == 0) blockers.Add("No prototype enemies were found under RoomController.enemyRoot.");

        string[] prefabPaths = chapter == 1 ? C1PrefabPaths : C2PrefabPaths;
        foreach (string path in prefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) blockers.Add("Missing enemy prefab: " + path);
            else
            {
                GroundingColliderInfo groundingInfo;
                string groundingError;
                if (!TryGetPrefabGroundingColliderInfo(path, out groundingInfo, out groundingError)) blockers.Add(groundingError);
            }
        }

        if (active.IsValid() && !string.IsNullOrEmpty(active.path))
            commitAnalysis.backupPath = GetNextBackupAssetPath(active.path);
        if (string.IsNullOrEmpty(commitAnalysis.backupPath))
            blockers.Add("Scene backup path is unavailable.");

        commitAnalysis.warnings.AddRange((validation ?? new List<Message>()).Where(x => x.severity == "WARNING").Select(x => x.text));
        commitAnalysis.ready = blockers.Count == 0;
        commitAnalysis.reason = blockers.Count == 0 ? "READY" : string.Join(" ", blockers);
        commitReport = BuildCommitAnalysisReport(commitAnalysis, blockers);
        Repaint();
    }

    private string BuildCommitAnalysisReport(CommitAnalysis analysis, List<string> blockers)
    {
        int errors = validation == null ? 0 : validation.Count(x => x.severity == "ERROR");
        int warnings = validation == null ? 0 : validation.Count(x => x.severity == "WARNING");
        string nl = Environment.NewLine;
        string text = "ANALYZE COMMIT" + nl;
        text += "Open Scene: " + analysis.currentScene + nl;
        text += "Expected Scene: " + analysis.expectedScene + nl;
        text += "Layout: " + (layout == null ? "MISSING" : "READY") + nl;
        text += "Validation Errors: " + errors + nl;
        text += "Warnings: " + warnings + nl;
        text += "Player: " + (analysis.player == null ? "MISSING" : "FOUND") + nl;
        text += "Exit: " + (analysis.exit == null ? "MISSING" : "FOUND") + nl;
        text += "RoomController: " + (analysis.roomController == null ? "MISSING" : "FOUND") + nl;
        text += "Prototype Geometry: " + analysis.prototypeGeometry.Count + nl;
        text += "Prototype Enemies: " + analysis.prototypeEnemies.Count + nl;
        text += "C" + chapter + " Enemy Prefabs: FOUND" + nl;
        text += "Backup Path: " + (string.IsNullOrEmpty(analysis.backupPath) ? "UNAVAILABLE" : analysis.backupPath) + nl;
        text += "Commit Status: " + (analysis.ready ? "READY" : "BLOCKED") + nl;
        if (blockers.Count > 0) text += "Reason: " + string.Join(" ", blockers);
        return text;
    }

    private void AnalyzeReplaceExistingCommit()
    {
        replaceAnalysis = new ReplaceAnalysis();
        commitAnalysis = null;
        commitReport = string.Empty;

        Scene active = SceneManager.GetActiveScene();
        replaceAnalysis.scene = active;
        string expectedScene = "Room" + chapter + "-" + roomIndex;
        string expectedPath = "Assets/Scenes/" + expectedScene + ".unity";
        List<string> blockers = replaceAnalysis.blockers;

        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            blockers.Add("Play Mode is active.");
        if (!active.IsValid() || !active.isLoaded)
            blockers.Add("Target Room Scene is not loaded.");
        else
        {
            if (!string.Equals(active.name, expectedScene, StringComparison.Ordinal))
                blockers.Add("Selected room does not match open scene. Expected: " + expectedScene + ", Current: " + active.name + ".");
            if (!string.Equals(active.path, expectedPath, StringComparison.Ordinal) || !File.Exists(AssetPathToAbsolute(expectedPath)))
                blockers.Add("Selected room Scene path is not the expected project asset: " + expectedPath + ".");
            if (string.IsNullOrEmpty(active.path))
                blockers.Add("Current Scene is not a saved Scene Asset.");
            if (active.isDirty)
                blockers.Add("Save or revert your current Scene changes before replacing gameplay.");
        }

        if (layout == null)
            blockers.Add("Generate Preview from Combat Room Generator first.");
        GameObject previewRoot = active.IsValid() && active.isLoaded ? FindSceneRoot(active, PreviewRootName) : null;
        if (previewRoot == null)
            blockers.Add("Combat Room Generator Preview is missing.");
        if (!validationCompleted)
            blockers.Add("Run Validate before Analyze Replace Existing Commit.");
        int validationErrors = validation == null ? 0 : validation.Count(x => x.severity == "ERROR");
        if (validationErrors > 0)
            blockers.Add("Preview Validation contains one or more errors.");

        GameObject existingRoot = active.IsValid() && active.isLoaded ? FindSceneRoot(active, CommittedRootName) : null;
        replaceAnalysis.existingRoot = existingRoot;
        if (existingRoot == null)
            blockers.Add("Existing " + CommittedRootName + " was not found.");
        if (active.IsValid() && active.isLoaded && FindSceneRoot(active, CastleCommittedRootName) != null)
            blockers.Add("Existing Castle Visual must be removed/restored before replacing gameplay geometry.");
        if (active.IsValid() && active.isLoaded && FindSceneRoot(active, ReplacementBuildRootName) != null)
            blockers.Add("A stale replacement BUILD root already exists. Undo or reload the Scene before retrying.");

        CommitAnalysis inputs = new CommitAnalysis
        {
            scenePath = active.IsValid() ? active.path : string.Empty,
            currentScene = active.IsValid() ? active.name : "(none)",
            expectedScene = expectedScene
        };
        replaceAnalysis.buildInputs = inputs;

        PlayerController player = active.IsValid() && active.isLoaded ? FindSceneComponent<PlayerController>(active) : null;
        ExitDoor exit = active.IsValid() && active.isLoaded ? FindSceneComponent<ExitDoor>(active) : null;
        RoomController controller = active.IsValid() && active.isLoaded ? FindSceneComponent<RoomController>(active) : null;
        inputs.player = player == null ? null : player.transform;
        inputs.exit = exit == null ? null : exit.transform;
        inputs.roomController = controller == null ? null : controller.transform;
        inputs.enemyRoot = controller == null ? null : ReadEnemyRoot(controller);
        if (inputs.player == null) blockers.Add("Player not found.");
        if (inputs.exit == null) blockers.Add("ExitDoor not found.");
        if (inputs.roomController == null) blockers.Add("RoomController not found.");
        if (inputs.enemyRoot == null) blockers.Add("RoomController enemyRoot is missing.");

        Transform existingGround = existingRoot == null ? null : existingRoot.transform.Find("Geometry/GroundSegments");
        Transform existingPlatforms = existingRoot == null ? null : existingRoot.transform.Find("Geometry/Platforms");
        Transform existingEnemies = existingRoot == null ? null : existingRoot.transform.Find("Enemies");
        if (existingRoot != null)
        {
            if (inputs.player != null && inputs.player.IsChildOf(existingRoot.transform)) blockers.Add("Player must not be a child of the existing committed root.");
            if (inputs.exit != null && inputs.exit.IsChildOf(existingRoot.transform)) blockers.Add("ExitDoor must not be a child of the existing committed root.");
            if (inputs.roomController != null && inputs.roomController.IsChildOf(existingRoot.transform)) blockers.Add("RoomController must not be a child of the existing committed root.");
        }
        if (existingEnemies != null && inputs.enemyRoot != null && inputs.enemyRoot != existingEnemies)
            blockers.Add("RoomController enemyRoot does not reference the existing committed Enemies root.");
        inputs.groundReference = FindCommittedGeometryReference(existingGround);
        inputs.platformReference = FindCommittedGeometryReference(existingPlatforms) ?? inputs.groundReference;
        if (existingGround == null || inputs.groundReference == null)
            blockers.Add("Existing committed GroundSegments cannot provide a safe geometry reference.");
        if (existingPlatforms == null)
            blockers.Add("Existing committed Platforms hierarchy is missing.");
        if (existingEnemies == null)
            blockers.Add("Existing committed Enemies hierarchy is missing.");

        string[] prefabPaths = chapter == 1 ? C1PrefabPaths : C2PrefabPaths;
        foreach (string path in prefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                blockers.Add("Missing enemy prefab: " + path);
                continue;
            }
            GroundingColliderInfo groundingInfo;
            string groundingError;
            if (!TryGetPrefabGroundingColliderInfo(path, out groundingInfo, out groundingError))
                blockers.Add(path + ": " + groundingError);
        }

        if (active.IsValid() && !string.IsNullOrEmpty(active.path))
            replaceAnalysis.backupPath = GetNextReplaceBackupAssetPath(active.path);
        inputs.backupPath = replaceAnalysis.backupPath;
        if (string.IsNullOrEmpty(replaceAnalysis.backupPath))
            blockers.Add("Scene replacement backup path is unavailable.");

        replaceAnalysis.ready = blockers.Count == 0;
        replaceAnalysis.reason = replaceAnalysis.ready ? "READY" : string.Join(" ", blockers);
        commitReport = BuildReplaceAnalysisReport(replaceAnalysis, previewRoot, validationErrors);
        Repaint();
    }

    private string BuildReplaceAnalysisReport(ReplaceAnalysis analysis, GameObject previewRoot, int validationErrors)
    {
        CommitAnalysis inputs = analysis.buildInputs;
        string nl = Environment.NewLine;
        string text = "ANALYZE REPLACE EXISTING COMMIT" + nl;
        text += "Open Scene: " + (inputs == null ? "(none)" : inputs.currentScene) + nl;
        text += "Expected Scene: " + (inputs == null ? "(none)" : inputs.expectedScene) + nl;
        text += "LayoutData: " + (layout == null ? "MISSING" : "READY (owned by this CRG Window)") + nl;
        text += "CRG Preview: " + (previewRoot == null ? "MISSING" : "FOUND") + nl;
        text += "Validation Completed: " + (validationCompleted ? "YES" : "NO") + nl;
        text += "Validation Errors: " + validationErrors + nl;
        text += "Existing Gameplay Commit: " + (analysis.existingRoot == null ? "MISSING" : "FOUND") + nl;
        text += "Existing Castle Visual: " + (analysis.scene.IsValid() && analysis.scene.isLoaded && FindSceneRoot(analysis.scene, CastleCommittedRootName) != null ? "BLOCKING" : "NONE") + nl;
        text += "Prototype Geometry: NOT USED BY REPLACE" + nl;
        text += "Player: " + (inputs == null || inputs.player == null ? "MISSING" : "FOUND") + nl;
        text += "ExitDoor: " + (inputs == null || inputs.exit == null ? "MISSING" : "FOUND") + nl;
        text += "RoomController: " + (inputs == null || inputs.roomController == null ? "MISSING" : "FOUND") + nl;
        text += "Enemy Prefabs: " + (analysis.blockers.Any(x => x.IndexOf("enemy prefab", StringComparison.OrdinalIgnoreCase) >= 0 || x.IndexOf("GROUNDING", StringComparison.OrdinalIgnoreCase) >= 0) ? "BLOCKED" : "FOUND") + nl;
        text += "Backup Path: " + (string.IsNullOrEmpty(analysis.backupPath) ? "UNAVAILABLE" : analysis.backupPath) + nl;
        text += "Replace Status: " + (analysis.ready ? "READY" : "BLOCKED") + nl;
        if (analysis.blockers.Count > 0) text += "Reason: " + string.Join(" ", analysis.blockers);
        return text.TrimEnd();
    }

    private void CommitPreviewToScene()
    {
        AnalyzeCommit();
        if (commitAnalysis == null || !commitAnalysis.ready)
        {
            commitReport = "[COMMIT BLOCKED]" + Environment.NewLine + (commitAnalysis == null ? "Run Analyze Commit first." : commitAnalysis.reason);
            Repaint();
            return;
        }
        int warningCount = validation == null ? 0 : validation.Count(x => x.severity == "WARNING");
        if (warningCount > 0)
        {
            string warningText = "This layout contains " + warningCount + " warning(s)." + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, commitAnalysis.warnings.Take(8)) + Environment.NewLine + Environment.NewLine + "Commit anyway?";
            if (!EditorUtility.DisplayDialog("Commit Combat Room", warningText, "Commit", "Cancel")) return;
        }
        else if (!EditorUtility.DisplayDialog("Commit Combat Room", "Commit generated combat room to:" + Environment.NewLine + Environment.NewLine + commitAnalysis.currentScene + Environment.NewLine + Environment.NewLine + "A backup Scene will be created first." + Environment.NewLine + "The Scene will NOT be automatically saved.", "Commit", "Cancel")) return;
        string backupPath;
        try { backupPath = CreateSceneBackup(commitAnalysis.scenePath); }
        catch (Exception ex)
        {
            commitReport = "[COMMIT ABORTED]" + Environment.NewLine + "Scene backup could not be created." + Environment.NewLine + ex.Message;
            Repaint();
            return;
        }
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Commit Combat Room Generator Layout");
        GameObject generatedRoot = null;
        try
        {
            Scene active = SceneManager.GetActiveScene();
            generatedRoot = BuildGameplayRoot(active, layout, commitAnalysis);
            List<string> buildErrors = ValidateGeneratedRoot(generatedRoot, layout, commitAnalysis, false);
            if (buildErrors.Count > 0) throw new InvalidOperationException(string.Join(" ", buildErrors));
            MovePlayerToStart(commitAnalysis.player, layout, generatedRoot);
            MoveExitToEnd(commitAnalysis.exit, layout, generatedRoot);
            UpdateRoomControllerReference(commitAnalysis.roomController, generatedRoot.transform.Find("Enemies"));
            SwapPrototypeObjects(commitAnalysis);
            List<string> postErrors = ValidateGeneratedRoot(generatedRoot, layout, commitAnalysis, true);
            if (commitAnalysis.prototypeGeometry.Any(x => x != null)) postErrors.Add("Unexpected old gameplay geometry remains.");
            if (commitAnalysis.prototypeEnemies.Any(x => x != null)) postErrors.Add("Unexpected old enemies remain.");
            if (postErrors.Count > 0) throw new InvalidOperationException(string.Join(" ", postErrors));
            EditorSceneManager.MarkSceneDirty(active);
            DestroyPreviewObjectsOnly();
            lastBackupPath = backupPath;
            commitReport = BuildCommitCompleteReport(active, backupPath, generatedRoot, commitAnalysis);
            commitAnalysis.ready = false;
            commitAnalysis.reason = "Commit complete. Scene not saved.";
            Repaint();
        }
        catch (Exception ex)
        {
            try { Undo.RevertAllDownToGroup(undoGroup); } catch { }
            if (generatedRoot != null) DestroyImmediate(generatedRoot);
            commitReport = "[COMMIT FAILED]" + Environment.NewLine + "DO NOT SAVE SCENE" + Environment.NewLine + "USE CTRL+Z OR BACKUP" + Environment.NewLine + ex.Message;
            Repaint();
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    private void ReplaceExistingGameplayCommit()
    {
        AnalyzeReplaceExistingCommit();
        if (replaceAnalysis == null || !replaceAnalysis.ready)
        {
            commitReport = "[REPLACE BLOCKED]" + Environment.NewLine +
                           (replaceAnalysis == null ? "Run Analyze Replace Existing Commit first." : replaceAnalysis.reason);
            Repaint();
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Replace Existing Gameplay Commit",
                "Build and validate a new quantized Gameplay commit before replacing the existing one?" + Environment.NewLine + Environment.NewLine +
                "A replacement backup will be created first." + Environment.NewLine +
                "The Scene will NOT be automatically saved.",
                "Replace Existing Commit",
                "Cancel"))
            return;

        string backupPath;
        try
        {
            backupPath = CreateReplaceSceneBackup(replaceAnalysis.buildInputs.scenePath);
        }
        catch (Exception ex)
        {
            commitReport = "[REPLACE ABORTED]" + Environment.NewLine +
                           "Scene backup could not be created. Existing gameplay was not changed." + Environment.NewLine +
                           ex.Message;
            Repaint();
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        GameObject existingRoot = replaceAnalysis.existingRoot;
        CommitAnalysis inputs = replaceAnalysis.buildInputs;
        GameObject buildRoot = null;
        ReplacementPlacementPlan placementPlan = null;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Replace Existing CRG Gameplay Commit");

        try
        {
            if (!active.IsValid() || !active.isLoaded || active.isDirty)
                throw new InvalidOperationException("Scene state changed after replacement analysis. Analyze again from a clean Scene.");
            if (FindSceneRoot(active, CommittedRootName) != existingRoot)
                throw new InvalidOperationException("Existing gameplay commit changed after replacement analysis.");
            if (FindSceneRoot(active, CastleCommittedRootName) != null)
                throw new InvalidOperationException("Existing Castle Visual must be removed/restored before replacing gameplay geometry.");

            // BUILD FIRST: the existing committed hierarchy and all Scene references remain untouched here.
            buildRoot = BuildGameplayRoot(active, layout, inputs, ReplacementBuildRootName);
            List<string> buildErrors = ValidateReplacementBuild(buildRoot, layout, inputs, out placementPlan);
            if (buildErrors.Count > 0)
                throw new InvalidOperationException("REPLACEMENT BUILD VALIDATION FAILED" + Environment.NewLine + string.Join(Environment.NewLine, buildErrors));

            // SWAP begins only after the isolated BUILD has passed every check.
            Undo.DestroyObjectImmediate(existingRoot);
            Undo.RecordObject(buildRoot, "Promote CRG Replacement Build");
            buildRoot.name = CommittedRootName;
            ApplyReplacementPlacementPlan(inputs, placementPlan);
            Transform newEnemyRoot = buildRoot.transform.Find("Enemies");
            UpdateRoomControllerReference(inputs.roomController, newEnemyRoot);

            List<string> postErrors = ValidateGeneratedRoot(buildRoot, layout, inputs, true);
            postErrors.AddRange(ValidateReplacementPlacementAlignment(inputs, placementPlan));
            GameObject[] committedRoots = active.GetRootGameObjects().Where(x => x.name == CommittedRootName).ToArray();
            if (committedRoots.Length != 1 || committedRoots[0] != buildRoot)
                postErrors.Add("Committed gameplay root swap did not produce exactly one promoted BUILD root.");
            if (FindSceneRoot(active, ReplacementBuildRootName) != null)
                postErrors.Add("Replacement BUILD root remains after SWAP.");
            if (FindSceneRoot(active, CastleCommittedRootName) != null)
                postErrors.Add("Castle committed visual unexpectedly appeared during gameplay replacement.");
            if (postErrors.Count > 0)
                throw new InvalidOperationException("REPLACEMENT POST VALIDATION FAILED" + Environment.NewLine + string.Join(Environment.NewLine, postErrors));

            EditorSceneManager.MarkSceneDirty(active);
            DestroyPreviewObjectsOnly();
            lastBackupPath = backupPath;
            commitReport = "GAMEPLAY REPLACEMENT COMPLETE" + Environment.NewLine + Environment.NewLine +
                           "New Quantized Gameplay committed." + Environment.NewLine +
                           "Backup: " + backupPath + Environment.NewLine +
                           "Scene NOT SAVED." + Environment.NewLine + Environment.NewLine +
                           "Next:" + Environment.NewLine +
                           "Generate/Validate Castle Visual against CRG COMMITTED.";
            replaceAnalysis = null;
            commitAnalysis = null;
            Repaint();
        }
        catch (Exception ex)
        {
            bool rollbackAttempted = false;
            try
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Physics2D.SyncTransforms();
                rollbackAttempted = true;
            }
            catch { }

            GameObject staleBuild = active.IsValid() && active.isLoaded ? FindSceneRoot(active, ReplacementBuildRootName) : null;
            if (staleBuild != null)
            {
                try { DestroyImmediate(staleBuild); } catch { }
            }
            bool oldCommitPresent = active.IsValid() && active.isLoaded && FindSceneRoot(active, CommittedRootName) != null;
            commitReport = "[GAMEPLAY REPLACEMENT FAILED]" + Environment.NewLine +
                           "Existing commit restored: " + (oldCommitPresent ? "YES" : "VERIFY BACKUP") + Environment.NewLine +
                           "Rollback attempted: " + (rollbackAttempted ? "YES" : "FAILED") + Environment.NewLine +
                           "Backup: " + backupPath + Environment.NewLine +
                           "SCENE NOT SAVED" + Environment.NewLine +
                           "DO NOT SAVE SCENE" + Environment.NewLine +
                           ex.Message;
            replaceAnalysis = null;
            Repaint();
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private GameObject BuildGameplayRoot(Scene scene, Layout data, CommitAnalysis analysis, string rootName = CommittedRootName)
    {
        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Create CRG Gameplay Root");
        SceneManager.MoveGameObjectToScene(root, scene);
        Transform geometry = NewCommittedChild(root.transform, "Geometry");
        Transform groundRoot = NewCommittedChild(geometry, "GroundSegments");
        Transform platformRoot = NewCommittedChild(geometry, "Platforms");
        Transform enemiesRoot = NewCommittedChild(root.transform, "Enemies");
        var committedSurfaceColliders = new Dictionary<Surface, BoxCollider2D>();

        foreach (Surface surface in data.surfaces)
        {
            Transform parent = surface.critical ? groundRoot : platformRoot;
            GameObject reference = surface.critical ? analysis.groundReference : analysis.platformReference;
            if (reference == null) reference = analysis.groundReference;
            BoxCollider2D committedCollider = CreateCommittedGeometry(surface, parent, reference);
            if (committedCollider == null) throw new InvalidOperationException("Committed surface collider is missing: " + surface.name);
            committedSurfaceColliders[surface] = committedCollider;
        }
        Physics2D.SyncTransforms();

        string[] prefabPaths = chapter == 1 ? C1PrefabPaths : C2PrefabPaths;
        for (int i = 0; i < data.encounters.Count; i++)
        {
            Transform encounterRoot = NewCommittedChild(enemiesRoot, "Encounter_" + (i + 1).ToString("00"));
            foreach (Marker marker in data.encounters[i].markers)
            {
                string prefabPath = prefabPaths[(int)marker.role];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) throw new InvalidOperationException("Missing enemy prefab: " + prefabPath);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Create CRG Enemy");
                SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.SetParent(encounterRoot, true);
                instance.transform.position = new Vector3(marker.position.x, marker.position.y, 0f);
                Surface support = FindSupportSurface(data, marker);
                BoxCollider2D supportCollider = support != null && committedSurfaceColliders.ContainsKey(support) ? committedSurfaceColliders[support] : null;
                SnapRootToSurface(instance, support, supportCollider, marker.role.ToString(), prefabPath, i + 1);
            }
        }
        return root;
    }

    private static Transform NewCommittedChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create CRG Gameplay Object");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static BoxCollider2D CreateCommittedGeometry(Surface surface, Transform parent, GameObject reference)
    {
        if (reference == null) throw new InvalidOperationException("Geometry reference is missing.");
        GameObject go = new GameObject(surface.name);
        Undo.RegisterCreatedObjectUndo(go, "Create CRG Geometry");
        go.transform.SetParent(parent, false);
        float thickness = surface.critical ? 1f : 0.5f;
        go.transform.localPosition = new Vector3((surface.minX + surface.maxX) * 0.5f, surface.y - thickness * 0.5f, 0f);
        go.transform.localScale = new Vector3(surface.Width, thickness, 1f);
        go.tag = reference.tag;
        go.layer = reference.layer;

        SpriteRenderer sourceRenderer = reference.GetComponent<SpriteRenderer>();
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        if (sourceRenderer != null)
        {
            renderer.sprite = sourceRenderer.sprite;
            renderer.color = sourceRenderer.color;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder;
            renderer.flipX = sourceRenderer.flipX;
            renderer.flipY = sourceRenderer.flipY;
        }
        BoxCollider2D sourceCollider = reference.GetComponent<BoxCollider2D>();
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        if (sourceCollider != null)
        {
            collider.size = sourceCollider.size;
            collider.offset = sourceCollider.offset;
            collider.sharedMaterial = sourceCollider.sharedMaterial;
            collider.isTrigger = sourceCollider.isTrigger;
            collider.usedByEffector = sourceCollider.usedByEffector;
        }
        // Align the actual committed collider top to LayoutData.SurfaceTop.
        Physics2D.SyncTransforms();
        go.transform.position += Vector3.up * (surface.y - collider.bounds.max.y);
        Physics2D.SyncTransforms();
        return collider;
    }

    private static Surface FindSupportSurface(Layout data, Marker marker)
    {
        float expectedSurfaceY = marker.position.y - marker.colliderBottomOffset;
        return data.surfaces.Where(s => s.minX <= marker.position.x && s.maxX >= marker.position.x)
            .OrderBy(s => Mathf.Abs(s.y - expectedSurfaceY)).FirstOrDefault();
    }


    private static string FormatVector(Vector3 value)
    {
        return "(" + value.x.ToString("0.0000") + ", " + value.y.ToString("0.0000") + ", " + value.z.ToString("0.0000") + ")";
    }

    private static string FormatVector2(Vector2 value)
    {
        return "(" + value.x.ToString("0.0000") + ", " + value.y.ToString("0.0000") + ")";
    }

    private static string BuildGroundingAlignmentDiagnostic(GameObject instance, Surface support, BoxCollider2D supportingCollider, GroundingColliderInfo info, string role, string prefabPath, int encounterIndex, float layoutTop, float actualSurfaceTop, float rootBefore, float bottomBefore, float delta, float rootAfter, float bottomAfter, float secondDelta, float finalRoot, float finalBottom, float finalError, string extra)
    {
        string nl = Environment.NewLine;
        Transform parent = instance == null ? null : instance.transform.parent;
        string text = "[GROUNDING ALIGNMENT FAILED]" + nl;
        text += "Role: " + role + nl;
        text += "Prefab: " + prefabPath + nl;
        text += "Instance: " + (instance == null ? "(null)" : instance.name) + nl;
        text += "Encounter: " + encounterIndex + nl;
        text += "Supporting Surface ID: " + (support == null ? "(null)" : support.name) + nl;
        text += "Layout Surface Top Y: " + layoutTop.ToString("0.0000") + nl;
        text += "Committed Surface Collider Top Y: " + actualSurfaceTop.ToString("0.0000") + nl;
        text += "Before: Root Y = " + rootBefore.ToString("0.0000") + ", Collider Bottom = " + bottomBefore.ToString("0.0000") + nl;
        text += "Calculated Delta Y: " + delta.ToString("0.0000") + nl;
        text += "After First Snap: Root Y = " + rootAfter.ToString("0.0000") + ", Collider Bottom = " + bottomAfter.ToString("0.0000") + nl;
        text += "Second Correction Delta: " + secondDelta.ToString("0.0000") + nl;
        text += "Final: Root Y = " + finalRoot.ToString("0.0000") + ", Collider Bottom = " + finalBottom.ToString("0.0000") + nl;
        text += "Final Error: " + finalError.ToString("0.0000") + nl;
        text += "Collider Type: " + (info == null ? "(unknown)" : info.type) + nl;
        text += "Collider Path: " + (info == null ? "(unknown)" : info.path) + nl;
        text += "Collider Offset: " + (info == null ? "(unknown)" : FormatVector2(info.offset)) + nl;
        text += "Collider Size: " + (info == null ? "(unknown)" : FormatVector2(info.size)) + nl;
        text += "Root Lossy Scale: " + (instance == null ? "(unknown)" : FormatVector(instance.transform.lossyScale)) + nl;
        text += "Parent Transform Position: " + (parent == null ? "(none)" : FormatVector(parent.position)) + nl;
        text += "Parent Transform Scale: " + (parent == null ? "(none)" : FormatVector(parent.lossyScale)) + nl;
        if (supportingCollider != null) text += "Supporting Collider Bounds: " + FormatVector(supportingCollider.bounds.min) + " to " + FormatVector(supportingCollider.bounds.max) + nl;
        if (!string.IsNullOrEmpty(extra)) text += extra + nl;
        return text.TrimEnd();
    }

    private static void SnapRootToSurface(GameObject instance, Surface support, BoxCollider2D supportingCollider, string role, string prefabPath, int encounterIndex)
    {
        if (support == null) throw new InvalidOperationException("Enemy support surface is missing.");
        Physics2D.SyncTransforms();
        if (supportingCollider == null) throw new InvalidOperationException("Committed supporting collider is missing: " + support.name);
        float layoutTop = support.y;
        float actualSurfaceTop = supportingCollider.bounds.max.y;
        if (Mathf.Abs(actualSurfaceTop - layoutTop) > 0.001f)
            throw new InvalidOperationException("[ERROR] Committed surface top differs from LayoutData. Surface=" + support.name + ", Layout=" + layoutTop.ToString("0.0000") + ", Actual=" + actualSurfaceTop.ToString("0.0000") + ", Bounds=" + FormatVector(supportingCollider.bounds.min) + " to " + FormatVector(supportingCollider.bounds.max));
        float targetTop = actualSurfaceTop;
        GroundingColliderInfo info;
        string groundingError;
        if (!TryGetGroundingColliderInfo(instance, out info, out groundingError)) throw new InvalidOperationException(groundingError);
        Collider2D groundingCollider = info.collider;
        float rootBefore = instance.transform.position.y;
        float bottomBefore = groundingCollider.bounds.min.y;
        float delta = targetTop - bottomBefore;
        Vector3 position = instance.transform.position;
        position.y += delta;
        instance.transform.position = position;
        Physics2D.SyncTransforms();
        float rootAfter = instance.transform.position.y;
        float bottomAfter = groundingCollider.bounds.min.y;
        float observedShift = bottomAfter - bottomBefore;
        if (Mathf.Abs(observedShift - delta) > 0.001f)
        {
            string diagnostic = BuildGroundingAlignmentDiagnostic(instance, support, supportingCollider, info, role, prefabPath, encounterIndex, layoutTop, actualSurfaceTop, rootBefore, bottomBefore, delta, rootAfter, bottomAfter, targetTop - bottomAfter, rootAfter, bottomAfter, Mathf.Abs(bottomAfter - targetTop), "[ERROR] Collider bounds did not update after transform move. Expected shift=" + delta.ToString("0.0000") + ", Observed shift=" + observedShift.ToString("0.0000") + ".");
            throw new InvalidOperationException(diagnostic);
        }
        float secondDelta = targetTop - bottomAfter;
        float finalRoot = rootAfter;
        float finalBottom = bottomAfter;
        if (Mathf.Abs(secondDelta) > 0.0001f)
        {
            position = instance.transform.position;
            position.y += secondDelta;
            instance.transform.position = position;
            Physics2D.SyncTransforms();
            finalRoot = instance.transform.position.y;
            finalBottom = groundingCollider.bounds.min.y;
        }
        float finalError = Mathf.Abs(finalBottom - targetTop);
        if (finalError > 0.001f)
        {
            string diagnostic = BuildGroundingAlignmentDiagnostic(instance, support, supportingCollider, info, role, prefabPath, encounterIndex, layoutTop, actualSurfaceTop, rootBefore, bottomBefore, delta, rootAfter, bottomAfter, secondDelta, finalRoot, finalBottom, finalError, "[ERROR] Enemy grounding collider could not be aligned.");
            throw new InvalidOperationException(diagnostic);
        }
    }


    private List<string> ValidateGeneratedRoot(GameObject root, Layout data, CommitAnalysis analysis, bool requireControllerReference)
    {
        var errors = new List<string>();
        if (root == null) { errors.Add("Generated root is missing."); return errors; }
        Transform geometry = root.transform.Find("Geometry");
        Transform ground = geometry == null ? null : geometry.Find("GroundSegments");
        Transform platforms = geometry == null ? null : geometry.Find("Platforms");
        Transform enemies = root.transform.Find("Enemies");
        if (ground == null || platforms == null || enemies == null) errors.Add("Generated hierarchy is incomplete.");
        int expectedGround = data.surfaces.Count(s => s.critical);
        int expectedPlatforms = data.surfaces.Count(s => !s.critical);
        if (ground != null && ground.childCount != expectedGround) errors.Add("Ground count mismatch.");
        if (platforms != null && platforms.childCount != expectedPlatforms) errors.Add("Platform count mismatch.");
        int expectedEnemies = data.markers.Count;
        int actualEnemies = enemies == null ? 0 : enemies.GetComponentsInChildren<EnemyRoomMember>(true).Length;
        if (actualEnemies != expectedEnemies) errors.Add("Enemy count mismatch.");
        int expectedMelee = data.markers.Count(x => x.role == Role.Melee);
        int expectedRanged = data.markers.Count(x => x.role == Role.Ranged);
        int expectedCharge = data.markers.Count(x => x.role == Role.Charge);
        int actualMelee = CountGeneratedRole(enemies, C1PrefabPaths, C2PrefabPaths, Role.Melee);
        int actualRanged = CountGeneratedRole(enemies, C1PrefabPaths, C2PrefabPaths, Role.Ranged);
        int actualCharge = CountGeneratedRole(enemies, C1PrefabPaths, C2PrefabPaths, Role.Charge);
        if (actualMelee != expectedMelee || actualRanged != expectedRanged || actualCharge != expectedCharge) errors.Add("Enemy role count mismatch.");
        foreach (GameObject enemy in enemies == null ? new GameObject[0] : enemies.GetComponentsInChildren<EnemyRoomMember>(true).Select(x => x.gameObject))
            if (PrefabUtility.GetPrefabInstanceStatus(enemy) != PrefabInstanceStatus.Connected) errors.Add("Enemy prefab connection missing.");
        if (requireControllerReference)
        {
            RoomController controller = analysis.roomController == null ? null : analysis.roomController.GetComponent<RoomController>();
            if (controller == null || ReadEnemyRoot(controller) != enemies) errors.Add("RoomController enemyRoot reference mismatch.");
        }
        return errors;
    }

    private List<string> ValidateReplacementBuild(GameObject root, Layout data, CommitAnalysis analysis, out ReplacementPlacementPlan placementPlan)
    {
        placementPlan = null;
        List<string> errors = ValidateGeneratedRoot(root, data, analysis, false);
        if (root == null || data == null || analysis == null) return errors;
        if (!string.Equals(root.name, ReplacementBuildRootName, StringComparison.Ordinal))
            errors.Add("Replacement BUILD root has an unexpected name.");
        if (analysis.player == null || analysis.exit == null || analysis.roomController == null)
            errors.Add("Required Player / ExitDoor / RoomController references are missing.");

        Transform geometry = root.transform.Find("Geometry");
        Transform groundRoot = geometry == null ? null : geometry.Find("GroundSegments");
        Transform platformRoot = geometry == null ? null : geometry.Find("Platforms");
        Transform enemiesRoot = root.transform.Find("Enemies");
        if (groundRoot == null || platformRoot == null || enemiesRoot == null) return errors;

        Physics2D.SyncTransforms();
        var surfaceColliders = new Dictionary<Surface, BoxCollider2D>();
        foreach (Surface surface in data.surfaces)
        {
            Transform parent = surface.critical ? groundRoot : platformRoot;
            Transform child = parent.Find(surface.name);
            BoxCollider2D collider = child == null ? null : child.GetComponent<BoxCollider2D>();
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                errors.Add(surface.name + ": enabled non-trigger BUILD BoxCollider2D is missing.");
                continue;
            }
            surfaceColliders[surface] = collider;

            float gridWidth = surface.critical ? data.groundTileWidth : data.platformTileWidth;
            string kind = surface.critical ? "Ground" : "Platform";
            if (gridWidth <= 0.0001f)
            {
                errors.Add(kind + " visual-grid width is unavailable.");
            }
            else
            {
                float roundedWidth = Mathf.Round(collider.bounds.size.x / gridWidth) * gridWidth;
                if (Mathf.Abs(collider.bounds.size.x - roundedWidth) > 0.001f)
                    errors.Add(kind + " " + surface.name + " is not tile-grid compatible. Width=" + collider.bounds.size.x.ToString("0.0000") + ", Grid=" + gridWidth.ToString("0.0000") + ".");
            }

            if (Mathf.Abs(collider.bounds.size.x - surface.Width) > 0.001f)
                errors.Add(surface.name + ": BUILD collider width differs from final LayoutData width.");
            if (Mathf.Abs(collider.bounds.min.x - surface.minX) > 0.001f || Mathf.Abs(collider.bounds.max.x - surface.maxX) > 0.001f)
                errors.Add(surface.name + ": BUILD collider X bounds differ from final LayoutData bounds.");
            if (Mathf.Abs(collider.bounds.max.y - surface.y) > 0.001f)
                errors.Add(surface.name + ": BUILD collider top differs from final LayoutData surface top.");
        }

        if (surfaceColliders.Count > 0)
        {
            float layoutMinX = data.surfaces.Min(s => s.minX);
            float layoutMaxX = data.surfaces.Max(s => s.maxX);
            float buildMinX = surfaceColliders.Values.Min(c => c.bounds.min.x);
            float buildMaxX = surfaceColliders.Values.Max(c => c.bounds.max.x);
            if (Mathf.Abs(layoutMinX - buildMinX) > 0.001f || Mathf.Abs(layoutMaxX - buildMaxX) > 0.001f)
                errors.Add("Replacement BUILD room X bounds differ from LayoutData room bounds.");
        }

        for (int encounterIndex = 0; encounterIndex < data.encounters.Count; encounterIndex++)
        {
            Encounter encounter = data.encounters[encounterIndex];
            Transform encounterRoot = enemiesRoot.Find("Encounter_" + (encounterIndex + 1).ToString("00"));
            if (encounterRoot == null)
            {
                errors.Add("Encounter BUILD root is missing: " + (encounterIndex + 1).ToString("00") + ".");
                continue;
            }
            if (encounterRoot.childCount != encounter.markers.Count)
            {
                errors.Add("Encounter " + (encounterIndex + 1) + " enemy count differs from LayoutData.");
                continue;
            }
            for (int markerIndex = 0; markerIndex < encounter.markers.Count; markerIndex++)
            {
                Marker marker = encounter.markers[markerIndex];
                GameObject enemy = encounterRoot.GetChild(markerIndex).gameObject;
                Surface support = FindSupportSurface(data, marker);
                BoxCollider2D supportCollider = support != null && surfaceColliders.ContainsKey(support) ? surfaceColliders[support] : null;
                if (supportCollider == null)
                {
                    errors.Add("Enemy support collider is missing for Encounter " + (encounterIndex + 1) + ", " + marker.role + ".");
                    continue;
                }
                GroundingColliderInfo grounding;
                string groundingError;
                if (!TryGetGroundingColliderInfo(enemy, out grounding, out groundingError))
                {
                    errors.Add("Encounter " + (encounterIndex + 1) + " " + marker.role + ": " + groundingError);
                    continue;
                }
                float groundingErrorValue = Mathf.Abs(grounding.collider.bounds.min.y - supportCollider.bounds.max.y);
                if (groundingErrorValue > 0.001f)
                    errors.Add("Encounter " + (encounterIndex + 1) + " " + marker.role + " grounding error=" + groundingErrorValue.ToString("0.0000") + ".");
            }
        }

        ReplacementPlacementPlan plan;
        List<string> placementErrors = TryCreateReplacementPlacementPlan(data, analysis, surfaceColliders, out plan);
        errors.AddRange(placementErrors);
        if (placementErrors.Count == 0) placementPlan = plan;
        return errors;
    }

    private static List<string> TryCreateReplacementPlacementPlan(Layout data, CommitAnalysis analysis, Dictionary<Surface, BoxCollider2D> surfaceColliders, out ReplacementPlacementPlan plan)
    {
        var errors = new List<string>();
        plan = new ReplacementPlacementPlan();
        plan.startSurface = data.surfaces.Where(s => s.critical && s.surfaceType != "Bridge").OrderBy(s => s.minX).FirstOrDefault();
        plan.exitSurface = data.surfaces.Where(s => s.critical && s.surfaceType != "Bridge").OrderBy(s => s.maxX).LastOrDefault();
        if (plan.startSurface == null || !surfaceColliders.TryGetValue(plan.startSurface, out plan.startCollider))
            errors.Add("Replacement START supporting surface is missing.");
        if (plan.exitSurface == null || !surfaceColliders.TryGetValue(plan.exitSurface, out plan.exitSurfaceCollider))
            errors.Add("Replacement EXIT supporting surface is missing.");
        if (errors.Count > 0) return errors;

        plan.startTop = plan.startCollider.bounds.max.y;
        plan.exitTop = plan.exitSurfaceCollider.bounds.max.y;
        if (Mathf.Abs(plan.startTop - plan.startSurface.y) > 0.001f)
            errors.Add("Replacement START collider top differs from LayoutData.");
        if (Mathf.Abs(plan.exitTop - plan.exitSurface.y) > 0.001f)
            errors.Add("Replacement EXIT collider top differs from LayoutData.");

        GroundingColliderInfo playerGrounding;
        string playerGroundingError = string.Empty;
        if (analysis.player == null || !TryGetGroundingColliderInfo(analysis.player.gameObject, out playerGrounding, out playerGroundingError))
        {
            errors.Add("Player grounding collider is unavailable: " + playerGroundingError);
        }
        else
        {
            plan.playerTarget = analysis.player.position;
            plan.playerTarget.x = plan.startSurface.minX + 1.5f;
            plan.playerTarget.y += plan.startTop - playerGrounding.collider.bounds.min.y;
            if (plan.playerTarget.x < plan.startCollider.bounds.min.x || plan.playerTarget.x > plan.startCollider.bounds.max.x)
                errors.Add("Replacement Player START X lies outside the START supporting surface.");
        }

        if (analysis.exit == null)
        {
            errors.Add("ExitDoor reference is missing.");
        }
        else
        {
            plan.exitCollider = analysis.exit.GetComponentsInChildren<Collider2D>(true).FirstOrDefault(c => c != null && c.enabled);
            plan.exitRenderer = analysis.exit.GetComponentInChildren<SpriteRenderer>(true);
            if (plan.exitCollider == null && plan.exitRenderer == null)
            {
                errors.Add("ExitDoor has no enabled Collider2D or SpriteRenderer bounds.");
            }
            else
            {
                float exitBottom = plan.exitCollider != null ? plan.exitCollider.bounds.min.y : plan.exitRenderer.bounds.min.y;
                plan.exitTarget = analysis.exit.position;
                plan.exitTarget.x = plan.exitSurface.maxX - 1f;
                plan.exitTarget.y += plan.exitTop - exitBottom;
                if (plan.exitTarget.x < plan.exitSurfaceCollider.bounds.min.x || plan.exitTarget.x > plan.exitSurfaceCollider.bounds.max.x)
                    errors.Add("Replacement ExitDoor X lies outside the EXIT supporting surface.");
            }
        }
        return errors;
    }

    private static void ApplyReplacementPlacementPlan(CommitAnalysis analysis, ReplacementPlacementPlan plan)
    {
        if (analysis == null || plan == null || analysis.player == null || analysis.exit == null)
            throw new InvalidOperationException("Replacement placement plan is incomplete.");
        Undo.RecordObject(analysis.player, "Move CRG Replacement Player Spawn");
        Undo.RecordObject(analysis.exit, "Move CRG Replacement Exit");
        analysis.player.position = plan.playerTarget;
        analysis.exit.position = plan.exitTarget;
        Physics2D.SyncTransforms();
    }

    private static List<string> ValidateReplacementPlacementAlignment(CommitAnalysis analysis, ReplacementPlacementPlan plan)
    {
        var errors = new List<string>();
        if (analysis == null || plan == null)
        {
            errors.Add("Replacement placement validation data is missing.");
            return errors;
        }
        Physics2D.SyncTransforms();
        GroundingColliderInfo playerGrounding;
        string playerError;
        if (!TryGetGroundingColliderInfo(analysis.player == null ? null : analysis.player.gameObject, out playerGrounding, out playerError))
            errors.Add("Player grounding validation failed: " + playerError);
        else
        {
            float error = Mathf.Abs(playerGrounding.collider.bounds.min.y - plan.startCollider.bounds.max.y);
            if (error > 0.001f) errors.Add("Player START grounding alignment failed. Error=" + error.ToString("0.0000") + ".");
        }

        if (analysis.exit == null || (plan.exitCollider == null && plan.exitRenderer == null))
            errors.Add("ExitDoor grounding validation data is missing.");
        else
        {
            float bottom = plan.exitCollider != null ? plan.exitCollider.bounds.min.y : plan.exitRenderer.bounds.min.y;
            float error = Mathf.Abs(bottom - plan.exitSurfaceCollider.bounds.max.y);
            if (error > 0.001f) errors.Add("ExitDoor EXIT grounding alignment failed. Error=" + error.ToString("0.0000") + ".");
        }
        return errors;
    }

    private int CountGeneratedRole(Transform enemies, string[] c1Paths, string[] c2Paths, Role role)
    {
        if (enemies == null) return 0;
        string path = (chapter == 1 ? c1Paths : c2Paths)[(int)role];
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return 0;
        string prefabName = prefab.name;
        return enemies.GetComponentsInChildren<EnemyRoomMember>(true).Count(x => x.gameObject.name.StartsWith(prefabName, StringComparison.Ordinal));
    }

    private void SwapPrototypeObjects(CommitAnalysis analysis)
    {
        foreach (GameObject enemy in analysis.prototypeEnemies.Distinct().ToList())
            if (enemy != null) Undo.DestroyObjectImmediate(enemy);
        foreach (GameObject geometry in analysis.prototypeGeometry.Distinct().ToList())
            if (geometry != null) Undo.DestroyObjectImmediate(geometry);
    }

    private void MovePlayerToStart(Transform player, Layout data, GameObject generatedRoot)
    {
        if (player == null) throw new InvalidOperationException("Player is missing.");
        if (generatedRoot == null) throw new InvalidOperationException("Generated gameplay root is missing.");
        Surface start = data.surfaces.Where(s => s.critical && s.surfaceType != "Bridge").OrderBy(s => s.minX).FirstOrDefault();
        if (start == null) throw new InvalidOperationException("Start surface is missing.");
        Transform supportTransform = generatedRoot.transform.Find("Geometry/GroundSegments/" + start.name);
        BoxCollider2D supportCollider = supportTransform == null ? null : supportTransform.GetComponent<BoxCollider2D>();
        if (supportCollider == null) throw new InvalidOperationException("Committed START supporting collider is missing: " + start.name);
        Undo.RecordObject(player, "Move CRG Player Spawn");
        Vector3 position = player.position;
        position.x = start.minX + 1.5f;
        position.y = start.y;
        player.position = position;
        SnapRootToSurface(player.gameObject, start, supportCollider, "Player", "(Scene Player)", 0);
    }

    private void MoveExitToEnd(Transform exit, Layout data, GameObject generatedRoot)
    {
        if (exit == null)
            throw new InvalidOperationException("Exit is missing.");
        if (data == null || generatedRoot == null)
            throw new InvalidOperationException("Exit layout or generated geometry is missing.");

        Surface end = data.surfaces
            .Where(s => s.critical && s.surfaceType != "Bridge")
            .OrderBy(s => s.maxX)
            .LastOrDefault();
        if (end == null)
            throw new InvalidOperationException("Exit surface is missing.");

        Transform supportTransform = generatedRoot.transform.Find(
            "Geometry/GroundSegments/" + end.name
        );
        BoxCollider2D supportCollider = supportTransform == null
            ? null
            : supportTransform.GetComponent<BoxCollider2D>();
        if (supportCollider == null)
            throw new InvalidOperationException(
                "Committed EXIT supporting collider is missing: " + end.name
            );

        Physics2D.SyncTransforms();
        float layoutSurfaceTop = end.y;
        float actualSurfaceTop = supportCollider.bounds.max.y;
        if (Mathf.Abs(actualSurfaceTop - layoutSurfaceTop) > 0.001f)
        {
            throw new InvalidOperationException(
                "[ERROR] Committed EXIT surface top differs from LayoutData. " +
                "Surface=" + end.name +
                ", Layout=" + layoutSurfaceTop.ToString("0.0000") +
                ", Actual=" + actualSurfaceTop.ToString("0.0000")
            );
        }

        Collider2D exitCollider = exit.GetComponentsInChildren<Collider2D>(true)
            .FirstOrDefault(c => c != null && c.enabled);
        SpriteRenderer exitRenderer = exit.GetComponentInChildren<SpriteRenderer>(true);
        if (exitCollider == null && exitRenderer == null)
            throw new InvalidOperationException("ExitDoor has no Collider2D or SpriteRenderer bounds.");

        Undo.RecordObject(exit, "Move CRG Exit");
        Vector3 position = exit.position;
        position.x = end.maxX - 1f;
        exit.position = position;
        Physics2D.SyncTransforms();

        float exitBottom = exitCollider != null
            ? exitCollider.bounds.min.y
            : exitRenderer.bounds.min.y;
        float deltaY = actualSurfaceTop - exitBottom;
        position = exit.position;
        position.y += deltaY;
        exit.position = position;
        Physics2D.SyncTransforms();

        float finalBottom = exitCollider != null
            ? exitCollider.bounds.min.y
            : exitRenderer.bounds.min.y;
        float finalError = Mathf.Abs(finalBottom - actualSurfaceTop);
        if (finalError > 0.001f)
        {
            throw new InvalidOperationException(
                "[ERROR] ExitDoor grounding alignment failed. " +
                "Surface=" + end.name +
                ", Layout Surface Top=" + layoutSurfaceTop.ToString("0.0000") +
                ", Actual Ground Top=" + actualSurfaceTop.ToString("0.0000") +
                ", Exit Bottom=" + finalBottom.ToString("0.0000") +
                ", Exit X=" + exit.position.x.ToString("0.0000") +
                ", Delta Y=" + deltaY.ToString("0.0000") +
                ", Error=" + finalError.ToString("0.0000")
            );
        }
    }

    private void UpdateRoomControllerReference(Transform roomControllerTransform, Transform enemyRoot)
    {
        RoomController controller = roomControllerTransform == null ? null : roomControllerTransform.GetComponent<RoomController>();
        if (controller == null || enemyRoot == null) throw new InvalidOperationException("RoomController reference update failed.");
        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty property = serialized.FindProperty("enemyRoot");
        if (property == null) throw new InvalidOperationException("RoomController enemyRoot property is missing.");
        Undo.RecordObject(controller, "Update RoomController Enemy Root");
        property.objectReferenceValue = enemyRoot;
        serialized.ApplyModifiedProperties();
    }

    private static List<GameObject> CollectPrototypeGeometry(Scene scene)
    {
        var result = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = t.gameObject;
                if (IsProtectedHierarchy(t) || go.GetComponent<SpriteRenderer>() == null || go.GetComponent<BoxCollider2D>() == null) continue;
                bool namedGeometry = go.CompareTag("Ground") || go.name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     go.name.IndexOf("platform", StringComparison.OrdinalIgnoreCase) >= 0 || go.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0;
                if (namedGeometry) result.Add(go);
            }
        return result.Distinct().ToList();
    }

    private static List<GameObject> CollectPrototypeEnemies(Transform enemyRoot)
    {
        if (enemyRoot == null) return new List<GameObject>();
        return enemyRoot.GetComponentsInChildren<EnemyRoomMember>(true).Select(x => x.gameObject).Distinct().ToList();
    }

    private static bool IsProtectedHierarchy(Transform t)
    {
        for (Transform current = t; current != null; current = current.parent)
        {
            if (current.name == PreviewRootName || current.name == CommittedRootName) return true;
            string name = current.name;
            if (name == "Player" || name == "Main Camera" || name == "CameraRoot" || name == "RoomController" ||
                name == "ExitDoor" || name == "Global Light 2D" || name == "Environment Visual" ||
                name == "Canvas" || name == "EventSystem" || name == "Managers" || name == "UI") return true;
        }
        return false;
    }

    private static GameObject FindGroundReference(Scene scene)
    {
        return CollectPrototypeGeometry(scene).OrderByDescending(x => x.CompareTag("Ground")).FirstOrDefault();
    }

    private static GameObject FindPlatformReference(Scene scene, GameObject fallback)
    {
        GameObject platform = CollectPrototypeGeometry(scene).FirstOrDefault(x => x.name.IndexOf("platform", StringComparison.OrdinalIgnoreCase) >= 0);
        return platform ?? fallback;
    }

    private static GameObject FindCommittedGeometryReference(Transform collectionRoot)
    {
        if (collectionRoot == null) return null;
        for (int i = 0; i < collectionRoot.childCount; i++)
        {
            GameObject candidate = collectionRoot.GetChild(i).gameObject;
            BoxCollider2D collider = candidate.GetComponent<BoxCollider2D>();
            SpriteRenderer renderer = candidate.GetComponent<SpriteRenderer>();
            if (collider != null && collider.enabled && !collider.isTrigger && renderer != null)
                return candidate;
        }
        return null;
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x != null && x.gameObject.scene == scene && !IsProtectedPreview(x.transform));
    }

    private static GameObject FindSceneRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(x => x.name == name);
    }

    private static bool IsProtectedPreview(Transform t)
    {
        for (Transform current = t; current != null; current = current.parent)
            if (current.name == PreviewRootName) return true;
        return false;
    }

    private static Transform ReadEnemyRoot(RoomController controller)
    {
        if (controller == null) return null;
        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty property = serialized.FindProperty("enemyRoot");
        return property == null ? null : property.objectReferenceValue as Transform;
    }

    private static string GetNextBackupAssetPath(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return string.Empty;
        string directory = BackupFolderPath;
        string baseName = Path.GetFileNameWithoutExtension(scenePath) + "_BeforeCRG_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string candidate = directory + "/" + baseName + ".unity";
        // Analyze must remain read-only. AssetDatabase.GenerateUniqueAssetPath can return
        // an empty path when the destination folder does not exist yet, so compute a
        // collision-free reservation without creating/importing anything.
        int suffix = 0;
        while (File.Exists(AssetPathToAbsolute(candidate)))
        {
            suffix++;
            candidate = directory + "/" + baseName + "_" + suffix.ToString("00") + ".unity";
        }
        return candidate;
    }

    private static string GetNextReplaceBackupAssetPath(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(scenePath) + "_BeforeCRGReplace_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string candidate = BackupFolderPath + "/" + baseName + ".unity";
        int suffix = 0;
        while (File.Exists(AssetPathToAbsolute(candidate)))
        {
            suffix++;
            candidate = BackupFolderPath + "/" + baseName + "_" + suffix.ToString("00") + ".unity";
        }
        return candidate;
    }

    private static void EnsureAssetFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string CreateSceneBackup(string scenePath)
    {
        EnsureAssetFolder(BackupFolderPath);
        string destination = GetNextBackupAssetPath(scenePath);
        if (string.IsNullOrEmpty(destination) || !AssetDatabase.CopyAsset(scenePath, destination) || !File.Exists(AssetPathToAbsolute(destination)))
            throw new IOException("Backup copy failed: " + destination);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        return destination;
    }

    private static string CreateReplaceSceneBackup(string scenePath)
    {
        EnsureAssetFolder(BackupFolderPath);
        string destination = GetNextReplaceBackupAssetPath(scenePath);
        if (string.IsNullOrEmpty(destination) || !AssetDatabase.CopyAsset(scenePath, destination) || !File.Exists(AssetPathToAbsolute(destination)))
            throw new IOException("Replacement backup copy failed: " + destination);
        AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        return destination;
    }

    private static string AssetPathToAbsolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private void DestroyPreviewObjectsOnly()
    {
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || !active.isLoaded) return;
        foreach (GameObject root in active.GetRootGameObjects())
            if (root.name == PreviewRootName) DestroyImmediate(root);
    }

    private static string BuildCommitCompleteReport(Scene scene, string backupPath, GameObject root, CommitAnalysis analysis)
    {
        string nl = Environment.NewLine;
        Transform geometry = root == null ? null : root.transform.Find("Geometry");
        Transform ground = geometry == null ? null : geometry.Find("GroundSegments");
        Transform platforms = geometry == null ? null : geometry.Find("Platforms");
        Transform enemies = root == null ? null : root.transform.Find("Enemies");
        int enemyCount = enemies == null ? 0 : enemies.GetComponentsInChildren<EnemyRoomMember>(true).Length;
        return "COMMIT COMPLETE - SCENE NOT SAVED" + nl +
               "Scene: " + scene.name + nl +
               "Backup: " + backupPath + nl +
               "Generated Root: " + (root == null ? "(missing)" : root.name) + nl +
               "Ground Segments: " + (ground == null ? 0 : ground.childCount) + nl +
               "Platforms: " + (platforms == null ? 0 : platforms.childCount) + nl +
               "Enemies: " + enemyCount + nl +
               "Player: Moved to START" + nl +
               "Exit: Moved to EXIT" + nl +
               "RoomController: Verified" + nl +
               "Camera: Unchanged" + nl +
               "Castle Visual: Unchanged" + nl +
               "Scene Dirty: YES" + nl +
               "Next: Enter Play Mode and test. If correct, Ctrl+S. If incorrect, Ctrl+Z or restore Backup.";
    }

}







