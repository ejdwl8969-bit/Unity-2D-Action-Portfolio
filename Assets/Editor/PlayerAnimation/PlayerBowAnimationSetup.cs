using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class PlayerBowAnimationSetup
{
    private const string AnimationFolder = "Assets/Arts/Player/Animations";
    private const string OverridePath = AnimationFolder + "/PlayerBowAnimator.overrideController";
    private const string SourceFolder = "Assets/Arts/Sprites/Bow";
    private const string SheetFolder = "Assets/Arts/Player/AnimationSheets/Bow";
    private const string PrefabPath = "Assets/Resources/PlayerAnimation/PlayerVisual.prefab";
    private const string Ownership = "PlayerAnimationIntegrationV1";

    private static readonly string[] SheetNames = { "Idle", "Walk", "Jump", "Attack", "Hit" };
    private static readonly int[] ExpectedCounts = { 6, 8, 8, 8, 4 };
    private static readonly int[] Columns = { 6, 4, 4, 4, 4 };
    private static readonly int[] Rows = { 1, 2, 2, 2, 1 };

    public static void Setup(
        AnimatorController baseController,
        AnimationClip[] baseClips,
        StringBuilder report)
    {
        Require(baseController != null && baseClips != null && baseClips.Length == 15,
            "Bow setup requires the generated 15-clip PlayerAnimator controller.");

        EnsureFolder(SheetFolder);
        PreflightOwnership();

        var sheets = new Sprite[SheetNames.Length][];
        for (int i = 0; i < sheets.Length; i++)
            sheets[i] = ImportSheet(i, report);

        AnimationClip[] bowClips = MakeBowClips(sheets);
        AnimatorOverrideController bowController =
            MakeOverrideController(baseController, baseClips, bowClips);
        ConfigurePrefab(baseController, bowController, baseClips, bowClips, sheets, report);
    }

    private static void PreflightOwnership()
    {
        for (int i = 0; i < SheetNames.Length; i++)
        {
            Require(AssetImporter.GetAtPath(SourcePath(i)) is TextureImporter,
                "Missing Bow source sheet: " + SourcePath(i));
            Object working = AssetDatabase.LoadMainAssetAtPath(WorkingPath(i));
            Require(working == null || AssetDatabase.GetLabels(working).Contains(Ownership),
                "Unowned Bow working sheet exists: " + WorkingPath(i));
        }

        foreach (string clipName in BowClipNames())
        {
            Object clip = AssetDatabase.LoadMainAssetAtPath(ClipPath(clipName));
            Require(clip == null || AssetDatabase.GetLabels(clip).Contains(Ownership),
                "Unowned Bow clip exists: " + clipName);
        }

        Object controller = AssetDatabase.LoadMainAssetAtPath(OverridePath);
        Require(controller == null || AssetDatabase.GetLabels(controller).Contains(Ownership),
            "Unowned Bow AnimatorOverrideController exists.");
    }

    private static Sprite[] ImportSheet(int index, StringBuilder report)
    {
        string sourcePath = SourcePath(index);
        string workingPath = WorkingPath(index);
        if (AssetImporter.GetAtPath(workingPath) == null)
        {
            Require(AssetDatabase.CopyAsset(sourcePath, workingPath),
                "Cannot create Bow animation working copy: " + workingPath);
            AssetDatabase.ImportAsset(workingPath, ImportAssetOptions.ForceSynchronousImport);
        }

        Object workingAsset = AssetDatabase.LoadMainAssetAtPath(workingPath);
        AssetDatabase.SetLabels(workingAsset, new[] { Ownership });

        var importer = (TextureImporter)AssetImporter.GetAtPath(workingPath);
        var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider =
            factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        SpriteRect[] rects = provider.GetSpriteRects()
            .OrderBy(rect => FrameNumber(rect.name)).ToArray();
        Require(rects.Length == ExpectedCounts[index],
            "Unexpected Bow frame count in " + workingPath + ": " + rects.Length);

        provider.GetDataProvider<ITextureDataProvider>()
            .GetTextureActualWidthAndHeight(out int width, out int height);
        Require(width > 0 && height > 0, "Invalid Bow texture size: " + workingPath);
        Require(importer.spritePixelsPerUnit > 0f, "Invalid Bow PPU: " + workingPath);
        Require(Mathf.Approximately(importer.spritePixelsPerUnit, sourceImporter.spritePixelsPerUnit),
            "Bow working-copy PPU must retain the authored source value: " + workingPath);

        foreach (SpriteRect rect in rects)
        {
            Require(rect.rect.width > 0f && rect.rect.height > 0f &&
                rect.rect.xMin >= 0f && rect.rect.yMin >= 0f &&
                rect.rect.xMax <= width && rect.rect.yMax <= height,
                "Bow authored Sprite rect is outside its texture: " + rect.name);
            rect.alignment = SpriteAlignment.BottomCenter;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.border = Vector4.zero;
        }

        // The supplied 1774x887 sheets are not evenly divisible into integer cells.
        // Their already-authored rects are preserved; no crop, resize or pixel write occurs.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = Mathf.Max(importer.maxTextureSize,
            Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            TextureImporterPlatformSettings platformSettings =
                importer.GetPlatformTextureSettings(platform);
            if (!platformSettings.overridden) continue;
            platformSettings.format = TextureImporterFormat.RGBA32;
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            platformSettings.maxTextureSize = importer.maxTextureSize;
            importer.SetPlatformTextureSettings(platformSettings);
        }

        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        Require(File.ReadAllBytes(sourcePath).SequenceEqual(File.ReadAllBytes(workingPath)),
            "Bow source PNG pixels changed while creating the working copy: " + SheetNames[index]);

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(workingPath)
            .OfType<Sprite>().OrderBy(sprite => FrameNumber(sprite.name)).ToArray();
        Require(sprites.Length == ExpectedCounts[index],
            "Imported Bow frame count mismatch: " + workingPath);
        ValidateReadingOrder(index, sprites);

        bool integerGrid = width % Columns[index] == 0 && height % Rows[index] == 0;
        report.AppendLine("Bow " + SheetNames[index] + ": " + width + "x" + height +
            "; frames " + sprites.Length + "; authored rects preserved; PPU " +
            importer.spritePixelsPerUnit + "; integer count-grid=" + integerGrid +
            "; top-row-first; Point/None/FullRect/no mipmaps");
        return sprites;
    }

    private static void ValidateReadingOrder(int index, Sprite[] sprites)
    {
        if (Rows[index] != 2) return;
        int topCount = Columns[index];
        float lowestTop = sprites.Take(topCount).Min(sprite => sprite.rect.center.y);
        float highestBottom = sprites.Skip(topCount).Max(sprite => sprite.rect.center.y);
        Require(lowestTop > highestBottom,
            "Bow frame naming/order must be left-to-right, top row then bottom row: " +
            SheetNames[index]);
    }

    private static AnimationClip[] MakeBowClips(Sprite[][] sheets)
    {
        AnimationClip idle = MakeClip("Player_BowIdle", sheets[0], 6f, true);
        AnimationClip walk = MakeClip("Player_BowWalk", sheets[1], 10f, true);
        AnimationClip jumpStart = MakeClip("Player_BowJumpStart", new[] { sheets[2][0] }, 12f, false);
        AnimationClip jumpRise = MakeClip("Player_BowJumpRise", new[] { sheets[2][1] }, 12f, true);
        AnimationClip jumpApex = MakeClip("Player_BowJumpApex", new[] { sheets[2][2] }, 12f, true);
        AnimationClip jumpFall = MakeClip("Player_BowJumpFall", new[] { sheets[2][3] }, 12f, true);
        AnimationClip doubleJump = MakeClip("Player_BowDoubleJump", new[] { sheets[2][4] }, 12f, false);
        AnimationClip doubleJumpRise = MakeClip("Player_BowDoubleJumpRise", new[] { sheets[2][5] }, 12f, true);
        AnimationClip doubleJumpFall = MakeClip("Player_BowDoubleJumpFall", new[] { sheets[2][6] }, 12f, true);
        AnimationClip land = MakeClip("Player_BowLand", new[] { sheets[2][7] }, 12f, false);
        AnimationClip attack = MakeClip("Player_BowAttack", sheets[3], 12f, false);
        AnimationClip hit = MakeClip("Player_BowHit", sheets[4], 12f, false);

        return new[]
        {
            idle, walk, jumpStart, jumpRise, jumpApex, jumpFall,
            doubleJump, doubleJumpRise, doubleJumpFall, land,
            attack, attack, attack, hit, attack
        };
    }

    private static AnimationClip MakeClip(
        string assetName,
        Sprite[] frames,
        float fps,
        bool loop)
    {
        string path = ClipPath(assetName);
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = assetName };
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.ClearCurves();
        clip.frameRate = fps;
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(
            clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
            keys);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.startTime = 0f;
        settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        AssetDatabase.SetLabels(clip, new[] { Ownership });
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    private static AnimatorOverrideController MakeOverrideController(
        AnimatorController baseController,
        AnimationClip[] baseClips,
        AnimationClip[] bowClips)
    {
        AnimatorOverrideController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        if (controller == null)
        {
            controller = new AnimatorOverrideController(baseController)
            {
                name = "PlayerBowAnimator"
            };
            AssetDatabase.CreateAsset(controller, OverridePath);
        }
        else
        {
            controller.runtimeAnimatorController = baseController;
        }

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        for (int pairIndex = 0; pairIndex < overrides.Count; pairIndex++)
        {
            int poseIndex = Array.IndexOf(baseClips, overrides[pairIndex].Key);
            Require(poseIndex >= 0 && poseIndex < bowClips.Length,
                "Unexpected clip in PlayerAnimator override table: " + overrides[pairIndex].Key);
            overrides[pairIndex] = new KeyValuePair<AnimationClip, AnimationClip>(
                overrides[pairIndex].Key, bowClips[poseIndex]);
        }
        Require(overrides.Count == 15, "Bow override must map all 15 base clips.");
        controller.ApplyOverrides(overrides);
        AssetDatabase.SetLabels(controller, new[] { Ownership });
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return controller;
    }

    private static void ConfigurePrefab(
        AnimatorController baseController,
        AnimatorOverrideController bowController,
        AnimationClip[] baseClips,
        AnimationClip[] bowClips,
        Sprite[][] sheets,
        StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PlayerAnimationController driver =
                root.GetComponent<PlayerAnimationController>();
            Require(driver != null, "PlayerVisual driver is missing.");
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("swordController").objectReferenceValue = baseController;
            serialized.FindProperty("bowController").objectReferenceValue = bowController;

            SerializedProperty baseArray = serialized.FindProperty("clips");
            Require(baseArray.arraySize == baseClips.Length,
                "PlayerVisual base clip array is incomplete.");
            for (int i = 0; i < baseClips.Length; i++)
                baseArray.GetArrayElementAtIndex(i).objectReferenceValue = baseClips[i];

            SerializedProperty bowArray = serialized.FindProperty("bowClips");
            bowArray.arraySize = bowClips.Length;
            for (int i = 0; i < bowClips.Length; i++)
                bowArray.GetArrayElementAtIndex(i).objectReferenceValue = bowClips[i];

            SerializedProperty registrations = serialized.FindProperty("frameRegistration");
            int swordCount = registrations.arraySize;
            Require(swordCount == 38, "Expected 38 Sword registrations before adding Bow.");
            registrations.arraySize = swordCount + sheets.Sum(sheet => sheet.Length);
            int registrationIndex = swordCount;
            float bowReferenceHeight = 0f;
            for (int sheetIndex = 0; sheetIndex < sheets.Length; sheetIndex++)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    Require(ImageConversion.LoadImage(
                        texture,
                        File.ReadAllBytes(WorkingPath(sheetIndex))),
                        "Cannot inspect Bow working-copy pixels.");
                    Color32[] pixels = texture.GetPixels32();
                    foreach (Sprite sprite in sheets[sheetIndex])
                    {
                        MeasureRegistration(sprite, texture.width, texture.height, pixels,
                            out float feet, out float bodyHeight);
                        SerializedProperty registration =
                            registrations.GetArrayElementAtIndex(registrationIndex++);
                        registration.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                        registration.FindPropertyRelative("feetAbovePivot").floatValue = feet;
                        if (sheetIndex == 0 && sprite == sheets[0][0])
                            bowReferenceHeight = bodyHeight;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }

            float swordReferenceHeight =
                serialized.FindProperty("referenceBodyHeight").floatValue;
            Require(bowReferenceHeight >= swordReferenceHeight * 0.8f &&
                bowReferenceHeight <= swordReferenceHeight * 1.2f,
                "Bow and Sword visual heights differ by more than 20 percent.");
            report.AppendLine("Bow opaque reference height: " +
                bowReferenceHeight.ToString("F4") + " world; Sword reference: " +
                swordReferenceHeight.ToString("F4") + " world.");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) != null,
                "PlayerVisual prefab save failed.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void MeasureRegistration(
        Sprite sprite,
        int width,
        int height,
        Color32[] pixels,
        out float feet,
        out float bodyHeight)
    {
        Rect rect = sprite.rect;
        int minY = int.MaxValue;
        int maxY = -1;
        int footY = int.MaxValue;
        for (int y = Mathf.CeilToInt(rect.yMin);
             y < Mathf.Min(height, Mathf.FloorToInt(rect.yMax));
             y++)
        {
            for (int x = Mathf.CeilToInt(rect.xMin);
                 x < Mathf.Min(width, Mathf.FloorToInt(rect.xMax));
                 x++)
            {
                Color32 pixel = pixels[y * width + x];
                if (pixel.a < 128) continue;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                int low = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                int high = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                if (low >= 110 && high - low <= 60 &&
                    y < rect.yMin + rect.height * 0.65f &&
                    x > rect.xMin + rect.width * 0.2f &&
                    x < rect.xMin + rect.width * 0.8f)
                {
                    footY = Mathf.Min(footY, y);
                }
            }
        }

        Require(maxY >= minY, "Empty Bow sprite: " + sprite.name);
        if (footY == int.MaxValue) footY = minY;
        feet = (footY - rect.yMin) / sprite.pixelsPerUnit;
        bodyHeight = (maxY - minY + 1) / sprite.pixelsPerUnit;
    }

    public static void Validate(
        AnimatorController baseController,
        GameObject prefab,
        StringBuilder report)
    {
        var expectedBowClips = new AnimationClip[15];
        string[] clipNames =
        {
            "Player_BowIdle", "Player_BowWalk", "Player_BowJumpStart",
            "Player_BowJumpRise", "Player_BowJumpApex", "Player_BowJumpFall",
            "Player_BowDoubleJump", "Player_BowDoubleJumpRise",
            "Player_BowDoubleJumpFall", "Player_BowLand",
            "Player_BowAttack", "Player_BowAttack", "Player_BowAttack",
            "Player_BowHit", "Player_BowAttack"
        };
        for (int i = 0; i < clipNames.Length; i++)
        {
            expectedBowClips[i] =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(clipNames[i]));
            Require(expectedBowClips[i] != null,
                "Missing Bow clip: " + clipNames[i]);
        }

        for (int i = 0; i < SheetNames.Length; i++)
        {
            string sourcePath = SourcePath(i);
            string workingPath = WorkingPath(i);
            Require(File.ReadAllBytes(sourcePath).SequenceEqual(
                File.ReadAllBytes(workingPath)),
                "Bow source/working PNG pixels differ: " + SheetNames[i]);
            var importer = (TextureImporter)AssetImporter.GetAtPath(workingPath);
            var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Require(importer.spriteImportMode == SpriteImportMode.Multiple &&
                importer.filterMode == FilterMode.Point &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                !importer.mipmapEnabled &&
                settings.spriteMeshType == SpriteMeshType.FullRect &&
                Mathf.Approximately(importer.spritePixelsPerUnit,
                    sourceImporter.spritePixelsPerUnit),
                "Bow import settings failed: " + SheetNames[i]);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(workingPath)
                .OfType<Sprite>().OrderBy(sprite => FrameNumber(sprite.name)).ToArray();
            Require(sprites.Length == ExpectedCounts[i],
                "Bow slice count failed: " + SheetNames[i]);
            ValidateReadingOrder(i, sprites);
            foreach (Sprite sprite in sprites)
            {
                Require(Mathf.Abs(sprite.pivot.y) < 0.01f &&
                    Mathf.Abs(sprite.pivot.x - sprite.rect.width * 0.5f) < 0.6f,
                    "Bow Bottom Center pivot failed: " + sprite.name);
            }
        }

        foreach (AnimationClip clip in expectedBowClips.Distinct())
        {
            EditorCurveBinding[] bindings =
                AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Require(bindings.Length == 1 && bindings[0].path == "" &&
                bindings[0].type == typeof(SpriteRenderer) &&
                bindings[0].propertyName == "m_Sprite" &&
                AnimationUtility.GetCurveBindings(clip).Length == 0 &&
                AnimationUtility.GetAnimationEvents(clip).Length == 0,
                "Bow clip must be sprite-only with no Animation Events: " + clip.name);
        }

        AnimatorOverrideController bowController =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        Require(bowController != null &&
            bowController.runtimeAnimatorController == baseController,
            "Bow override controller/base reference is invalid.");
        var mappings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        bowController.GetOverrides(mappings);
        Require(mappings.Count == 15, "Bow override mapping count must be 15.");
        AnimationClip[] baseClips = Enum.GetNames(typeof(PlayerAnimationController.Pose))
            .Select(name => AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationFolder + "/Player_" + name + ".anim")).ToArray();
        for (int i = 0; i < baseClips.Length; i++)
        {
            KeyValuePair<AnimationClip, AnimationClip> mapping =
                mappings.Single(pair => pair.Key == baseClips[i]);
            Require(mapping.Value == expectedBowClips[i],
                "Bow override mismatch for " + baseClips[i].name);
        }

        Require(prefab.GetComponentsInChildren<Animator>(true).Length == 1,
            "PlayerVisual must contain exactly one Animator.");
        var driver = new SerializedObject(
            prefab.GetComponent<PlayerAnimationController>());
        Require(driver.FindProperty("swordController").objectReferenceValue == baseController &&
            driver.FindProperty("bowController").objectReferenceValue == bowController &&
            driver.FindProperty("clips").arraySize == 15 &&
            driver.FindProperty("bowClips").arraySize == 15 &&
            driver.FindProperty("frameRegistration").arraySize >= 72,
            "PlayerVisual Bow references are incomplete.");
        for (int i = 0; i < expectedBowClips.Length; i++)
        {
            Require(driver.FindProperty("bowClips")
                .GetArrayElementAtIndex(i).objectReferenceValue == expectedBowClips[i],
                "PlayerVisual Bow clip reference mismatch at pose " + i);
        }

        report.AppendLine(
            "PASS Bow: 34 sprites; 12 clips; 15 override mappings; one PlayerVisual Animator; source PNG pixels preserved.");
    }

    private static IEnumerable<string> BowClipNames()
    {
        return new[]
        {
            "Player_BowIdle", "Player_BowWalk", "Player_BowJumpStart",
            "Player_BowJumpRise", "Player_BowJumpApex", "Player_BowJumpFall",
            "Player_BowDoubleJump", "Player_BowDoubleJumpRise",
            "Player_BowDoubleJumpFall", "Player_BowLand",
            "Player_BowAttack", "Player_BowHit"
        };
    }

    private static int FrameNumber(string name)
    {
        return int.TryParse(name.Substring(name.LastIndexOf('_') + 1),
            out int index) ? index : int.MaxValue;
    }

    private static string SourcePath(int index)
    {
        return SourceFolder + "/Bow" + SheetNames[index] + "Sprite.png";
    }

    private static string WorkingPath(int index)
    {
        return SheetFolder + "/Bow" + SheetNames[index] + "Sprite.png";
    }

    private static string ClipPath(string clipName)
    {
        return AnimationFolder + "/" + clipName + ".anim";
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
