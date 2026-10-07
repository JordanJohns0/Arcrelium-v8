using System.Collections.Generic;
using UnityEditor;

namespace Cards.EditorTools
{
    /// <summary>
    /// Inspector for every card effect asset, laying its fields out like the Overwatch
    /// Workshop action list: lists of card effects / card options render as draggable rows
    /// with inline parameters and an "+ Add" type menu. Everything else uses the default
    /// property UI.
    /// </summary>
    [CustomEditor(typeof(CardEffectSO), true)]
    public class CardEffectSOEditor : UnityEditor.Editor
    {
        private readonly HashSet<int> expanded = new HashSet<int>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            WorkshopListGUI.DrawProperties(serializedObject, expanded);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
