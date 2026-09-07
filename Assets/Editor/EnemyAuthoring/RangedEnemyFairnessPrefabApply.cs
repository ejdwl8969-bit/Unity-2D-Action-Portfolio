using UnityEditor;
using UnityEngine;

public static class RangedEnemyFairnessPrefabApply
{
    [MenuItem("Tools/Enemy Authoring/Apply Ranged Fairness Pass")]
    public static void Apply()
    {
        ApplyOne("Assets/Prefabs/Enemy_Ranged_C1.prefab", 6.5f);
        ApplyOne("Assets/Prefabs/Enemy_Ranged_C2.prefab", 7.0f);
        AssetDatabase.SaveAssets();
        Debug.Log("Ranged Enemy Fairness Pass prefab values applied.");
    }

    private static void ApplyOne(string path, float detectRange)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
            throw new System.InvalidOperationException("Unable to load " + path);

        SerializedObject serialized = new SerializedObject(root.GetComponent<RangedEnemyAI>());
        SerializedProperty property = serialized.FindProperty("detectRange");
        if (property == null)
            throw new System.InvalidOperationException("detectRange field not found in " + path);

        property.floatValue = detectRange;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log(path + " detectRange=" + detectRange.ToString("0.###"));
    }
}



