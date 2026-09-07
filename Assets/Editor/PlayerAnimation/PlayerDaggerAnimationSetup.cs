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

internal static class PlayerDaggerAnimationSetup
{
    private const string AnimationFolder = "Assets/Arts/Player/Animations";
    private const string OverridePath = AnimationFolder + "/PlayerDaggerAnimator.overrideController";
    private const string SourceFolder = "Assets/Arts/Sprites/Dagger";
    private const string SheetFolder = "Assets/Arts/Player/AnimationSheets/Dagger";
    private const string PrefabPath = "Assets/Resources/PlayerAnimation/PlayerVisual.prefab";
    private const string Ownership = "PlayerAnimationIntegrationV1";

    private static readonly string[] SheetNames = { "Idle", "Walk", "Jump", "Attack", "Hit" };
    private static readonly int[] ExpectedCounts = { 6, 8, 8, 16, 4 };
    private static readonly int[] Columns = { 3, 4, 4, 4, 4 };
    private static readonly int[] Rows = { 2, 2, 2, 4, 1 };

    [MenuItem("Tools/Player Animation/Setup Dagger Animation")]
    private static void SetupFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        PlayerAnimationSetup.Setup();
    }

    [MenuItem("Tools/Player Animation/Validate Dagger Animation (Read Only)")]
    private static void ValidateFromMenu()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            AnimationFolder + "/PlayerAnimator.controller");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var report = new StringBuilder();
        Validate(controller, prefab, report);
        Debug.Log(report.ToString());
    }

    public static void Setup(
        AnimatorController baseController,
        AnimationClip[] baseClips,
        StringBuilder report)
    {
        Require(baseController != null && baseClips != null && baseClips.Length == 15,
            "Dagger setup requires the generated 15-clip PlayerAnimator controller.");

        EnsureFolder(SheetFolder);
        PreflightOwnership();
        float targetPpu = DetermineTargetPpu(report);

        var sheets = new Sprite[SheetNames.Length][];
        for (int i = 0; i < sheets.Length; i++)
            sheets[i] = ImportSheet(i, targetPpu, report);

        AnimationClip[] daggerClips = MakeDaggerClips(sheets);
        AnimatorOverrideController daggerController =
            MakeOverrideController(baseController, baseClips, daggerClips);
        ConfigurePrefab(daggerController, daggerClips, sheets, report);
    }

    private static void PreflightOwnership()
    {
        for (int i = 0; i < SheetNames.Length; i++)
        {
            Require(AssetImporter.GetAtPath(SourcePath(i)) is TextureImporter,
                "Missing Dagger source sheet: " + SourcePath(i));
            Object working = AssetDatabase.LoadMainAssetAtPath(WorkingPath(i));
            Require(working == null || AssetDatabase.GetLabels(working).Contains(Ownership),
                "Unowned Dagger working sheet exists: " + WorkingPath(i));
        }

        foreach (string clipName in DaggerClipNames())
        {
            Object clip = AssetDatabase.LoadMainAssetAtPath(ClipPath(clipName));
            Require(clip == null || AssetDatabase.GetLabels(clip).Contains(Ownership),
                "Unowned Dagger clip exists: " + clipName);
        }

        Object controller = AssetDatabase.LoadMainAssetAtPath(OverridePath);
        Require(controller == null || AssetDatabase.GetLabels(controller).Contains(Ownership),
            "Unowned Dagger AnimatorOverrideController exists.");
    }

    private static float DetermineTargetPpu(StringBuilder report)
    {
        TextureImporter sourceImporter =
            (TextureImporter)AssetImporter.GetAtPath(SourcePath(0));
        Require(sourceImporter != null && sourceImporter.spritePixelsPerUnit > 0f,
            "Dagger Idle source PPU is invalid.");

        Sprite idle = AssetDatabase.LoadAllAssetsAtPath(SourcePath(0))
            .OfType<Sprite>().OrderBy(sprite => FrameNumber(sprite.name)).FirstOrDefault();
        Require(idle != null, "Dagger Idle source Sprite is missing.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null, "PlayerVisual prefab is missing.");
        var serialized = new SerializedObject(prefab.GetComponent<PlayerAnimationController>());
        float swordReferenceHeight = serialized.FindProperty("referenceBodyHeight").floatValue;
        Require(swordReferenceHeight > 0f, "Sword reference height is invalid.");

        int opaqueHeight = ReadOpaqueHeightPixels(SourcePath(0), idle.rect);
        float sourceWorldHeight = opaqueHeight / sourceImporter.spritePixelsPerUnit;
        float targetPpu = sourceImporter.spritePixelsPerUnit;
        if (sourceWorldHeight < swordReferenceHeight * 0.8f ||
            sourceWorldHeight > swordReferenceHeight * 1.2f)
        {
            targetPpu = opaqueHeight / swordReferenceHeight;
        }

        targetPpu = Mathf.Max(1f, Mathf.Round(targetPpu * 1000f) / 1000f);
        report.AppendLine("Dagger PPU: source=" + sourceImporter.spritePixelsPerUnit.ToString("F3") +
            "; opaque Idle=" + opaqueHeight + "px / " + sourceWorldHeight.ToString("F4") +
            " world; Sword reference=" + swordReferenceHeight.ToString("F4") +
            "; working=" + targetPpu.ToString("F3") + ".");
        return targetPpu;
    }

    private static Sprite[] ImportSheet(int index, float targetPpu, StringBuilder report)
    {
        string sourcePath = SourcePath(index);
        string workingPath = WorkingPath(index);
        bool createdWorkingCopy = false;
        if (AssetImporter.GetAtPath(workingPath) == null)
        {
            Require(AssetDatabase.CopyAsset(sourcePath, workingPath),
                "Cannot create Dagger animation working copy: " + workingPath);
            AssetDatabase.ImportAsset(workingPath, ImportAssetOptions.ForceSynchronousImport);
            createdWorkingCopy = true;
        }

        Object workingAsset = AssetDatabase.LoadMainAssetAtPath(workingPath);
        AssetDatabase.SetLabels(workingAsset, new[] { Ownership });

        var importer = (TextureImporter)AssetImporter.GetAtPath(workingPath);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider =
            factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        SpriteRect[] rects = provider.GetSpriteRects()
            .OrderBy(rect => FrameNumber(rect.name)).ToArray();
        Require(rects.Length == ExpectedCounts[index],
            "Unexpected Dagger frame count in " + workingPath + ": " + rects.Length);

        provider.GetDataProvider<ITextureDataProvider>()
            .GetTextureActualWidthAndHeight(out int width, out int height);
        Require(width > 0 && height > 0, "Invalid Dagger texture size: " + workingPath);

        foreach (SpriteRect rect in rects)
        {
            Require(rect.rect.width > 0f && rect.rect.height > 0f &&
                rect.rect.xMin >= 0f && rect.rect.yMin >= 0f &&
                rect.rect.xMax <= width && rect.rect.yMax <= height,
                "Dagger authored Sprite rect is outside its texture: " + rect.name);
            rect.alignment = SpriteAlignment.BottomCenter;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.border = Vector4.zero;
        }

        // Preserve the supplied authored rects and sprite IDs. Only the working-copy importer changes.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        if (createdWorkingCopy || importer.spritePixelsPerUnit <= 0f)
            importer.spritePixelsPerUnit = targetPpu;
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
            "Dagger source PNG pixels changed while creating the working copy: " + SheetNames[index]);

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(workingPath)
            .OfType<Sprite>().OrderBy(sprite => FrameNumber(sprite.name)).ToArray();
        Require(sprites.Length == ExpectedCounts[index],
            "Imported Dagger frame count mismatch: " + workingPath);
        ValidateReadingOrder(index, sprites);

        bool integerGrid = width % Columns[index] == 0 && height % Rows[index] == 0;
        report.AppendLine("Dagger " + SheetNames[index] + ": " + width + "x" + height +
            "; frames " + sprites.Length + "; authored rects preserved; PPU " +
            importer.spritePixelsPerUnit.ToString("F3") + "; integer count-grid=" + integerGrid +
            "; top-row-first; Point/None/FullRect/no mipmaps");
        return sprites;
    }

    private static void ValidateReadingOrder(int index, Sprite[] sprites)
    {
        if (Rows[index] <= 1) return;
        for (int row = 0; row < Rows[index] - 1; row++)
        {
            float lowestCurrent = sprites.Skip(row * Columns[index]).Take(Columns[index])
                .Min(sprite => sprite.rect.center.y);
            float highestNext = sprites.Skip((row + 1) * Columns[index]).Take(Columns[index])
                .Max(sprite => sprite.rect.center.y);
            Require(lowestCurrent > highestNext,
                "Dagger frame naming/order must be left-to-right, top row then bottom row: " +
                SheetNames[index] + " row " + row);
        }
    }

    private static AnimationClip[] MakeDaggerClips(Sprite[][] sheets)
    {
        AnimationClip idle = MakeClip("Player_DaggerIdle", sheets[0], 6f, true);
        AnimationClip walk = MakeClip("Player_DaggerWalk", sheets[1], 10f, true);
        AnimationClip jumpStart = MakeClip("Player_DaggerJumpStart", new[] { sheets[2][0] }, 12f, false);
        AnimationClip jumpRise = MakeClip("Player_DaggerJumpRise", new[] { sheets[2][1] }, 12f, true);
        AnimationClip jumpApex = MakeClip("Player_DaggerJumpApex", new[] { sheets[2][2] }, 12f, true);
        AnimationClip jumpFall = MakeClip("Player_DaggerJumpFall", new[] { sheets[2][3] }, 12f, true);
        AnimationClip doubleJump = MakeClip("Player_DaggerDoubleJump", new[] { sheets[2][4] }, 12f, false);
        AnimationClip doubleJumpRise = MakeClip("Player_DaggerDoubleJumpRise", new[] { sheets[2][5] }, 12f, true);
        AnimationClip doubleJumpFall = MakeClip("Player_DaggerDoubleJumpFall", new[] { sheets[2][6] }, 12f, true);
        AnimationClip land = MakeClip("Player_DaggerLand", new[] { sheets[2][7] }, 12f, false);
        AnimationClip attack1 = MakeClip("Player_DaggerAttack1", sheets[3].Take(4).ToArray(), 16f, false);
        AnimationClip attack2 = MakeClip("Player_DaggerAttack2", sheets[3].Skip(4).Take(4).ToArray(), 16f, false);
        AnimationClip attack3 = MakeClip("Player_DaggerAttack3", sheets[3].Skip(8).Take(4).ToArray(), 16f, false);
        AnimationClip attack4 = MakeClip("Player_DaggerAttack4", sheets[3].Skip(12).Take(4).ToArray(), 16f, false);
        AnimationClip hit = MakeClip("Player_DaggerHit", sheets[4], 12f, false);

        return new[]
        {
            idle, walk, jumpStart, jumpRise, jumpApex, jumpFall,
            doubleJump, doubleJumpRise, doubleJumpFall, land,
            attack1, attack2, attack3, hit, attack4
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
        AnimationClip[] daggerClips)
    {
        AnimatorOverrideController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        if (controller == null)
        {
            controller = new AnimatorOverrideController(baseController)
            {
                name = "PlayerDaggerAnimator"
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
            Require(poseIndex >= 0 && poseIndex < daggerClips.Length,
                "Unexpected clip in PlayerAnimator override table: " + overrides[pairIndex].Key);
            overrides[pairIndex] = new KeyValuePair<AnimationClip, AnimationClip>(
                overrides[pairIndex].Key, daggerClips[poseIndex]);
        }
        Require(overrides.Count == 15, "Dagger override must map all 15 base clips.");
        controller.ApplyOverrides(overrides);
        AssetDatabase.SetLabels(controller, new[] { Ownership });
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return controller;
    }

    private static void ConfigurePrefab(
        AnimatorOverrideController daggerController,
        AnimationClip[] daggerClips,
        Sprite[][] sheets,
        StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PlayerAnimationController driver = root.GetComponent<PlayerAnimationController>();
            Require(driver != null, "PlayerVisual driver is missing.");
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("daggerController").objectReferenceValue = daggerController;

            SerializedProperty daggerArray = serialized.FindProperty("daggerClips");
            daggerArray.arraySize = daggerClips.Length;
            for (int i = 0; i < daggerClips.Length; i++)
                daggerArray.GetArrayElementAtIndex(i).objectReferenceValue = daggerClips[i];

            SerializedProperty registrations = serialized.FindProperty("frameRegistration");
            int existingCount = registrations.arraySize;
            Require(existingCount == 72,
                "Expected 72 Sword/Bow registrations before adding Dagger, actual=" + existingCount);
            registrations.arraySize = existingCount + sheets.Sum(sheet => sheet.Length);
            int registrationIndex = existingCount;
            float daggerReferenceHeight = 0f;
            for (int sheetIndex = 0; sheetIndex < sheets.Length; sheetIndex++)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(WorkingPath(sheetIndex))),
                        "Cannot inspect Dagger working-copy pixels.");
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
                            daggerReferenceHeight = bodyHeight;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }

            float swordReferenceHeight = serialized.FindProperty("referenceBodyHeight").floatValue;
            Require(daggerReferenceHeight >= swordReferenceHeight * 0.8f &&
                daggerReferenceHeight <= swordReferenceHeight * 1.2f,
                "Dagger and Sword visual heights differ by more than 20 percent.");
            report.AppendLine("Dagger opaque reference height: " +
                daggerReferenceHeight.ToString("F4") + " world; Sword reference: " +
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

        Require(maxY >= minY, "Empty Dagger sprite: " + sprite.name);
        if (footY == int.MaxValue) footY = minY;
        feet = (footY - rect.yMin) / sprite.pixelsPerUnit;
        bodyHeight = (maxY - minY + 1) / sprite.pixelsPerUnit;
    }

    private static int ReadOpaqueHeightPixels(string assetPath, Rect rect)
    {
        var texture = new Texture2D(2, 2);
        try
        {
            Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(assetPath)),
                "Cannot inspect Dagger source pixels: " + assetPath);
            Color32[] pixels = texture.GetPixels32();
            int minY = int.MaxValue;
            int maxY = -1;
            for (int y = Mathf.CeilToInt(rect.yMin);
                 y < Mathf.Min(texture.height, Mathf.FloorToInt(rect.yMax));
                 y++)
            {
                for (int x = Mathf.CeilToInt(rect.xMin);
                     x < Mathf.Min(texture.width, Mathf.FloorToInt(rect.xMax));
                     x++)
                {
                    if (pixels[y * texture.width + x].a < 128) continue;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            Require(maxY >= minY, "Dagger source Sprite has no opaque pixels.");
            return maxY - minY + 1;
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    public static void Validate(
        AnimatorController baseController,
        GameObject prefab,
        StringBuilder report)
    {
        Require(baseController != null, "PlayerAnimator controller is missing.");
        Require(prefab != null, "PlayerVisual prefab is missing.");

        string[] clipNames =
        {
            "Player_DaggerIdle", "Player_DaggerWalk", "Player_DaggerJumpStart",
            "Player_DaggerJumpRise", "Player_DaggerJumpApex", "Player_DaggerJumpFall",
            "Player_DaggerDoubleJump", "Player_DaggerDoubleJumpRise",
            "Player_DaggerDoubleJumpFall", "Player_DaggerLand",
            "Player_DaggerAttack1", "Player_DaggerAttack2", "Player_DaggerAttack3",
            "Player_DaggerHit", "Player_DaggerAttack4"
        };
        var expectedClips = new AnimationClip[clipNames.Length];
        for (int i = 0; i < clipNames.Length; i++)
        {
            expectedClips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(clipNames[i]));
            Require(expectedClips[i] != null, "Missing Dagger clip: " + clipNames[i]);
        }

        float minimumWorkingPpu = float.PositiveInfinity;
        float maximumWorkingPpu = 0f;
        for (int i = 0; i < SheetNames.Length; i++)
        {
            string sourcePath = SourcePath(i);
            string workingPath = WorkingPath(i);
            Require(File.ReadAllBytes(sourcePath).SequenceEqual(File.ReadAllBytes(workingPath)),
                "Dagger source/working PNG pixels differ: " + SheetNames[i]);
            var importer = (TextureImporter)AssetImporter.GetAtPath(workingPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Require(importer.spriteImportMode == SpriteImportMode.Multiple &&
                importer.filterMode == FilterMode.Point &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                !importer.mipmapEnabled && settings.spriteMeshType == SpriteMeshType.FullRect,
                "Dagger import settings failed: " + SheetNames[i]);
            Require(importer.spritePixelsPerUnit > 0f,
                "Dagger working-copy PPU must be positive: " + SheetNames[i]);
            minimumWorkingPpu = Mathf.Min(minimumWorkingPpu, importer.spritePixelsPerUnit);
            maximumWorkingPpu = Mathf.Max(maximumWorkingPpu, importer.spritePixelsPerUnit);

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(workingPath)
                .OfType<Sprite>().OrderBy(sprite => FrameNumber(sprite.name)).ToArray();
            Require(sprites.Length == ExpectedCounts[i],
                "Dagger slice count failed: " + SheetNames[i]);
            ValidateReadingOrder(i, sprites);
            foreach (Sprite sprite in sprites)
            {
                Require(Mathf.Abs(sprite.pivot.y) < 0.01f &&
                    Mathf.Abs(sprite.pivot.x - sprite.rect.width * 0.5f) < 0.6f,
                    "Dagger Bottom Center pivot failed: " + sprite.name);
            }
        }

        foreach (AnimationClip clip in expectedClips)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Require(bindings.Length == 1 && bindings[0].path == "" &&
                bindings[0].type == typeof(SpriteRenderer) &&
                bindings[0].propertyName == "m_Sprite" &&
                AnimationUtility.GetCurveBindings(clip).Length == 0 &&
                AnimationUtility.GetAnimationEvents(clip).Length == 0,
                "Dagger clip must be sprite-only with no Animation Events: " + clip.name);
        }

        AnimatorOverrideController daggerController =
            AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(OverridePath);
        Require(daggerController != null &&
            daggerController.runtimeAnimatorController == baseController,
            "Dagger override controller/base reference is invalid.");
        var mappings = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        daggerController.GetOverrides(mappings);
        Require(mappings.Count == 15, "Dagger override mapping count must be 15.");
        AnimationClip[] baseClips = Enum.GetNames(typeof(PlayerAnimationController.Pose))
            .Select(name => AssetDatabase.LoadAssetAtPath<AnimationClip>(
                AnimationFolder + "/Player_" + name + ".anim")).ToArray();
        for (int i = 0; i < baseClips.Length; i++)
        {
            Require(baseClips[i] != null, "Missing base clip for Dagger mapping at pose " + i);
            KeyValuePair<AnimationClip, AnimationClip> mapping =
                mappings.Single(pair => pair.Key == baseClips[i]);
            Require(mapping.Value == expectedClips[i],
                "Dagger override mismatch for " + baseClips[i].name);
        }

        Require(prefab.GetComponentsInChildren<Animator>(true).Length == 1 &&
            prefab.GetComponentsInChildren<SpriteRenderer>(true).Length == 1 &&
            prefab.GetComponentsInChildren<Collider2D>(true).Length == 0 &&
            prefab.GetComponentsInChildren<Rigidbody2D>(true).Length == 0,
            "PlayerVisual must contain one Animator/Renderer and no physics components.");
        var driver = new SerializedObject(prefab.GetComponent<PlayerAnimationController>());
        Require(driver.FindProperty("daggerController").objectReferenceValue == daggerController &&
            driver.FindProperty("daggerClips").arraySize == 15 &&
            driver.FindProperty("frameRegistration").arraySize == 114,
            "PlayerVisual Dagger references are incomplete.");
        for (int i = 0; i < expectedClips.Length; i++)
        {
            Require(driver.FindProperty("daggerClips")
                .GetArrayElementAtIndex(i).objectReferenceValue == expectedClips[i],
                "PlayerVisual Dagger clip reference mismatch at pose " + i);
        }

        report.AppendLine(
            "PASS Dagger: 42 sprites; 15 clips; 15 override mappings; working PPU range " +
            minimumWorkingPpu.ToString("F3") + "-" + maximumWorkingPpu.ToString("F3") +
            "; one PlayerVisual Animator; source PNG pixels preserved.");
    }

    private static IEnumerable<string> DaggerClipNames()
    {
        return new[]
        {
            "Player_DaggerIdle", "Player_DaggerWalk", "Player_DaggerJumpStart",
            "Player_DaggerJumpRise", "Player_DaggerJumpApex", "Player_DaggerJumpFall",
            "Player_DaggerDoubleJump", "Player_DaggerDoubleJumpRise",
            "Player_DaggerDoubleJumpFall", "Player_DaggerLand",
            "Player_DaggerAttack1", "Player_DaggerAttack2", "Player_DaggerAttack3",
            "Player_DaggerAttack4", "Player_DaggerHit"
        };
    }

    private static int FrameNumber(string name)
    {
        return int.TryParse(name.Substring(name.LastIndexOf('_') + 1),
            out int index) ? index : int.MaxValue;
    }

    private static string SourcePath(int index)
    {
        return SourceFolder + "/Dagger" + SheetNames[index] + "Sprite.png";
    }

    private static string WorkingPath(int index)
    {
        return SheetFolder + "/Dagger" + SheetNames[index] + "Sprite.png";
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
