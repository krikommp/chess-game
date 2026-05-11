using System.Collections.Generic;
using System.Linq;
using MiniChess.GameplayTags;
using UnityEditor;
using UnityEngine;

namespace MiniChess.EditorTools
{
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        private const string k_RegistryPath = "Assets/Data/Tags/GameplayTagRegistry.asset";

        private static TagRegistry s_registry;
        private static List<string> s_registeredTagValues = new List<string>();
        private static double s_lastLoadAttemptTime;
        private static int s_loadFailCount;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureRegistryLoaded();

            var valueProp = property.FindPropertyRelative("m_value");
            var idProp = property.FindPropertyRelative("m_id");

            EditorGUI.BeginProperty(position, label, property);

            // Layout: label | text field | dropdown button | validation icon
            Rect labelRect = EditorGUI.PrefixLabel(position, label);
            float btnWidth = 20f;
            float iconWidth = 20f;
            float fieldWidth = labelRect.width - btnWidth - iconWidth - 4f;

            Rect fieldRect = new Rect(labelRect.x, labelRect.y, fieldWidth, labelRect.height);
            Rect btnRect = new Rect(labelRect.x + fieldWidth + 2f, labelRect.y, btnWidth, labelRect.height);
            Rect iconRect = new Rect(labelRect.x + fieldWidth + btnWidth + 4f, labelRect.y, iconWidth, labelRect.height);

            string currentValue = valueProp.stringValue;

            // Text field
            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUI.TextField(fieldRect, currentValue);
            if (EditorGUI.EndChangeCheck())
            {
                valueProp.stringValue = newValue;
                idProp.intValue = string.IsNullOrEmpty(newValue) ? 0 : GameplayTag.ComputeTagHash(newValue);
                valueProp.serializedObject.ApplyModifiedProperties();
            }

            // Dropdown button
            if (GUI.Button(btnRect, "▼", EditorStyles.miniButton))
            {
                ShowTagMenu(valueProp, idProp);
            }

            // Validation indicator
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
                // Empty is technically invalid but common as default — no indicator
            }
            else if (!GameplayTag.IsValid(value))
            {
                GUIContent errorIcon = EditorGUIUtility.IconContent("console.erroricon");
                if (errorIcon != null && errorIcon.image != null)
                {
                    GUI.Label(rect, new GUIContent(errorIcon.image, $"Invalid tag: '{value}'"));
                }
            }
            else if (!IsRegistered(value))
            {
                GUIContent warnIcon = EditorGUIUtility.IconContent("console.warnicon");
                if (warnIcon != null && warnIcon.image != null)
                {
                    GUI.Label(rect, new GUIContent(warnIcon.image, $"Unregistered tag: '{value}'"));
                }
            }
        }

        private void ShowTagMenu(SerializedProperty valueProp, SerializedProperty idProp)
        {
            var menu = new GenericMenu();

            if (s_registeredTagValues.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("(No registered tags in TagRegistry)"));
            }
            else
            {
                // Group by root segment
                var groups = s_registeredTagValues
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
                        bool isCurrent = string.Equals(valueProp.stringValue, tagValue, System.StringComparison.OrdinalIgnoreCase);

                        string menuPath = tagValue.Replace('.', '/');
                        menu.AddItem(
                            new GUIContent(menuPath),
                            isCurrent,
                            () =>
                            {
                                valueProp.stringValue = captured;
                                idProp.intValue = capturedHash;
                                valueProp.serializedObject.ApplyModifiedProperties();
                            }
                        );
                    }
                }
            }

            // Add "Clear" option
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Clear"), false, () =>
            {
                valueProp.stringValue = string.Empty;
                idProp.intValue = 0;
                valueProp.serializedObject.ApplyModifiedProperties();
            });

            menu.ShowAsContext();
        }

        private void EnsureRegistryLoaded()
        {
            double now = EditorApplication.timeSinceStartup;

            // Determine retry interval: immediate on first attempt, then exponential backoff
            double retryInterval = s_loadFailCount == 0 ? 0.0
                : s_loadFailCount == 1 ? 0.1
                : s_loadFailCount == 2 ? 0.5
                : s_loadFailCount == 3 ? 2.0
                : 5.0;

            bool needsLoad = s_registry == null || (now - s_lastLoadAttemptTime) >= retryInterval;

            if (!needsLoad)
                return;

            s_lastLoadAttemptTime = now;
            s_registry = AssetDatabase.LoadAssetAtPath<TagRegistry>(k_RegistryPath);

            if (s_registry != null)
            {
                s_loadFailCount = 0;
                s_registeredTagValues = s_registry.Entries
                    .Select(e =>
                    {
                        try { return e.Tag.Value; }
                        catch { return null; }
                    })
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct(System.StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v, System.StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                s_loadFailCount++;
                s_registeredTagValues.Clear();
            }
        }

        private bool IsRegistered(string value)
        {
            return s_registeredTagValues.Contains(value, System.StringComparer.OrdinalIgnoreCase);
        }
    }
}
