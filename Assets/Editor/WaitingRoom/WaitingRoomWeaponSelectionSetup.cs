using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WaitingRoomWeaponSelectionSetup
{
    private const string WaitingRoomScenePath = "Assets/Scenes/WaitingRoom.unity";
    private const string SelectionRootName = "WeaponSelection";
    private const string AutomationReportPath = "C:/Temp/UnityWaitingRoomWeaponSelectionReport.txt";

    static WaitingRoomWeaponSelectionSetup()
    {
        EditorApplication.delayCall += AddToOpenWaitingRoomIfMissing;
    }

    [MenuItem("Tools/Player/Setup WaitingRoom Weapon Chests")]
    private static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || scene.name != "WaitingRoom")
        {
            EditorUtility.DisplayDialog(
                "WaitingRoom Weapon Selection",
                "WaitingRoom Scene을 연 뒤 다시 실행하세요.",
                "OK");
            return;
        }

        bool wasDirty = scene.isDirty;
        SetupOpenScene(scene, true);
        string validation = ValidateOpenScene(scene);

        if (!wasDirty && string.IsNullOrEmpty(validation))
        {
            EditorSceneManager.SaveScene(scene);
        }

        if (string.IsNullOrEmpty(validation))
        {
            string saveMessage = wasDirty
                ? "기존 Dirty Scene을 보호하기 위해 자동 저장하지 않았습니다. 확인 후 Ctrl+S로 저장하세요."
                : "WaitingRoom Scene에 저장했습니다.";
            EditorUtility.DisplayDialog("WaitingRoom Weapon Selection", "Setup 완료.\n" + saveMessage, "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("WaitingRoom Weapon Selection", validation, "OK");
        }
    }

    [MenuItem("Tools/Player/Validate WaitingRoom Weapon Chests")]
    private static void ValidateFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        string validation = ValidateOpenScene(scene);
        EditorUtility.DisplayDialog(
            "WaitingRoom Weapon Selection",
            string.IsNullOrEmpty(validation) ? "Validation PASS" : validation,
            "OK");
    }

    public static void SetupAndSaveForAutomation()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(WaitingRoomScenePath, OpenSceneMode.Single);
            SetupOpenScene(scene, false);
            string validation = ValidateOpenScene(scene);

            if (!string.IsNullOrEmpty(validation))
                throw new InvalidOperationException(validation);

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("WaitingRoom Scene save failed.");

            File.WriteAllText(
                AutomationReportPath,
                "PASS\nScene=" + scene.path + "\n" + BuildSummary(scene));
            Debug.Log("WaitingRoom weapon selection setup PASS");
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AutomationReportPath));
            File.WriteAllText(AutomationReportPath, "FAIL\n" + exception);
            Debug.LogException(exception);
            throw;
        }
    }

    private static void AddToOpenWaitingRoomIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded || scene.name != "WaitingRoom")
            return;

        if (FindRoot(scene, SelectionRootName) != null)
            return;

        SetupOpenScene(scene, true);
        Debug.Log("WaitingRoom weapon chests were added to the open Scene. Review and save with Ctrl+S.");
    }

    private static void SetupOpenScene(Scene scene, bool registerUndo)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "WaitingRoom")
            throw new InvalidOperationException("Active Scene must be WaitingRoom.");

        GameObject selectionRoot = FindRoot(scene, SelectionRootName);

        if (selectionRoot == null)
        {
            selectionRoot = new GameObject(SelectionRootName);
            SceneManager.MoveGameObjectToScene(selectionRoot, scene);

            if (registerUndo)
                Undo.RegisterCreatedObjectUndo(selectionRoot, "Setup WaitingRoom Weapon Chests");
        }

        Sprite squareSprite = FindReusableSquareSprite(scene);
        TMP_FontAsset font = FindExistingFont();

        ConfigureChest(selectionRoot.transform, "BowChest", WeaponType.Bow, new Vector3(-6f, 0f, 0f), new Color(0.24f, 0.55f, 0.78f, 1f), squareSprite, font);
        ConfigureChest(selectionRoot.transform, "DaggerChest", WeaponType.Dagger, new Vector3(6f, 0f, 0f), new Color(0.68f, 0.30f, 0.55f, 1f), squareSprite, font);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void ConfigureChest(
        Transform parent,
        string chestName,
        WeaponType weaponType,
        Vector3 position,
        Color color,
        Sprite squareSprite,
        TMP_FontAsset font)
    {
        Transform existing = parent.Find(chestName);
        GameObject chest = existing != null ? existing.gameObject : new GameObject(chestName);
        chest.transform.SetParent(parent, false);
        chest.transform.position = position;

        BoxCollider2D trigger = GetOrAddComponent<BoxCollider2D>(chest);
        trigger.isTrigger = true;
        trigger.size = new Vector2(2.6f, 2.5f);
        trigger.offset = new Vector2(0f, 0.75f);

        WeaponSelectionChest selection = GetOrAddComponent<WeaponSelectionChest>(chest);

        GameObject visual = GetOrCreateChild(chest.transform, "Visual");
        visual.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(1.6f, 0.9f, 1f);
        SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(visual);
        renderer.sprite = squareSprite;
        renderer.color = color;
        renderer.sortingOrder = -1;

        TextMeshPro weaponLabel = GetOrCreateWorldText(chest.transform, "WeaponLabel", font);
        weaponLabel.text = weaponType == WeaponType.Bow ? "BOW" : "DAGGER";
        weaponLabel.color = Color.white;
        weaponLabel.fontSize = 28f;
        weaponLabel.alignment = TextAlignmentOptions.Center;
        SetTextTransform(weaponLabel.rectTransform, new Vector3(0f, 0.08f, -0.1f));

        TextMeshPro prompt = GetOrCreateWorldText(chest.transform, "InteractionPrompt", font);
        prompt.text = "SPACE";
        prompt.color = Color.white;
        prompt.fontSize = 25f;
        prompt.alignment = TextAlignmentOptions.Center;
        SetTextTransform(prompt.rectTransform, new Vector3(0f, 1.8f, -0.1f));
        prompt.gameObject.SetActive(false);

        SerializedObject serializedSelection = new SerializedObject(selection);
        serializedSelection.FindProperty("weaponType").enumValueIndex = (int)weaponType;
        serializedSelection.FindProperty("interactionPrompt").objectReferenceValue = prompt.gameObject;
        serializedSelection.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TextMeshPro GetOrCreateWorldText(Transform parent, string name, TMP_FontAsset font)
    {
        Transform existing = parent.Find(name);
        GameObject textObject;

        if (existing != null)
        {
            textObject = existing.gameObject;
        }
        else
        {
            textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
            textObject.transform.SetParent(parent, false);
        }

        TextMeshPro text = GetOrAddComponent<TextMeshPro>(textObject);

        if (font != null)
            text.font = font;

        text.enableAutoSizing = false;
        text.raycastTarget = false;
        return text;
    }

    private static void SetTextTransform(RectTransform rectTransform, Vector3 localPosition)
    {
        rectTransform.localPosition = localPosition;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = new Vector3(0.02f, 0.02f, 1f);
        rectTransform.sizeDelta = new Vector2(120f, 30f);
    }

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
            return existing.gameObject;

        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Sprite FindReusableSquareSprite(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite != null && renderer.sprite.name == "Square")
                    return renderer.sprite;
            }
        }

        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static TMP_FontAsset FindExistingFont()
    {
        string[] preferred = AssetDatabase.FindAssets("Galmuri11 t:TMP_FontAsset");
        string[] candidates = preferred.Length > 0 ? preferred : AssetDatabase.FindAssets("t:TMP_FontAsset");

        if (candidates.Length == 0)
            return null;

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(candidates[0]));
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
    }

    private static string ValidateOpenScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != "WaitingRoom")
            return "WaitingRoom Scene이 열려 있지 않습니다.";

        GameObject root = FindRoot(scene, SelectionRootName);

        if (root == null)
            return "WeaponSelection Root가 없습니다.";

        WeaponSelectionChest[] chests = root.GetComponentsInChildren<WeaponSelectionChest>(true);
        List<string> errors = new List<string>();

        if (chests.Length != 2)
            errors.Add("WeaponSelectionChest count must be 2, actual=" + chests.Length);

        if (chests.Count(chest => chest.WeaponType == WeaponType.Bow) != 1)
            errors.Add("Bow Chest count must be 1.");

        if (chests.Count(chest => chest.WeaponType == WeaponType.Dagger) != 1)
            errors.Add("Dagger Chest count must be 1.");

        foreach (WeaponSelectionChest chest in chests)
        {
            BoxCollider2D trigger = chest.GetComponent<BoxCollider2D>();

            if (trigger == null || !trigger.isTrigger)
                errors.Add(chest.name + " requires an enabled trigger BoxCollider2D.");

            if (chest.GetComponent<Rigidbody2D>() != null)
                errors.Add(chest.name + " must not contain a Rigidbody2D.");

            Transform prompt = chest.transform.Find("InteractionPrompt");

            if (prompt == null || prompt.gameObject.activeSelf)
                errors.Add(chest.name + " prompt must exist and start hidden.");
        }

        return string.Join("\n", errors);
    }

    private static string BuildSummary(Scene scene)
    {
        GameObject root = FindRoot(scene, SelectionRootName);
        WeaponSelectionChest[] chests = root.GetComponentsInChildren<WeaponSelectionChest>(true);
        return string.Join(
            "\n",
            chests.Select(chest =>
                chest.name + ": Weapon=" + chest.WeaponType +
                ", Position=" + chest.transform.position +
                ", Trigger=" + chest.GetComponent<BoxCollider2D>().isTrigger));
    }
}