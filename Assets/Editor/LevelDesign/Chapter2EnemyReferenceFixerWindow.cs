using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniProject.EditorTools.LevelDesign
{
    internal sealed class Chapter2EnemyReferenceFixReport
    {
        private readonly List<string> lines = new List<string>();
        private readonly List<string> errors = new List<string>();
        public bool IsValid => errors.Count == 0;
        public string Text => string.Join("\n", lines);
        public void Add(string text) => lines.Add(text);
        public void Warning(string text) => lines.Add("[WARNING] " + text);
        public void Error(string text) { errors.Add(text); lines.Add("[ERROR] " + text); }
        public void Finish() => lines.Add(IsValid
            ? "Dry Run passed. Apply is available; Scenes will not be saved automatically."
            : $"Dry Run failed with {errors.Count} error(s). No Scenes were changed.");
    }

    internal static class Chapter2EnemyReferenceFixer
    {
        private const string Room21 = "Assets/Scenes/Room2-1.unity";
        private const string Room22 = "Assets/Scenes/Room2-2.unity";
        private const string Room23 = "Assets/Scenes/Room2-3.unity";
        private const string Enemy = "Assets/Prefabs/Enemy.prefab";
        private const string EnemyC2 = "Assets/Prefabs/Enemy_C2.prefab";
        private const string RangedC2 = "Assets/Prefabs/Enemy_Ranged_C2.prefab";
        private const string ChargeC2 = "Assets/Prefabs/Enemy_Charge_C2.prefab";

        internal static readonly string[] TargetScenePaths =
        {
            Room21,
            Room22,
            Room23
        };

        internal static readonly string[] TargetSceneNames =
        {
            "Room2-1",
            "Room2-2",
            "Room2-3"
        };

        internal sealed class Snapshot
        {
            public Transform Parent;
            public int Sibling;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public Vector3 WorldPosition;
            public Quaternion WorldRotation;
            public string Name;
            public bool Active;
            public int Layer;
            public string Tag;
            public bool Static;
        }

        internal sealed class Plan
        {
            public Scene Scene;
            public GameObject Old;
            public GameObject Target;
            public Snapshot Before;
            public string Reason;
            public GameObject New;
        }

        internal sealed class ScenePlan
        {
            public Scene Scene;
            public readonly List<Plan> Plans = new List<Plan>();
            public readonly List<GameObject> Candidates = new List<GameObject>();
            public Dictionary<string, string> ProtectedBefore;
        }

        internal sealed class Analysis
        {
            public readonly List<ScenePlan> Scenes = new List<ScenePlan>();
            public readonly Chapter2EnemyReferenceFixReport Report =
                new Chapter2EnemyReferenceFixReport();
        }

        public static Analysis DryRun(string targetScenePath)
        {
            var result = new Analysis();
            result.Report.Add("Chapter 2 Enemy Prefab Reference Fixer - Dry Run");
            result.Report.Add("Target: " + GetTargetSceneName(targetScenePath));
            result.Report.Add("Existing instances only; no Enemy is added and no Prefab asset is edited.");
            result.Report.Add(string.Empty);

            if (!TargetScenePaths.Contains(targetScenePath, StringComparer.OrdinalIgnoreCase))
            {
                result.Report.Error("Unsupported target Scene: " + targetScenePath);
                result.Report.Finish();
                return result;
            }

            GameObject melee = RequiredPrefab(
                EnemyC2, typeof(EnemyAI), typeof(EnemyHealth), result.Report);
            GameObject charge = RequiredPrefab(
                ChargeC2, typeof(ChargeEnemyAI), typeof(EnemyHealth), result.Report);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RangedC2) != null)
                result.Report.Add("[INFO] Enemy_Ranged_C2.prefab found; it is not used.");
            else
                result.Report.Warning("Enemy_Ranged_C2.prefab is missing; no ranged instance will be added.");

            Inspect(targetScenePath, melee, charge, result);

            result.Report.Add(string.Empty);
            result.Report.Add("[PROTECTED] Player, ExitDoor, RoomController, Camera, Geometry, Environment Visual, and Prefab assets are read-only.");
            result.Report.Add("[PROTECTED] Apply uses PrefabUtility and Undo; Scenes remain dirty and unsaved.");
            result.Report.Finish();
            return result;
        }

        internal static string GetTargetSceneName(string path)
        {
            int index = Array.IndexOf(TargetScenePaths, path);
            return index >= 0 ? TargetSceneNames[index] : path;
        }

        public static void Apply(Analysis analysis)
        {
            if (analysis == null || !analysis.Report.IsValid)
                throw new InvalidOperationException("Run a successful Dry Run first.");

            var plans = analysis.Scenes.SelectMany(s => s.Plans).ToList();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fix Chapter 2 Enemy Prefab References");

            try
            {
                foreach (ScenePlan scenePlan in analysis.Scenes)
                {
                    if (!scenePlan.Scene.IsValid() || !scenePlan.Scene.isLoaded)
                        throw new InvalidOperationException("A target Scene is not loaded.");
                    if (scenePlan.Scene.isDirty)
                        throw new InvalidOperationException(
                            scenePlan.Scene.name + " is dirty. Save or discard existing changes first.");
                    scenePlan.ProtectedBefore =
                        CaptureProtected(scenePlan.Scene, scenePlan.Candidates);
                }

                foreach (Plan plan in plans)
                    Replace(plan);

                foreach (ScenePlan scenePlan in analysis.Scenes)
                    Verify(scenePlan);

                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static void Inspect(
            string path,
            GameObject meleePrefab,
            GameObject chargePrefab,
            Analysis result)
        {
            Scene scene = FindLoaded(path);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                result.Report.Error(path + ": 대상 Scene을 먼저 열어주세요.");
                return;
            }
            if (scene.isDirty)
                result.Report.Error(scene.name + ": Scene is already dirty; save or discard it before Apply.");

            var scenePlan = new ScenePlan { Scene = scene };
            result.Scenes.Add(scenePlan);
            result.Report.Add(scene.name);

            List<GameObject> roots = PrefabRoots(scene);
            List<GameObject> melee = roots.Where(IsMelee).ToList();
            List<GameObject> brokenCharge = roots.Where(IsBrokenCharge).ToList();
            int ranged = roots.Count(IsRanged);

            result.Report.Add("[FOUND] Enemy.prefab instances: " + melee.Count);
            result.Report.Add("[INFO] Ranged Enemy instances: " + ranged);
            if (ranged == 0)
                result.Report.Add("[INFO] No ranged enemies will be added.");

            if (brokenCharge.Count > 0)
                result.Report.Add("[BROKEN PREFAB] Enemy_Charge instances: " + brokenCharge.Count);
            if (brokenCharge.Count > 0 && path != Room23)
                result.Report.Error(scene.name + ": broken Charge is only supported in Room2-3.");
            if (path == Room23 && brokenCharge.Count != 2)
                result.Report.Error("Room2-3: expected exactly 2 clearly named broken Charge instances, found " + brokenCharge.Count + ".");

            foreach (GameObject go in melee)
                AddPlan(scenePlan, go, meleePrefab, "Enemy.prefab -> Enemy_C2.prefab", result.Report);
            foreach (GameObject go in brokenCharge)
                AddPlan(scenePlan, go, chargePrefab, "Broken Charge -> Enemy_Charge_C2.prefab", result.Report);

            if (scenePlan.Candidates.Count > 0)
            {
                foreach (string warning in DirectReferences(scene, scenePlan.Candidates))
                    result.Report.Error(warning);
                scenePlan.ProtectedBefore = CaptureProtected(scene, scenePlan.Candidates);
            }
            result.Report.Add(string.Empty);
        }

        private static void AddPlan(
            ScenePlan scenePlan,
            GameObject oldObject,
            GameObject target,
            string reason,
            Chapter2EnemyReferenceFixReport report)
        {
            if (target == null)
            {
                report.Error("Target Chapter 2 Prefab is missing for " + oldObject.name);
                return;
            }
            var plan = new Plan
            {
                Scene = scenePlan.Scene,
                Old = oldObject,
                Target = target,
                Before = TakeSnapshot(oldObject),
                Reason = reason
            };
            scenePlan.Plans.Add(plan);
            scenePlan.Candidates.Add(oldObject);
            Vector3 p = plan.Before.WorldPosition;
            report.Add($"[PLAN] {reason} | {oldObject.name} | Position=({p.x:0.###}, {p.y:0.###}, {p.z:0.###}) | Parent={Path(plan.Before.Parent)} | Sibling={plan.Before.Sibling}");
        }

        private static GameObject RequiredPrefab(
            string path,
            Type aiType,
            Type healthType,
            Chapter2EnemyReferenceFixReport report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.Error("Missing required Prefab: " + path);
                return null;
            }
            if (prefab.GetComponentInChildren(aiType, true) == null)
                report.Error(path + " has no " + aiType.Name + " component.");
            if (prefab.GetComponentInChildren(healthType, true) == null)
                report.Error(path + " has no " + healthType.Name + " component.");
            report.Add("[PREFAB VERIFIED] " + path + ": " + aiType.Name + ", " + healthType.Name);
            return prefab;
        }

        private static Scene FindLoaded(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase))
                    return scene;
            }
            return default;
        }

        private static List<GameObject> PrefabRoots(Scene scene)
        {
            var list = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
                CollectRoots(root.transform, list);
            return list;
        }

        private static void CollectRoots(Transform transform, List<GameObject> list)
        {
            GameObject go = transform.gameObject;
            bool root = PrefabUtility.IsPartOfPrefabInstance(go) &&
                (transform.parent == null ||
                 !PrefabUtility.IsPartOfPrefabInstance(transform.parent.gameObject));
            if (root) { list.Add(go); return; }
            foreach (Transform child in transform)
                CollectRoots(child, list);
        }

        private static bool IsMelee(GameObject go)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(go);
            string path = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            return path.Equals(Enemy, StringComparison.OrdinalIgnoreCase) &&
                go.GetComponentInChildren<EnemyAI>(true) != null &&
                go.GetComponentInChildren<RangedEnemyAI>(true) == null &&
                go.GetComponentInChildren<ChargeEnemyAI>(true) == null;
        }

        private static bool IsRanged(GameObject go)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(go);
            string path = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            return path.Equals(RangedC2, StringComparison.OrdinalIgnoreCase) ||
                go.GetComponentInChildren<RangedEnemyAI>(true) != null;
        }

        private static bool IsBrokenCharge(GameObject go)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go) ||
                PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(go) != null)
                return false;
            PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(go);
            return status == PrefabInstanceStatus.MissingAsset &&
                go.name.IndexOf("Enemy_Charge", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Replace(Plan plan)
        {
            GameObject replacement =
                PrefabUtility.InstantiatePrefab(plan.Target, plan.Scene) as GameObject;
            if (replacement == null)
                throw new InvalidOperationException("Failed to instantiate " + plan.Target.name);

            plan.New = replacement;
            Undo.RegisterCreatedObjectUndo(replacement, "Create Chapter 2 Enemy");
            replacement.name = plan.Before.Name;
            replacement.layer = plan.Before.Layer;
            replacement.tag = plan.Before.Tag;
            replacement.isStatic = plan.Before.Static;
            replacement.SetActive(plan.Before.Active);

            Transform t = replacement.transform;
            t.SetParent(plan.Before.Parent, false);
            t.localPosition = plan.Before.LocalPosition;
            t.localRotation = plan.Before.LocalRotation;
            t.localScale = plan.Before.LocalScale;
            t.SetSiblingIndex(plan.Before.Sibling);
            Undo.DestroyObjectImmediate(plan.Old);
            EditorSceneManager.MarkSceneDirty(plan.Scene);
        }

        private static void Verify(ScenePlan scenePlan)
        {
            List<GameObject> replacedRoots = scenePlan.Plans
                .Where(plan => plan.New != null)
                .Select(plan => plan.New)
                .ToList();
            Dictionary<string, string> after =
                CaptureProtected(scenePlan.Scene, replacedRoots);
            List<string> changes = Diff(scenePlan.ProtectedBefore, after);
            if (changes.Count > 0)
                throw new InvalidOperationException(
                    "Protected Scene state changed in " + scenePlan.Scene.name + ":\n" +
                    string.Join("\n", changes));

            foreach (Plan plan in scenePlan.Plans)
            {
                if (plan.New == null ||
                    !SameTransform(plan.New.transform, plan.Before) ||
                    !HasSource(plan.New, AssetDatabase.GetAssetPath(plan.Target)))
                    throw new InvalidOperationException(
                        "Replacement verification failed for " + plan.Before.Name);
            }

            List<GameObject> roots = PrefabRoots(scenePlan.Scene);
            if (roots.Count(IsMelee) != 0)
                throw new InvalidOperationException(scenePlan.Scene.name + " still contains Enemy.prefab.");
            if (scenePlan.Scene.path.Equals(Room23, StringComparison.OrdinalIgnoreCase) &&
                roots.Count(go => HasSource(go, ChargeC2)) != 2)
                throw new InvalidOperationException("Room2-3 does not contain exactly 2 Enemy_Charge_C2 instances.");
        }

        private static bool HasSource(GameObject go, string path)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(go);
            return source != null &&
                AssetDatabase.GetAssetPath(source).Equals(path, StringComparison.OrdinalIgnoreCase);
        }

        private static Snapshot TakeSnapshot(GameObject go)
        {
            Transform t = go.transform;
            return new Snapshot
            {
                Parent = t.parent,
                Sibling = t.GetSiblingIndex(),
                LocalPosition = t.localPosition,
                LocalRotation = t.localRotation,
                LocalScale = t.localScale,
                WorldPosition = t.position,
                WorldRotation = t.rotation,
                Name = go.name,
                Active = go.activeSelf,
                Layer = go.layer,
                Tag = go.tag,
                Static = go.isStatic
            };
        }

        private static bool SameTransform(Transform t, Snapshot s)
        {
            return (t.localPosition - s.LocalPosition).sqrMagnitude < 0.000001f &&
                Quaternion.Angle(t.localRotation, s.LocalRotation) < 0.001f &&
                (t.localScale - s.LocalScale).sqrMagnitude < 0.000001f &&
                (t.position - s.WorldPosition).sqrMagnitude < 0.000001f &&
                Quaternion.Angle(t.rotation, s.WorldRotation) < 0.001f &&
                t.parent == s.Parent && t.GetSiblingIndex() == s.Sibling;
        }

        private static List<string> DirectReferences(Scene scene, List<GameObject> candidates)
        {
            var objects = new HashSet<UnityEngine.Object>();
            foreach (GameObject candidate in candidates)
            {
                foreach (Transform t in candidate.GetComponentsInChildren<Transform>(true))
                    objects.Add(t.gameObject);
                foreach (Component c in candidate.GetComponentsInChildren<Component>(true))
                    if (c != null) objects.Add(c);
            }

            var found = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || objects.Contains(component.gameObject))
                    continue;
                SerializedObject serialized;
                try { serialized = new SerializedObject(component); }
                catch { continue; }
                SerializedProperty property = serialized.GetIterator();
                bool enter = true;
                while (property.NextVisible(enter))
                {
                    enter = false;
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue != null &&
                        objects.Contains(property.objectReferenceValue))
                        found.Add("Direct Scene reference: " + Path(component.transform) + " -> " + property.propertyPath);
                }
            }
            return found.Distinct().ToList();
        }

        private static Dictionary<string, string> CaptureProtected(
            Scene scene,
            List<GameObject> excludedRoots)
        {
            var excluded = new HashSet<GameObject>();
            foreach (GameObject root in excludedRoots)
            {
                if (root == null) continue;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    excluded.Add(t.gameObject);
            }

            var state = new Dictionary<string, string>();
            foreach (GameObject root in scene.GetRootGameObjects())
                Capture(root.transform, excluded, state);
            return state;
        }

        private static void Capture(
            Transform t,
            HashSet<GameObject> excluded,
            Dictionary<string, string> state)
        {
            if (excluded.Contains(t.gameObject)) return;
            string path = Path(t);
            GameObject go = t.gameObject;
            state["GO:" + path] =
                $"name={go.name}|active={go.activeSelf}|layer={go.layer}|tag={go.tag}|static={go.isStatic}";
            state["T:" + path] =
                $"lp={t.localPosition}|lr={t.localRotation}|ls={t.localScale}";
            int index = 0;
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                state[$"C:{path}:{component.GetType().FullName}:{index++}"] =
                    EditorJsonUtility.ToJson(component, true);
            }
            foreach (Transform child in t)
                Capture(child, excluded, state);
        }

        private static List<string> Diff(
            Dictionary<string, string> before,
            Dictionary<string, string> after)
        {
            var changes = new List<string>();
            var keys = new HashSet<string>(before.Keys);
            keys.UnionWith(after.Keys);
            foreach (string key in keys)
            {
                before.TryGetValue(key, out string a);
                after.TryGetValue(key, out string b);
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    changes.Add(key);
            }
            return changes;
        }

        private static string Path(Transform t)
        {
            if (t == null) return "<null>";
            var parts = new Stack<string>();
            while (t != null)
            {
                parts.Push(t.GetSiblingIndex() + ":" + t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }
    }

    internal sealed class Chapter2EnemyReferenceFixerWindow : EditorWindow
    {
        [SerializeField]
        private int targetSceneIndex;
        private Chapter2EnemyReferenceFixReport report;
        private Chapter2EnemyReferenceFixer.Analysis analysis;
        private Vector2 scroll;

        [MenuItem("Tools/Level Design/Fix Chapter 2 Enemy References")]
        private static void Open()
        {
            var window = GetWindow<Chapter2EnemyReferenceFixerWindow>();
            window.titleContent = new GUIContent("C2 Enemy References");
            window.minSize = new Vector2(680f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Chapter 2 Enemy Prefab Reference Fixer",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Open the selected target Scene first. Dry Run inspects loaded instances only. " +
                "Apply uses Undo and never saves Scenes automatically.",
                MessageType.Info);

            targetSceneIndex = Mathf.Clamp(
                targetSceneIndex,
                0,
                Chapter2EnemyReferenceFixer.TargetSceneNames.Length - 1);
            int nextIndex = EditorGUILayout.Popup(
                "Target Scene",
                targetSceneIndex,
                Chapter2EnemyReferenceFixer.TargetSceneNames);
            if (nextIndex != targetSceneIndex)
            {
                targetSceneIndex = nextIndex;
                analysis = null;
                report = null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry Run", GUILayout.Height(30f)))
                {
                    analysis = Chapter2EnemyReferenceFixer.DryRun(TargetScenePath());
                    report = analysis.Report;
                }
                using (new EditorGUI.DisabledScope(
                           analysis == null || report == null || !report.IsValid))
                {
                    if (GUILayout.Button("Apply", GUILayout.Height(30f)))
                        Apply();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(
                report == null
                    ? "Open the selected target Scene, then run Dry Run."
                    : report.Text,
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Apply()
        {
            if (!EditorUtility.DisplayDialog(
                    "Apply Chapter 2 Enemy References?",
                    "Existing instances will be replaced 1:1. Prefabs and protected Scene objects are read-only. Scenes remain unsaved.",
                    "Apply",
                    "Cancel"))
                return;

            try
            {
                Chapter2EnemyReferenceFixer.Apply(analysis);
                analysis = Chapter2EnemyReferenceFixer.DryRun(TargetScenePath());
                report = analysis.Report;
                EditorUtility.DisplayDialog(
                    "Chapter 2 Enemy References",
                    "Apply completed. Review the dirty Scenes and save them manually.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                report = new Chapter2EnemyReferenceFixReport();
                report.Error(exception.Message);
                report.Add("Apply stopped. Undo is available; no automatic Scene save was performed.");
                EditorUtility.DisplayDialog(
                    "Chapter 2 Enemy References Failed",
                    "Apply stopped. Check the report and Console.",
                    "OK");
            }
        }

        private string TargetScenePath()
        {
            return Chapter2EnemyReferenceFixer.TargetScenePaths[targetSceneIndex];
        }
    }
}
