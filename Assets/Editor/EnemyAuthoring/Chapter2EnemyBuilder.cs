using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace MiniProject.EditorTools.EnemyAuthoring
{
    internal sealed class Chapter2EnemyBuildRequest
    {
        public Texture2D MeleeSource;
        public Texture2D RangedSource;
        public Texture2D ChargeSource;
        public int RangedFireEventFrame = 7;
    }

    internal sealed class Chapter2EnemyBuildReport
    {
        private readonly List<string> lines = new List<string>();
        private readonly List<string> errors = new List<string>();

        public bool IsValid => errors.Count == 0;
        public string Text => string.Join("\n", lines);

        public void Add(string line)
        {
            lines.Add(line);
        }

        public void AddError(string error)
        {
            errors.Add(error);
            lines.Add($"[ERROR] {error}");
        }

        public void AddDryRunSummary()
        {
            lines.Add(string.Empty);
            lines.Add(IsValid
                ? "Dry Run passed. Apply Changes is now available."
                : $"Dry Run failed with {errors.Count} error(s). No files were changed.");
        }

        public void AddApplySummary()
        {
            lines.Add(string.Empty);
            lines.Add(IsValid
                ? "Apply completed successfully."
                : $"Apply failed with {errors.Count} error(s). Review the current C2 output before retrying.");
        }
    }

    internal static class Chapter2EnemyBuilder
    {
        public const string DefaultMeleeSourcePath =
            "Assets/Assets/MinifolksUndead/MinifolksUndead/Without Outline/MiniSkeleton.png";

        public const string DefaultRangedSourcePath =
            "Assets/Assets/MinifolksUndead/MinifolksUndead/Without Outline/MiniSkeletonArcher.png";

        public const string DefaultChargeSourcePath =
            "Assets/Assets/MinifolksUndead/MinifolksUndead/Without Outline/MiniReaper.png";

        private const int CellSize = 32;
        private const float PixelsPerUnit = 16f;
        private const string OutputRoot = "Assets/Arts/Chapter2";
        private const string GeneratedMarkerPrefix = "Chapter2EnemyBuilder:v1";

        private static readonly Vector2 SpritePivot = new Vector2(0.5f, 0.3f);

        private static readonly string[] ProtectedFolderPaths =
        {
            "Assets/Arts/Chapter1",
            "Assets/Scenes"
        };

        private static readonly string[] ProtectedAssetPaths =
        {
            "Assets/Prefabs/Enemy_C1.prefab",
            "Assets/Prefabs/Enemy_Ranged_C1.prefab",
            "Assets/Prefabs/Enemy_Charge.prefab",
            "Assets/Arts/Sprites/MiniSwordMan.png",
            "Assets/Arts/Sprites/MiniSpearMan.png",
            "Assets/Assets/MinifolksHumans/Without Outline/MiniArcherMan.png"
        };

        private static readonly string[] AllowedTargetPrefabPaths =
        {
            "Assets/Prefabs/Enemy_C2.prefab",
            "Assets/Prefabs/Enemy_Ranged_C2.prefab",
            "Assets/Prefabs/Enemy_Charge_C2.prefab"
        };

        private enum EnemyArchetype
        {
            Melee,
            Ranged,
            Charge
        }

        private enum ClipKind
        {
            Idle,
            Walk,
            Attack,
            Hit,
            Death
        }

        private sealed class ClipDefinition
        {
            public ClipKind Kind;
            public int RowFromTop;
            public int[] Columns;
            public float SecondsPerFrame;
            public bool Loop;
            public bool AddFireProjectileEvent;
            public int FireProjectileFrame;
        }

        private sealed class EnemyBuildProfile
        {
            public EnemyArchetype Archetype;
            public Texture2D SourceTexture;
            public string TargetTexturePath;
            public string TargetPrefabPath;
            public string OutputDirectory;
            public string ControllerPath;
            public string AssetNamePrefix;
            public Type ExpectedAiType;
            public ClipDefinition[] Clips;
        }

        public static Chapter2EnemyBuildReport CreateDryRun(
            Chapter2EnemyBuildRequest request)
        {
            var report = new Chapter2EnemyBuildReport();

            report.Add("Chapter 2 Enemy Builder - Dry Run");
            report.Add("Supported: Melee, Ranged, Charge");
            report.Add("Excluded: all Chapter 1 assets and all Scene assets");
            report.Add(string.Empty);

            if (request == null)
            {
                report.AddError("Build request is missing.");
                report.AddDryRunSummary();
                return report;
            }

            EnemyBuildProfile[] profiles = CreateProfiles(request);

            foreach (EnemyBuildProfile profile in profiles)
            {
                ValidateProfile(profile, report);
            }

            report.Add(string.Empty);
            report.Add("[PROTECTED] No AssetPostprocessor, InitializeOnLoad, Scene API, or global AssetDatabase.SaveAssets call will be used.");
            report.Add("[PROTECTED] Chapter 1 and Scene assets are hashed before and after Apply.");
            report.Add("[PROTECTED] Original Asset Pack PNG and .meta files are hashed before and after Apply.");
            report.AddDryRunSummary();

            return report;
        }

        public static Chapter2EnemyBuildReport Apply(
            Chapter2EnemyBuildRequest request)
        {
            Chapter2EnemyBuildReport preflight = CreateDryRun(request);

            if (!preflight.IsValid)
            {
                throw new InvalidOperationException(
                    "Dry Run validation failed. No changes were applied.\n\n" +
                    preflight.Text);
            }

            EnemyBuildProfile[] profiles = CreateProfiles(request);
            Dictionary<string, string> protectedBefore =
                CaptureProtectedFileHashes(request);

            var result = new Chapter2EnemyBuildReport();
            result.Add("Chapter 2 Enemy Builder - Apply Result");

            foreach (EnemyBuildProfile profile in profiles)
            {
                BuildProfile(profile, result);
            }

            Dictionary<string, string> protectedAfter =
                CaptureProtectedFileHashes(request);

            List<string> protectedChanges = FindHashChanges(
                protectedBefore,
                protectedAfter);

            if (protectedChanges.Count > 0)
            {
                throw new InvalidOperationException(
                    "Protected Chapter 1, Scene, or source Asset Pack files changed unexpectedly:\n" +
                    string.Join("\n", protectedChanges) +
                    "\nC2 output may already have been written before this post-Apply check.");
            }

            result.Add(string.Empty);
            result.Add("[VERIFIED] Protected Chapter 1 assets were not changed.");
            result.Add("[VERIFIED] Scene files were not changed.");
            result.Add("[VERIFIED] Original Asset Pack PNG and .meta files were not changed.");
            result.AddApplySummary();

            return result;
        }

        private static EnemyBuildProfile[] CreateProfiles(
            Chapter2EnemyBuildRequest request)
        {
            return new[]
            {
                new EnemyBuildProfile
                {
                    Archetype = EnemyArchetype.Melee,
                    SourceTexture = request.MeleeSource,
                    TargetTexturePath =
                        "Assets/Arts/Chapter2/Melee/MiniSkeleton_C2.png",
                    TargetPrefabPath =
                        "Assets/Prefabs/Enemy_C2.prefab",
                    OutputDirectory =
                        "Assets/Arts/Chapter2/Melee",
                    ControllerPath =
                        "Assets/Arts/Chapter2/Melee/Enemy_C2.controller",
                    AssetNamePrefix = "Enemy_C2",
                    ExpectedAiType = typeof(EnemyAI),
                    Clips = new[]
                    {
                        CreateClip(ClipKind.Idle, 0, 0.25f, true, 0, 1, 2, 3),
                        CreateClip(ClipKind.Walk, 1, 0.2f, true, 0, 1, 2, 3, 4, 5),
                        CreateClip(ClipKind.Attack, 3, 1f / 6f, false, 0, 1, 2, 3, 4),
                        CreateClip(ClipKind.Hit, 4, 0.25f, false, 0, 1, 2, 3),
                        CreateClip(ClipKind.Death, 5, 0.25f, false, 0, 1, 2, 3, 4, 5)
                    }
                },
                new EnemyBuildProfile
                {
                    Archetype = EnemyArchetype.Ranged,
                    SourceTexture = request.RangedSource,
                    TargetTexturePath =
                        "Assets/Arts/Chapter2/Ranged/MiniSkeletonArcher_C2.png",
                    TargetPrefabPath =
                        "Assets/Prefabs/Enemy_Ranged_C2.prefab",
                    OutputDirectory =
                        "Assets/Arts/Chapter2/Ranged",
                    ControllerPath =
                        "Assets/Arts/Chapter2/Ranged/Enemy_Ranged_C2.controller",
                    AssetNamePrefix = "Enemy_Ranged_C2",
                    ExpectedAiType = typeof(RangedEnemyAI),
                    Clips = new[]
                    {
                        CreateClip(ClipKind.Idle, 0, 0.25f, true, 0, 1, 2, 3),
                        CreateClip(ClipKind.Walk, 1, 0.2f, true, 0, 1, 2, 3, 4, 5),
                        CreateRangedAttackClip(request.RangedFireEventFrame),
                        CreateClip(ClipKind.Hit, 5, 0.25f, false, 0, 1, 2),
                        CreateClip(ClipKind.Death, 6, 0.25f, false, 0, 1, 2, 3, 4, 5)
                    }
                },
                new EnemyBuildProfile
                {
                    Archetype = EnemyArchetype.Charge,
                    SourceTexture = request.ChargeSource,
                    TargetTexturePath =
                        "Assets/Arts/Chapter2/Charge/MiniReaper_C2.png",
                    TargetPrefabPath =
                        "Assets/Prefabs/Enemy_Charge_C2.prefab",
                    OutputDirectory =
                        "Assets/Arts/Chapter2/Charge",
                    ControllerPath =
                        "Assets/Arts/Chapter2/Charge/Enemy_Charge_C2.controller",
                    AssetNamePrefix = "Enemy_Charge_C2",
                    ExpectedAiType = typeof(ChargeEnemyAI),
                    Clips = new[]
                    {
                        CreateClip(ClipKind.Idle, 0, 0.25f, true, 0, 1, 2, 3),
                        CreateClip(ClipKind.Walk, 1, 0.2f, true, 0, 1, 2, 3, 4, 5),
                        CreateClip(ClipKind.Attack, 2, 0.15f, false, 0, 1, 2, 3, 4, 5, 6, 7),
                        CreateClip(ClipKind.Hit, 3, 0.25f, false, 0, 1),
                        CreateClip(ClipKind.Death, 4, 0.25f, false, 0, 1, 2, 3, 4, 5)
                    }
                }
            };
        }

        private static ClipDefinition CreateClip(
            ClipKind kind,
            int rowFromTop,
            float secondsPerFrame,
            bool loop,
            params int[] columns)
        {
            return new ClipDefinition
            {
                Kind = kind,
                RowFromTop = rowFromTop,
                Columns = columns,
                SecondsPerFrame = secondsPerFrame,
                Loop = loop,
                AddFireProjectileEvent = false,
                FireProjectileFrame = -1
            };
        }

        private static ClipDefinition CreateRangedAttackClip(
            int fireProjectileFrame)
        {
            ClipDefinition clip = CreateClip(
                ClipKind.Attack,
                3,
                0.15f,
                false,
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9);

            clip.AddFireProjectileEvent = true;
            clip.FireProjectileFrame = fireProjectileFrame;

            return clip;
        }

        private static void ValidateProfile(
            EnemyBuildProfile profile,
            Chapter2EnemyBuildReport report)
        {
            report.Add($"[{profile.Archetype}]");

            if (profile.SourceTexture == null)
            {
                report.AddError($"{profile.Archetype}: Source Sprite Sheet is not assigned.");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(profile.SourceTexture);

            if (string.IsNullOrWhiteSpace(sourcePath) ||
                !sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                report.AddError($"{profile.Archetype}: Source must be a PNG asset inside this Unity project.");
                return;
            }

            if (IsProtectedPath(sourcePath) ||
                IsPathInside(sourcePath, OutputRoot))
            {
                report.AddError($"{profile.Archetype}: Source cannot be a Chapter 1, Scene, or generated Chapter 2 asset: {sourcePath}");
                return;
            }

            if (profile.SourceTexture.width % CellSize != 0 ||
                profile.SourceTexture.height % CellSize != 0)
            {
                report.AddError($"{profile.Archetype}: Texture dimensions must be multiples of 32. Current: {profile.SourceTexture.width}x{profile.SourceTexture.height}");
            }

            TextureImporter sourceImporter =
                AssetImporter.GetAtPath(sourcePath) as TextureImporter;

            if (sourceImporter == null)
            {
                report.AddError($"{profile.Archetype}: TextureImporter was not found for {sourcePath}");
                return;
            }

            SpriteRect[] sourceRects = GetSpriteRects(sourceImporter);

            if (sourceRects.Length == 0)
            {
                report.AddError($"{profile.Archetype}: Source has no existing Sprite slices to normalize safely.");
                return;
            }

            ValidateUniqueNormalizedCells(profile, sourceRects, report);
            ValidateRequiredFrames(profile, sourceRects, report);
            ValidateTargetAssets(profile, sourcePath, report);
            ValidateTargetPrefab(profile, report);

            report.Add($"  [SOURCE READ ONLY] {sourcePath}");
            report.Add($"  {GetCreateOrUpdateLabel(profile.TargetTexturePath)} {profile.TargetTexturePath}");

            foreach (ClipDefinition clip in profile.Clips)
            {
                string clipPath = GetClipPath(profile, clip.Kind);
                string eventText = clip.AddFireProjectileEvent
                    ? $", FireProjectile frame={clip.FireProjectileFrame}"
                    : string.Empty;

                report.Add(
                    $"  {GetCreateOrUpdateLabel(clipPath)} {clipPath} " +
                    $"({clip.Columns.Length} frames, loop={clip.Loop}{eventText})");
            }

            report.Add($"  {GetCreateOrUpdateLabel(profile.ControllerPath)} {profile.ControllerPath}");
            report.Add($"  [VISUAL UPDATE ONLY] {profile.TargetPrefabPath}: Sprite, Color, Animator, Controller");
            report.Add("  [GAMEPLAY PRESERVED] All other components, hierarchy, serialized values, colliders, and child transforms");
            report.Add(string.Empty);
        }

        private static void ValidateUniqueNormalizedCells(
            EnemyBuildProfile profile,
            SpriteRect[] rects,
            Chapter2EnemyBuildReport report)
        {
            var occupied = new HashSet<Vector2Int>();

            foreach (SpriteRect spriteRect in rects)
            {
                Vector2Int cell = GetCell(spriteRect.rect);

                if (!occupied.Add(cell))
                {
                    report.AddError($"{profile.Archetype}: Multiple source slices map to 32x32 cell ({cell.x}, {cell.y}).");
                }
            }
        }

        private static void ValidateRequiredFrames(
            EnemyBuildProfile profile,
            SpriteRect[] rects,
            Chapter2EnemyBuildReport report)
        {
            var cells = new HashSet<Vector2Int>(
                rects.Select(rect => GetCell(rect.rect)));

            int rowCount = profile.SourceTexture.height / CellSize;
            int columnCount = profile.SourceTexture.width / CellSize;

            foreach (ClipDefinition clip in profile.Clips)
            {
                if (clip.RowFromTop < 0 || clip.RowFromTop >= rowCount)
                {
                    report.AddError($"{profile.Archetype} {clip.Kind}: Row {clip.RowFromTop} is outside the texture.");
                    continue;
                }

                int cellY = rowCount - clip.RowFromTop - 1;

                foreach (int column in clip.Columns)
                {
                    if (column < 0 || column >= columnCount)
                    {
                        report.AddError($"{profile.Archetype} {clip.Kind}: Column {column} is outside the texture.");
                        continue;
                    }

                    if (!cells.Contains(new Vector2Int(column, cellY)))
                    {
                        report.AddError($"{profile.Archetype} {clip.Kind}: No source sprite exists in cell ({column}, {cellY}).");
                    }
                }

                if (clip.AddFireProjectileEvent &&
                    (clip.FireProjectileFrame < 0 ||
                     clip.FireProjectileFrame >= clip.Columns.Length))
                {
                    report.AddError($"{profile.Archetype} Attack: FireProjectile frame must be between 0 and {clip.Columns.Length - 1}.");
                }
            }
        }

        private static void ValidateTargetAssets(
            EnemyBuildProfile profile,
            string sourcePath,
            Chapter2EnemyBuildReport report)
        {
            ValidateGeneratedAssetType<Texture2D>(
                profile.TargetTexturePath,
                profile,
                sourcePath,
                report);

            foreach (ClipDefinition clip in profile.Clips)
            {
                ValidateGeneratedAssetType<AnimationClip>(
                    GetClipPath(profile, clip.Kind),
                    profile,
                    sourcePath,
                    report,
                    false);
            }

            ValidateGeneratedAssetType<AnimatorController>(
                profile.ControllerPath,
                profile,
                sourcePath,
                report,
                false);
        }

        private static void ValidateGeneratedAssetType<T>(
            string path,
            EnemyBuildProfile profile,
            string sourcePath,
            Chapter2EnemyBuildReport report,
            bool validateMarker = true)
            where T : UnityEngine.Object
        {
            if (IsProtectedPath(path) || !IsPathInside(path, OutputRoot))
            {
                report.AddError($"{profile.Archetype}: Generated path is outside the protected Chapter 2 output root: {path}");
                return;
            }

            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);

            if (existing == null)
                return;

            if (!(existing is T))
            {
                report.AddError($"{profile.Archetype}: Existing asset has an unexpected type at {path}");
                return;
            }

            if (!validateMarker)
                return;

            AssetImporter importer = AssetImporter.GetAtPath(path);
            string expectedMarker = GetGeneratedMarker(profile, sourcePath);

            if (importer == null ||
                !string.Equals(importer.userData, expectedMarker, StringComparison.Ordinal))
            {
                report.AddError($"{profile.Archetype}: Existing texture is not owned by this builder and will not be overwritten: {path}");
            }
        }

        private static void ValidateTargetPrefab(
            EnemyBuildProfile profile,
            Chapter2EnemyBuildReport report)
        {
            if (!AllowedTargetPrefabPaths.Contains(profile.TargetPrefabPath) ||
                IsProtectedPath(profile.TargetPrefabPath))
            {
                report.AddError($"{profile.Archetype}: Target Prefab is not an allowed Chapter 2 Prefab: {profile.TargetPrefabPath}");
                return;
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(profile.TargetPrefabPath);

            if (prefab == null ||
                PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            {
                report.AddError($"{profile.Archetype}: Target Prefab was not found: {profile.TargetPrefabPath}");
                return;
            }

            if (prefab.GetComponent<SpriteRenderer>() == null)
            {
                report.AddError($"{profile.Archetype}: Target Prefab root has no SpriteRenderer.");
            }

            if (prefab.GetComponent(profile.ExpectedAiType) == null)
            {
                report.AddError($"{profile.Archetype}: Target Prefab root does not contain {profile.ExpectedAiType.Name}.");
            }

            if (prefab.GetComponents<Animator>().Length > 1)
            {
                report.AddError($"{profile.Archetype}: Target Prefab root has multiple Animator components.");
            }

            if (profile.Archetype == EnemyArchetype.Ranged)
            {
                RangedEnemyAI rangedAi = prefab.GetComponent<RangedEnemyAI>();

                if (rangedAi != null)
                {
                    var serializedAi = new SerializedObject(rangedAi);
                    SerializedProperty firePoint = serializedAi.FindProperty("firePoint");
                    SerializedProperty projectilePrefab = serializedAi.FindProperty("projectilePrefab");

                    if (firePoint == null || firePoint.objectReferenceValue == null)
                    {
                        report.AddError("Ranged: Existing C2 Prefab has no FirePoint reference. The builder will not repair gameplay references.");
                    }

                    if (projectilePrefab == null || projectilePrefab.objectReferenceValue == null)
                    {
                        report.AddError("Ranged: Existing C2 Prefab has no Projectile Prefab reference. The builder will not repair gameplay references.");
                    }
                }

                if (typeof(RangedEnemyAI).GetMethod("FireProjectile") == null)
                {
                    report.AddError("RangedEnemyAI.FireProjectile() was not found.");
                }
            }
        }

        private static void BuildProfile(
            EnemyBuildProfile profile,
            Chapter2EnemyBuildReport report)
        {
            string sourcePath = AssetDatabase.GetAssetPath(profile.SourceTexture);

            EnsureAssetFolder(profile.OutputDirectory);
            PrepareTextureCopy(profile, sourcePath);

            Dictionary<ClipKind, Sprite[]> spritesByClip =
                ResolveClipSprites(profile);

            Dictionary<ClipKind, AnimationClip> clips =
                CreateOrUpdateClips(profile, spritesByClip);

            AnimatorController controller =
                CreateOrUpdateController(profile, clips);

            ApplyVisualsToPrefab(
                profile,
                spritesByClip[ClipKind.Idle][0],
                controller);

            report.Add($"[{profile.Archetype}] Built {profile.TargetTexturePath}");
            report.Add($"[{profile.Archetype}] Built 5 Animation Clips and {profile.ControllerPath}");
            report.Add($"[{profile.Archetype}] Applied visual-only changes to {profile.TargetPrefabPath}");
        }

        private static void PrepareTextureCopy(
            EnemyBuildProfile profile,
            string sourcePath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(profile.TargetTexturePath) == null)
            {
                if (!AssetDatabase.CopyAsset(sourcePath, profile.TargetTexturePath))
                {
                    throw new InvalidOperationException(
                        $"Failed to copy source texture to {profile.TargetTexturePath}");
                }
            }

            TextureImporter importer =
                AssetImporter.GetAtPath(profile.TargetTexturePath) as TextureImporter;

            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"TextureImporter was not found at {profile.TargetTexturePath}");
            }

            string marker = GetGeneratedMarker(profile, sourcePath);

            if (!string.IsNullOrEmpty(importer.userData) &&
                !string.Equals(importer.userData, marker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to overwrite a texture not owned by this builder: {profile.TargetTexturePath}");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.userData = marker;

            TextureImporterPlatformSettings defaultSettings =
                importer.GetDefaultPlatformTextureSettings();

            defaultSettings.textureCompression =
                TextureImporterCompression.Uncompressed;
            defaultSettings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(defaultSettings);

            ClearPlatformOverride(importer, "Standalone");
            ClearPlatformOverride(importer, "Android");
            ClearPlatformOverride(importer, "iPhone");
            ClearPlatformOverride(importer, "WebGL");
            ClearPlatformOverride(importer, "Windows Store Apps");

            importer.SaveAndReimport();

            SpriteRect[] rects = GetSpriteRects(importer);
            var occupied = new HashSet<Vector2Int>();

            for (int i = 0; i < rects.Length; i++)
            {
                SpriteRect spriteRect = rects[i];
                Vector2Int cell = GetCell(spriteRect.rect);

                if (!occupied.Add(cell))
                {
                    throw new InvalidOperationException(
                        $"Multiple sprites map to cell ({cell.x}, {cell.y}) in {profile.TargetTexturePath}");
                }

                Rect normalizedRect = new Rect(
                    cell.x * CellSize,
                    cell.y * CellSize,
                    CellSize,
                    CellSize);

                if (normalizedRect.xMax > profile.SourceTexture.width ||
                    normalizedRect.yMax > profile.SourceTexture.height)
                {
                    throw new InvalidOperationException(
                        $"Normalized slice is outside the texture: {spriteRect.name}");
                }

                spriteRect.rect = normalizedRect;
                spriteRect.alignment = SpriteAlignment.Custom;
                spriteRect.pivot = SpritePivot;
                rects[i] = spriteRect;
            }

            ISpriteEditorDataProvider dataProvider =
                GetSpriteDataProvider(importer);

            dataProvider.SetSpriteRects(rects);
            dataProvider.Apply();
            importer.SaveAndReimport();
        }

        private static void ClearPlatformOverride(
            TextureImporter importer,
            string platformName)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platformName);

            if (!settings.overridden)
                return;

            settings.overridden = false;
            importer.SetPlatformTextureSettings(settings);
        }

        private static Dictionary<ClipKind, Sprite[]> ResolveClipSprites(
            EnemyBuildProfile profile)
        {
            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(profile.TargetTexturePath);
            TextureImporter importer =
                AssetImporter.GetAtPath(profile.TargetTexturePath) as TextureImporter;

            if (texture == null || importer == null)
            {
                throw new InvalidOperationException(
                    $"Generated texture could not be loaded: {profile.TargetTexturePath}");
            }

            SpriteRect[] rects = GetSpriteRects(importer);
            var rectByCell = rects.ToDictionary(
                rect => GetCell(rect.rect),
                rect => rect);

            var spriteByName = AssetDatabase
                .LoadAllAssetRepresentationsAtPath(profile.TargetTexturePath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name, sprite => sprite);

            int rowCount = texture.height / CellSize;
            var result = new Dictionary<ClipKind, Sprite[]>();

            foreach (ClipDefinition clip in profile.Clips)
            {
                int cellY = rowCount - clip.RowFromTop - 1;
                var sprites = new Sprite[clip.Columns.Length];

                for (int i = 0; i < clip.Columns.Length; i++)
                {
                    var cell = new Vector2Int(clip.Columns[i], cellY);

                    if (!rectByCell.TryGetValue(cell, out SpriteRect spriteRect) ||
                        !spriteByName.TryGetValue(spriteRect.name, out Sprite sprite))
                    {
                        throw new InvalidOperationException(
                            $"Could not resolve {profile.Archetype} {clip.Kind} frame at cell ({cell.x}, {cell.y}).");
                    }

                    sprites[i] = sprite;
                }

                result.Add(clip.Kind, sprites);
            }

            return result;
        }

        private static Dictionary<ClipKind, AnimationClip> CreateOrUpdateClips(
            EnemyBuildProfile profile,
            Dictionary<ClipKind, Sprite[]> spritesByClip)
        {
            var result = new Dictionary<ClipKind, AnimationClip>();

            foreach (ClipDefinition definition in profile.Clips)
            {
                string clipPath = GetClipPath(profile, definition.Kind);
                AnimationClip clip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, clipPath);
                }

                clip.name = Path.GetFileNameWithoutExtension(clipPath);
                clip.frameRate = 60f;
                clip.legacy = false;
                clip.wrapMode = definition.Loop
                    ? WrapMode.Loop
                    : WrapMode.Once;

                Sprite[] sprites = spritesByClip[definition.Kind];
                var keyframes = new ObjectReferenceKeyframe[sprites.Length];

                for (int i = 0; i < sprites.Length; i++)
                {
                    keyframes[i] = new ObjectReferenceKeyframe
                    {
                        time = i * definition.SecondsPerFrame,
                        value = sprites[i]
                    };
                }

                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
                    string.Empty,
                    typeof(SpriteRenderer),
                    "m_Sprite");

                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    binding,
                    keyframes);

                SetClipLoop(clip, definition.Loop);
                SetClipEvents(clip, definition);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);

                VerifyClip(clip, definition, sprites.Length);
                result.Add(definition.Kind, clip);
            }

            return result;
        }

        private static void SetClipLoop(
            AnimationClip clip,
            bool loop)
        {
            var serializedClip = new SerializedObject(clip);
            SerializedProperty loopTime = serializedClip.FindProperty(
                "m_AnimationClipSettings.m_LoopTime");

            if (loopTime == null)
            {
                throw new InvalidOperationException(
                    $"Loop setting was not found for {clip.name}");
            }

            loopTime.boolValue = loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetClipEvents(
            AnimationClip clip,
            ClipDefinition definition)
        {
            if (!definition.AddFireProjectileEvent)
            {
                AnimationUtility.SetAnimationEvents(
                    clip,
                    Array.Empty<AnimationEvent>());
                return;
            }

            float eventTime =
                definition.FireProjectileFrame *
                definition.SecondsPerFrame;

            var fireEvent = new AnimationEvent
            {
                time = eventTime,
                functionName = "FireProjectile",
                messageOptions = SendMessageOptions.RequireReceiver
            };

            AnimationUtility.SetAnimationEvents(
                clip,
                new[] { fireEvent });
        }

        private static void VerifyClip(
            AnimationClip clip,
            ClipDefinition definition,
            int expectedFrameCount)
        {
            EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
                string.Empty,
                typeof(SpriteRenderer),
                "m_Sprite");

            ObjectReferenceKeyframe[] frames =
                AnimationUtility.GetObjectReferenceCurve(clip, binding);

            if (frames == null || frames.Length != expectedFrameCount)
            {
                throw new InvalidOperationException(
                    $"Unexpected frame count in {clip.name}. Expected {expectedFrameCount}.");
            }

            AnimationEvent[] events =
                AnimationUtility.GetAnimationEvents(clip);

            if (definition.AddFireProjectileEvent)
            {
                int fireEventCount = events.Count(
                    animationEvent =>
                        animationEvent.functionName == "FireProjectile");

                if (events.Length != 1 || fireEventCount != 1)
                {
                    throw new InvalidOperationException(
                        $"{clip.name} must contain exactly one FireProjectile event.");
                }
            }
            else if (events.Length != 0)
            {
                throw new InvalidOperationException(
                    $"{clip.name} contains an unexpected Animation Event.");
            }
        }

        private static AnimatorController CreateOrUpdateController(
            EnemyBuildProfile profile,
            Dictionary<ClipKind, AnimationClip> clips)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    profile.ControllerPath);

            if (controller == null)
            {
                controller =
                    AnimatorController.CreateAnimatorControllerAtPath(
                        profile.ControllerPath);
            }

            if (controller == null || controller.layers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Controller must contain exactly one layer: {profile.ControllerPath}");
            }

            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;

            if (stateMachine.stateMachines.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Nested state machines are not supported in generated controller: {profile.ControllerPath}");
            }

            foreach (AnimatorStateTransition transition in
                     stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            foreach (ChildAnimatorState childState in
                     stateMachine.states.ToArray())
            {
                foreach (AnimatorStateTransition transition in
                         childState.state.transitions.ToArray())
                {
                    childState.state.RemoveTransition(transition);
                }
            }

            stateMachine.defaultState = null;

            foreach (ChildAnimatorState childState in
                     stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(childState.state);
            }

            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                controller.RemoveParameter(i);
            }

            controller.AddParameter(
                "IsMoving",
                AnimatorControllerParameterType.Bool);
            controller.AddParameter(
                "Attack",
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                "Hit",
                AnimatorControllerParameterType.Trigger);
            controller.AddParameter(
                "Death",
                AnimatorControllerParameterType.Trigger);

            AnimatorState idle = stateMachine.AddState("Idle");
            AnimatorState walk = stateMachine.AddState("Walk");
            AnimatorState attack = stateMachine.AddState("Attack");
            AnimatorState hit = stateMachine.AddState("Hit");
            AnimatorState death = stateMachine.AddState("Death");

            idle.motion = clips[ClipKind.Idle];
            walk.motion = clips[ClipKind.Walk];
            attack.motion = clips[ClipKind.Attack];
            hit.motion = clips[ClipKind.Hit];
            death.motion = clips[ClipKind.Death];

            stateMachine.defaultState = idle;

            ConfigureConditionTransition(
                idle.AddTransition(walk),
                "IsMoving",
                AnimatorConditionMode.If);

            ConfigureConditionTransition(
                walk.AddTransition(idle),
                "IsMoving",
                AnimatorConditionMode.IfNot);

            ConfigureTriggerTransition(
                stateMachine.AddAnyStateTransition(death),
                "Death");

            ConfigureTriggerTransition(
                stateMachine.AddAnyStateTransition(hit),
                "Hit");

            ConfigureTriggerTransition(
                stateMachine.AddAnyStateTransition(attack),
                "Attack");

            ConfigureExitTransition(attack.AddTransition(idle));
            ConfigureExitTransition(hit.AddTransition(idle));

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(stateMachine);
            AssetDatabase.SaveAssetIfDirty(controller);

            VerifyController(controller);
            return controller;
        }

        private static void ConfigureConditionTransition(
            AnimatorStateTransition transition,
            string parameter,
            AnimatorConditionMode mode)
        {
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.hasFixedDuration = true;
            transition.AddCondition(mode, 0f, parameter);
        }

        private static void ConfigureTriggerTransition(
            AnimatorStateTransition transition,
            string parameter)
        {
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = true;
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                parameter);
        }

        private static void ConfigureExitTransition(
            AnimatorStateTransition transition)
        {
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
            transition.hasFixedDuration = true;
        }

        private static void VerifyController(
            AnimatorController controller)
        {
            Dictionary<string, AnimatorControllerParameterType> expected =
                new Dictionary<string, AnimatorControllerParameterType>
                {
                    { "IsMoving", AnimatorControllerParameterType.Bool },
                    { "Attack", AnimatorControllerParameterType.Trigger },
                    { "Hit", AnimatorControllerParameterType.Trigger },
                    { "Death", AnimatorControllerParameterType.Trigger }
                };

            if (controller.parameters.Length != expected.Count)
            {
                throw new InvalidOperationException(
                    $"Unexpected parameter count in {controller.name}");
            }

            foreach (AnimatorControllerParameter parameter in
                     controller.parameters)
            {
                if (!expected.TryGetValue(parameter.name, out
                        AnimatorControllerParameterType expectedType) ||
                    parameter.type != expectedType)
                {
                    throw new InvalidOperationException(
                        $"Unexpected Animator parameter in {controller.name}: {parameter.name}");
                }
            }

            AnimatorStateMachine stateMachine =
                controller.layers[0].stateMachine;

            HashSet<string> stateNames = new HashSet<string>(
                stateMachine.states.Select(child => child.state.name));

            string[] expectedStates =
                { "Idle", "Walk", "Attack", "Hit", "Death" };

            if (!expectedStates.All(stateNames.Contains) ||
                stateNames.Count != expectedStates.Length)
            {
                throw new InvalidOperationException(
                    $"Unexpected state graph in {controller.name}");
            }
        }

        private static void ApplyVisualsToPrefab(
            EnemyBuildProfile profile,
            Sprite idleSprite,
            AnimatorController controller)
        {
            if (!AllowedTargetPrefabPaths.Contains(profile.TargetPrefabPath) ||
                IsProtectedPath(profile.TargetPrefabPath))
            {
                throw new InvalidOperationException(
                    $"Refusing to modify non-C2 Prefab: {profile.TargetPrefabPath}");
            }

            GameObject root =
                PrefabUtility.LoadPrefabContents(profile.TargetPrefabPath);

            try
            {
                Dictionary<string, string> gameplayBefore =
                    CaptureNonVisualPrefabState(root);

                SpriteRenderer spriteRenderer =
                    root.GetComponent<SpriteRenderer>();

                if (spriteRenderer == null)
                {
                    throw new InvalidOperationException(
                        $"Target Prefab root has no SpriteRenderer: {profile.TargetPrefabPath}");
                }

                Animator[] animators = root.GetComponents<Animator>();

                if (animators.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"Target Prefab root has multiple Animator components: {profile.TargetPrefabPath}");
                }

                Animator animator = animators.Length == 1
                    ? animators[0]
                    : root.AddComponent<Animator>();

                spriteRenderer.sprite = idleSprite;
                spriteRenderer.color = Color.white;

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Dictionary<string, string> gameplayAfter =
                    CaptureNonVisualPrefabState(root);

                List<string> gameplayChanges =
                    FindStateChanges(gameplayBefore, gameplayAfter);

                if (gameplayChanges.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Visual update changed gameplay or hierarchy state:\n" +
                        string.Join("\n", gameplayChanges));
                }

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    profile.TargetPrefabPath,
                    out bool savedSuccessfully);

                if (!savedSuccessfully)
                {
                    throw new InvalidOperationException(
                        $"Failed to save Prefab: {profile.TargetPrefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Dictionary<string, string> CaptureNonVisualPrefabState(
            GameObject root)
        {
            var state = new Dictionary<string, string>();

            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                string hierarchyPath = GetHierarchyPath(root.transform, transform);
                GameObject gameObject = transform.gameObject;

                state[$"GameObject:{hierarchyPath}"] =
                    $"name={gameObject.name}|active={gameObject.activeSelf}|layer={gameObject.layer}|tag={gameObject.tag}";

                Component[] components = gameObject.GetComponents<Component>();
                var typeCounts = new Dictionary<Type, int>();

                foreach (Component component in components)
                {
                    if (component == null ||
                        component is SpriteRenderer ||
                        component is Animator)
                    {
                        continue;
                    }

                    Type type = component.GetType();
                    typeCounts.TryGetValue(type, out int typeIndex);
                    typeCounts[type] = typeIndex + 1;

                    string key =
                        $"Component:{hierarchyPath}:{type.FullName}:{typeIndex}";

                    state[key] = EditorJsonUtility.ToJson(component, true);
                }
            }

            return state;
        }

        private static string GetHierarchyPath(
            Transform root,
            Transform current)
        {
            var segments = new Stack<string>();
            Transform cursor = current;

            while (cursor != null)
            {
                segments.Push($"{cursor.GetSiblingIndex()}:{cursor.name}");

                if (cursor == root)
                    break;

                cursor = cursor.parent;
            }

            return string.Join("/", segments);
        }

        private static List<string> FindStateChanges(
            Dictionary<string, string> before,
            Dictionary<string, string> after)
        {
            var changes = new List<string>();
            var allKeys = new HashSet<string>(before.Keys);
            allKeys.UnionWith(after.Keys);

            foreach (string key in allKeys.OrderBy(value => value))
            {
                bool hadBefore = before.TryGetValue(key, out string beforeValue);
                bool hasAfter = after.TryGetValue(key, out string afterValue);

                if (!hadBefore || !hasAfter ||
                    !string.Equals(beforeValue, afterValue, StringComparison.Ordinal))
                {
                    changes.Add(key);
                }
            }

            return changes;
        }

        private static Dictionary<string, string> CaptureProtectedFileHashes(
            Chapter2EnemyBuildRequest request)
        {
            var seedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string folder in ProtectedFolderPaths)
            {
                seedPaths.Add(folder);

                foreach (string guid in AssetDatabase.FindAssets(
                             string.Empty,
                             new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    seedPaths.Add(path);
                }
            }

            foreach (string path in ProtectedAssetPaths)
            {
                seedPaths.Add(path);
            }

            AddSourcePath(request?.MeleeSource, seedPaths);
            AddSourcePath(request?.RangedSource, seedPaths);
            AddSourcePath(request?.ChargeSource, seedPaths);

            var hashes = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (string assetPath in seedPaths)
            {
                AddFileHashIfPresent(assetPath, hashes);
                AddFileHashIfPresent(assetPath + ".meta", hashes);
            }

            return hashes;
        }

        private static void AddSourcePath(
            Texture2D texture,
            HashSet<string> paths)
        {
            if (texture == null)
                return;

            string path = AssetDatabase.GetAssetPath(texture);

            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        private static void AddFileHashIfPresent(
            string assetPath,
            Dictionary<string, string> hashes)
        {
            string fullPath = GetFullPath(assetPath);

            if (!File.Exists(fullPath))
                return;

            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(fullPath))
            {
                byte[] hash = sha.ComputeHash(stream);
                hashes[assetPath] = ToHex(hash);
            }
        }

        private static List<string> FindHashChanges(
            Dictionary<string, string> before,
            Dictionary<string, string> after)
        {
            var changes = new List<string>();
            var paths = new HashSet<string>(
                before.Keys,
                StringComparer.OrdinalIgnoreCase);

            paths.UnionWith(after.Keys);

            foreach (string path in paths.OrderBy(value => value))
            {
                bool hadBefore = before.TryGetValue(path, out string beforeHash);
                bool hasAfter = after.TryGetValue(path, out string afterHash);

                if (!hadBefore || !hasAfter ||
                    !string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
                {
                    changes.Add(path);
                }
            }

            return changes;
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);

            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        private static SpriteRect[] GetSpriteRects(
            TextureImporter importer)
        {
            return GetSpriteDataProvider(importer).GetSpriteRects();
        }

        private static ISpriteEditorDataProvider GetSpriteDataProvider(
            TextureImporter importer)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();

            ISpriteEditorDataProvider dataProvider =
                factory.GetSpriteEditorDataProviderFromObject(importer);

            if (dataProvider == null)
            {
                throw new InvalidOperationException(
                    $"Sprite data provider was not found for {importer.assetPath}");
            }

            dataProvider.InitSpriteEditorDataProvider();
            return dataProvider;
        }

        private static Vector2Int GetCell(Rect rect)
        {
            return new Vector2Int(
                Mathf.FloorToInt(rect.x / CellSize),
                Mathf.FloorToInt(rect.y / CellSize));
        }

        private static string GetClipPath(
            EnemyBuildProfile profile,
            ClipKind kind)
        {
            return $"{profile.OutputDirectory}/{profile.AssetNamePrefix}_{kind}.anim";
        }

        private static string GetGeneratedMarker(
            EnemyBuildProfile profile,
            string sourcePath)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            return $"{GeneratedMarkerPrefix}:{profile.Archetype}:{sourceGuid}";
        }

        private static string GetCreateOrUpdateLabel(string assetPath)
        {
            return AssetDatabase.LoadMainAssetAtPath(assetPath) == null
                ? "[CREATE]"
                : "[UPDATE]";
        }

        private static bool IsProtectedPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return true;

            string normalized = assetPath.Replace('\\', '/');

            if (ProtectedFolderPaths.Any(
                    folder => IsPathInside(normalized, folder)))
            {
                return true;
            }

            return ProtectedAssetPaths.Any(
                path => string.Equals(
                    normalized,
                    path,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPathInside(
            string assetPath,
            string folderPath)
        {
            string normalizedPath = assetPath
                .Replace('\\', '/')
                .TrimEnd('/');
            string normalizedFolder = folderPath
                .Replace('\\', '/')
                .TrimEnd('/');

            return string.Equals(
                       normalizedPath,
                       normalizedFolder,
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(
                       normalizedFolder + "/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string normalized = folderPath.Replace('\\', '/').Trim('/');
            string[] segments = normalized.Split('/');

            if (segments.Length == 0 || segments[0] != "Assets")
            {
                throw new InvalidOperationException(
                    $"Asset folder must be inside Assets: {folderPath}");
            }

            string current = "Assets";

            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string GetFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
