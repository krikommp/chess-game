using System.Linq;
using MiniChess.GameplayTags;
using UnityEditor;
using UnityEngine;

namespace MiniChess.EditorTools
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProp = property.FindPropertyRelative("m_value");
            var idProp = property.FindPropertyRelative("m_id");

            EditorGUI.BeginProperty(position, label, property);

            Rect labelRect = EditorGUI.PrefixLabel(position, label);
            float btnWidth = 20f;
            float iconWidth = 20f;
            float fieldWidth = labelRect.width - btnWidth - iconWidth - 4f;

            Rect fieldRect = new Rect(labelRect.x, labelRect.y, fieldWidth, labelRect.height);
            Rect btnRect = new Rect(labelRect.x + fieldWidth + 2f, labelRect.y, btnWidth, labelRect.height);
            Rect iconRect = new Rect(labelRect.x + fieldWidth + btnWidth + 4f, labelRect.y, iconWidth, labelRect.height);

            string currentValue = valueProp.stringValue;

            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUI.TextField(fieldRect, currentValue);
            if (EditorGUI.EndChangeCheck())
            {
                valueProp.stringValue = newValue;
                idProp.intValue = string.IsNullOrEmpty(newValue) ? 0 : GameplayTag.ComputeTagHash(newValue);
                valueProp.serializedObject.ApplyModifiedProperties();
            }

            if (GUI.Button(btnRect, "v", EditorStyles.miniButton))
            {
                ShowTagMenu(valueProp, idProp);
            }

            DrawValidationIndicator(iconRect, currentValue);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        private void DrawValidationIndicator(Rect rect, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (!GameplayTag.IsValid(value))
            {
                GUIContent errorIcon = EditorGUIUtility.IconContent("console.erroricon");
                if (errorIcon != null && errorIcon.image != null)
                {
                    GUI.Label(rect, new GUIContent(errorIcon.image, $"Invalid tag: '{value}'"));
                }
            }
            else if (!GameplayTagEditorSources.IsKnown(value))
            {
                GUIContent warnIcon = EditorGUIUtility.IconContent("console.warnicon");
                if (warnIcon != null && warnIcon.image != null)
                {
                    GUI.Label(rect, new GUIContent(warnIcon.image, $"Unknown tag: '{value}'"));
                }
            }
        }

        private void ShowTagMenu(SerializedProperty valueProp, SerializedProperty idProp)
        {
            var menu = new GenericMenu();
            var tagValues = GameplayTagEditorSources.KnownTagValues;

            if (tagValues.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("(No tags in TagRegistry)"));
            }
            else
            {
                var groups = tagValues
                    .GroupBy(v =>
                    {
                        int dotIdx = v.IndexOf('.');
                        return dotIdx >= 0 ? v.Substring(0, dotIdx) : v;
                    })
                    .OrderBy(g => g.Key, System.StringComparer.OrdinalIgnoreCase);

                foreach (var group in groups)
                {
                    foreach (string tagValue in group.OrderBy(v => v, System.StringComparer.OrdinalIgnoreCase))
                    {
                        string captured = tagValue;
                        int capturedHash = GameplayTag.ComputeTagHash(captured);
                        bool isCurrent = string.Equals(
                            valueProp.stringValue,
                            tagValue,
                            System.StringComparison.OrdinalIgnoreCase);

                        string menuPath = tagValue.Replace('.', '/');
                        menu.AddItem(
                            new GUIContent(menuPath),
                            isCurrent,
                            () =>
                            {
                                valueProp.stringValue = captured;
                                idProp.intValue = capturedHash;
                                valueProp.serializedObject.ApplyModifiedProperties();
                            });
                    }
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(
                new GUIContent("Clear"),
                false,
                () =>
                {
                    valueProp.stringValue = string.Empty;
                    idProp.intValue = 0;
                    valueProp.serializedObject.ApplyModifiedProperties();
                });

            menu.ShowAsContext();
        }
    }
}
