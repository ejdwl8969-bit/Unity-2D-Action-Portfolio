using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time, guarded repair for the three known UpgradeData connection errors.
/// This intentionally edits only upgradeType/value on the named assets.
/// </summary>
public static class UpgradeDataConnectionFixer
{
    private const string DaggerPath =
        "Assets/Data/Upgrades/Common/DaggerAttackSpeed_C.asset";
    private const string ArrowPath =
        "Assets/Data/Upgrades/Common/ArrowSpeed_C.asset";
    private const string CriticalPath =
        "Assets/Data/Upgrades/Common/CriticalChance_C.asset";

    [MenuItem("Tools/Balance/Fix Upgrade Data Connections")]
    private static void FixFromMenu()
    {
        ApplyFixes();
    }

    // Kept separate so a command-line Unity verification run can invoke it.
    public static void ApplyFixesBatch()
    {
        bool success = ApplyFixes();
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static bool ApplyFixes()
    {
        bool success = true;
        success &= FixType(DaggerPath, UpgradeType.DaggerAttackSpeed, 0.05f);
        success &= FixType(ArrowPath, UpgradeType.ArrowSpeed, 1f);
        success &= FixValue(CriticalPath, UpgradeType.CriticalChance, 0.02f, 20f);

        if (success)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            success = VerifyFinalValues();
        }

        Debug.Log(
            success
                ? "Upgrade data connection fixes completed."
                : "Upgrade data connection fixes were not completed."
        );
        return success;
    }

    private static bool FixType(
        string path,
        UpgradeType expectedType,
        float expectedValue
    )
    {
        UpgradeData data = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        if (data == null)
        {
            Debug.LogError("Missing UpgradeData asset: " + path);
            return false;
        }

        UpgradeType beforeType = data.upgradeType;
        float beforeValue = data.value;
        Debug.Log(
            $"Before {path}: Type={beforeType}, Value={beforeValue}"
        );

        if (beforeValue != expectedValue)
        {
            Debug.LogError(
                $"Unexpected value in {path}; expected {expectedValue}, leaving it unchanged."
            );
            return false;
        }

        if (beforeType != expectedType)
        {
            data.upgradeType = expectedType;
            EditorUtility.SetDirty(data);
        }

        Debug.Log(
            $"After {path}: Type={data.upgradeType}, Value={data.value}"
        );
        return true;
    }

    private static bool FixValue(
        string path,
        UpgradeType expectedType,
        float expectedBeforeValue,
        float expectedAfterValue
    )
    {
        UpgradeData data = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        if (data == null)
        {
            Debug.LogError("Missing UpgradeData asset: " + path);
            return false;
        }

        UpgradeType beforeType = data.upgradeType;
        float beforeValue = data.value;
        Debug.Log(
            $"Before {path}: Type={beforeType}, Value={beforeValue}"
        );

        if (beforeType == expectedType && beforeValue == expectedAfterValue)
        {
            Debug.Log("Already fixed: " + path);
            return true;
        }

        if (beforeType != expectedType || beforeValue != expectedBeforeValue)
        {
            Debug.LogError(
                $"Unexpected state in {path}; expected Type={expectedType}, Value={expectedBeforeValue}, leaving it unchanged."
            );
            return false;
        }

        data.value = expectedAfterValue;
        EditorUtility.SetDirty(data);
        Debug.Log(
            $"After {path}: Type={data.upgradeType}, Value={data.value}"
        );
        return true;
    }

    private static bool VerifyFinalValues()
    {
        return Verify(DaggerPath, UpgradeType.DaggerAttackSpeed, 0.05f)
            && Verify(ArrowPath, UpgradeType.ArrowSpeed, 1f)
            && Verify(CriticalPath, UpgradeType.CriticalChance, 20f);
    }

    private static bool Verify(
        string path,
        UpgradeType expectedType,
        float expectedValue
    )
    {
        UpgradeData data = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        bool valid = data != null
            && data.upgradeType == expectedType
            && data.value == expectedValue;

        if (!valid)
        {
            Debug.LogError("Verification failed for " + path);
        }
        return valid;
    }
}
