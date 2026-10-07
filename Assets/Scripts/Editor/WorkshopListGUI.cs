using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Cards.EditorTools
{
    /// <summary>
    /// Overwatch-Workshop-style list UI for effect assets: every row has a drag handle so it
    /// can be reordered, a foldout to edit the row's parameters inline, an object field to
    /// swap in another asset of the same type, and an × to remove it. The "+ Add" button at
    /// the bottom opens a menu of concrete types (workshop's action dropdown) that creates a
    /// new asset of the picked type and drops it into the row.
    /// Used by CardEffectSOEditor for lists of card modifiers and on-hit effects.
    /// </summary>
    internal static class WorkshopListGUI
    {
        private const float RowHeight = 20f;
        private const float HandleWidth = 18f;
        private const float FoldoutWidth = 14f;
        private const float RemoveWidth = 22f;

        private static string dragListKey;
        private static int dragIndex = -1;
        private static int dropIndex = -1;

        /// <summary>
        /// Draws every visible field of the object. Lists of card effects / card options become
        /// workshop-style rows; everything else uses the default property UI.
        /// </summary>
        public static void DrawProperties(SerializedObject serializedObject, HashSet<int> expanded)
        {
            Type ownerType = serializedObject.targetObject.GetType();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script") continue;

                if (TryGetSpecialElementType(ownerType, iterator.name, out Type elementType))
                {
                    DrawList(iterator, elementType, expanded);
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        private static void DrawList(SerializedProperty listProp, Type elementType, HashSet<int> expanded)
        {
            SerializedObject host = listProp.serializedObject;
            string listKey = host.targetObject.GetInstanceID() + ":" + listProp.propertyPath;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(listProp.name), EditorStyles.boldLabel);

            int size = listProp.arraySize;
            var rowRects = new Rect[size];

            for (int i = 0; i < size; i++)
            {
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                UnityEngine.Object value = element.objectReferenceValue;

                Rect row = GUILayoutUtility.GetRect(0f, float.MaxValue, RowHeight, RowHeight,
                    GUILayout.ExpandWidth(true));
                rowRects[i] = row;

                bool dropHere = dragListKey == listKey && dropIndex == i && dragIndex >= 0;
                DrawRowChrome(row, i, dropHere);

                // Drag handle.
                Rect handle = new Rect(row.x + 1f, row.y + 1f, HandleWidth, RowHeight - 2f);
                GUI.Label(handle, "≡", EditorStyles.centeredGreyMiniLabel);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.Pan);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && handle.Contains(Event.current.mousePosition))
                {
                    dragListKey = listKey;
                    dragIndex = i;
                    dropIndex = i;
                    Event.current.Use();
                }

                // Foldout to expand the row's parameters, like a workshop action row.
                float fieldX = row.x + HandleWidth + 4f;
                if (value != null)
                {
                    int id = value.GetInstanceID();
                    bool wasOpen = expanded.Contains(id);
                    Rect fold = new Rect(fieldX, row.y, FoldoutWidth, RowHeight);
                    bool isOpen = EditorGUI.Foldout(fold, wasOpen, GUIContent.none, true);
                    if (isOpen != wasOpen)
                    {
                        if (isOpen) expanded.Add(id);
                        else expanded.Remove(id);
                        GUI.changed = true;
                    }
                    fieldX += FoldoutWidth + 2f;
                }

                // Object field: pick among existing assets of this type (or clear the row).
                Rect fieldRect = new Rect(fieldX, row.y,
                    row.xMax - RemoveWidth - 4f - fieldX, RowHeight);
                UnityEngine.Object picked = EditorGUI.ObjectField(fieldRect, value, elementType, false);
                if (picked != value)
                    element.objectReferenceValue = picked;

                // Remove.
                Rect removeRect = new Rect(row.xMax - RemoveWidth, row.y, RemoveWidth, RowHeight);
                if (GUI.Button(removeRect, "×", EditorStyles.miniButton))
                {
                    element.objectReferenceValue = null;
                    listProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                // Inline parameters of the expanded row.
                if (value != null && expanded.Contains(value.GetInstanceID()))
                    DrawInlineFields(value, expanded);
            }

            HandleDrag(listProp, listKey, rowRects);

            Rect addRect = GUILayoutUtility.GetRect(0f, float.MaxValue, RowHeight, RowHeight,
                GUILayout.ExpandWidth(true));
            if (GUI.Button(addRect, "+ Add", EditorStyles.miniButton))
                ShowAddMenu(host, listProp.propertyPath, elementType, expanded);

            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// Workshop rows for a card's step list (CardData.Steps): drag a handle to reorder,
        /// expand a row to edit the step's own fields, its attached card options (modifiers)
        /// and the effect's parameters all inline. Used by CardDataEditor so everything a step
        /// does is editable right in the card's inspector.
        /// </summary>
        internal static void DrawStepList(SerializedProperty stepsProp, HashSet<int> expanded)
        {
            SerializedObject host = stepsProp.serializedObject;
            string listKey = host.targetObject.GetInstanceID() + ":" + stepsProp.propertyPath;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(stepsProp.name), EditorStyles.boldLabel);

            int size = stepsProp.arraySize;
            var rowRects = new Rect[size];

            for (int i = 0; i < size; i++)
            {
                SerializedProperty stepEl = stepsProp.GetArrayElementAtIndex(i);
                SerializedProperty effectProp = stepEl.FindPropertyRelative("Effect");
                UnityEngine.Object value = effectProp?.objectReferenceValue;

                Rect row = GUILayoutUtility.GetRect(0f, float.MaxValue, RowHeight, RowHeight,
                    GUILayout.ExpandWidth(true));
                rowRects[i] = row;

                bool dropHere = dragListKey == listKey && dropIndex == i && dragIndex >= 0;
                DrawRowChrome(row, i, dropHere);

                // Drag handle.
                Rect handle = new Rect(row.x + 1f, row.y + 1f, HandleWidth, RowHeight - 2f);
                GUI.Label(handle, "≡", EditorStyles.centeredGreyMiniLabel);
                EditorGUIUtility.AddCursorRect(handle, MouseCursor.Pan);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && handle.Contains(Event.current.mousePosition))
                {
                    dragListKey = listKey;
                    dragIndex = i;
                    dropIndex = i;
                    Event.current.Use();
                }

                // Foldout to expand the step row.
                float fieldX = row.x + HandleWidth + 4f;
                bool isOpen = false;
                if (value != null)
                {
                    int id = value.GetInstanceID();
                    isOpen = expanded.Contains(id);
                    Rect fold = new Rect(fieldX, row.y, FoldoutWidth, RowHeight);
                    bool nowOpen = EditorGUI.Foldout(fold, isOpen, GUIContent.none, true);
                    if (nowOpen != isOpen)
                    {
                        if (nowOpen) expanded.Add(id);
                        else expanded.Remove(id);
                        GUI.changed = true;
                    }
                    fieldX += FoldoutWidth + 2f;
                }

                // Object field: which effect this step runs.
                Rect fieldRect = new Rect(fieldX, row.y,
                    row.xMax - RemoveWidth - 4f - fieldX, RowHeight);
                if (effectProp != null)
                {
                    UnityEngine.Object picked = EditorGUI.ObjectField(fieldRect, value, typeof(CardEffectSO), false);
                    if (picked != value)
                        effectProp.objectReferenceValue = picked;
                }

                // Remove the step.
                Rect removeRect = new Rect(row.xMax - RemoveWidth, row.y, RemoveWidth, RowHeight);
                if (GUI.Button(removeRect, "×", EditorStyles.miniButton))
                {
                    stepsProp.DeleteArrayElementAtIndex(i);
                    break;
                }

                // Inline step parameters, card options and effect fields.
                if (value != null && isOpen)
                    DrawExpandedStep(stepEl, value, expanded);
            }

            HandleDrag(stepsProp, listKey, rowRects);

            Rect addRect = GUILayoutUtility.GetRect(0f, float.MaxValue, RowHeight, RowHeight,
                GUILayout.ExpandWidth(true));
            if (GUI.Button(addRect, "+ Add Step", EditorStyles.miniButton))
            {
                stepsProp.arraySize++;
            }

            EditorGUILayout.Space(4f);
        }

        private static void DrawExpandedStep(SerializedProperty stepEl, UnityEngine.Object effect,
            HashSet<int> expanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(stepEl.FindPropertyRelative("TargetType"));
            EditorGUILayout.PropertyField(stepEl.FindPropertyRelative("Range"));
            EditorGUILayout.PropertyField(stepEl.FindPropertyRelative("CellConstraint"));

            // Card options attached to this step ("set cost to 1", ...).
            SerializedProperty modifiers = stepEl.FindPropertyRelative("CardModifiers");
            if (modifiers != null)
                DrawList(modifiers, typeof(CardModifierSO), expanded);

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Effect", EditorStyles.boldLabel);
            DrawInlineFields(effect, expanded);
            EditorGUI.indentLevel--;
        }

        private static void DrawRowChrome(Rect row, int index, bool showDropLine)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(row, new Color(0f, 0f, 0f, index % 2 == 0 ? 0.05f : 0.11f));
            if (showDropLine)
                EditorGUI.DrawRect(new Rect(row.x, row.y, row.width, 2f),
                    new Color(0.35f, 0.6f, 1f, 0.95f));
        }

        private static void HandleDrag(SerializedProperty listProp, string listKey, Rect[] rowRects)
        {
            if (dragListKey == null || dragListKey != listKey) return;

            Event e = Event.current;
            if (e.type == EventType.MouseDrag)
            {
                // Default: dropping past the last row appends to the list.
                dropIndex = rowRects.Length;
                for (int i = 0; i < rowRects.Length; i++)
                {
                    if (!rowRects[i].Contains(e.mousePosition)) continue;
                    dropIndex = e.mousePosition.y < rowRects[i].y + rowRects[i].height * 0.5f ? i : i + 1;
                    break;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                int src = dragIndex;
                int dst = dropIndex;
                dragListKey = null;
                dragIndex = -1;
                dropIndex = -1;
                e.Use();

                if (src < 0 || src >= listProp.arraySize) return;
                // Moving down: removing the source first shifts later rows left by one.
                int target = src < dst ? dst - 1 : dst;
                if (target != src)
                    listProp.MoveArrayElement(src, target);
            }
        }

        private static void ShowAddMenu(SerializedObject host, string listPath, Type elementType,
            HashSet<int> expanded)
        {
            var menu = new GenericMenu();
            bool any = false;
            foreach (Type type in TypeCache.GetTypesDerivedFrom(elementType))
            {
                if (type.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(type)) continue;
                any = true;
                Type captured = type;
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false,
                    () => CreateElement(host, listPath, captured, expanded));
            }
            if (!any)
                menu.AddDisabledItem(new GUIContent($"No {elementType.Name} types available"));
            menu.ShowAsContext();
        }

        private static void CreateElement(SerializedObject host, string listPath, Type type,
            HashSet<int> expanded)
        {
            var instance = (ScriptableObject)ScriptableObject.CreateInstance(type);

            string hostPath = AssetDatabase.GetAssetPath(host.targetObject);
            string folder = string.IsNullOrEmpty(hostPath)
                ? "Assets"
                : Path.GetDirectoryName(hostPath).Replace('\\', '/');
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + FileNameFor(type) + ".asset");
            AssetDatabase.CreateAsset(instance, assetPath);
            Undo.RegisterCreatedObjectUndo(instance, "Add " + type.Name);

            host.Update();
            SerializedProperty list = host.FindProperty(listPath);
            if (list != null)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = instance;
                host.ApplyModifiedProperties();
            }

            expanded.Add(instance.GetInstanceID());
            AssetDatabase.SaveAssets();
        }

        private static void DrawInlineFields(UnityEngine.Object target, HashSet<int> expanded)
        {
            EditorGUI.indentLevel++;
            var serializedObject = new SerializedObject(target);
            Type ownerType = target.GetType();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script") continue;

                if (TryGetSpecialElementType(ownerType, iterator.name, out Type elementType))
                {
                    DrawList(iterator, elementType, expanded);
                    continue;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUI.indentLevel--;
        }

        private static bool TryGetSpecialElementType(Type ownerType, string fieldName, out Type elementType)
        {
            elementType = null;
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return false;

            Type fieldType = field.FieldType;
            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(List<>))
                return false;

            Type argument = fieldType.GetGenericArguments()[0];
            if (argument != typeof(CardEffectSO) && argument != typeof(CardModifierSO))
                return false;

            elementType = argument;
            return true;
        }

        private static string FileNameFor(Type type)
        {
            var attribute = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(
                type, typeof(CreateAssetMenuAttribute));
            string name = !string.IsNullOrEmpty(attribute?.fileName) ? attribute.fileName : type.Name;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }
    }
}
