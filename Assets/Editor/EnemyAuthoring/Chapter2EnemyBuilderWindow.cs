using UnityEditor;
using UnityEngine;

namespace MiniProject.EditorTools.EnemyAuthoring
{
    internal sealed class Chapter2EnemyBuilderWindow : EditorWindow
    {
        [SerializeField]
        private Texture2D meleeSource;

        [SerializeField]
        private Texture2D rangedSource;

        [SerializeField]
        private Texture2D chargeSource;

        [SerializeField]
        private int rangedFireEventFrame = 7;

        private Chapter2EnemyBuildReport report;
        private Vector2 reportScroll;

        [MenuItem("Tools/Enemy Authoring/Chapter 2 Builder")]
        private static void Open()
        {
            Chapter2EnemyBuilderWindow window =
                GetWindow<Chapter2EnemyBuilderWindow>();

            window.titleContent = new GUIContent("Chapter 2 Builder");
            window.minSize = new Vector2(620f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            if (meleeSource == null)
            {
                meleeSource = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    Chapter2EnemyBuilder.DefaultMeleeSourcePath);
            }

            if (rangedSource == null)
            {
                rangedSource = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    Chapter2EnemyBuilder.DefaultRangedSourcePath);
            }

            if (chargeSource == null)
            {
                chargeSource = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    Chapter2EnemyBuilder.DefaultChargeSourcePath);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Chapter 2 Enemy Builder",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Creates Chapter 2-only Melee, Ranged, and Charge visual assets. " +
                "Chapter 1 assets and all Scenes are excluded. " +
                "Nothing changes until Apply Changes is clicked.",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            meleeSource = (Texture2D)EditorGUILayout.ObjectField(
                "Melee Source PNG",
                meleeSource,
                typeof(Texture2D),
                false);

            rangedSource = (Texture2D)EditorGUILayout.ObjectField(
                "Ranged Source PNG",
                rangedSource,
                typeof(Texture2D),
                false);

            chargeSource = (Texture2D)EditorGUILayout.ObjectField(
                "Charge Source PNG",
                chargeSource,
                typeof(Texture2D),
                false);

            rangedFireEventFrame = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Ranged Fire Event Frame",
                    "Zero-based frame index in the 10-frame Ranged Attack clip."),
                rangedFireEventFrame,
                0,
                9);

            if (EditorGUI.EndChangeCheck())
            {
                report = null;
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Dry Run", GUILayout.Height(32f)))
                {
                    report = Chapter2EnemyBuilder.CreateDryRun(
                        CreateRequest());
                }

                using (new EditorGUI.DisabledScope(
                           report == null || !report.IsValid))
                {
                    if (GUILayout.Button(
                            "Apply Changes",
                            GUILayout.Height(32f)))
                    {
                        ApplyChanges();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);

            reportScroll = EditorGUILayout.BeginScrollView(reportScroll);

            string reportText = report == null
                ? "Run Dry Run to inspect every planned create/update operation."
                : report.Text;

            EditorGUILayout.TextArea(
                reportText,
                GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();
        }

        private void ApplyChanges()
        {
            Chapter2EnemyBuildRequest request = CreateRequest();
            Chapter2EnemyBuildReport latestDryRun =
                Chapter2EnemyBuilder.CreateDryRun(request);

            report = latestDryRun;

            if (!latestDryRun.IsValid)
                return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Chapter 2 Enemy Visuals?",
                "This will create/update Chapter 2 Melee, Ranged, and Charge visual assets " +
                "and apply SpriteRenderer/Animator/Controller changes to the three " +
                "existing C2 Prefabs. Chapter 1 and Scenes are excluded.",
                "Apply",
                "Cancel");

            if (!confirmed)
                return;

            try
            {
                report = Chapter2EnemyBuilder.Apply(request);
                EditorUtility.DisplayDialog(
                    "Chapter 2 Builder",
                    "Melee, Ranged, and Charge visual assets were built successfully. " +
                    "Review the report and test the three C2 Prefabs in the Unity Editor.",
                    "OK");
            }
            catch (System.Exception exception)
            {
                report = new Chapter2EnemyBuildReport();
                report.AddError(exception.Message);
                report.AddApplySummary();
                Debug.LogException(exception);

                EditorUtility.DisplayDialog(
                    "Chapter 2 Builder Failed",
                    "The build stopped. Check the report and Console for details.",
                    "OK");
            }
        }

        private Chapter2EnemyBuildRequest CreateRequest()
        {
            return new Chapter2EnemyBuildRequest
            {
                MeleeSource = meleeSource,
                RangedSource = rangedSource,
                ChargeSource = chargeSource,
                RangedFireEventFrame = rangedFireEventFrame
            };
        }
    }
}
