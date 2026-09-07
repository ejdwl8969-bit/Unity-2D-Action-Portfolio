using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MiniProject.EditorTools.RoomProduction
{
    internal readonly struct VisualGridSpriteInfo
    {
        public VisualGridSpriteInfo(Sprite originalSprite, Sprite workingCopySprite)
        {
            OriginalSprite = originalSprite;
            WorkingCopySprite = workingCopySprite;
        }

        public Sprite OriginalSprite { get; }
        public Sprite WorkingCopySprite { get; }
        public float OriginalWorldWidth => RoomProductionVisualGridUtility.GetWorldWidth(OriginalSprite);
        public float OriginalWorldHeight => RoomProductionVisualGridUtility.GetWorldHeight(OriginalSprite);
        public float WorkingCopyWorldWidth => RoomProductionVisualGridUtility.GetWorldWidth(WorkingCopySprite);
        public float WorkingCopyWorldHeight => RoomProductionVisualGridUtility.GetWorldHeight(WorkingCopySprite);
    }

    /// <summary>
    /// Editor-only source of truth for production visual grid dimensions.
    /// Geometry always uses the normalized Castle Builder working-copy Sprite.
    /// </summary>
    internal static class RoomProductionVisualGridUtility
    {
        internal const float ProductionPixelsPerUnit = 16f;
        internal const string PreferencesPrefix = "MiniProject.CastleRoomBuilder.v1.";
        internal const string GeneratedRoot = "Assets/Arts/Environment/Generated";

        internal static bool TryGetVisualGridInfo(int chapter, string slot, out VisualGridSpriteInfo info, out string error)
        {
            return TryGetVisualGridInfo(chapter == 1 ? "Royal" : "Dark", slot, out info, out error);
        }

        internal static bool TryGetVisualGridInfo(string theme, string slot, out VisualGridSpriteInfo info, out string error)
        {
            info = default;
            if (!TryGetConfiguredSourceSprite(theme, slot, out Sprite sourceSprite, out error)) return false;
            if (!TryResolveWorkingCopySprite(theme, sourceSprite, out Sprite workingCopySprite, out error)) return false;

            if (!Mathf.Approximately(workingCopySprite.pixelsPerUnit, ProductionPixelsPerUnit))
            {
                error = $"{theme} {slot} working copy must use PPU {ProductionPixelsPerUnit:0.###}, actual={workingCopySprite.pixelsPerUnit:0.###}.";
                return false;
            }

            info = new VisualGridSpriteInfo(sourceSprite, workingCopySprite);
            if (info.WorkingCopyWorldWidth <= 0f)
            {
                error = $"{theme} {slot} working-copy Sprite width is invalid.";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool TryGetConfiguredSourceSprite(string theme, string slot, out Sprite sprite, out string error)
        {
            sprite = null;
            string preferenceSlot = NormalizeSlotKey(slot);
            string value = EditorPrefs.GetString(PreferencesPrefix + theme + "." + preferenceSlot, string.Empty);
            string[] parts = value.Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long localId))
            {
                error = $"{theme} {preferenceSlot} Sprite is not configured.";
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(parts[0]);
            if (string.IsNullOrEmpty(path))
            {
                error = $"{theme} {preferenceSlot} source asset GUID cannot be resolved.";
                return false;
            }

            sprite = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .FirstOrDefault(candidate =>
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long candidateId) &&
                    candidateId == localId);
            if (sprite == null)
            {
                error = $"{theme} {preferenceSlot} source Sprite local ID cannot be resolved.";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool TryResolveWorkingCopySprite(string theme, Sprite sourceSprite, out Sprite workingCopySprite, out string error)
        {
            workingCopySprite = null;
            if (sourceSprite == null)
            {
                error = "Source Sprite is null.";
                return false;
            }

            string sourcePath = NormalizeAssetPath(AssetDatabase.GetAssetPath(sourceSprite));
            string workingRoot = GeneratedRoot + "/" + theme;
            string workingPath = IsPathUnder(sourcePath, workingRoot)
                ? sourcePath
                : workingRoot + "/" + Path.GetFileName(sourcePath);

            workingCopySprite = AssetDatabase.LoadAllAssetsAtPath(workingPath)
                .OfType<Sprite>()
                .FirstOrDefault(candidate => candidate.name == sourceSprite.name && RectApproximatelyEqual(candidate.rect, sourceSprite.rect));

            if (workingCopySprite == null)
            {
                error = $"{theme} working-copy Sprite was not found: {workingPath} [{sourceSprite.name}]. Run Castle Room Builder Preview once to create it.";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool IsValidThemeSprite(string theme, string slot, Sprite sprite, out string reason)
        {
            if (sprite == null)
            {
                reason = $"{theme} {slot} Sprite is not assigned.";
                return false;
            }

            string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(sprite));
            if (string.IsNullOrEmpty(path))
            {
                reason = $"{slot} Asset path cannot be resolved.";
                return false;
            }

            if (TryGetConfiguredSourceSprite(theme, slot, out Sprite configured, out _))
            {
                if (SameSpriteIdentity(sprite, configured))
                {
                    reason = null;
                    return true;
                }

                if (IsPathUnder(path, GeneratedRoot + "/" + theme) &&
                    TryResolveWorkingCopySprite(theme, configured, out Sprite expectedWorkingCopy, out _) &&
                    SameSpriteIdentity(sprite, expectedWorkingCopy))
                {
                    reason = null;
                    return true;
                }
            }

            reason = $"{slot} is neither the configured {theme} source Sprite nor its normalized working copy: {path}";
            return false;
        }

        internal static float GetWorldWidth(Sprite sprite)
        {
            return sprite == null || sprite.pixelsPerUnit <= 0f ? 0f : sprite.rect.width / sprite.pixelsPerUnit;
        }

        internal static float GetWorldHeight(Sprite sprite)
        {
            return sprite == null || sprite.pixelsPerUnit <= 0f ? 0f : sprite.rect.height / sprite.pixelsPerUnit;
        }

        internal static string FormatGridReport(string label, VisualGridSpriteInfo info)
        {
            return label + " Original: " + FormatSpriteSize(info.OriginalSprite) +
                   " | " + label + " Working Copy: " + FormatSpriteSize(info.WorkingCopySprite) +
                   " | Production " + label + " Grid: " + info.WorkingCopyWorldWidth.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatSpriteSize(Sprite sprite)
        {
            if (sprite == null) return "MISSING";
            return sprite.rect.width.ToString("0.##", CultureInfo.InvariantCulture) + "px / PPU" +
                   sprite.pixelsPerUnit.ToString("0.###", CultureInfo.InvariantCulture) + " = " +
                   GetWorldWidth(sprite).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string NormalizeSlotKey(string slot)
        {
            return string.Equals(slot, "Background Fill", StringComparison.OrdinalIgnoreCase) ? "BackgroundFill" : slot.Replace(" ", string.Empty);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
        }

        private static bool IsPathUnder(string assetPath, string root)
        {
            string normalizedRoot = NormalizeAssetPath(root) + "/";
            return NormalizeAssetPath(assetPath).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameSpriteIdentity(Sprite first, Sprite second)
        {
            if (first == null || second == null) return false;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string firstGuid, out long firstId) &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(second, out string secondGuid, out long secondId) &&
                   string.Equals(firstGuid, secondGuid, StringComparison.OrdinalIgnoreCase) &&
                   firstId == secondId;
        }

        private static bool RectApproximatelyEqual(Rect first, Rect second)
        {
            return Mathf.Abs(first.x - second.x) < 0.01f &&
                   Mathf.Abs(first.y - second.y) < 0.01f &&
                   Mathf.Abs(first.width - second.width) < 0.01f &&
                   Mathf.Abs(first.height - second.height) < 0.01f;
        }
    }
}
