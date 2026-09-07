using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MiniProject.EditorTools.RoomProduction;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniProject.EditorTools.EnvironmentAuthoring
{
    internal enum CastleGeometrySourceMode { Auto, CRGPreview, CRGCommitted, BossExistingGeometry }

    internal sealed class CastleRoomBuilderWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Environment Authoring/Castle Room Builder";
        private const string PreferencesPrefix = "MiniProject.CastleRoomBuilder.v1.";
        private const string EnvironmentRootName = "Environment Visual";
        private const string PreviewRootName = "__CRB_PREVIEW__Environment Visual";
        private const string CommittedEnvironmentRootName = "__CRB_COMMITTED__Environment Visual";
        private const string CommitBuildRootName = "__CRB_BUILD__Environment Visual";
        private const string GeneratedEnvironmentRoot = "Assets/Arts/Environment/Generated";
        private const string VisualBackupFolder = "Assets/Generated/CastleRoomBackups";
        private const string SolidBackgroundFillAssetPath = GeneratedEnvironmentRoot + "/Common/SolidBackgroundFill.png";
        private const string BossRoom1Name = "BossRoom1";
        private const string BossRoom2Name = "BossRoom2";
        private static readonly Color RoyalRecommendedSolidFillColor = new Color32(98, 91, 102, 255);

        private static readonly RoomDefinition[] Rooms =
        {
            new RoomDefinition("Room1-1", "Assets/Scenes/Room1-1.unity", CastleTheme.Royal),
            new RoomDefinition("Room1-2", "Assets/Scenes/Room1-2.unity", CastleTheme.Royal),
            new RoomDefinition("Room1-3", "Assets/Scenes/Room1-3.unity", CastleTheme.Royal),
            new RoomDefinition("Room2-1", "Assets/Scenes/Room2-1.unity", CastleTheme.Dark),
            new RoomDefinition("Room2-2", "Assets/Scenes/Room2-2.unity", CastleTheme.Dark),
            new RoomDefinition("Room2-3", "Assets/Scenes/Room2-3.unity", CastleTheme.Dark),
            new RoomDefinition("BossRoom1", "Assets/Scenes/BossRoom1.unity", CastleTheme.Royal),
            new RoomDefinition("BossRoom2", "Assets/Scenes/BossRoom2.unity", CastleTheme.Dark)
        };

        private const string CrgRootName = "__CRG_COMMITTED__Gameplay";

        [SerializeField] private int selectedRoomIndex;
        [SerializeField] private CastleGeometrySourceMode requestedGeometrySourceMode = CastleGeometrySourceMode.Auto;
        private static CastleGeometrySourceMode activeGeometrySourceMode = CastleGeometrySourceMode.Auto;
        [SerializeField] private bool useBackgroundFill = true;
        [SerializeField] private bool useBackWall = true;
        [SerializeField] private float backWallVisualScale = 0.7f;
        [SerializeField] private float backWallVerticalOffset = -1.5f;
        [SerializeField] private float backWallHorizontalBleed = 0.5f;
        [SerializeField] private float backWallModuleOverlap = 0.02f;
        [SerializeField] private Color backWallTint = Color.white;
        [SerializeField] private float backgroundHorizontalSafetyMargin = 2f;
        [SerializeField] private float backgroundVerticalSafetyMargin = 1.5f;
        [SerializeField] private bool usePlatformVisuals = true;
        [SerializeField] private bool useArchitecture;
        [SerializeField] private bool useMinimalDecoration;
        [SerializeField] private bool hideExistingGroundRenderers;
        [SerializeField] private SpriteSlots royalSlots = new SpriteSlots();
        [SerializeField] private SpriteSlots darkSlots = new SpriteSlots();
        [SerializeField] private Vector2 windowScroll;
        [SerializeField] private Vector2 reportScroll;
        [SerializeField] private bool lastDryRunPassed;

        [NonSerialized] private GameObject previewRoot;
        [NonSerialized] private int previewCacheSceneHandle = -1;
        [NonSerialized] private GameObject commitBuildRoot;
        [NonSerialized] private bool commitBuildValidated;
        [SerializeField] private string commitBuildReport;
        [NonSerialized] private readonly List<GroundRendererState> hiddenGroundRenderers = new List<GroundRendererState>();
        [NonSerialized] private bool previewChangedExistingRenderers;
        [SerializeField] private string reportText = "Dry Run을 실행하면 Preview 적용 전 검사 결과가 여기에 표시됩니다.";

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            CastleRoomBuilderWindow window = GetWindow<CastleRoomBuilderWindow>();
            window.titleContent = new GUIContent("Castle Room Builder");
            window.minSize = new Vector2(520f, 680f);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Castle Room Builder");
            LoadPreferences();
            SyncPreviewCacheWithActiveScene();
        }

        private void OnDisable()
        {
            SavePreferences();
            previewRoot = null;
            previewCacheSceneHandle = -1;
        }

        private void OnFocus()
        {
            SyncPreviewCacheWithActiveScene();
            Repaint();
        }

        private void OnHierarchyChange()
        {
            SyncPreviewCacheWithActiveScene();
            Repaint();
        }

        private void OnGUI()
        {
            SyncPreviewCacheWithActiveScene();
            selectedRoomIndex = Mathf.Clamp(selectedRoomIndex, 0, Rooms.Length - 1);
            RoomDefinition room = Rooms[selectedRoomIndex];

            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            EditorGUILayout.HelpBox(
                "Dry Run, Preview, Clear Preview와 CRG COMMITTED 기반 Visual Commit을 제공합니다. Commit 후 Scene은 자동 저장되지 않습니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();

            DrawSectionHeader("Target Scene");
            selectedRoomIndex = EditorGUILayout.Popup("Scene", selectedRoomIndex, Rooms.Select(item => item.DisplayName).ToArray());
            room = Rooms[selectedRoomIndex];
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Theme", room.Theme.ToString());
                EditorGUILayout.TextField("Scene Asset", room.ScenePath);
            }

            DrawSectionHeader("Generation Options");
            useBackgroundFill = EditorGUILayout.Toggle("Background Fill", useBackgroundFill);
            useBackWall = EditorGUILayout.Toggle("BackWall Decoration", useBackWall);
            backWallVisualScale = EditorGUILayout.Slider("BackWall Visual Scale", backWallVisualScale, 0.25f, 1.5f);
            backWallVerticalOffset = EditorGUILayout.Slider("BackWall Vertical Offset", backWallVerticalOffset, -10f, 10f);
            backWallHorizontalBleed = EditorGUILayout.Slider("BackWall Horizontal Bleed", backWallHorizontalBleed, 0f, 3f);
            backWallModuleOverlap = EditorGUILayout.Slider("BackWall Module Overlap", backWallModuleOverlap, 0f, 0.05f);
            EditorGUILayout.HelpBox("BackWall 전용 uniform scale입니다. 값이 작을수록 원본 비율을 유지하며 축소됩니다.", MessageType.None);
            backWallTint = EditorGUILayout.ColorField("BackWall Tint", backWallTint);
            backgroundHorizontalSafetyMargin = EditorGUILayout.Slider("Background Horizontal Margin", backgroundHorizontalSafetyMargin, 0f, 10f);
            backgroundVerticalSafetyMargin = EditorGUILayout.Slider("Background Vertical Margin", backgroundVerticalSafetyMargin, 0f, 10f);
            usePlatformVisuals = EditorGUILayout.Toggle("Platform Visual", usePlatformVisuals);
            useArchitecture = EditorGUILayout.Toggle("Architecture", useArchitecture);
            useMinimalDecoration = EditorGUILayout.Toggle("Minimal Decoration", useMinimalDecoration);
            hideExistingGroundRenderers = EditorGUILayout.Toggle("Hide Existing Ground Renderers", hideExistingGroundRenderers);
            if (hideExistingGroundRenderers)
            {
                EditorGUILayout.HelpBox("Preview 동안에만 기존 Renderer를 숨깁니다. Clear Preview에서 원래 enabled 값을 복원하며 Scene은 저장하지 않습니다.", MessageType.Warning);
            }

            DrawSectionHeader("Royal Castle Sprite Slots");
            DrawSlots(royalSlots);

            DrawSectionHeader("Dark Castle Sprite Slots");
            DrawSlots(darkSlots);

            if (EditorGUI.EndChangeCheck())
            {
                SavePreferences();
                lastDryRunPassed = false;
                reportText = "설정이 변경되었습니다. Dry Run을 다시 실행하세요.";
            }

            EditorGUILayout.Space(12f);
            if (GUILayout.Button("Dry Run", GUILayout.Height(32f)))
            {
                SavePreferences();
                reportText = RunDryRun(Rooms[selectedRoomIndex]);
            }

            bool canPreview = lastDryRunPassed && HasLoadedCleanTargetScene(Rooms[selectedRoomIndex]);
            bool hasPreview = previewRoot != null;
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUILayout.Button(new GUIContent("Preview", "Dry Run passed and target Scene is clean"), GUILayout.Height(26f)))
                {
                    reportText = RunPreview(Rooms[selectedRoomIndex]);
                }
            }

            using (new EditorGUI.DisabledScope(!hasPreview))
            {
                if (GUILayout.Button(new GUIContent("Clear Preview", "Tool이 생성한 Preview만 제거"), GUILayout.Height(26f)))
                {
                    reportText = ClearPreview(Rooms[selectedRoomIndex]);
                }
            }

            bool canCommit = CanCommit(room, out string commitUnavailableReason);
            using (new EditorGUI.DisabledScope(!canCommit))
            {
                if (GUILayout.Button(new GUIContent("Commit", "Fresh-build Castle Visual from committed or Boss existing Gameplay Geometry"), GUILayout.Height(26f)))
                {
                    reportText = CommitVisualToScene(room);
                }
            }
            if (!canCommit)
            {
                EditorGUILayout.HelpBox("Commit unavailable: " + commitUnavailableReason, MessageType.Info);
            }

            if (!canPreview)
            {
                EditorGUILayout.HelpBox("Preview는 Dry Run을 통과하고 선택한 Room Scene이 열려 있으며 Clean 상태일 때 사용할 수 있습니다.", MessageType.None);
            }

            DrawSectionHeader("Report");
            reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.MinHeight(240f));
            EditorGUILayout.SelectableLabel(reportText, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSectionHeader(string title)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static void DrawSlots(SpriteSlots slots)
        {
            slots.BackgroundFill = (Sprite)EditorGUILayout.ObjectField("Background Fill Sprite", slots.BackgroundFill, typeof(Sprite), false);
            slots.UseSolidBackgroundFill = EditorGUILayout.Toggle("Use Solid Background Fill", slots.UseSolidBackgroundFill);
            using (new EditorGUI.DisabledScope(!slots.UseSolidBackgroundFill || slots.BackgroundFill != null))
            {
                slots.SolidBackgroundColor = EditorGUILayout.ColorField("Solid Background Color", slots.SolidBackgroundColor);
            }
            if (slots.BackgroundFill != null && slots.UseSolidBackgroundFill)
            {
                EditorGUILayout.HelpBox("Background Fill Sprite is assigned, so SPRITE mode takes priority over Solid Color.", MessageType.Info);
            }
            slots.BackWall = (Sprite)EditorGUILayout.ObjectField("BackWall Module Sprite", slots.BackWall, typeof(Sprite), false);
            slots.Ground = (Sprite)EditorGUILayout.ObjectField("Ground Sprite", slots.Ground, typeof(Sprite), false);
            slots.Platform = (Sprite)EditorGUILayout.ObjectField("Platform Sprite", slots.Platform, typeof(Sprite), false);
            slots.Pillar = (Sprite)EditorGUILayout.ObjectField("Pillar Sprite", slots.Pillar, typeof(Sprite), false);
            slots.Arch = (Sprite)EditorGUILayout.ObjectField("Arch Sprite", slots.Arch, typeof(Sprite), false);
            slots.Window = (Sprite)EditorGUILayout.ObjectField("Window Sprite", slots.Window, typeof(Sprite), false);
        }

        private string RunDryRun(RoomDefinition room)
        {
            DryRunReport report = new DryRunReport(room);
            activeGeometrySourceMode = requestedGeometrySourceMode;
            report.Info("MODE", "미리보기 전용 Dry Run입니다. Scene/Prefab/원본 Castle Asset은 저장하거나 변경하지 않습니다.");
            report.Info("PLAN", "생성 Root: Environment Visual/{BackWall, Architecture, Platforms, Foreground, Decoration}");
            report.Info("PLAN", "Working Copy만 PPU 16 / Point / Compression None으로 정규화합니다. 원본은 변경하지 않습니다.");
            report.Info("OPTION", $"Background Fill={useBackgroundFill}, BackWall Decoration={useBackWall}, Platform Visual={usePlatformVisuals}, Architecture={useArchitecture}, Minimal Decoration={useMinimalDecoration}");
            SpriteSlots activeSlots = room.Theme == CastleTheme.Royal ? royalSlots : darkSlots;
            report.Info("BACKGROUND", "Background Mode: " + GetBackgroundModeLabel(ResolveBackgroundMode(activeSlots)));
            report.Info("BACKGROUND", "Solid Fill Color: " + FormatColor(activeSlots.SolidBackgroundColor));
            report.Info("OPTION", $"Hide Existing Ground Renderers={hideExistingGroundRenderers} (Preview에만 적용)");

            ValidateSpriteSlots(room, report);
            ValidateScene(room, report);
            ReportBackgroundCoveragePlan(room, report);
            report.Complete();
            lastDryRunPassed = !report.HasErrors;
            return report.ToString();
        }

        private void ValidateSpriteSlots(RoomDefinition room, DryRunReport report)
        {
            SpriteSlots slots = room.Theme == CastleTheme.Royal ? royalSlots : darkSlots;

            BackgroundFillMode backgroundMode = ResolveBackgroundMode(slots);
            report.Info("BACKGROUND", "Background Mode: " + GetBackgroundModeLabel(backgroundMode));
            if (useBackgroundFill)
            {
                if (backgroundMode == BackgroundFillMode.Sprite)
                {
                    report.Info("BACKGROUND", "Background Fill Configured: YES (SPRITE)");
                    ValidateSprite("Background Fill", slots.BackgroundFill, room, report);
                }
                else if (backgroundMode == BackgroundFillMode.SolidColor)
                {
                    report.Success("BACKGROUND", "Background Fill Configured: YES (SOLID COLOR)");
                    report.Info("BACKGROUND", "Solid Fill Color: " + FormatColor(slots.SolidBackgroundColor));
                    report.Info("BACKGROUND", "Solid Fill Sprite Asset: " + SolidBackgroundFillAssetPath + " (created once on Preview/Commit if missing)");
                    if (slots.SolidBackgroundColor.a <= 0.001f)
                    {
                        report.Warning("BACKGROUND", "Solid Background Color is fully transparent.");
                    }
                }
                else
                {
                    report.Warning("BACKGROUND", "Background Fill Configured: NO");
                    report.Warning("SEAM RISK", "Seam Risk Warning: neither a Background Fill Sprite nor Solid Background Fill is configured. Decorative BackWall modules are not a seamless background replacement.");
                }
            }

            if (useBackWall)
            {
                report.Info("BACKWALL", "BackWall Role: DECORATIVE LAYER (rendered above Background Fill; never used as an automatic Fill substitute)");
                ValidateSprite("BackWall", slots.BackWall, room, report);
            }

            if (usePlatformVisuals)
            {
                ValidateSprite("Ground", slots.Ground, room, report);
                ValidateSprite("Platform", slots.Platform, room, report);
            }

            if (useArchitecture)
            {
                ValidateSprite("Pillar", slots.Pillar, room, report);
                ValidateSprite("Arch", slots.Arch, room, report);
            }

            if (useMinimalDecoration)
            {
                ValidateSprite("Window", slots.Window, room, report);
            }

            if (!useBackgroundFill && !useBackWall && !usePlatformVisuals && !useArchitecture && !useMinimalDecoration)
            {
                report.Warning("SPRITE", "모든 생성 옵션이 꺼져 있어 생성할 시각 요소가 없습니다.");
            }
        }

        private static void ValidateSprite(string slotName, Sprite sprite, RoomDefinition room, DryRunReport report)
        {
            if (sprite == null)
            {
                report.Error("SPRITE", $"{room.Theme} {slotName} Sprite is not assigned.");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(sprite).Replace('\\', '/');
            if (string.IsNullOrEmpty(assetPath))
            {
                report.Error("SPRITE", $"{slotName}: Asset 경로를 확인할 수 없습니다.");
                return;
            }
            if (!RoomProductionVisualGridUtility.IsValidThemeSprite(room.Theme.ToString(), slotName, sprite, out string validationReason))
            {
                report.Error("SPRITE", validationReason);
                return;
            }

            if (assetPath.IndexOf("/Backgroud/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                assetPath.IndexOf("/Background/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                report.Error("SPRITE", $"{slotName}: 외부 Parallax Background는 사용할 수 없습니다: {assetPath}");
                return;
            }

            report.Success("SPRITE", $"{slotName}: {assetPath} [{sprite.name}]");
            ValidateWorkingCopyStatus(slotName, sprite, room, report);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                report.Warning("IMPORT", $"{slotName}: TextureImporter를 확인할 수 없습니다. Working Copy 생성 전 확인이 필요합니다.");
                return;
            }

            bool alreadyNormalized = Mathf.Approximately(importer.spritePixelsPerUnit, 16f) &&
                                     importer.filterMode == FilterMode.Point &&
                                     importer.textureCompression == TextureImporterCompression.Uncompressed;
            if (alreadyNormalized)
            {
                report.Success("IMPORT", $"{slotName}: 이미 PPU 16 / Point / Compression None입니다.");
            }
            else
            {
                report.Warning(
                    "IMPORT",
                    $"{slotName}: 원본 설정은 PPU {importer.spritePixelsPerUnit.ToString("0.###", CultureInfo.InvariantCulture)} / {importer.filterMode} / {importer.textureCompression}입니다. Working Copy만 정규화합니다.");
            }
        }

        private static void ValidateWorkingCopyStatus(string slotName, Sprite sourceSprite, RoomDefinition room, DryRunReport report)
        {
            string generatedPath = GetWorkingCopyPath(sourceSprite, room);
            TextureImporter importer = AssetImporter.GetAtPath(generatedPath) as TextureImporter;
            if (importer == null)
            {
                report.Info("CREATE WORKING COPY", $"{slotName}: Preview에서 생성 예정: {generatedPath}");
                return;
            }

            bool valid = importer.textureType == TextureImporterType.Sprite &&
                         Mathf.Approximately(importer.spritePixelsPerUnit, 16f) &&
                         importer.filterMode == FilterMode.Point &&
                         importer.textureCompression == TextureImporterCompression.Uncompressed;
            if (valid)
            {
                report.Success("REUSE WORKING COPY", $"{slotName}: 기존 Working Copy 재사용: {generatedPath}");
            }
            else
            {
                report.Warning("INVALID WORKING COPY", $"{slotName}: Import 설정이 올바르지 않아 Preview에서 복구 예정: {generatedPath}");
            }
        }

        private SpriteSlots PrepareWorkingCopy(RoomDefinition room, SpriteSlots sourceSlots, DryRunReport report)
        {
            SpriteSlots result = new SpriteSlots
            {
                SolidBackgroundColor = sourceSlots.SolidBackgroundColor
            };
            Dictionary<string, string> copiedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            BackgroundFillMode backgroundMode = ResolveBackgroundMode(sourceSlots);
            if (backgroundMode == BackgroundFillMode.Sprite)
            {
                result.BackgroundFill = CopySpriteToWorkingFolder("BackgroundFill", sourceSlots.BackgroundFill, room, copiedPaths, report);
            }
            else if (backgroundMode == BackgroundFillMode.SolidColor)
            {
                result.BackgroundFill = EnsureSolidBackgroundFillSprite(report);
                result.UseSolidBackgroundFill = result.BackgroundFill != null;
            }
            if (useBackWall) result.BackWall = CopySpriteToWorkingFolder("BackWall", sourceSlots.BackWall, room, copiedPaths, report);
            if (usePlatformVisuals)
            {
                result.Ground = CopySpriteToWorkingFolder("Ground", sourceSlots.Ground, room, copiedPaths, report);
                result.Platform = CopySpriteToWorkingFolder("Platform", sourceSlots.Platform, room, copiedPaths, report);
            }
            if (useArchitecture)
            {
                result.Pillar = CopySpriteToWorkingFolder("Pillar", sourceSlots.Pillar, room, copiedPaths, report);
                result.Arch = CopySpriteToWorkingFolder("Arch", sourceSlots.Arch, room, copiedPaths, report);
            }
            if (useMinimalDecoration) result.Window = CopySpriteToWorkingFolder("Window", sourceSlots.Window, room, copiedPaths, report);
            return result;
        }

        private BackgroundFillMode ResolveBackgroundMode(SpriteSlots slots)
        {
            if (!useBackgroundFill || slots == null)
            {
                return BackgroundFillMode.None;
            }
            if (slots.BackgroundFill != null)
            {
                return BackgroundFillMode.Sprite;
            }
            return slots.UseSolidBackgroundFill ? BackgroundFillMode.SolidColor : BackgroundFillMode.None;
        }

        private static string GetBackgroundModeLabel(BackgroundFillMode mode)
        {
            switch (mode)
            {
                case BackgroundFillMode.Sprite: return "SPRITE";
                case BackgroundFillMode.SolidColor: return "SOLID COLOR";
                default: return "NONE";
            }
        }

        private static Sprite EnsureSolidBackgroundFillSprite(DryRunReport report)
        {
            EnsureAssetFolder(GeneratedEnvironmentRoot + "/Common");
            string absolutePath = Path.Combine(
                Application.dataPath,
                SolidBackgroundFillAssetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
            bool created = false;

            if (!File.Exists(absolutePath))
            {
                Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false, true);
                try
                {
                    Color32[] pixels = Enumerable.Repeat(new Color32(255, 255, 255, 255), 16 * 16).ToArray();
                    texture.SetPixels32(pixels);
                    texture.Apply(false, false);
                    File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
                    created = true;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
                AssetDatabase.ImportAsset(SolidBackgroundFillAssetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            TextureImporter importer = AssetImporter.GetAtPath(SolidBackgroundFillAssetPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(SolidBackgroundFillAssetPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(SolidBackgroundFillAssetPath) as TextureImporter;
            }
            if (importer == null)
            {
                report.Error("SOLID FILL ASSET", "TextureImporter is unavailable: " + SolidBackgroundFillAssetPath);
                return null;
            }

            bool importerChanged = importer.textureType != TextureImporterType.Sprite ||
                                   importer.spriteImportMode != SpriteImportMode.Single ||
                                   !HasFullRectMesh(importer) ||
                                   !Mathf.Approximately(importer.spritePixelsPerUnit, 16f) ||
                                   importer.filterMode != FilterMode.Point ||
                                   importer.textureCompression != TextureImporterCompression.Uncompressed ||
                                   importer.mipmapEnabled;
            if (importerChanged)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                SetFullRectMesh(importer);
                importer.SaveAndReimport();
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SolidBackgroundFillAssetPath);
            if (sprite == null)
            {
                report.Error("SOLID FILL ASSET", "Unable to load generated white Sprite: " + SolidBackgroundFillAssetPath);
                return null;
            }

            report.Success(
                created ? "CREATE SOLID FILL ASSET" : "REUSE SOLID FILL ASSET",
                SolidBackgroundFillAssetPath + " (16x16, PPU 16, Point, None, Full Rect)");
            return sprite;
        }

        private static Sprite CopySpriteToWorkingFolder(
            string slotName,
            Sprite sourceSprite,
            RoomDefinition room,
            Dictionary<string, string> copiedPaths,
            DryRunReport report)
        {
            if (sourceSprite == null)
            {
                report.Error("WORKING COPY", $"{slotName}: source Sprite가 없습니다.");
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceSprite).Replace('\\', '/');
            string destinationPath = GetWorkingCopyPath(sourceSprite, room);
            if (!copiedPaths.ContainsKey(sourcePath))
            {
                EnsureGeneratedFolder(room);
                if (!File.Exists(Path.Combine(Application.dataPath, destinationPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar))))
                {
                    if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                    {
                        report.Error("WORKING COPY", $"{slotName}: 복사 실패: {sourcePath} -> {destinationPath}");
                        return null;
                    }
                    report.Info("CREATE WORKING COPY", $"{slotName}: {destinationPath}");
                }

                copiedPaths[sourcePath] = destinationPath;
                ConfigureWorkingImporter(sourcePath, destinationPath, report);
            }

            string actualPath = copiedPaths[sourcePath];
            Sprite workingSprite = FindMatchingSprite(actualPath, sourceSprite);
            if (workingSprite == null)
            {
                report.Error("WORKING COPY", $"{slotName}: Working Copy에서 Source Sprite의 Name/Rect와 일치하는 Sprite를 찾지 못했습니다: {actualPath}");
            }

            return workingSprite;
        }

        private static void ConfigureWorkingImporter(string sourcePath, string destinationPath, DryRunReport report)
        {
            TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            TextureImporter importer = AssetImporter.GetAtPath(destinationPath) as TextureImporter;
            if (importer == null)
            {
                report.Error("WORKING COPY", $"Working Copy TextureImporter를 찾지 못했습니다: {destinationPath}");
                return;
            }

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != (sourceImporter == null ? SpriteImportMode.Single : sourceImporter.spriteImportMode) ||
                           !HasFullRectMesh(importer) ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, 16f) ||
                           importer.filterMode != FilterMode.Point ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed)
            {
                report.Success("REUSE WORKING COPY", destinationPath);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            if (sourceImporter != null)
            {
                importer.spriteImportMode = sourceImporter.spriteImportMode;
            }
            SetFullRectMesh(importer);
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            report.Info("WORKING COPY", $"Import 정규화 완료: {destinationPath} (PPU 16 / Point / None)");
        }

        private static bool HasFullRectMesh(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return settings.spriteMeshType == SpriteMeshType.FullRect;
        }

        private static void SetFullRectMesh(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        private static Sprite FindMatchingSprite(string assetPath, Sprite sourceSprite)
        {
            Sprite[] candidates = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
            Sprite byName = candidates.FirstOrDefault(candidate => candidate.name == sourceSprite.name);
            if (byName != null && RectApproximatelyEqual(byName.rect, sourceSprite.rect))
            {
                return byName;
            }

            return candidates.FirstOrDefault(candidate => RectApproximatelyEqual(candidate.rect, sourceSprite.rect));
        }

        private static bool RectApproximatelyEqual(Rect first, Rect second)
        {
            return Mathf.Abs(first.x - second.x) < 0.01f &&
                   Mathf.Abs(first.y - second.y) < 0.01f &&
                   Mathf.Abs(first.width - second.width) < 0.01f &&
                   Mathf.Abs(first.height - second.height) < 0.01f;
        }

        private static string GetWorkingCopyPath(Sprite sprite, RoomDefinition room)
        {
            string fileName = Path.GetFileName(AssetDatabase.GetAssetPath(sprite));
            string themeFolder = room.Theme == CastleTheme.Royal ? "Royal" : "Dark";
            return $"{GeneratedEnvironmentRoot}/{themeFolder}/{fileName}";
        }

        private static void EnsureGeneratedFolder(RoomDefinition room)
        {
            EnsureAssetFolder("Assets/Arts");
            EnsureAssetFolder(GeneratedEnvironmentRoot);
            EnsureAssetFolder($"{GeneratedEnvironmentRoot}/{(room.Theme == CastleTheme.Royal ? "Royal" : "Dark")}");
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string name = Path.GetFileName(folderPath);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureAssetFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }

        private string RunPreview(RoomDefinition room)
        {
            DryRunReport report = new DryRunReport(room);
            activeGeometrySourceMode = requestedGeometrySourceMode;
            report.Info("MODE", "Preview는 Working Copy와 DontSave Preview Object만 생성합니다. Scene은 저장하지 않습니다.");

            if (!HasLoadedCleanTargetScene(room))
            {
                report.Error("SCENE", "Preview 대상 Scene이 열려 있지 않거나 Dirty 상태입니다.");
                report.Complete();
                lastDryRunPassed = false;
                return report.ToString();
            }

            ValidateSpriteSlots(room, report);
            ValidateScene(room, report);
            if (report.HasErrors)
            {
                report.Complete();
                lastDryRunPassed = false;
                return report.ToString();
            }

            try
            {
                Scene scene = SceneManager.GetActiveScene();
                SpriteSlots sourceSlots = room.Theme == CastleTheme.Royal ? royalSlots : darkSlots;
                SpriteSlots workingSlots = PrepareWorkingCopy(room, sourceSlots, report);
                if (report.HasErrors)
                {
                    report.Complete();
                    return report.ToString();
                }

                ClearPreviewInternal(scene, null);
                previewRoot = CreatePreviewObject(PreviewRootName, null, scene);
                GeneratePreview(scene, room, workingSlots, previewRoot, report);

                if (report.HasErrors)
                {
                    ClearPreviewInternal(scene, report);
                    report.Error("PREVIEW", "Preview 생성 중 오류가 발생해 임시 Object와 Renderer 상태를 정리했습니다.");
                }
                else
                {
                    report.Success("PREVIEW", $"Preview 생성 완료: {GetHierarchyPath(previewRoot.transform)}");
                    report.Info("PREVIEW", "Preview Object에는 Transform/SpriteRenderer만 있으며 Scene 저장은 수행하지 않습니다.");
                    RecordPreviewDirtyStamp(scene);
                }
            }
            catch (Exception exception)
            {
                report.Error("PREVIEW", $"예외: {exception.GetType().Name}: {exception.Message}");
                Scene scene = SceneManager.GetActiveScene();
                ClearPreviewInternal(scene, report);
            }

            report.Complete();
            return report.ToString();
        }

        private string ClearPreview(RoomDefinition room)
        {
            DryRunReport report = new DryRunReport(room);
            activeGeometrySourceMode = requestedGeometrySourceMode;
            Scene scene = SceneManager.GetActiveScene();
            int clearedCount = ClearPreviewInternal(scene, report);
            if (clearedCount == 0)
            {
                report.Info("CLEAR", "Active Scene에서 Castle Preview Root를 찾지 못했습니다.");
            }
            else
            {
                report.Info("CLEAR", $"Active Scene의 Castle Preview Root {clearedCount}개를 제거했습니다.");
            }
            SyncPreviewCacheWithActiveScene();
            report.Complete();
            return report.ToString();
        }

        private static bool HasLoadedCleanTargetScene(RoomDefinition room)
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() &&
                   scene.isLoaded &&
                   !scene.isDirty &&
                   string.Equals(scene.path.Replace('\\', '/'), room.ScenePath, StringComparison.OrdinalIgnoreCase);
        }

        private static GameObject FindPreviewRoot(Scene scene)
        {
            return FindPreviewRoots(scene).FirstOrDefault();
        }

        private static GameObject[] FindPreviewRoots(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<GameObject>();
            }

            return scene.GetRootGameObjects()
                .Where(candidate => candidate != null && candidate.name == PreviewRootName)
                .ToArray();
        }

        private void SyncPreviewCacheWithActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            int activeSceneHandle = scene.IsValid() ? scene.handle : -1;
            if (previewCacheSceneHandle != activeSceneHandle)
            {
                previewRoot = null;
                hiddenGroundRenderers.Clear();
                previewChangedExistingRenderers = false;
                lastDryRunPassed = false;
                previewCacheSceneHandle = activeSceneHandle;
                if (scene.IsValid())
                {
                    int activeRoomIndex = Array.FindIndex(Rooms, room =>
                        string.Equals(scene.path.Replace('\\', '/'), room.ScenePath, StringComparison.OrdinalIgnoreCase));
                    if (activeRoomIndex >= 0)
                    {
                        selectedRoomIndex = activeRoomIndex;
                    }
                }
            }

            previewRoot = FindPreviewRoot(scene);
        }

        private static string GetPreviewDirtyStampKey(Scene scene)
        {
            return PreferencesPrefix + "PreviewDirtyStamp." + scene.path;
        }

        private static void RecordPreviewDirtyStamp(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                SessionState.SetInt(GetPreviewDirtyStampKey(scene), Undo.GetCurrentGroup());
            }
        }

        private static bool IsKnownPreviewOnlyDirtyState(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty || FindPreviewRoots(scene).Length == 0)
            {
                return false;
            }

            GameObject[] previewRoots = FindPreviewRoots(scene);
            bool allRootsAreDontSavePreview = previewRoots.All(root =>
                (root.hideFlags & HideFlags.DontSaveInEditor) != 0);
            int recordedUndoGroup = SessionState.GetInt(GetPreviewDirtyStampKey(scene), -1);
            return allRootsAreDontSavePreview &&
                   recordedUndoGroup >= 0 &&
                   recordedUndoGroup == Undo.GetCurrentGroup();
        }

        private static GameObject CreatePreviewObject(string objectName, Transform parent, Scene scene)
        {
            GameObject gameObject = new GameObject(objectName);
            gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static GameObject CreateVisual(string objectName, Transform parent, Scene scene, Sprite sprite, Vector3 position, int sortingLayerId, int sortingOrder)
        {
            GameObject visual = CreatePreviewObject(objectName, parent, scene);
            visual.transform.position = position;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            return visual;
        }

        private int ClearPreviewInternal(Scene scene, DryRunReport report)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return 0;
            }

            GameObject[] previews = FindPreviewRoots(scene);
            foreach (GameObject preview in previews)
            {
                UnityEngine.Object.DestroyImmediate(preview);
            }
            if (scene == SceneManager.GetActiveScene())
            {
                previewRoot = null;
                previewCacheSceneHandle = scene.handle;
            }
            if (previews.Length > 0)
            {
                report?.Success("CLEAR", $"Castle Preview Root {previews.Length}개를 제거했습니다.");
            }
            RestoreHiddenGroundRenderers(scene, report);
            SessionState.EraseInt(GetPreviewDirtyStampKey(scene));
            return previews.Length;
        }

        private void GeneratePreview(Scene scene, RoomDefinition room, SpriteSlots slots, GameObject root, DryRunReport report)
        {
            Transform background = CreatePreviewObject("Background", root.transform, scene).transform;
            Transform backgroundFillLayer = background;
            Transform backWall = CreatePreviewObject("BackWall", root.transform, scene).transform;
            Transform backWallModules = CreatePreviewObject("Decorative Modules", backWall, scene).transform;
            Transform architecture = CreatePreviewObject("Architecture", root.transform, scene).transform;
            Transform platforms = CreatePreviewObject("Platforms", root.transform, scene).transform;
            Transform foreground = CreatePreviewObject("Foreground", root.transform, scene).transform;
            Transform decoration = CreatePreviewObject("Decoration", root.transform, scene).transform;
            report.Info("HIERARCHY", "Background/{Solid or Sprite Background Fill} and BackWall/Decorative Modules are separate layers. BackWall is never stretched or substituted for Fill.");

            BoxCollider2D[] colliders = GetGeometryColliders(scene);
            if (colliders.Length == 0)
            {
                report.Error("GEOMETRY", "Preview 배치에 사용할 Ground/Platform Collider가 없습니다.");
                return;
            }

            Bounds roomBounds = CalculateRoomBounds(colliders);
            report.Info("BACKGROUND", "Gameplay Area Bounds: " + FormatVector(roomBounds.min) + ".." + FormatVector(roomBounds.max));
            Bounds cameraCoverage = CalculateCameraCoverageBounds(scene, roomBounds, colliders, backgroundHorizontalSafetyMargin, backgroundVerticalSafetyMargin, report);
            CastleGeometrySourceMode resolvedMode = ResolveGeometrySourceMode(scene, activeGeometrySourceMode);
            BoxCollider2D mainGround = FindMainGroundForSource(scene, colliders, resolvedMode);
            SortingPlan sorting = BuildSortingPlan(scene, report);
            report.Info("BACKGROUND", "Geometry Source: " + GetResolvedGeometrySourceLabel(scene));
            if (useBackgroundFill || useBackWall)
            {
                BackgroundLayerBuildResult backgroundResult = new BackgroundLayerBuildResult
                {
                    FillEnabled = useBackgroundFill,
                    BackWallEnabled = useBackWall
                };
                if (useBackgroundFill) GenerateBackgroundFill(scene, slots.BackgroundFill, slots.UseSolidBackgroundFill, slots.SolidBackgroundColor, cameraCoverage, backgroundFillLayer, sorting, report, backgroundResult);
                else report.Info("BACKGROUND", "Background Fill generation: DISABLED");
                if (useBackWall) GenerateBackWall(scene, slots.BackWall, cameraCoverage, mainGround, backWallModules, sorting, backWallVisualScale, backWallVerticalOffset, backWallHorizontalBleed, backWallModuleOverlap, backWallTint, report, backgroundResult);
                else report.Info("BACKWALL", "BackWall Decoration generation: DISABLED");
                ReportBackgroundLayerSummary(backgroundResult, cameraCoverage, backWallModuleOverlap, report);
            }
            if (usePlatformVisuals) GeneratePlatformVisuals(scene, room, colliders, slots, platforms, sorting, report);
            if (useArchitecture) GenerateArchitecture(scene, colliders, slots, architecture, sorting, backWallVisualScale, backWallTint, report);
            if (useMinimalDecoration) GenerateDecoration(scene, slots.Window, roomBounds, decoration, sorting, report);

            if (hideExistingGroundRenderers)
            {
                CaptureAndHideGroundRenderers(colliders, report);
            }
        }

        private bool CanCommit(RoomDefinition room, out string unavailableReason)
        {
            unavailableReason = string.Empty;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                unavailableReason = "Unity is in or entering Play Mode.";
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            {
                unavailableReason = "No valid saved Room Scene is active.";
                return false;
            }
            if (!string.Equals(scene.path.Replace('\\', '/'), room.ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                unavailableReason = "The selected Room does not match the active Scene.";
                return false;
            }
            if (scene.isDirty && !IsKnownPreviewOnlyDirtyState(scene))
            {
                unavailableReason = "Scene has unsaved user changes.";
                return false;
            }

            CastleGeometrySourceMode commitSource = ResolveCommitGeometrySourceMode(scene);
            if (commitSource == CastleGeometrySourceMode.Auto)
            {
                unavailableReason = IsBossRoomScene(scene)
                    ? scene.name + " has no valid existing horizontal Ground surfaces."
                    : "No CRG COMMITTED Gameplay.";
                return false;
            }
            if (GetGeometryColliders(scene, commitSource).Length == 0)
            {
                unavailableReason = commitSource == CastleGeometrySourceMode.BossExistingGeometry
                    ? scene.name + " has no valid existing horizontal Ground surfaces."
                    : "CRG COMMITTED has no valid Ground/Platform colliders.";
                return false;
            }

            GameObject[] sceneObjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();
            if (!HasNamedComponent(FindByName(sceneObjects, "Player"), "PlayerController"))
            {
                unavailableReason = "Player/PlayerController reference is missing.";
                return false;
            }
            if (!HasNamedComponent(FindByName(sceneObjects, "ExitDoor"), "ExitDoor"))
            {
                unavailableReason = "ExitDoor reference is missing.";
                return false;
            }
            if (!HasNamedComponent(FindByName(sceneObjects, "CameraRoot"), "CameraFollow"))
            {
                unavailableReason = "CameraRoot/CameraFollow reference is missing.";
                return false;
            }
            Camera camera = FindSceneCamera(scene);
            if (camera == null || !camera.orthographic)
            {
                unavailableReason = "An orthographic Main Camera is required.";
                return false;
            }
            if (!HasNamedComponent(FindByName(sceneObjects, "RoomController"), "RoomController"))
            {
                unavailableReason = "RoomController reference is missing.";
                return false;
            }
            if (commitSource == CastleGeometrySourceMode.BossExistingGeometry &&
                !HasNamedComponent(FindByName(sceneObjects, "Boss"), "BossController"))
            {
                unavailableReason = "Boss/BossController reference is missing.";
                return false;
            }

            SpriteSlots slots = room.Theme == CastleTheme.Royal ? royalSlots : darkSlots;
            if (!TryValidateCommitWorkingCopy("Ground", slots.Ground, room, out unavailableReason) ||
                !TryValidateCommitWorkingCopy("Platform", slots.Platform, room, out unavailableReason) ||
                !TryValidateCommitWorkingCopy("BackWall", slots.BackWall, room, out unavailableReason))
            {
                return false;
            }
            if (useBackgroundFill && slots.BackgroundFill != null &&
                !TryValidateCommitWorkingCopy("Background Fill", slots.BackgroundFill, room, out unavailableReason))
            {
                return false;
            }
            if (useArchitecture &&
                (!TryValidateCommitWorkingCopy("Pillar", slots.Pillar, room, out unavailableReason) ||
                 !TryValidateCommitWorkingCopy("Arch", slots.Arch, room, out unavailableReason)))
            {
                return false;
            }
            if (useMinimalDecoration && !TryValidateCommitWorkingCopy("Window", slots.Window, room, out unavailableReason))
            {
                return false;
            }

            return true;
        }

        private static bool HasNamedComponent(GameObject gameObject, string componentTypeName)
        {
            return gameObject != null && gameObject.GetComponents<Component>()
                .Any(component => component != null && component.GetType().Name == componentTypeName);
        }

        private static bool TryValidateCommitWorkingCopy(string slotName, Sprite sourceSprite, RoomDefinition room, out string unavailableReason)
        {
            unavailableReason = string.Empty;
            if (sourceSprite == null)
            {
                unavailableReason = slotName + " Sprite is not configured.";
                return false;
            }

            string path = GetWorkingCopyPath(sourceSprite, room);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                unavailableReason = slotName + " Working Copy is missing.";
                return false;
            }
            if (importer.textureType != TextureImporterType.Sprite ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 16f) ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !HasFullRectMesh(importer))
            {
                unavailableReason = slotName + " Working Copy import settings are invalid.";
                return false;
            }
            if (FindMatchingSprite(path, sourceSprite) == null)
            {
                unavailableReason = slotName + " Working Copy does not match the Source Sprite name/rect.";
                return false;
            }

            return true;
        }

        private string CommitVisualToScene(RoomDefinition room)
        {
            if (!CanCommit(room, out string unavailableReason))
            {
                return "[COMMIT BLOCKED]\n" + unavailableReason + "\nSCENE NOT SAVED";
            }

            Scene scene = SceneManager.GetActiveScene();
            CastleGeometrySourceMode commitSource = ResolveCommitGeometrySourceMode(scene);
            string sourceLabel = FormatGeometrySourceMode(commitSource);
            GameObject existingCommit = FindSceneRoot(scene, CommittedEnvironmentRootName);
            string action = existingCommit == null ? "create the first Castle Visual Commit" : "replace the existing Castle Visual Commit";
            if (!EditorUtility.DisplayDialog(
                    "Commit Castle Visual",
                    "A Scene backup will be created, then " + sourceLabel + " will be read again to " + action + ". The Scene will not be saved automatically.",
                    "Commit",
                    "Cancel"))
            {
                return "Visual Commit cancelled. SCENE NOT SAVED.";
            }

            string backupPath = CreateVisualCommitBackup(scene);
            if (string.IsNullOrEmpty(backupPath))
            {
                return "[COMMIT BLOCKED]\nScene backup could not be created.\nSCENE NOT SAVED";
            }

            string buildReport = BuildVisualCommitCandidate(room);
            GameObject validatedBuild = TakeValidatedCommitBuild();
            if (validatedBuild == null)
            {
                return buildReport + "\n[COMMIT FAILED]\nValidated fresh BUILD was not produced. Existing Visual Commit was preserved.\nSCENE NOT SAVED";
            }

            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Commit Castle Visual");
                Undo.RegisterCreatedObjectUndo(validatedBuild, "Commit Castle Visual");

                existingCommit = FindSceneRoot(scene, CommittedEnvironmentRootName);
                if (existingCommit != null)
                {
                    Undo.DestroyObjectImmediate(existingCommit);
                }

                validatedBuild.name = CommittedEnvironmentRootName;
                SetHideFlagsRecursively(validatedBuild, HideFlags.None);
                SceneManager.MoveGameObjectToScene(validatedBuild, scene);

                ClearPreviewInternal(scene, null);
                HideGameplaySourceSprites(scene, commitSource);
                Undo.CollapseUndoOperations(undoGroup);
                SyncPreviewCacheWithActiveScene();

                return buildReport +
                       "\nVISUAL COMMIT COMPLETE" +
                       "\nBackup: " + backupPath +
                       "\nSource: " + sourceLabel + " (fresh build)" +
                       "\nPreview Clone: NO" +
                       "\nPreview Transform Copy: NO" +
                       "\nSCENE NOT SAVED - inspect in Play Mode before Ctrl+S.";
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                else if (validatedBuild != null)
                {
                    UnityEngine.Object.DestroyImmediate(validatedBuild);
                }
                ClearCommitBuildInternal();
                SyncPreviewCacheWithActiveScene();
                return buildReport +
                       "\n[COMMIT FAILED]" +
                       "\nExisting Visual Commit rollback was attempted." +
                       "\n" + exception.GetType().Name + ": " + exception.Message +
                       "\nSCENE NOT SAVED";
            }
        }

        private static string CreateVisualCommitBackup(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                return null;
            }

            EnsureAssetFolder(VisualBackupFolder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string desiredPath = VisualBackupFolder + "/" + scene.name + "_BeforeCastleVisualCommit_" + timestamp + ".unity";
            string backupPath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
            return AssetDatabase.CopyAsset(scene.path, backupPath) ? backupPath : null;
        }

        private static GameObject FindSceneRoot(Scene scene, string rootName)
        {
            return !scene.IsValid() || !scene.isLoaded
                ? null
                : scene.GetRootGameObjects().FirstOrDefault(root => root != null && root.name == rootName);
        }

        private static void SetHideFlagsRecursively(GameObject gameObject, HideFlags hideFlags)
        {
            gameObject.hideFlags = hideFlags;
            foreach (Transform child in gameObject.transform)
            {
                SetHideFlagsRecursively(child.gameObject, hideFlags);
            }
        }

        private static void HideGameplaySourceSprites(Scene scene, CastleGeometrySourceMode sourceMode)
        {
            if (sourceMode == CastleGeometrySourceMode.BossExistingGeometry)
            {
                foreach (BoxCollider2D collider in GetBossExistingGeometryColliders(scene))
                {
                    SpriteRenderer renderer = collider == null ? null : collider.GetComponent<SpriteRenderer>();
                    if (renderer == null) continue;
                    Undo.RecordObject(renderer, "Hide BossRoom gameplay visual");
                    renderer.enabled = false;
                }
                return;
            }

            GameObject committedGameplay = ResolveGeometryRoot(scene, CastleGeometrySourceMode.CRGCommitted);
            Transform geometry = committedGameplay == null ? null : committedGameplay.transform.Find("Geometry");
            if (geometry == null) return;
            foreach (SpriteRenderer renderer in geometry.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Undo.RecordObject(renderer, "Hide CRG gameplay visual");
                renderer.enabled = false;
            }
        }

        /// <summary>
        /// Builds a temporary visual candidate exclusively from committed gameplay geometry.
        /// RoomProductionPipelineWindow invokes this method through its existing editor reflection bridge.
        /// Neither CRG nor CRB preview objects participate in this build.
        /// </summary>
        private string BuildVisualCommitCandidate(RoomDefinition room)
        {
            DryRunReport report = new DryRunReport(room);
            commitBuildValidated = false;
            commitBuildReport = string.Empty;
            ClearCommitBuildInternal();

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.path.Replace('\\', '/'), room.ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                report.Error("VISUAL COMMIT SOURCE", "The active Scene does not match the selected Room Scene.");
                commitBuildReport = report.ToString();
                return commitBuildReport;
            }

            CastleGeometrySourceMode commitSource = ResolveCommitGeometrySourceMode(scene);
            string sourceLabel = FormatGeometrySourceMode(commitSource);
            GameObject sourceRoot = commitSource == CastleGeometrySourceMode.CRGCommitted
                ? ResolveGeometryRoot(scene, CastleGeometrySourceMode.CRGCommitted)
                : null;
            report.Info("VISUAL COMMIT SOURCE", "Requested: " + sourceLabel);
            report.Info("VISUAL COMMIT SOURCE", "Resolved: " + (commitSource == CastleGeometrySourceMode.Auto ? "MISSING" : sourceLabel));
            report.Info("VISUAL COMMIT SOURCE", "Source Root: " +
                (commitSource == CastleGeometrySourceMode.BossExistingGeometry ? "ACTIVE SCENE / existing Ground colliders" :
                 sourceRoot == null ? "MISSING" : sourceRoot.name));
            if (commitSource == CastleGeometrySourceMode.Auto ||
                (commitSource == CastleGeometrySourceMode.CRGCommitted && (sourceRoot == null || sourceRoot.name != CrgRootName)))
            {
                report.Error("VISUAL COMMIT SOURCE", "Visual Commit requires CRG COMMITTED or supported Boss Room existing geometry.");
                commitBuildReport = report.ToString();
                return commitBuildReport;
            }

            Physics2D.SyncTransforms();
            BoxCollider2D[] colliders = GetGeometryColliders(scene, commitSource);
            if (colliders.Length == 0)
            {
                report.Error("VISUAL COMMIT SOURCE", sourceLabel + " contains no valid horizontal Ground/Platform BoxCollider2D.");
                commitBuildReport = report.ToString();
                return commitBuildReport;
            }

            Bounds gameplayBounds = CalculateRoomBounds(colliders);
            BoxCollider2D mainGround = FindMainGroundForSource(scene, colliders, commitSource);
            report.Info("VISUAL COMMIT", "Gameplay Bounds: " + FormatVector(gameplayBounds.min) + ".." + FormatVector(gameplayBounds.max));
            report.Info("VISUAL COMMIT", "Ground Count: " + colliders.Count(collider => IsGroundGeometryCollider(collider, mainGround, commitSource)));
            report.Info("VISUAL COMMIT", "Platform Count: " + colliders.Count(collider => !IsGroundGeometryCollider(collider, mainGround, commitSource)));
            report.Info("VISUAL COMMIT", "Preview Root Used For Build: NO");
            report.Info("VISUAL COMMIT", "Preview Transform Copied: NO");

            try
            {
                SpriteSlots sourceSlots = room.Theme == CastleTheme.Royal ? royalSlots : darkSlots;
                SpriteSlots workingSlots = PrepareWorkingCopy(room, sourceSlots, report);
                if (report.HasErrors)
                {
                    commitBuildReport = report.ToString();
                    return commitBuildReport;
                }

                commitBuildRoot = CreatePreviewObject(CommitBuildRootName, null, scene);
                Transform background = CreatePreviewObject("Background", commitBuildRoot.transform, scene).transform;
                Transform backgroundFillLayer = background;
                Transform backWall = CreatePreviewObject("BackWall", commitBuildRoot.transform, scene).transform;
                Transform backWallModules = CreatePreviewObject("Decorative Modules", backWall, scene).transform;
                Transform architecture = CreatePreviewObject("Architecture", commitBuildRoot.transform, scene).transform;
                Transform platforms = CreatePreviewObject("Platforms", commitBuildRoot.transform, scene).transform;
                CreatePreviewObject("Foreground", commitBuildRoot.transform, scene);
                Transform decoration = CreatePreviewObject("Decoration", commitBuildRoot.transform, scene).transform;

                Bounds cameraCoverageSource = gameplayBounds;
                foreach (Transform anchor in scene.GetRootGameObjects()
                    .SelectMany(rootObject => rootObject.GetComponentsInChildren<Transform>(true))
                    .Where(transform => transform.name == "Player" || transform.name == "ExitDoor"))
                {
                    cameraCoverageSource.Encapsulate(anchor.position);
                }
                Bounds cameraCoverage = CalculateCameraCoverageBounds(scene, cameraCoverageSource, colliders, backgroundHorizontalSafetyMargin, backgroundVerticalSafetyMargin, report);
                SortingPlan sorting = BuildSortingPlan(scene, report);
                report.Info("BACKGROUND", "Geometry Source: " + sourceLabel);
                if (useBackgroundFill || useBackWall)
                {
                    BackgroundLayerBuildResult backgroundResult = new BackgroundLayerBuildResult
                    {
                        FillEnabled = useBackgroundFill,
                        BackWallEnabled = useBackWall
                    };
                    if (useBackgroundFill) GenerateBackgroundFill(scene, workingSlots.BackgroundFill, workingSlots.UseSolidBackgroundFill, workingSlots.SolidBackgroundColor, cameraCoverage, backgroundFillLayer, sorting, report, backgroundResult);
                    else report.Info("BACKGROUND", "Background Fill generation: DISABLED");
                    if (useBackWall) GenerateBackWall(scene, workingSlots.BackWall, cameraCoverage, mainGround, backWallModules, sorting, backWallVisualScale, backWallVerticalOffset, backWallHorizontalBleed, backWallModuleOverlap, backWallTint, report, backgroundResult);
                    else report.Info("BACKWALL", "BackWall Decoration generation: DISABLED");
                    ReportBackgroundLayerSummary(backgroundResult, cameraCoverage, backWallModuleOverlap, report);
                }
                if (usePlatformVisuals) GeneratePlatformVisuals(scene, room, colliders, workingSlots, platforms, sorting, report, commitSource);
                if (useArchitecture) GenerateArchitecture(scene, colliders, workingSlots, architecture, sorting, backWallVisualScale, backWallTint, report);
                if (useMinimalDecoration) GenerateDecoration(scene, workingSlots.Window, gameplayBounds, decoration, sorting, report);

                if (!report.HasErrors) ValidateCommitBuild(commitBuildRoot, colliders, gameplayBounds, mainGround, commitSource, report);
                if (report.HasErrors)
                {
                    ClearCommitBuildInternal();
                    report.Error("VISUAL COMMIT", "Commit Build Validation: FAIL. Gameplay SpriteRenderers were not changed.");
                }
                else
                {
                    commitBuildValidated = true;
                    report.Success("VISUAL COMMIT", "Commit Build Validation: PASS");
                }
            }
            catch (Exception exception)
            {
                ClearCommitBuildInternal();
                report.Error("VISUAL COMMIT", exception.GetType().Name + ": " + exception.Message);
            }

            commitBuildReport = report.ToString();
            return commitBuildReport;
        }

        private GameObject TakeValidatedCommitBuild()
        {
            if (!commitBuildValidated || commitBuildRoot == null || commitBuildRoot.name != CommitBuildRootName) return null;
            GameObject result = commitBuildRoot;
            commitBuildRoot = null;
            commitBuildValidated = false;
            return result;
        }

        private void ClearCommitBuildInternal()
        {
            if (commitBuildRoot != null) UnityEngine.Object.DestroyImmediate(commitBuildRoot);
            commitBuildRoot = null;
            commitBuildValidated = false;
        }

        private static void ValidateCommitBuild(GameObject buildRoot, BoxCollider2D[] colliders, Bounds gameplayBounds, BoxCollider2D mainGround, CastleGeometrySourceMode sourceMode, DryRunReport report)
        {
            const float tolerance = 0.001f;
            Transform platformRoot = buildRoot == null ? null : buildRoot.transform.Find("Platforms");
            if (platformRoot == null)
            {
                report.Error("ALIGNMENT", "Commit build Platforms root is missing.");
                return;
            }

            float maxSurfaceError = 0f;
            float gameplaySurfaceSum = 0f;
            float visualSurfaceSum = 0f;
            int compared = 0;
            Bounds visualSurfaceBounds = new Bounds();
            bool visualBoundsInitialized = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider2D collider = colliders[i];
                if (collider == null || collider.bounds.size.x < collider.bounds.size.y) continue;
                string category = IsGroundGeometryCollider(collider, mainGround, sourceMode) ? "Ground" : "Platform";
                Transform row = platformRoot.Find(category + "_" + i + "_Visual");
                SpriteRenderer[] renderers = row == null ? new SpriteRenderer[0] : row.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0)
                {
                    report.Error("ALIGNMENT", category + " visual row is missing: " + GetHierarchyPath(collider.transform));
                    continue;
                }

                Bounds visualBounds = renderers[0].bounds;
                for (int rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++) visualBounds.Encapsulate(renderers[rendererIndex].bounds);
                if (!visualBoundsInitialized) { visualSurfaceBounds = visualBounds; visualBoundsInitialized = true; }
                else visualSurfaceBounds.Encapsulate(visualBounds);

                Bounds sourceBounds = collider.bounds;
                bool allowVisualBleed = sourceMode == CastleGeometrySourceMode.BossExistingGeometry;
                float minXError = allowVisualBleed
                    ? Mathf.Max(0f, visualBounds.min.x - sourceBounds.min.x)
                    : Mathf.Abs(visualBounds.min.x - sourceBounds.min.x);
                float maxXError = allowVisualBleed
                    ? Mathf.Max(0f, sourceBounds.max.x - visualBounds.max.x)
                    : Mathf.Abs(visualBounds.max.x - sourceBounds.max.x);
                float topError = Mathf.Abs(visualBounds.max.y - sourceBounds.max.y);
                float surfaceError = Mathf.Max(topError, Mathf.Max(minXError, maxXError));
                maxSurfaceError = Mathf.Max(maxSurfaceError, surfaceError);
                gameplaySurfaceSum += sourceBounds.max.y;
                visualSurfaceSum += visualBounds.max.y;
                compared++;
                if (surfaceError > tolerance)
                {
                    report.Error("ALIGNMENT", category + " visual does not align with committed collider. Path=" +
                        GetHierarchyPath(collider.transform) + " TopError=" + topError.ToString("0.000000") +
                        " MinXError=" + minXError.ToString("0.000000") + " MaxXError=" + maxXError.ToString("0.000000"));
                }
            }

            if (compared == 0)
            {
                report.Error("ALIGNMENT", "No committed Ground/Platform surface could be compared.");
                return;
            }

            float averageGameplayY = gameplaySurfaceSum / compared;
            float averageVisualY = visualSurfaceSum / compared;
            float previewOffsetDelta = averageVisualY - averageGameplayY;
            report.Info("ALIGNMENT", "Surface Alignment Max Error: " + maxSurfaceError.ToString("0.000000"));
            report.Info("ALIGNMENT", "Gameplay Average Surface Y: " + averageGameplayY.ToString("0.0000"));
            report.Info("ALIGNMENT", "Visual Average Surface Y: " + averageVisualY.ToString("0.0000"));
            if (Mathf.Abs(previewOffsetDelta) > 0.01f)
                report.Error("ALIGNMENT", "Committed castle visual appears to contain preview origin offset. Delta=" + previewOffsetDelta.ToString("0.0000"));
            else
                report.Success("ALIGNMENT", "Preview Offset Detected: NO");

            if (visualBoundsInitialized && (visualSurfaceBounds.max.x < gameplayBounds.min.x - tolerance || visualSurfaceBounds.min.x > gameplayBounds.max.x + tolerance))
                report.Error("ALIGNMENT", "Committed Castle Ground/Platform visuals are outside gameplay room X bounds.");
        }

        private static string FormatGeometrySourceMode(CastleGeometrySourceMode mode)
        {
            switch (mode)
            {
                case CastleGeometrySourceMode.CRGPreview: return "CRG PREVIEW";
                case CastleGeometrySourceMode.CRGCommitted: return "CRG COMMITTED";
                case CastleGeometrySourceMode.BossExistingGeometry: return "BOSS EXISTING GEOMETRY";
                default: return "AUTO";
            }
        }

        private static bool IsBossRoomScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded &&
                   (string.Equals(scene.name, BossRoom1Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(scene.name, BossRoom2Name, StringComparison.OrdinalIgnoreCase));
        }

        private static CastleGeometrySourceMode ResolveGeometrySourceMode(Scene scene, CastleGeometrySourceMode requestedMode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return CastleGeometrySourceMode.Auto;
            if (requestedMode == CastleGeometrySourceMode.CRGPreview)
                return ResolveGeometryRoot(scene, CastleGeometrySourceMode.CRGPreview) == null ? CastleGeometrySourceMode.Auto : CastleGeometrySourceMode.CRGPreview;
            if (requestedMode == CastleGeometrySourceMode.CRGCommitted)
                return ResolveGeometryRoot(scene, CastleGeometrySourceMode.CRGCommitted) == null ? CastleGeometrySourceMode.Auto : CastleGeometrySourceMode.CRGCommitted;
            if (requestedMode == CastleGeometrySourceMode.BossExistingGeometry)
                return IsBossRoomScene(scene) && GetBossExistingGeometryColliders(scene).Length > 0
                    ? CastleGeometrySourceMode.BossExistingGeometry
                    : CastleGeometrySourceMode.Auto;

            if (ResolveGeometryRoot(scene, CastleGeometrySourceMode.CRGCommitted) != null)
                return CastleGeometrySourceMode.CRGCommitted;
            if (IsBossRoomScene(scene) && GetBossExistingGeometryColliders(scene).Length > 0)
                return CastleGeometrySourceMode.BossExistingGeometry;
            return ResolveGeometryRoot(scene, CastleGeometrySourceMode.CRGPreview) != null
                ? CastleGeometrySourceMode.CRGPreview
                : CastleGeometrySourceMode.Auto;
        }

        private static CastleGeometrySourceMode ResolveCommitGeometrySourceMode(Scene scene)
        {
            if (ResolveGeometryRoot(scene, CastleGeometrySourceMode.CRGCommitted) != null)
                return CastleGeometrySourceMode.CRGCommitted;
            return IsBossRoomScene(scene) && GetBossExistingGeometryColliders(scene).Length > 0
                ? CastleGeometrySourceMode.BossExistingGeometry
                : CastleGeometrySourceMode.Auto;
        }

        private static GameObject ResolveGeometryRoot(Scene scene, CastleGeometrySourceMode mode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            GameObject committed = scene.GetRootGameObjects().FirstOrDefault(root => root.name == CrgRootName);
            GameObject preview = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "__CRG_PREVIEW__Combat Room");
            if (mode == CastleGeometrySourceMode.CRGPreview) return preview;
            if (mode == CastleGeometrySourceMode.CRGCommitted) return committed;
            return committed != null ? committed : preview;
        }

        private static BoxCollider2D[] GetGeometryColliders(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return new BoxCollider2D[0];
            CastleGeometrySourceMode resolvedMode = ResolveGeometrySourceMode(scene, activeGeometrySourceMode);
            return GetGeometryColliders(scene, resolvedMode);
        }

        private static BoxCollider2D[] GetGeometryColliders(Scene scene, CastleGeometrySourceMode resolvedMode)
        {
            if (!scene.IsValid() || !scene.isLoaded) return new BoxCollider2D[0];
            if (resolvedMode == CastleGeometrySourceMode.BossExistingGeometry)
                return GetBossExistingGeometryColliders(scene);
            GameObject crgRoot = ResolveGeometryRoot(scene, resolvedMode);
            return GetGeometryColliders(crgRoot);
        }

        private static BoxCollider2D[] GetGeometryColliders(GameObject crgRoot)
        {
            if (crgRoot == null) return new BoxCollider2D[0];
            Transform geometry = crgRoot.transform.Find("Geometry");
            if (geometry == null) return new BoxCollider2D[0];
            Transform ground = geometry.Find("GroundSegments");
            Transform platforms = geometry.Find("Platforms");
            return new[] { ground, platforms }
                .Where(parent => parent != null)
                .SelectMany(parent => parent.GetComponentsInChildren<BoxCollider2D>(true))
                .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
                .ToArray();
        }

        private static BoxCollider2D[] GetBossExistingGeometryCandidates(Scene scene)
        {
            if (!IsBossRoomScene(scene)) return new BoxCollider2D[0];
            return scene.GetRootGameObjects()
                .Where(root => root != null && root.name != PreviewRootName && root.name != CommitBuildRootName &&
                               root.name != CommittedEnvironmentRootName && root.name != CrgRootName)
                .SelectMany(root => root.GetComponentsInChildren<BoxCollider2D>(true))
                .Where(collider => collider != null && collider.enabled && !collider.isTrigger && IsBossGeometryCandidate(collider))
                .OrderBy(collider => collider.bounds.min.x)
                .ThenBy(collider => collider.bounds.min.y)
                .ToArray();
        }

        private static bool IsBossGeometryCandidate(BoxCollider2D collider)
        {
            for (Transform current = collider == null ? null : collider.transform; current != null; current = current.parent)
            {
                if (IsGroundOrPlatformName(current.name) || current.CompareTag("Ground")) return true;
            }
            return false;
        }

        private static BoxCollider2D[] GetBossExistingGeometryColliders(Scene scene)
        {
            return GetBossExistingGeometryCandidates(scene)
                .Where(collider => collider.bounds.size.x > collider.bounds.size.y)
                .ToArray();
        }

        private static BoxCollider2D[] GetBossIgnoredVerticalColliders(Scene scene)
        {
            return GetBossExistingGeometryCandidates(scene)
                .Where(collider => collider.bounds.size.y >= collider.bounds.size.x)
                .ToArray();
        }

        private static BoxCollider2D FindBossMainGround(BoxCollider2D[] colliders)
        {
            return (colliders ?? new BoxCollider2D[0])
                .Where(collider => collider != null && collider.bounds.size.x > collider.bounds.size.y)
                .OrderByDescending(collider => collider.bounds.size.x)
                .ThenBy(collider => collider.bounds.max.y)
                .FirstOrDefault();
        }

        private static BoxCollider2D FindMainGroundForSource(Scene scene, BoxCollider2D[] colliders, CastleGeometrySourceMode sourceMode)
        {
            return sourceMode == CastleGeometrySourceMode.BossExistingGeometry
                ? FindBossMainGround(colliders)
                : FindMainGround(colliders);
        }
        private void ReportBackgroundCoveragePlan(RoomDefinition room, DryRunReport report)
        {
            if (!useBackgroundFill && !useBackWall)
            {
                report.Info("BACKGROUND", "Background Fill / BackWall generation is disabled.");
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(room.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;
            BoxCollider2D[] colliders = GetGeometryColliders(scene);
            if (colliders.Length == 0) return;
            Bounds gameplayBounds = CalculateRoomBounds(colliders);
            Bounds coverage = CalculateCameraCoverageBounds(scene, gameplayBounds, colliders, backgroundHorizontalSafetyMargin, backgroundVerticalSafetyMargin, report);
            Bounds backWallPlan = coverage;
            backWallPlan.Expand(new Vector3(Mathf.Max(0f, backWallHorizontalBleed) * 2f, 0f, 0f));
            SpriteSlots slots = room.Theme == CastleTheme.Royal ? royalSlots : darkSlots;
            report.Info("BACKGROUND", "Geometry Source: " + GetResolvedGeometrySourceLabel(scene));
            report.Info("BACKGROUND", "Gameplay Area Bounds: " + FormatVector(gameplayBounds.min) + ".." + FormatVector(gameplayBounds.max));
            report.Info("BACKGROUND", "Margins: Horizontal=" + backgroundHorizontalSafetyMargin.ToString("0.###") + ", Vertical=" + backgroundVerticalSafetyMargin.ToString("0.###"));
            BackgroundFillMode backgroundMode = ResolveBackgroundMode(slots);
            report.Info("BACKGROUND", "Background Mode: " + GetBackgroundModeLabel(backgroundMode));
            report.Info("BACKGROUND", "Background Fill Configured: " + (backgroundMode == BackgroundFillMode.None ? "NO" : "YES"));
            report.Info("BACKGROUND", "Solid Fill Color: " + FormatColor(slots.SolidBackgroundColor));
            report.Info("BACKGROUND", "Background Fill Coverage Bounds (planned): " + FormatVector(coverage.min) + ".." + FormatVector(coverage.max));
            report.Info("BACKWALL", "BackWall Coverage Bounds (planned with bleed): " + FormatVector(backWallPlan.min) + ".." + FormatVector(backWallPlan.max));
            report.Info("BACKWALL", "BackWall Module Count: CALCULATED DURING PREVIEW");
            report.Info("BACKGROUND", "Background Coverage: NOT TESTED (Dry Run plan only)");
            if (backgroundMode == BackgroundFillMode.None)
                report.Warning("SEAM RISK", "Seam Risk Warning: Fill will not be generated, so decorative BackWall module boundaries cannot be hidden by a continuous base layer.");
        }

        private static string GetResolvedGeometrySourceLabel(Scene scene)
        {
            CastleGeometrySourceMode resolvedMode = ResolveGeometrySourceMode(scene, activeGeometrySourceMode);
            return resolvedMode == CastleGeometrySourceMode.Auto ? "MISSING" : FormatGeometrySourceMode(resolvedMode);
        }

        private static Bounds CalculateCameraCoverageBounds(Scene scene, Bounds gameplayBounds, BoxCollider2D[] walkableColliders, float horizontalMargin, float verticalMargin, DryRunReport report)
        {
            Camera camera = FindSceneCamera(scene);
            if (camera == null || !camera.orthographic)
            {
                report.Error("CAMERA COVERAGE", "An active orthographic Main Camera is required to calculate Background Fill coverage.");
                Bounds invalidCoverage = new Bounds(gameplayBounds.center, gameplayBounds.size);
                invalidCoverage.Expand(new Vector3(Mathf.Max(0f, horizontalMargin) * 2f, Mathf.Max(0f, verticalMargin) * 2f, 0f));
                return invalidCoverage;
            }
            if (camera.aspect <= 0f)
            {
                report.Error("CAMERA COVERAGE", "Main Camera aspect must be greater than zero.");
                return gameplayBounds;
            }

            BoxCollider2D[] validWalkable = (walkableColliders ?? new BoxCollider2D[0])
                .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
                .ToArray();
            if (validWalkable.Length == 0)
            {
                report.Error("CAMERA COVERAGE", "No enabled non-trigger Ground/Platform collider is available for vertical camera coverage.");
                return gameplayBounds;
            }

            Physics2D.SyncTransforms();
            float highestWalkableY = validWalkable.Max(collider => collider.bounds.max.y);
            float lowestWalkableY = validWalkable.Min(collider => collider.bounds.max.y);
            float playerStandingOffset;
            float cameraFollowYOffset;
            float cameraRigYOffset;
            string playerBodyDescription;
            string offsetError;
            if (!TryGetPlayerCameraVerticalOffsets(scene, camera, out playerStandingOffset, out cameraFollowYOffset, out cameraRigYOffset, out playerBodyDescription, out offsetError))
            {
                report.Error("CAMERA COVERAGE", offsetError);
                return gameplayBounds;
            }

            float worldHeight = camera.orthographicSize * 2f;
            float aspect = camera.aspect;
            float worldWidth = worldHeight * aspect;
            Vector3 center = gameplayBounds.center;
            float halfWidth = worldWidth * 0.5f;
            float halfHeight = camera.orthographicSize;
            float totalCameraTargetOffsetY = playerStandingOffset + cameraFollowYOffset + cameraRigYOffset;
            float highestCameraCenterY = highestWalkableY + totalCameraTargetOffsetY;
            float lowestCameraCenterY = lowestWalkableY + totalCameraTargetOffsetY;
            float highestCameraVisibleY = highestCameraCenterY + halfHeight;
            float lowestCameraVisibleY = lowestCameraCenterY - halfHeight;
            float coverageMinY = lowestCameraVisibleY - Mathf.Max(0f, verticalMargin);
            float coverageMaxY = highestCameraVisibleY + Mathf.Max(0f, verticalMargin);
            Bounds coverage = new Bounds(center, gameplayBounds.size);
            // Preserve the established horizontal calculation. Only vertical bounds now follow actual camera-center extremes.
            coverage.SetMinMax(
                new Vector3(gameplayBounds.min.x - halfWidth - horizontalMargin, coverageMinY, gameplayBounds.min.z),
                new Vector3(gameplayBounds.max.x + halfWidth + horizontalMargin, coverageMaxY, gameplayBounds.max.z));
            report.Info("CAMERA", "Camera World Width=" + worldWidth.ToString("0.###") + ", Height=" + worldHeight.ToString("0.###") + ", Half=" + halfWidth.ToString("0.###") + "/" + halfHeight.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Highest Walkable Y: " + highestWalkableY.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Lowest Walkable Y: " + lowestWalkableY.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Player Body Collider: " + playerBodyDescription);
            report.Info("CAMERA COVERAGE", "Player Standing Offset: " + playerStandingOffset.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Camera Follow Y Offset: " + cameraFollowYOffset.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Camera Rig/Child Y Offset: " + cameraRigYOffset.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Camera Half Height: " + halfHeight.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Highest Camera Center Y: " + highestCameraCenterY.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Lowest Camera Center Y: " + lowestCameraCenterY.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Highest Camera Visible Y: " + highestCameraVisibleY.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Lowest Camera Visible Y: " + lowestCameraVisibleY.ToString("0.###"));
            report.Info("CAMERA", "Camera Coverage Bounds: " + FormatVector(coverage.min) + ".." + FormatVector(coverage.max));
            return coverage;
        }

        private static bool TryGetPlayerCameraVerticalOffsets(Scene scene, Camera camera, out float playerStandingOffset, out float cameraFollowYOffset, out float cameraRigYOffset, out string playerBodyDescription, out string error)
        {
            playerStandingOffset = 0f;
            cameraFollowYOffset = 0f;
            cameraRigYOffset = 0f;
            playerBodyDescription = "MISSING";
            error = string.Empty;

            CameraFollow follow = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CameraFollow>(true))
                .FirstOrDefault(component => component != null && component.enabled);
            if (follow == null)
            {
                error = "Enabled CameraFollow was not found in the target Scene.";
                return false;
            }
            if (follow.target == null)
            {
                error = "CameraFollow target is missing.";
                return false;
            }

            PlayerController player = follow.target.GetComponent<PlayerController>() ?? follow.target.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                error = "CameraFollow target is not connected to PlayerController.";
                return false;
            }

            Collider2D[] candidates = player.GetComponentsInChildren<Collider2D>(true)
                .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
                .ToArray();
            Collider2D bodyCollider = null;
            Collider2D[] rootCandidates = candidates.Where(collider => collider.gameObject == player.gameObject).ToArray();
            if (rootCandidates.Length == 1)
            {
                bodyCollider = rootCandidates[0];
            }
            else if (rootCandidates.Length > 1)
            {
                error = "Player grounding collider is ambiguous: multiple enabled non-trigger colliders exist on the Player root.";
                return false;
            }
            else
            {
                Rigidbody2D body = player.GetComponent<Rigidbody2D>();
                Collider2D[] bodyCandidates = body == null ? new Collider2D[0] : candidates.Where(collider => collider.attachedRigidbody == body).ToArray();
                if (bodyCandidates.Length == 1) bodyCollider = bodyCandidates[0];
                else if (candidates.Length == 1) bodyCollider = candidates[0];
                else
                {
                    error = candidates.Length == 0
                        ? "Player has no enabled non-trigger body Collider2D."
                        : "Player grounding collider is ambiguous: no unique Rigidbody2D body collider could be selected.";
                    return false;
                }
            }

            SerializedProperty followOffsetProperty = new SerializedObject(follow).FindProperty("offset");
            if (followOffsetProperty == null || followOffsetProperty.propertyType != SerializedPropertyType.Vector3)
            {
                error = "CameraFollow offset Vector3 field could not be read.";
                return false;
            }

            playerStandingOffset = follow.target.position.y - bodyCollider.bounds.min.y;
            cameraFollowYOffset = followOffsetProperty.vector3Value.y;
            cameraRigYOffset = camera.transform.position.y - follow.transform.position.y;
            playerBodyDescription = bodyCollider.GetType().Name + " (" + GetHierarchyPath(bodyCollider.transform) + ")";
            return true;
        }
        private static Bounds CalculateRoomBounds(IEnumerable<BoxCollider2D> colliders)
        {
            Bounds result = new Bounds();
            bool initialized = false;
            foreach (BoxCollider2D collider in colliders)
            {
                if (!initialized)
                {
                    result = collider.bounds;
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(collider.bounds);
                }
            }
            return result;
        }

        private static bool CoversBounds2D(Bounds actual, Bounds required, float tolerance = 0.001f)
        {
            return actual.min.x <= required.min.x + tolerance && actual.max.x >= required.max.x - tolerance &&
                   actual.min.y <= required.min.y + tolerance && actual.max.y >= required.max.y - tolerance;
        }

        private static void GenerateBackgroundFill(
            Scene scene,
            Sprite sprite,
            bool solidColorMode,
            Color solidColor,
            Bounds coverageBounds,
            Transform parent,
            SortingPlan sorting,
            DryRunReport report,
            BackgroundLayerBuildResult result)
        {
            BackgroundFillMode mode = sprite == null
                ? BackgroundFillMode.None
                : solidColorMode ? BackgroundFillMode.SolidColor : BackgroundFillMode.Sprite;
            result.FillMode = mode;
            result.FillConfigured = mode != BackgroundFillMode.None;
            result.FillColor = mode == BackgroundFillMode.SolidColor ? solidColor : Color.white;
            report.Info("BACKGROUND", "Background Mode: " + GetBackgroundModeLabel(mode));
            report.Info("BACKGROUND", "Background Fill Configured: " + (result.FillConfigured ? "YES" : "NO"));
            report.Info("BACKGROUND", "Solid Fill Color: " + FormatColor(solidColor));

            if (mode == BackgroundFillMode.None)
            {
                report.Warning("BACKGROUND", "Background Fill Coverage Bounds: NOT GENERATED; Background Coverage: NOT TESTED");
                report.Warning("CAMERA COVERAGE", "Upper Camera Coverage: NOT TESTED");
                report.Warning("CAMERA COVERAGE", "Lower Camera Coverage: NOT TESTED");
                report.Warning("CAMERA COVERAGE", "Left Camera Coverage: NOT TESTED");
                report.Warning("CAMERA COVERAGE", "Right Camera Coverage: NOT TESTED");
                return;
            }
            if (sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                report.Error("BACKGROUND", "Background Fill Sprite bounds are invalid.");
                return;
            }

            string objectName = mode == BackgroundFillMode.SolidColor ? "Solid Background Fill" : "Sprite Background Fill";
            GameObject fill = CreateVisual(objectName, parent, scene, sprite, coverageBounds.center, sorting.LayerId, sorting.BackWallOrder - 20);
            SpriteRenderer renderer = fill.GetComponent<SpriteRenderer>();
            fill.transform.localScale = Vector3.one;

            if (mode == BackgroundFillMode.SolidColor)
            {
                renderer.drawMode = SpriteDrawMode.Simple;
                Vector3 scale = new Vector3(
                    coverageBounds.size.x / sprite.bounds.size.x,
                    coverageBounds.size.y / sprite.bounds.size.y,
                    1f);
                fill.transform.localScale = scale;
                fill.transform.position = coverageBounds.center - Vector3.Scale(sprite.bounds.center, scale);
                renderer.color = solidColor;
            }
            else
            {
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(coverageBounds.size.x, coverageBounds.size.y);
                renderer.color = Color.white;
            }

            result.FillGenerated = true;
            result.FillBounds = renderer.bounds;
            result.FillCoverageOk = CoversBounds2D(result.FillBounds, coverageBounds);
            bool upperCoverageOk = result.FillBounds.max.y >= coverageBounds.max.y - 0.001f;
            bool lowerCoverageOk = result.FillBounds.min.y <= coverageBounds.min.y + 0.001f;
            bool leftCoverageOk = result.FillBounds.min.x <= coverageBounds.min.x + 0.001f;
            bool rightCoverageOk = result.FillBounds.max.x >= coverageBounds.max.x - 0.001f;
            report.Info("BACKGROUND", "Background Fill Coverage Bounds: " + FormatVector(result.FillBounds.min) + ".." + FormatVector(result.FillBounds.max));
            report.Info("CAMERA COVERAGE", "Background Fill Min Y: " + result.FillBounds.min.y.ToString("0.###"));
            report.Info("CAMERA COVERAGE", "Background Fill Max Y: " + result.FillBounds.max.y.ToString("0.###"));
            if (upperCoverageOk) report.Success("CAMERA COVERAGE", "Upper Camera Coverage: PASS");
            else report.Error("CAMERA COVERAGE", "Upper Camera Coverage: FAIL");
            if (lowerCoverageOk) report.Success("CAMERA COVERAGE", "Lower Camera Coverage: PASS");
            else report.Error("CAMERA COVERAGE", "Lower Camera Coverage: FAIL");
            if (leftCoverageOk) report.Success("CAMERA COVERAGE", "Left Camera Coverage: PASS");
            else report.Error("CAMERA COVERAGE", "Left Camera Coverage: FAIL");
            if (rightCoverageOk) report.Success("CAMERA COVERAGE", "Right Camera Coverage: PASS");
            else report.Error("CAMERA COVERAGE", "Right Camera Coverage: FAIL");
            if (result.FillCoverageOk) report.Success("BACKGROUND", "Background Coverage: OK");
            else report.Error("BACKGROUND", "Background Coverage: FAIL. Fill does not cover the required camera/margin bounds.");
        }

        private static void GenerateBackWall(Scene scene, Sprite sprite, Bounds coverageBounds, BoxCollider2D mainGround, Transform parent, SortingPlan sorting, float visualScale, float verticalOffset, float horizontalBleed, float moduleOverlap, Color tint, DryRunReport report, BackgroundLayerBuildResult result)
        {
            if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                report.Error("BACKWALL", "BackWall Sprite bounds가 0이거나 유효하지 않습니다.");
                return;
            }
            if (mainGround == null)
            {
                report.Error("BACKWALL", "Main Ground Collider를 결정하지 못해 BackWall을 배치할 수 없습니다.");
                return;
            }
            visualScale = Mathf.Max(0.01f, visualScale);
            Camera camera = FindSceneCamera(scene);
            float scaledHeight = sprite.bounds.size.y * visualScale;
            if (camera != null && camera.orthographic && scaledHeight < camera.orthographicSize * 2f)
                report.Warning("BACKWALL", "BackWall sprite is shorter than camera vertical coverage");

            float moduleWidth = sprite.bounds.size.x * visualScale;
            float overlap = Mathf.Clamp(moduleOverlap, 0f, Mathf.Min(0.05f, moduleWidth * 0.25f));
            float step = Mathf.Max(0.001f, moduleWidth - overlap);
            float bleed = Mathf.Max(0f, horizontalBleed);
            float desiredMinX = coverageBounds.min.x - bleed;
            float desiredMaxX = coverageBounds.max.x + bleed;
            float firstLeft = desiredMinX;
            int xCount = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0f, desiredMaxX - firstLeft - moduleWidth) / step) + 1);
            float rowY = mainGround.bounds.max.y - sprite.bounds.min.y * visualScale + verticalOffset;
            Bounds previewBounds = new Bounds();
            bool initialized = false;
            Vector3 observedScale = Vector3.one;
            bool observedScaleInitialized = false;
            for (int x = 0; x < xCount; x++)
            {
                float left = firstLeft + x * step;
                Vector3 position = new Vector3(left - sprite.bounds.min.x * visualScale, rowY, 0f);
                GameObject visual = CreateVisual("Module_" + x.ToString("000"), parent, scene, sprite, position, sorting.LayerId, sorting.BackWallOrder);
                visual.transform.localScale = new Vector3(visualScale, visualScale, 1f);
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                renderer.color = tint;
                if (!observedScaleInitialized)
                {
                    observedScale = visual.transform.lossyScale;
                    observedScaleInitialized = true;
                }
                Bounds bounds = renderer.bounds;
                if (!initialized) { previewBounds = bounds; initialized = true; }
                else previewBounds.Encapsulate(bounds);
            }
            result.BackWallGenerated = initialized;
            result.BackWallBounds = previewBounds;
            result.BackWallModuleCount = initialized ? xCount : 0;
            result.BackWallHorizontalCoverageOk = initialized && previewBounds.min.x <= coverageBounds.min.x + 0.001f && previewBounds.max.x >= coverageBounds.max.x - 0.001f;
            report.Info("BACKWALL", "BackWall Role: DECORATIVE LAYER");
            report.Info("BACKWALL", "BackWall Coverage Bounds: " + (initialized ? FormatVector(previewBounds.min) + ".." + FormatVector(previewBounds.max) : "NOT GENERATED"));
            report.Info("BACKWALL", "BackWall Module Count: " + result.BackWallModuleCount);
            result.BackWallScale = observedScale;
            report.Info("BACKWALL", "BackWall Scale: " + FormatVector(observedScale));
            if (observedScaleInitialized && Mathf.Abs(observedScale.x - observedScale.y) > 0.001f)
            {
                report.Warning("BACKWALL SCALE", "BackWall Non-uniform Scale: WARNING. BackWall is non-uniformly scaled; uniform X/Y scale is recommended.");
            }
            else if (observedScaleInitialized)
            {
                report.Success("BACKWALL SCALE", "BackWall Non-uniform Scale: NO. BackWall uses uniform X/Y scale.");
            }
            report.Info("BACKWALL", "Module Width=" + moduleWidth.ToString("0.###") + ", Step=" + step.ToString("0.###") + ", Overlap=" + overlap.ToString("0.###") + ", Horizontal Bleed=" + bleed.ToString("0.###"));
            if (result.BackWallHorizontalCoverageOk) report.Success("BACKWALL", "BackWall Horizontal Coverage: PASS (one decorative row)");
            else report.Warning("BACKWALL", "BackWall Horizontal Coverage: FAIL");
        }

        private static void ReportBackgroundLayerSummary(BackgroundLayerBuildResult result, Bounds requiredCoverage, float moduleOverlap, DryRunReport report)
        {
            if (!result.BackWallEnabled)
            {
                report.Info("BACKWALL", "BackWall Coverage Bounds: NOT GENERATED");
                report.Info("BACKWALL", "BackWall Module Count: 0");
            }
            if (!result.FillEnabled)
            {
                report.Warning("SEAM RISK", "Seam Risk Warning: Background Fill generation is disabled. BackWall remains decorative and cannot guarantee continuous background coverage.");
            }
            else if (!result.FillConfigured)
            {
                report.Warning("SEAM RISK", "Seam Risk Warning: HIGH. Background Fill is missing; BackWall remains decorative and is not stretched or substituted to hide uncovered background areas.");
            }
            else if (!result.FillGenerated || !result.FillCoverageOk)
            {
                report.Error("SEAM RISK", "Seam Risk Warning: Background Fill is configured but failed to cover the required bounds.");
            }
            else if (result.BackWallEnabled)
            {
                report.Info("SEAM RISK", "Seam Risk Warning: Decorative module boundaries may remain, but the continuous Background Fill prevents empty gaps. Module overlap=" + Mathf.Clamp(moduleOverlap, 0f, 0.05f).ToString("0.###") + ".");
            }
            else report.Info("SEAM RISK", "Seam Risk Warning: NONE from BackWall modules because the decorative layer is disabled; Background Fill coverage is valid.");
            report.Info("BACKGROUND", "Required Camera/Margin Coverage Bounds: " + FormatVector(requiredCoverage.min) + ".." + FormatVector(requiredCoverage.max));
        }
        private static bool IsGroundGeometryCollider(BoxCollider2D collider, BoxCollider2D mainGround = null, CastleGeometrySourceMode sourceMode = CastleGeometrySourceMode.CRGCommitted)
        {
            if (collider == null) return false;
            if (sourceMode == CastleGeometrySourceMode.BossExistingGeometry) return collider == mainGround;
            for (Transform current = collider.transform; current != null; current = current.parent)
            {
                if (current.name == "GroundSegments") return true;
            }
            return false;
        }

        private static void GeneratePlatformVisuals(Scene scene, RoomDefinition room, BoxCollider2D[] colliders, SpriteSlots slots, Transform parent, SortingPlan sorting, DryRunReport report, CastleGeometrySourceMode? sourceModeOverride = null)
        {
            if (slots.Ground == null || slots.Platform == null) { report.Error("PLATFORMS", "Ground or Platform Working Copy is missing."); return; }
            string theme = room.Theme.ToString();
            if (!RoomProductionVisualGridUtility.TryGetVisualGridInfo(theme, "Ground", out VisualGridSpriteInfo groundGridInfo, out string groundGridError))
            {
                report.Error("FIT", groundGridError);
                return;
            }
            if (!RoomProductionVisualGridUtility.TryGetVisualGridInfo(theme, "Platform", out VisualGridSpriteInfo platformGridInfo, out string platformGridError))
            {
                report.Error("FIT", platformGridError);
                return;
            }
            report.Info("FIT", "VISUAL GRID SOURCE: WORKING COPY");
            report.Info("FIT", RoomProductionVisualGridUtility.FormatGridReport("Ground", groundGridInfo));
            report.Info("FIT", RoomProductionVisualGridUtility.FormatGridReport("Platform", platformGridInfo));
            CastleGeometrySourceMode sourceMode = sourceModeOverride ?? ResolveGeometrySourceMode(scene, activeGeometrySourceMode);
            bool allowBossVisualBleed = sourceMode == CastleGeometrySourceMode.BossExistingGeometry;
            BoxCollider2D mainGround = FindMainGroundForSource(scene, colliders, sourceMode);
            bool gridMatch = Mathf.Abs(RoomProductionVisualGridUtility.GetWorldWidth(slots.Ground) - groundGridInfo.WorkingCopyWorldWidth) <= 0.001f &&
                             Mathf.Abs(RoomProductionVisualGridUtility.GetWorldWidth(slots.Platform) - platformGridInfo.WorkingCopyWorldWidth) <= 0.001f;
            if (!gridMatch)
            {
                report.Error("FIT", "Visual Grid Source mismatch.");
                return;
            }
            report.Success("FIT", allowBossVisualBleed
                ? "Working Copy Grid Source: PASS. Boss Gameplay Collider는 변경하지 않고 full-tile bleed를 허용합니다."
                : "CRG/CRB Grid Match: PASS");
            float maxGroundFitError = 0f, maxPlatformFitError = 0f;
            int partialCount = 0, stretchCount = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider2D collider = colliders[i];
                if (collider == null || collider.bounds.size.x < collider.bounds.size.y) continue;
                bool isGround = IsGroundGeometryCollider(collider, mainGround, sourceMode);
                Sprite sprite = isGround ? slots.Ground : slots.Platform;
                string category = isGround ? "Ground" : "Platform";
                float tileWidth = isGround ? groundGridInfo.WorkingCopyWorldWidth : platformGridInfo.WorkingCopyWorldWidth;
                string path = GetHierarchyPath(collider.transform);
                if (tileWidth <= 0f) { report.Error("FIT", category + " tile width is invalid: " + path); continue; }
                float width = collider.bounds.size.x;
                int tileCount = allowBossVisualBleed
                    ? Mathf.Max(1, Mathf.CeilToInt(width / tileWidth - 0.0001f))
                    : Mathf.RoundToInt(width / tileWidth);
                float gridError = Mathf.Abs(tileCount * tileWidth - width);
                string details = category + " " + path + " width=" + width.ToString("0.####") + " tileWidth=" + tileWidth.ToString("0.####") + " ratio=" + (width / tileWidth).ToString("0.####");
                if (gridError > 0.001f)
                {
                    if (!allowBossVisualBleed)
                    {
                        report.Error("FIT", "Gameplay geometry is not visual-grid compatible. " + details);
                        continue;
                    }
                    report.Warning("FIT", "Boss existing collider is not visual-grid compatible; full tiles will bleed without changing Gameplay. " + details +
                        " Visual Bleed=" + gridError.ToString("0.####"));
                }
                else
                    report.Info("FIT", "[" + category.ToUpperInvariant() + "] " + path + " Bounds Width=" + width.ToString("0.####") + " Tile Width=" + tileWidth.ToString("0.####") + " Tile Count=" + tileCount + " Grid Compatible: PASS");
                Transform row = CreatePreviewObject(category + "_" + i + "_Visual", parent, scene).transform;
                float visualMin = allowBossVisualBleed
                    ? collider.bounds.center.x - tileCount * tileWidth * 0.5f
                    : collider.bounds.min.x;
                for (int tile = 0; tile < tileCount; tile++)
                {
                    float left = visualMin + tile * tileWidth;
                    Vector3 position = new Vector3(left - sprite.bounds.min.x, collider.bounds.max.y - sprite.bounds.max.y, 0f);
                    GameObject visual = CreateVisual("Tile_" + tile.ToString("000"), row, scene, sprite, position, sorting.LayerId, sorting.PlatformOrder);
                    visual.transform.localScale = Vector3.one;
                    visual.GetComponent<SpriteRenderer>().drawMode = SpriteDrawMode.Simple;
                }
                float visualMax = visualMin + tileCount * tileWidth;
                float fitError = Mathf.Max(Mathf.Abs(visualMin - collider.bounds.min.x), Mathf.Abs(visualMax - collider.bounds.max.x));
                if (isGround) maxGroundFitError = Mathf.Max(maxGroundFitError, fitError); else maxPlatformFitError = Mathf.Max(maxPlatformFitError, fitError);
                if (fitError > 0.001f) report.Warning("FIT", category + " visual length mismatches collider bounds. " + path);
            }
            report.Info("FIT", "Ground Visual Fit Error (max): " + maxGroundFitError.ToString("0.####"));
            report.Info("FIT", "Platform Visual Fit Error (max): " + maxPlatformFitError.ToString("0.####"));
            report.Info("FIT", "Pixel Stretch Count=" + stretchCount + ", Partial Tile Count=" + partialCount + ", Crop Count=0");
        }

        private static void GenerateArchitecture(Scene scene, BoxCollider2D[] colliders, SpriteSlots slots, Transform parent, SortingPlan sorting, float visualScale, Color tint, DryRunReport report)
        {
            BoxCollider2D mainGround = FindMainGround(colliders);
            if (mainGround == null)
            {
                report.Warning("ARCHITECTURE", "Main Ground를 판별하지 못해 Architecture를 배치할 수 없습니다.");
                return;
            }

            Vector3[] xPositions =
            {
                new Vector3(mainGround.bounds.min.x + mainGround.bounds.size.x * 0.25f, 0f, 0f),
                new Vector3(mainGround.bounds.max.x - mainGround.bounds.size.x * 0.25f, 0f, 0f)
            };
            int pillarCount = 0;
            int archCount = 0;
            for (int i = 0; i < xPositions.Length; i++)
            {
                if (slots.Pillar != null && IsSafeDecorationPosition(scene, xPositions[i].x, mainGround.bounds.max.y, 1.25f))
                {
                    float y = mainGround.bounds.max.y - slots.Pillar.bounds.min.y;
                    CreateVisual($"Pillar_{pillarCount++}", parent, scene, slots.Pillar, new Vector3(xPositions[i].x, y, 0f), sorting.LayerId, sorting.ArchitectureOrder);
                }
                if (slots.Arch != null && IsSafeDecorationPosition(scene, xPositions[i].x, mainGround.bounds.max.y + slots.Arch.bounds.size.y * 0.5f, 1.25f))
                {
                    float y = mainGround.bounds.max.y - slots.Arch.bounds.min.y;
                    GameObject arch = CreateVisual($"Arch_{archCount++}", parent, scene, slots.Arch, new Vector3(xPositions[i].x, y, 0f), sorting.LayerId, sorting.ArchitectureOrder);
                    arch.transform.localScale = Vector3.one * Mathf.Max(0.01f, visualScale);
                    arch.GetComponent<SpriteRenderer>().color = tint;
                }
            }

            if (pillarCount == 0) report.Warning("ARCHITECTURE", "안전한 Pillar 위치를 찾지 못했습니다.");
            if (archCount == 0) report.Warning("ARCHITECTURE", "안전한 Arch 위치를 찾지 못했습니다.");
            report.Info("ARCHITECTURE", $"Deterministic 배치 결과: Pillar {pillarCount}/2, Arch {archCount}/2");
        }

        private static void GenerateDecoration(Scene scene, Sprite windowSprite, Bounds roomBounds, Transform parent, SortingPlan sorting, DryRunReport report)
        {
            if (windowSprite == null)
            {
                report.Warning("DECORATION", "Window Sprite가 없어 Decoration을 생략합니다.");
                return;
            }

            Camera camera = FindSceneCamera(scene);
            float y = camera != null && camera.orthographic
                ? camera.transform.position.y + camera.orthographicSize * 0.35f
                : roomBounds.center.y + roomBounds.size.y * 0.25f;
            float[] xPositions =
            {
                roomBounds.min.x + roomBounds.size.x * 0.33f,
                roomBounds.min.x + roomBounds.size.x * 0.67f
            };
            int count = 0;
            foreach (float x in xPositions)
            {
                if (!IsSafeDecorationPosition(scene, x, y, 1.5f))
                {
                    continue;
                }
                CreateVisual($"Window_{count++}", parent, scene, windowSprite, new Vector3(x, y, 0f), sorting.LayerId, sorting.DecorationOrder);
            }
            if (count == 0) report.Warning("DECORATION", "안전한 Window 위치를 찾지 못했습니다.");
            report.Info("DECORATION", $"Deterministic Window 배치 결과: {count}/2");
        }

        private static bool IsSafeDecorationPosition(Scene scene, float x, float y, float radius)
        {
            IEnumerable<Transform> transforms = scene.GetRootGameObjects()
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == "Player" || transform.name == "ExitDoor" || transform.GetComponent<MonoBehaviour>() != null && transform.parent != null && transform.parent.name == "Enemies");
            return transforms.All(transform => Vector2.Distance(new Vector2(x, y), transform.position) >= radius);
        }

        private static BoxCollider2D FindMainGround(BoxCollider2D[] colliders)
        {
            return colliders
                .Where(collider => collider.bounds.size.x >= collider.bounds.size.y)
                .OrderBy(collider => collider.bounds.min.y)
                .ThenByDescending(collider => collider.bounds.size.x)
                .FirstOrDefault();
        }

        private static Camera FindSceneCamera(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(camera => camera.gameObject.name == "Main Camera");
        }

        private static SortingPlan BuildSortingPlan(Scene scene, DryRunReport report)
        {
            SpriteRenderer[] renderers = scene.GetRootGameObjects()
                .Where(rootObject => rootObject != null &&
                                     rootObject.name != PreviewRootName &&
                                     rootObject.name != CommitBuildRootName &&
                                     rootObject.name != CommittedEnvironmentRootName)
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<SpriteRenderer>(true))
                .ToArray();
            SpriteRenderer reference = renderers.FirstOrDefault();
            int layerId = reference == null ? SortingLayer.NameToID("Default") : reference.sortingLayerID;
            int baseline = renderers.Length == 0 ? 0 : renderers.Min(renderer => renderer.sortingOrder);
            foreach (SpriteRenderer renderer in renderers.Where(item => item != null))
            {
                report.Info("SORTING", $"{GetHierarchyPath(renderer.transform)}: Layer={renderer.sortingLayerName}, Order={renderer.sortingOrder}");
            }
            SortingPlan plan = new SortingPlan(layerId, baseline - 100, baseline - 50, baseline - 10, baseline - 60);
            report.Info("SORTING", $"Preview Layer={SortingLayer.IDToName(layerId)}, BackgroundFill={plan.BackWallOrder - 20}, BackWall={plan.BackWallOrder}, Architecture={plan.ArchitectureOrder}, Platforms={plan.PlatformOrder}, Decoration={plan.DecorationOrder}");
            return plan;
        }

        private void CaptureAndHideGroundRenderers(IEnumerable<BoxCollider2D> colliders, DryRunReport report)
        {
            hiddenGroundRenderers.Clear();
            StringBuilder sessionState = new StringBuilder();
            foreach (BoxCollider2D collider in colliders)
            {
                if (!IsGroundOrPlatformName(collider.name))
                {
                    continue;
                }
                SpriteRenderer renderer = collider.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    continue;
                }
                hiddenGroundRenderers.Add(new GroundRendererState(renderer, renderer.enabled));
                sessionState.Append(GlobalObjectId.GetGlobalObjectIdSlow(renderer)).Append('|').Append(renderer.enabled ? '1' : '0').Append(';');
                renderer.enabled = false;
            }
            SessionState.SetString(GetHiddenRendererStateKey(colliders.FirstOrDefault()?.gameObject.scene.path), sessionState.ToString());
            previewChangedExistingRenderers = hiddenGroundRenderers.Count > 0;
            report.Info("GROUND RENDERER", $"Preview-only hidden existing Renderers: {hiddenGroundRenderers.Count}");
        }

        private void RestoreHiddenGroundRenderers(Scene scene, DryRunReport report)
        {
            if (hiddenGroundRenderers.Count == 0)
            {
                string savedState = SessionState.GetString(GetHiddenRendererStateKey(scene.path), string.Empty);
                foreach (string entry in savedState.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator = entry.LastIndexOf('|');
                    if (separator <= 0 || !GlobalObjectId.TryParse(entry.Substring(0, separator), out GlobalObjectId globalId))
                    {
                        continue;
                    }
                    UnityEngine.Object target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                    if (target is SpriteRenderer renderer)
                    {
                        renderer.enabled = entry.Substring(separator + 1) == "1";
                    }
                }
            }
            foreach (GroundRendererState state in hiddenGroundRenderers)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.Enabled;
                }
            }
            if (previewChangedExistingRenderers)
            {
                report?.Info("GROUND RENDERER", $"기존 Ground/Platform Renderer {hiddenGroundRenderers.Count}개의 상태를 복원했습니다.");
            }
            hiddenGroundRenderers.Clear();
            previewChangedExistingRenderers = false;
            SessionState.EraseString(GetHiddenRendererStateKey(scene.path));
        }

        private static string GetHiddenRendererStateKey(string scenePath)
        {
            return PreferencesPrefix + "HiddenRenderers." + scenePath;
        }

        private static void ValidateScene(RoomDefinition room, DryRunReport report)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(room.ScenePath);
            if (sceneAsset == null)
            {
                report.Error("SCENE", $"대상 Scene을 찾을 수 없습니다: {room.ScenePath}");
                return;
            }

            report.Success("SCENE", $"대상 Scene 확인: {room.ScenePath}");

            Scene scene = SceneManager.GetSceneByPath(room.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Error("SCENE", "대상 Scene을 먼저 열어주세요.");
                return;
            }

            if (scene.isDirty)
            {
                report.Error("DIRTY", "대상 Scene이 이미 Dirty 상태입니다. 사용자 변경사항 보호를 위해 검사를 중단합니다.");
                return;
            }

            try
            {
                report.Success("DIRTY", scene.isDirty ? "Scene이 Dirty 상태입니다." : "Scene이 Clean 상태입니다.");
                InspectSceneObjects(scene, report);

                if (scene.isDirty)
                {
                    report.Error("SAFETY", "Dry Run 중 Scene이 Dirty 상태가 되었습니다. Scene은 저장하지 않습니다.");
                }
                else
                {
                    report.Success("SAFETY", "Dry Run 후에도 Scene은 Clean 상태이며 저장 호출은 수행되지 않았습니다.");
                }
            }
            catch (Exception exception)
            {
                report.Error("SCENE", $"검사 중 예외: {exception.GetType().Name}: {exception.Message}");
            }
        }

        private static void InspectSceneObjects(Scene scene, DryRunReport report)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            List<Transform> transforms = roots
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToList();

            CastleGeometrySourceMode resolvedMode = ResolveGeometrySourceMode(scene, activeGeometrySourceMode);
            GameObject crgRoot = ResolveGeometryRoot(scene, resolvedMode);
            if (resolvedMode == CastleGeometrySourceMode.BossExistingGeometry)
            {
                report.Info("GEOMETRY", "Requested Geometry Source: " + FormatGeometrySourceMode(activeGeometrySourceMode));
                report.Info("GEOMETRY", "Resolved Geometry Source: BOSS EXISTING GEOMETRY");
                report.Info("GEOMETRY", "Root: ACTIVE SCENE / existing Ground colliders");
            }
            else if (crgRoot != null)
            {
                string resolved = crgRoot.name == CrgRootName ? "CRG COMMITTED" : "CRG PREVIEW";
                report.Info("GEOMETRY", "Requested Geometry Source: " + FormatGeometrySourceMode(activeGeometrySourceMode));
                report.Info("GEOMETRY", "Resolved Geometry Source: " + resolved);
                report.Info("GEOMETRY", "Root: " + GetHierarchyPath(crgRoot.transform));
            }
            else report.Error("GEOMETRY", activeGeometrySourceMode == CastleGeometrySourceMode.CRGPreview ? "Pipeline requested CRG PREVIEW but another geometry source was selected." : "Requested geometry source was not found.");

            GameObject environmentRoot = FindByName(roots, EnvironmentRootName);
            if (environmentRoot != null)
            {
                report.Error("ENVIRONMENT", "Scene에 Environment Visual Root가 이미 존재합니다. 중복 생성을 방지해야 합니다.");
            }
            else
            {
                report.Success("ENVIRONMENT", "Environment Visual Root가 없어 새로 생성할 수 있습니다.");
            }

            ValidateNamedObject("Player", FindByName(transforms, "Player"), "PlayerController", report);
            if (resolvedMode == CastleGeometrySourceMode.BossExistingGeometry)
                ValidateNamedObject("Boss", FindByName(transforms, "Boss"), "BossController", report);

            GameObject enemies = FindByName(transforms, "Enemies");
            if (enemies == null)
            {
                report.Error("REFERENCE", "Enemies Root를 찾을 수 없습니다.");
            }
            else
            {
                report.Success("REFERENCE", $"Enemies: {GetHierarchyPath(enemies.transform)}");
                ValidateEnemyPrefabReferences(enemies, report);
            }

            ValidateNamedObject("ExitDoor", FindByName(transforms, "ExitDoor"), "ExitDoor", report);
            ValidateRoomController(FindByName(transforms, "RoomController"), report);
            ValidateNamedObject("CameraRoot", FindByName(transforms, "CameraRoot"), "CameraFollow", report);

            GameObject mainCamera = FindByName(transforms, "Main Camera");
            if (mainCamera == null || mainCamera.GetComponent<Camera>() == null)
            {
                report.Error("REFERENCE", "Main Camera 또는 Camera Component를 찾을 수 없습니다.");
            }
            else
            {
                report.Success("REFERENCE", $"Main Camera: {GetHierarchyPath(mainCamera.transform)}");
            }

            ValidateGroundColliders(scene, report);
        }

        private static void ValidateNamedObject(string objectName, GameObject gameObject, string componentTypeName, DryRunReport report)
        {
            if (gameObject == null)
            {
                report.Error("REFERENCE", $"{objectName} Object를 찾을 수 없습니다.");
                return;
            }

            Component component = gameObject.GetComponents<Component>()
                .FirstOrDefault(item => item != null && item.GetType().Name == componentTypeName);
            if (component == null)
            {
                report.Error("REFERENCE", $"{objectName}에 {componentTypeName} Component가 없습니다.");
                return;
            }

            report.Success("REFERENCE", $"{objectName}: {GetHierarchyPath(gameObject.transform)} / {componentTypeName} 확인 완료");
        }

        private static void ValidateRoomController(GameObject roomControllerObject, DryRunReport report)
        {
            if (roomControllerObject == null)
            {
                report.Error("REFERENCE", "RoomController Object를 찾을 수 없습니다.");
                return;
            }

            Component component = roomControllerObject.GetComponents<Component>()
                .FirstOrDefault(item => item != null && item.GetType().Name == "RoomController");
            if (component == null)
            {
                report.Error("REFERENCE", "RoomController Component를 찾을 수 없습니다.");
                return;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty enemyRoot = serializedObject.FindProperty("enemyRoot");
            SerializedProperty exitDoor = serializedObject.FindProperty("exitDoor");
            bool enemyRootValid = enemyRoot != null && enemyRoot.objectReferenceValue != null;
            bool exitDoorValid = exitDoor != null && exitDoor.objectReferenceValue != null;

            if (!enemyRootValid || !exitDoorValid)
            {
                report.Error("REFERENCE", $"RoomController 참조가 불완전합니다: enemyRoot={enemyRootValid}, exitDoor={exitDoorValid}");
                return;
            }

            report.Success("REFERENCE", "RoomController의 enemyRoot / exitDoor 참조를 확인했습니다.");
        }

        private static void ValidateEnemyPrefabReferences(GameObject enemiesRoot, DryRunReport report)
        {
            MonoBehaviour[] behaviours = enemiesRoot.GetComponentsInChildren<MonoBehaviour>(true);
            GameObject[] enemyObjects = behaviours
                .Where(item => item != null && item.GetType().Name == "EnemyRoomMember")
                .Select(item => item.gameObject)
                .Distinct()
                .ToArray();

            if (enemyObjects.Length == 0)
            {
                report.Warning("ENEMY", "EnemyRoomMember를 찾지 못했습니다. Enemies Root는 존재하지만 Prefab 참조를 개별 확인할 수 없습니다.");
                return;
            }

            int validPrefabReferences = 0;
            foreach (GameObject enemyObject in enemyObjects)
            {
                GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(enemyObject);
                GameObject source = instanceRoot == null
                    ? null
                    : PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                if (source == null)
                {
                    report.Error("ENEMY", $"Prefab 원본 참조를 확인할 수 없습니다: {GetHierarchyPath(enemyObject.transform)}");
                    continue;
                }

                validPrefabReferences++;
                report.Success("ENEMY", $"{GetHierarchyPath(instanceRoot.transform)} -> {AssetDatabase.GetAssetPath(source)}");
            }

            report.Info("ENEMY", $"EnemyRoomMember {enemyObjects.Length}개 중 Prefab 참조 {validPrefabReferences}개 확인");
        }

        private static void ValidateGroundColliders(Scene scene, DryRunReport report)
        {
            CastleGeometrySourceMode resolvedMode = ResolveGeometrySourceMode(scene, activeGeometrySourceMode);
            if (resolvedMode == CastleGeometrySourceMode.BossExistingGeometry)
            {
                ReportBossExistingGeometry(scene, report);
                return;
            }

            GameObject crgRoot = ResolveGeometryRoot(scene, resolvedMode);
            if (crgRoot == null)
            {
                report.Error("GEOMETRY", "Committed Combat Room Geometry not found.");
                return;
            }
            BoxCollider2D[] colliders = GetGeometryColliders(scene);
            if (colliders.Length == 0)
            {
                report.Error("GEOMETRY", "CRG Geometry에 enabled non-trigger BoxCollider2D가 없습니다.");
                return;
            }
            report.Success("GEOMETRY", $"CRG Geometry: FOUND / Gameplay Colliders {colliders.Length}");
            foreach (BoxCollider2D collider in colliders)
            {
                Bounds bounds = collider.bounds;
                report.Info("GEOMETRY", $"{GetHierarchyPath(collider.transform)} bounds={FormatVector(bounds.min)}..{FormatVector(bounds.max)} / top={bounds.max.y:0.###}");
            }
        }

        private static void ReportBossExistingGeometry(Scene scene, DryRunReport report)
        {
            BoxCollider2D[] horizontal = GetBossExistingGeometryColliders(scene);
            BoxCollider2D[] vertical = GetBossIgnoredVerticalColliders(scene);
            BoxCollider2D mainGround = FindBossMainGround(horizontal);
            report.Info("BOSS ROOM", "Horizontal Surfaces: " + horizontal.Length);
            report.Info("BOSS ROOM", "Main Ground: " + (mainGround == null ? "MISSING" : GetHierarchyPath(mainGround.transform)));
            report.Info("BOSS ROOM", "Platforms: " + string.Join(", ", horizontal
                .Where(collider => collider != mainGround)
                .Select(collider => GetHierarchyPath(collider.transform))));
            report.Info("BOSS ROOM", "Ignored Vertical Surfaces: " +
                (vertical.Length == 0 ? "NONE" : string.Join(", ", vertical.Select(collider => GetHierarchyPath(collider.transform)))));

            foreach (BoxCollider2D collider in horizontal)
            {
                Bounds bounds = collider.bounds;
                string classification = collider == mainGround ? "Main Ground" : "Platform";
                report.Info("BOSS ROOM", classification + " / Horizontal Surface: " + GetHierarchyPath(collider.transform) +
                    " bounds=" + FormatVector(bounds.min) + ".." + FormatVector(bounds.max));
            }
            foreach (BoxCollider2D collider in vertical)
            {
                Bounds bounds = collider.bounds;
                report.Info("BOSS ROOM", "Vertical / Ignored Surface: " + GetHierarchyPath(collider.transform) +
                    " bounds=" + FormatVector(bounds.min) + ".." + FormatVector(bounds.max));
            }

            if (horizontal.Length == 0 || mainGround == null)
                report.Error("BOSS ROOM", scene.name + " existing horizontal Gameplay Surface를 찾을 수 없습니다.");
            else
                report.Success("GEOMETRY", "BOSS EXISTING GEOMETRY: FOUND / Horizontal Surfaces " + horizontal.Length);
        }
        private static bool IsGameplayGeometryName(string objectName)
        {
            return objectName == "Ground" || objectName.StartsWith("Ground (", StringComparison.Ordinal) ||
                   objectName == "Wall" || objectName.StartsWith("Wall (", StringComparison.Ordinal) ||
                   objectName == "Platform" || objectName.StartsWith("Platform (", StringComparison.Ordinal);
        }

        private static bool IsGroundOrPlatformName(string objectName)
        {
            return objectName == "Ground" || objectName.StartsWith("Ground (", StringComparison.Ordinal) ||
                   objectName == "Platform" || objectName.StartsWith("Platform (", StringComparison.Ordinal);
        }

        private static GameObject FindByName(IEnumerable<GameObject> gameObjects, string objectName)
        {
            return gameObjects.FirstOrDefault(gameObject => gameObject.name == objectName);
        }

        private static GameObject FindByName(IEnumerable<Transform> transforms, string objectName)
        {
            Transform transform = transforms.FirstOrDefault(item => item.name == objectName);
            return transform == null ? null : transform.gameObject;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            List<string> names = new List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string FormatVector(Vector3 vector)
        {
            return $"({vector.x:0.###}, {vector.y:0.###}, {vector.z:0.###})";
        }

        private static string FormatColor(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(color);
        }

        private void SavePreferences()
        {
            EditorPrefs.SetInt(PreferencesPrefix + "Room", selectedRoomIndex);
            EditorPrefs.SetBool(PreferencesPrefix + "UseBackgroundFill", useBackgroundFill);
            EditorPrefs.SetBool(PreferencesPrefix + "UseBackWall", useBackWall);
            EditorPrefs.SetFloat(PreferencesPrefix + "BackWallVisualScale", backWallVisualScale);
            EditorPrefs.SetFloat(PreferencesPrefix + "BackWallVerticalOffset", backWallVerticalOffset);
            EditorPrefs.SetFloat(PreferencesPrefix + "BackWallHorizontalBleed", backWallHorizontalBleed);
            EditorPrefs.SetFloat(PreferencesPrefix + "BackWallModuleOverlap", backWallModuleOverlap);
            EditorPrefs.SetString(PreferencesPrefix + "BackWallTint", ColorUtility.ToHtmlStringRGBA(backWallTint));
            EditorPrefs.SetFloat(PreferencesPrefix + "BackgroundHorizontalMargin", backgroundHorizontalSafetyMargin);
            EditorPrefs.SetFloat(PreferencesPrefix + "BackgroundVerticalMargin", backgroundVerticalSafetyMargin);
            EditorPrefs.SetBool(PreferencesPrefix + "UsePlatformVisuals", usePlatformVisuals);
            EditorPrefs.SetBool(PreferencesPrefix + "UseArchitecture", useArchitecture);
            EditorPrefs.SetBool(PreferencesPrefix + "UseMinimalDecoration", useMinimalDecoration);
            EditorPrefs.SetBool(PreferencesPrefix + "HideExistingGroundRenderers", hideExistingGroundRenderers);
            SaveSlots("Royal", royalSlots);
            SaveSlots("Dark", darkSlots);
        }

        private void LoadPreferences()
        {
            string defaultsKey = PreferencesPrefix + "DefaultsV2Applied";
            bool applyV2Defaults = !EditorPrefs.GetBool(defaultsKey, false);
            selectedRoomIndex = Mathf.Clamp(EditorPrefs.GetInt(PreferencesPrefix + "Room", 0), 0, Rooms.Length - 1);
            useBackgroundFill = EditorPrefs.GetBool(PreferencesPrefix + "UseBackgroundFill", true);
            useBackWall = EditorPrefs.GetBool(PreferencesPrefix + "UseBackWall", true);
            backWallVisualScale = EditorPrefs.GetFloat(PreferencesPrefix + "BackWallVisualScale", 0.7f);
            backWallVerticalOffset = EditorPrefs.GetFloat(PreferencesPrefix + "BackWallVerticalOffset", -1.5f);
            backWallHorizontalBleed = EditorPrefs.GetFloat(PreferencesPrefix + "BackWallHorizontalBleed", 0.5f);
            backWallModuleOverlap = EditorPrefs.GetFloat(PreferencesPrefix + "BackWallModuleOverlap", 0.02f);
            backgroundHorizontalSafetyMargin = EditorPrefs.GetFloat(PreferencesPrefix + "BackgroundHorizontalMargin", 2f);
            backgroundVerticalSafetyMargin = EditorPrefs.GetFloat(PreferencesPrefix + "BackgroundVerticalMargin", 1.5f);
            string tintValue = EditorPrefs.GetString(PreferencesPrefix + "BackWallTint", "FFFFFFFF");
            if (ColorUtility.TryParseHtmlString("#" + tintValue, out Color parsedTint))
            {
                backWallTint = parsedTint;
            }
            usePlatformVisuals = EditorPrefs.GetBool(PreferencesPrefix + "UsePlatformVisuals", true);
            useArchitecture = EditorPrefs.GetBool(PreferencesPrefix + "UseArchitecture", false);
            useMinimalDecoration = EditorPrefs.GetBool(PreferencesPrefix + "UseMinimalDecoration", false);
            hideExistingGroundRenderers = EditorPrefs.GetBool(PreferencesPrefix + "HideExistingGroundRenderers", false);
            if (applyV2Defaults)
            {
                useArchitecture = false;
                useMinimalDecoration = false;
                EditorPrefs.SetBool(defaultsKey, true);
            }
            royalSlots = LoadSlots("Royal");
            darkSlots = LoadSlots("Dark");
        }

        private static void SaveSlots(string themeKey, SpriteSlots slots)
        {
            SaveSpritePreference(themeKey + ".BackgroundFill", slots.BackgroundFill);
            EditorPrefs.SetBool(PreferencesPrefix + themeKey + ".UseSolidBackgroundFill", slots.UseSolidBackgroundFill);
            EditorPrefs.SetString(PreferencesPrefix + themeKey + ".SolidBackgroundColor", ColorUtility.ToHtmlStringRGBA(slots.SolidBackgroundColor));
            SaveSpritePreference(themeKey + ".BackWall", slots.BackWall);
            SaveSpritePreference(themeKey + ".Ground", slots.Ground);
            SaveSpritePreference(themeKey + ".Platform", slots.Platform);
            SaveSpritePreference(themeKey + ".Pillar", slots.Pillar);
            SaveSpritePreference(themeKey + ".Arch", slots.Arch);
            SaveSpritePreference(themeKey + ".Window", slots.Window);
        }

        private static SpriteSlots LoadSlots(string themeKey)
        {
            bool isRoyal = string.Equals(themeKey, "Royal", StringComparison.OrdinalIgnoreCase);
            Color defaultSolidColor = isRoyal ? RoyalRecommendedSolidFillColor : Color.white;
            return new SpriteSlots
            {
                BackgroundFill = LoadSpritePreference(themeKey + ".BackgroundFill"),
                UseSolidBackgroundFill = EditorPrefs.GetBool(PreferencesPrefix + themeKey + ".UseSolidBackgroundFill", isRoyal),
                SolidBackgroundColor = LoadColorPreference(themeKey + ".SolidBackgroundColor", defaultSolidColor),
                BackWall = LoadSpritePreference(themeKey + ".BackWall"),
                Ground = LoadSpritePreference(themeKey + ".Ground"),
                Platform = LoadSpritePreference(themeKey + ".Platform"),
                Pillar = LoadSpritePreference(themeKey + ".Pillar"),
                Arch = LoadSpritePreference(themeKey + ".Arch"),
                Window = LoadSpritePreference(themeKey + ".Window")
            };
        }

        private static Color LoadColorPreference(string key, Color fallback)
        {
            string fullKey = PreferencesPrefix + key;
            if (!EditorPrefs.HasKey(fullKey))
            {
                return fallback;
            }
            string html = EditorPrefs.GetString(fullKey, ColorUtility.ToHtmlStringRGBA(fallback));
            return ColorUtility.TryParseHtmlString("#" + html, out Color parsed) ? parsed : fallback;
        }

        private static void SaveSpritePreference(string key, Sprite sprite)
        {
            string fullKey = PreferencesPrefix + key;
            if (sprite == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long localId))
            {
                EditorPrefs.DeleteKey(fullKey);
                return;
            }

            EditorPrefs.SetString(fullKey, guid + "|" + localId.ToString(CultureInfo.InvariantCulture));
        }

        private static Sprite LoadSpritePreference(string key)
        {
            string value = EditorPrefs.GetString(PreferencesPrefix + key, string.Empty);
            string[] parts = value.Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long localId))
            {
                return null;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(parts[0]);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite sprite &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out _, out long candidateLocalId) &&
                    candidateLocalId == localId)
                {
                    return sprite;
                }
            }

            return null;
        }

        [Serializable]
        private sealed class SpriteSlots
        {
            public Sprite BackgroundFill;
            public bool UseSolidBackgroundFill;
            public Color SolidBackgroundColor;
            public Sprite BackWall;
            public Sprite Ground;
            public Sprite Platform;
            public Sprite Pillar;
            public Sprite Arch;
            public Sprite Window;
        }

        private enum BackgroundFillMode
        {
            None,
            Sprite,
            SolidColor
        }

        private enum CastleTheme
        {
            Royal,
            Dark
        }

        private sealed class RoomDefinition
        {
            public RoomDefinition(string displayName, string scenePath, CastleTheme theme)
            {
                DisplayName = displayName;
                ScenePath = scenePath;
                Theme = theme;
            }

            public string DisplayName { get; }
            public string ScenePath { get; }
            public CastleTheme Theme { get; }
        }

        private sealed class SortingPlan
        {
            public SortingPlan(int layerId, int backWallOrder, int architectureOrder, int platformOrder, int decorationOrder)
            {
                LayerId = layerId;
                BackWallOrder = backWallOrder;
                ArchitectureOrder = architectureOrder;
                PlatformOrder = platformOrder;
                DecorationOrder = decorationOrder;
            }

            public int LayerId { get; }
            public int BackWallOrder { get; }
            public int ArchitectureOrder { get; }
            public int PlatformOrder { get; }
            public int DecorationOrder { get; }
        }

        private sealed class GroundRendererState
        {
            public GroundRendererState(SpriteRenderer renderer, bool enabled)
            {
                Renderer = renderer;
                Enabled = enabled;
            }

            public SpriteRenderer Renderer { get; }
            public bool Enabled { get; }
        }

        private sealed class BackgroundLayerBuildResult
        {
            public bool FillEnabled;
            public bool BackWallEnabled;
            public bool FillConfigured;
            public bool FillGenerated;
            public bool FillCoverageOk;
            public BackgroundFillMode FillMode;
            public Color FillColor;
            public Bounds FillBounds;
            public bool BackWallGenerated;
            public bool BackWallHorizontalCoverageOk;
            public Bounds BackWallBounds;
            public int BackWallModuleCount;
            public Vector3 BackWallScale;
        }

        private sealed class DryRunReport
        {
            private readonly StringBuilder builder = new StringBuilder();
            private int errorCount;
            private int warningCount;

            public bool HasErrors => errorCount > 0;

            public DryRunReport(RoomDefinition room)
            {
                builder.AppendLine($"Castle Room Builder Dry Run — {room.DisplayName} / {room.Theme}");
                builder.AppendLine(new string('-', 72));
            }

            public void Success(string category, string message) => Append("OK", category, message);
            public void Info(string category, string message) => Append("INFO", category, message);

            public void Warning(string category, string message)
            {
                warningCount++;
                Append("WARN", category, message);
            }

            public void Error(string category, string message)
            {
                errorCount++;
                Append("ERROR", category, message);
            }

            public void Complete()
            {
                builder.AppendLine(new string('-', 72));
                if (errorCount == 0)
                {
                    builder.AppendLine($"Dry Run 결과: error 0, warning {warningCount}. 프로젝트 파일과 Scene은 변경되지 않았습니다.");
                }
                else
                {
                    builder.AppendLine($"Dry Run 실패: error {errorCount}, warning {warningCount}. 프로젝트 파일과 Scene은 변경되지 않았습니다.");
                }

            }

            public override string ToString() => builder.ToString();

            private void Append(string severity, string category, string message)
            {
                builder.Append('[').Append(severity).Append("][").Append(category).Append("] ").AppendLine(message);
            }
        }
    }
}
















