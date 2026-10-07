using System.Collections.Generic;
using UnityEditor;

namespace Cards.EditorTools
{
    /// <summary>
    /// Inspector for card assets (CardData), laid out like the Overwatch Workshop: the card's
    /// Steps render as draggable rows that expand to edit the step's target/range, the card
    /// options attached to it (+ Add) and the effect's parameters inline. Everything else uses
    /// the default property UI.
    /// </summary>
    [CustomEditor(typeof(CardData), true)]
    public class CardDataEditor : UnityEditor.Editor
    {
        private readonly HashSet<int> expanded = new HashSet<int>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script") continue;

                if (iterator.name == "Steps")
                {
                    WorkshopListGUI.DrawStepList(iterator, expanded);
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}