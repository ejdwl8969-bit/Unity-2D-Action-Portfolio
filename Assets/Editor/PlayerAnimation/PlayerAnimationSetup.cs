using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit, asset-only setup. Never opens, saves or edits a gameplay scene.</summary>
public static class PlayerAnimationSetup
{
    private const string Folder = "Assets/Arts/Player/Animations";
    private const string ControllerPath = "Assets/Arts/Player/Animations/PlayerAnimator.controller";
    private const string SheetFolder = "Assets/Arts/Player/AnimationSheets";
    private const string SwordSheetFolder = SheetFolder + "/Sword";
    private const string PrefabPath = "Assets/Resources/PlayerAnimation/PlayerVisual.prefab";
    private const string LegacyControllerPath = "Assets/Arts/Player/Player.controller";
    private const string Ownership = "PlayerAnimationIntegrationV1";
    private const string AuditFolder = "C:/Temp/UnityPlayerAnimationAudit";
    private static readonly string[] SheetNames = { "Idle", "Walk", "Jump", "Attack", "Hit" };
    private static readonly int[] Columns = { 6, 8, 4, 4, 4 };
    private static readonly int[] Rows = { 1, 1, 2, 3, 1 };

    [MenuItem("Tools/Player Animation/Setup and Validate")]
    public static void SetupFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorUtility.DisplayDialog("Player Animation", "Update only Player sprite import settings, generated clips/controller and PlayerVisual prefab? Gameplay scenes and source PNG pixels are not changed.", "Setup", "Cancel"))
            Setup();
    }

    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before asset setup.");
        Directory.CreateDirectory(AuditFolder);
        var report = new StringBuilder("PLAYER ANIMATION SETUP\n");
        Scene active = SceneManager.GetActiveScene();
        string originalScene = active.path;
        bool originalDirty = active.isDirty;
        report.AppendLine($"Active scene preserved: {originalScene}; Dirty before: {originalDirty}");
        PreflightOwnership();
        EnsureFolder(Folder);
        EnsureFolder(SheetFolder);
        EnsureFolder(SwordSheetFolder);
        EnsureFolder("Assets/Resources/PlayerAnimation");
        var sheets = new Sprite[5][];
        for (int i = 0; i < sheets.Length; i++)
            sheets[i] = ImportSheet(i, report);

        var clips = new List<AnimationClip>();
        clips.Add(MakeClip("Idle", sheets[0], 6f, true));
        clips.Add(MakeClip("Walk", sheets[1], 10f, true));
        clips.Add(MakeClip("JumpStart", new[] { sheets[2][0] }, 12f, false));
        clips.Add(MakeClip("JumpRise", new[] { sheets[2][1] }, 12f, true));
        clips.Add(MakeClip("JumpApex", new[] { sheets[2][2] }, 12f, true));
        clips.Add(MakeClip("JumpFall", new[] { sheets[2][3] }, 12f, true));
        clips.Add(MakeClip("DoubleJump", new[] { sheets[2][4], sheets[2][5] }, 12f, false));
        clips.Add(MakeClip("DoubleJumpRise", new[] { sheets[2][5] }, 12f, true));
        clips.Add(MakeClip("DoubleJumpFall", new[] { sheets[2][6] }, 12f, true));
        clips.Add(MakeClip("Land", new[] { sheets[2][7] }, 12f, false));
        for (int hit = 0; hit < 3; hit++)
            clips.Add(MakeClip("SwordAttack" + (hit + 1), sheets[3].Skip(hit * 4).Take(4).ToArray(), 12f, false));
        clips.Add(MakeClip("Hit", sheets[4], 12f, false));
        // Unique base motion for the shared Attack4 state. Sword and Bow never request this pose.
        clips.Add(MakeClip("Attack4", new[] { sheets[3][11] }, 12f, false));

        AnimatorController controller = MakeController(clips);
        MakePrefab(controller, clips, sheets, report);
        PlayerBowAnimationSetup.Setup(controller, clips.ToArray(), report);
        PlayerDaggerAnimationSetup.Setup(controller, clips.ToArray(), report);
        ValidateAssets(report);
        Require(SceneManager.GetActiveScene().path == originalScene && active.isDirty == originalDirty,
            "Active scene or its Dirty state changed unexpectedly.");
        report.AppendLine("Scene Save: NO; Gameplay root/Collider/Rigidbody writes: NO");
        report.AppendLine("Original Sword/Bow/Dagger PNG and .meta plus Player.controller: unchanged. Working copies have identical PNG pixels; original Sprite names retained.");
        report.AppendLine("Uniform count-grid slicing cannot repair art that crosses an intended cell boundary; inspect the supplied sheets visually.");
        File.WriteAllText(AuditFolder + "/UnitySetupReport.txt", report.ToString(), new UTF8Encoding(false));
        Debug.Log("Player animation assets generated and validated. No gameplay scene was saved. Report: " + AuditFolder + "/UnitySetupReport.txt");
    }

    private static void PreflightOwnership()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null && controller.layers.Length > 0)
            Require(AssetDatabase.GetLabels(controller).Contains(Ownership), "Existing Player.controller is not empty and is not owned by this setup; refusing to overwrite.");
        foreach (string pose in Enum.GetNames(typeof(PlayerAnimationController.Pose)))
        {
            Object existing = AssetDatabase.LoadMainAssetAtPath(Folder + "/Player_" + pose + ".anim");
            Require(existing == null || AssetDatabase.GetLabels(existing).Contains(Ownership), "Unowned clip exists: " + pose);
        }
        Object prefab = AssetDatabase.LoadMainAssetAtPath(PrefabPath);
        Require(prefab == null || AssetDatabase.GetLabels(prefab).Contains(Ownership), "Unowned PlayerVisual prefab exists.");
        foreach (string name in SheetNames)
        {
            Require(AssetImporter.GetAtPath(SourceSheetPath(name)) is TextureImporter, "Missing Player source sheet: " + name);
            Object sheet = AssetDatabase.LoadMainAssetAtPath(SheetPath(name));
            Require(sheet == null || AssetDatabase.GetLabels(sheet).Contains(Ownership), "Unowned working sheet exists: " + name);
        }
    }

    private static string SourceSheetPath(string name) => "Assets/Arts/Sprites/Sword/Sword" + name + "Sprite.png";
    private static string SheetPath(string name) => SwordSheetFolder + "/Sword" + name + "Sprite.png";

    private static Sprite[] ImportSheet(int index, StringBuilder report)
    {
        string path = SheetPath(SheetNames[index]);
        if (AssetImporter.GetAtPath(path) == null)
        {
            Require(AssetDatabase.CopyAsset(SourceSheetPath(SheetNames[index]), path), "Cannot create Player animation working copy: " + path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SetLabels(AssetDatabase.LoadMainAssetAtPath(path), new[] { Ownership });
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        SpriteRect[] previous = provider.GetSpriteRects().OrderBy(s => FrameNumber(s.name)).ToArray();
        provider.GetDataProvider<ITextureDataProvider>().GetTextureActualWidthAndHeight(out int width, out int height);
        int count = Columns[index] * Rows[index];
        Require(previous.Length == count, $"Unexpected existing frame count in {path}: {previous.Length}");
        float ppu = importer.spritePixelsPerUnit;
        Require(ppu > 0f, "Invalid original PPU: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = Mathf.Max(importer.maxTextureSize, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
        {
            TextureImporterPlatformSettings platformSettings = importer.GetPlatformTextureSettings(platform);
            if (!platformSettings.overridden) continue;
            platformSettings.format = TextureImporterFormat.RGBA32;
            platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
            platformSettings.maxTextureSize = importer.maxTextureSize;
            importer.SetPlatformTextureSettings(platformSettings);
        }
        float cellWidth = (float)width / Columns[index];
        float cellHeight = (float)height / Rows[index];
        for (int frame = 0; frame < count; frame++)
        {
            int column = frame % Columns[index];
            int row = frame / Columns[index];
            previous[frame].rect = new Rect(column * cellWidth, height - (row + 1) * cellHeight, cellWidth, cellHeight);
            previous[frame].alignment = SpriteAlignment.BottomCenter;
            previous[frame].pivot = new Vector2(0.5f, 0f);
            previous[frame].border = Vector4.zero;
        }
        provider.SetSpriteRects(previous);
        // Preserve the name/GUID mapping, including existing legacy sub-asset names.
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            previous.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => FrameNumber(s.name)).ToArray();
        Require(sprites.Length == count, "Imported frame count mismatch: " + path);
        report.AppendLine($"{SheetNames[index]}: {width}x{height}; {Columns[index]}x{Rows[index]} = {count}; rect {cellWidth}x{cellHeight}; PPU {ppu} (retained); Bottom Center; Point/None/FullRect/no mipmaps");
        return sprites;
    }

    private static int FrameNumber(string name)
    {
        return int.TryParse(name.Substring(name.LastIndexOf('_') + 1), out int index) ? index : int.MaxValue;
    }

    private static AnimationClip MakeClip(string pose, Sprite[] frames, float fps, bool loop)
    {
        string path = Folder + "/Player_" + pose + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = "Player_" + pose };
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.frameRate = fps;
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loop;
        clipSettings.startTime = 0f;
        clipSettings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        AssetDatabase.SetLabels(clip, new[] { Ownership });
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return clip;
    }

    private static AnimatorController MakeController(List<AnimationClip> clips)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (controller.layers.Length > 0)
        {
            // Generated assets may be rebuilt explicitly; never replace an unrelated controller.
            controller.layers = Array.Empty<AnimatorControllerLayer>();
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
                if (subAsset != controller) Object.DestroyImmediate(subAsset, true);
        }
        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddLayer("Base Layer");
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("YVelocity", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VisualState", AnimatorControllerParameterType.Int);
        controller.AddParameter(new AnimatorControllerParameter { name = "AttackAnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        for (int i = 0; i < clips.Count; i++)
        {
            string pose = ((PlayerAnimationController.Pose)i).ToString();
            AnimatorState state = machine.AddState(pose, new Vector3(240 + (i % 4) * 240, (i / 4) * 90));
            state.motion = clips[i];
            state.writeDefaultValues = false;
            if ((i >= 10 && i <= 12) || i == (int)PlayerAnimationController.Pose.Attack4)
            {
                state.speedParameter = "AttackAnimSpeed";
                state.speedParameterActive = true;
            }
            AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
            transition.duration = 0f;
            transition.hasExitTime = false;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.Equals, i, "VisualState");
            if (i == 0) machine.defaultState = state;
        }
        AssetDatabase.SetLabels(controller, new[] { Ownership });
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        return controller;
    }

    private static void MakePrefab(AnimatorController controller, List<AnimationClip> clips, Sprite[][] sheets, StringBuilder report)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("PlayerVisual");
        SceneManager.MoveGameObjectToScene(root, preview);
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sheets[0][0];
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PlayerAnimationController driver = root.AddComponent<PlayerAnimationController>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("visualRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("legacyRootController").objectReferenceValue = GetVerifiedLegacyController();
            serialized.FindProperty("swordController").objectReferenceValue = controller;
            SerializedProperty clipArray = serialized.FindProperty("clips");
            clipArray.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) clipArray.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            SerializedProperty registrations = serialized.FindProperty("frameRegistration");
            registrations.arraySize = sheets.Sum(s => s.Length);
            int registrationIndex = 0;
            for (int sheet = 0; sheet < sheets.Length; sheet++)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(SheetPath(SheetNames[sheet]))), "Cannot inspect sheet pixels.");
                    Color32[] pixels = texture.GetPixels32();
                    foreach (Sprite sprite in sheets[sheet])
                    {
                        MeasureRegistration(sprite, texture.width, texture.height, pixels, out float feet, out float bodyHeight);
                        SerializedProperty registration = registrations.GetArrayElementAtIndex(registrationIndex++);
                        registration.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                        registration.FindPropertyRelative("feetAbovePivot").floatValue = feet;
                        if (sheet == 0 && sprite == sheets[0][0])
                        {
                            serialized.FindProperty("referenceBodyHeight").floatValue = bodyHeight;
                            report.AppendLine($"Idle opaque reference height: {bodyHeight:F4} world; visual-only scale for existing 1-world collider: {1f / bodyHeight:F4}");
                        }
                    }
                }
                finally { Object.DestroyImmediate(texture); }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Require(saved != null, "PlayerVisual prefab save failed.");
            AssetDatabase.SetLabels(saved, new[] { Ownership });
            AssetDatabase.SaveAssetIfDirty(saved);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void MeasureRegistration(Sprite sprite, int width, int height, Color32[] pixels, out float feet, out float bodyHeight)
    {
        Rect rect = sprite.rect;
        int minY = int.MaxValue, maxY = -1, footY = int.MaxValue;
        for (int y = Mathf.CeilToInt(rect.yMin); y < Mathf.Min(height, Mathf.FloorToInt(rect.yMax)); y++)
            for (int x = Mathf.CeilToInt(rect.xMin); x < Mathf.Min(width, Mathf.FloorToInt(rect.xMax)); x++)
            {
                Color32 pixel = pixels[y * width + x];
                if (pixel.a < 128) continue;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                // White/gray boots in the lower central region exclude hair/scarf fragments
                // crossing a generated sheet's cell edge. This is registration, not pixel editing.
                int low = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                int high = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                if (low >= 110 && high - low <= 60 && y < rect.yMin + rect.height * 0.65f &&
                    x > rect.xMin + rect.width * 0.2f && x < rect.xMin + rect.width * 0.8f)
                    footY = Mathf.Min(footY, y);
            }
        Require(maxY >= minY, "Empty sprite: " + sprite.name);
        if (footY == int.MaxValue) footY = minY;
        feet = (footY - rect.yMin) / sprite.pixelsPerUnit;
        bodyHeight = (maxY - minY + 1) / sprite.pixelsPerUnit;
    }

    [MenuItem("Tools/Player Animation/Validate Assets (Read Only)")]
    public static void ValidateFromMenu()
    {
        var report = new StringBuilder();
        ValidateAssets(report);
        Debug.Log(report.ToString());
    }

    private static void ValidateAssets(StringBuilder report)
    {
        ValidateLegacyMigration();
        foreach (string name in SheetNames)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath(name));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Require(importer.spriteImportMode == SpriteImportMode.Multiple && importer.filterMode == FilterMode.Point &&
                importer.textureCompression == TextureImporterCompression.Uncompressed && !importer.mipmapEnabled &&
                settings.spriteMeshType == SpriteMeshType.FullRect, "Import settings failed: " + name);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath(name)).OfType<Sprite>().ToArray();
            Require(sprites.Length == Columns[Array.IndexOf(SheetNames, name)] * Rows[Array.IndexOf(SheetNames, name)], "Slice count failed.");
            foreach (Sprite sprite in sprites)
                Require(Mathf.Abs(sprite.pivot.y) < 0.01f && Mathf.Abs(sprite.pivot.x - sprite.rect.width * 0.5f) < 0.6f, "Bottom Center pivot failed: " + sprite.name);
        }
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Require(controller != null && controller.layers.Length == 1 && controller.parameters.Length == 5, "Controller structure failed.");
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Require(machine.states.Length == 15 && machine.anyStateTransitions.Length == 15, "State/transition count failed.");
        foreach (ChildAnimatorState child in machine.states)
        {
            AnimationClip clip = child.state.motion as AnimationClip;
            Require(clip != null, "Missing clip: " + child.state.name);
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            Require(bindings.Length == 1 && bindings[0].path == "" && bindings[0].type == typeof(SpriteRenderer) && bindings[0].propertyName == "m_Sprite", "Non-sprite binding: " + clip.name);
            Require(AnimationUtility.GetCurveBindings(clip).Length == 0 && AnimationUtility.GetAnimationEvents(clip).Length == 0,
                "Clip must not drive transform/physics/flip or gameplay events: " + clip.name);
            Require(AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]).All(k => k.value is Sprite), "Missing Sprite key: " + clip.name);
            Require(child.state.transitions.Length == 0, "Unexpected automatic attack/state chaining.");
            bool isAttack = child.state.name.StartsWith("SwordAttack", StringComparison.Ordinal) ||
                child.state.name == PlayerAnimationController.Pose.Attack4.ToString();
            Require(child.state.speedParameterActive == isAttack, "Attack-only speed multiplier failed.");
            report.AppendLine($"PASS {clip.name}: {clip.length:F3}s @ {clip.frameRate}fps; sprite-only; loop={AnimationUtility.GetAnimationClipSettings(clip).loopTime}");
        }
        Require(machine.anyStateTransitions.All(t => t.duration == 0f && !t.hasExitTime && !t.canTransitionToSelf), "Transition policy failed.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null && prefab.GetComponent<PlayerAnimationController>() != null && prefab.GetComponent<SpriteRenderer>() != null,
            "Visual prefab incomplete.");
        Animator animator = prefab.GetComponent<Animator>();
        Require(animator != null && animator.runtimeAnimatorController == controller && !animator.applyRootMotion && animator.updateMode == AnimatorUpdateMode.Normal,
            "Animator reference/time mode failed.");
        Require(prefab.GetComponentsInChildren<Collider2D>(true).Length == 0 && prefab.GetComponentsInChildren<Rigidbody2D>(true).Length == 0,
            "Visual prefab must contain no physics components.");
        var driver = new SerializedObject(prefab.GetComponent<PlayerAnimationController>());
        Require(driver.FindProperty("swordController").objectReferenceValue == controller &&
            driver.FindProperty("clips").arraySize == 15 && driver.FindProperty("bowClips").arraySize == 15 &&
            driver.FindProperty("daggerClips").arraySize == 15 &&
            driver.FindProperty("frameRegistration").arraySize == 114, "Driver references failed.");
        PlayerBowAnimationSetup.Validate(controller, prefab, report);
        PlayerDaggerAnimationSetup.Validate(controller, prefab, report);
        report.AppendLine("PASS 114 Sword/Bow/Dagger sprite registrations; one Animator; shared 15-state controller; normal scaled time; no physics components.");
    }

    // Only the shared prefab is updated; no clips, textures or gameplay scenes are rebuilt.
    [MenuItem("Tools/Player Animation/Configure Legacy Animator Handoff")]
    public static void ConfigureLegacyAnimatorHandoff()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play Mode before configuring the prefab.");
        RuntimeAnimatorController legacy = GetVerifiedLegacyController();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var driver = root.GetComponent<PlayerAnimationController>();
            Require(driver != null, "PlayerVisual driver is missing.");
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("legacyRootController").objectReferenceValue = legacy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) != null, "PlayerVisual prefab save failed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ValidateLegacyMigration();
    }

    private static RuntimeAnimatorController GetVerifiedLegacyController()
    {
        var legacy = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LegacyControllerPath);
        Require(legacy != null && PlayerAnimationController.IsSpriteOnlyLegacyController(legacy),
            "Legacy Player controller must contain only root SpriteRenderer.sprite curves, no Animation Events or StateMachineBehaviours.");
        return legacy;
    }

    public static void ValidateLegacyMigration()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null && prefab.GetComponent<PlayerAnimationController>() != null, "PlayerVisual is missing.");
        var serialized = new SerializedObject(prefab.GetComponent<PlayerAnimationController>());
        Require(serialized.FindProperty("legacyRootController").objectReferenceValue == GetVerifiedLegacyController(),
            "PlayerVisual Legacy Controller reference is not configured.");
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

// Prevent a future non-visual edit of the allowlisted asset from silently disabling gameplay in a build.
internal sealed class PlayerLegacyAnimatorBuildValidation : UnityEditor.Build.IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
    {
        try { PlayerAnimationSetup.ValidateLegacyMigration(); }
        catch (Exception exception) { throw new UnityEditor.Build.BuildFailedException(exception.Message); }
    }
}
