using UnityEditor;
using UnityEngine;

namespace Enemies.EditorTools
{
    /// <summary>Adds a "Procedural Preview" panel so a group's composition can be checked against a
    /// difficulty budget without running the game.</summary>
    [CustomEditor(typeof(EnemyGroup))]
    public class EnemyGroupEditor : UnityEditor.Editor
    {
        private int previewBudget = 60;
        private EncounterPlan previewPlan;

        private void OnEnable()
        {
            previewPlan = null;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var group = (EnemyGroup)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Procedural Preview", EditorStyles.boldLabel);

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                previewBudget = EditorGUILayout.IntField("Difficulty Budget", Mathf.Max(0, previewBudget));
                if (check.changed) previewPlan = null;
            }

            if (GUILayout.Button("Generate plan"))
                previewPlan = EnemySpawnGenerator.Generate(group, previewBudget, null);

            if (previewPlan == null) return;

            EditorGUILayout.Space();
            foreach (CountedEnemy counted in previewPlan.Enemies)
                EditorGUILayout.LabelField($"{counted.Count} x {EnemyName(counted.Type)}");

            string overrun = previewPlan.Overshot ? " (minima forced above budget)" : "";
            EditorGUILayout.LabelField($"Total: {previewPlan.TotalCost} / {previewPlan.Budget}{overrun}");
            if (previewPlan.Budget > 0 && previewPlan.TotalCost > previewPlan.Budget && !previewPlan.Overshot)
                EditorGUILayout.HelpBox("Plan exceeds the budget unexpectedly.", MessageType.Error);
        }

        private static string EnemyName(EnemyType type)
        {
            return string.IsNullOrEmpty(type.EnemyName) ? type.name : type.EnemyName;
        }
    }
}