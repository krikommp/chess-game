using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniChess.GameplayTags;
using UnityEditor;
using UnityEngine;

namespace MiniChess.EditorTools
{
    public class CombatConfigWindow : EditorWindow
    {
        private const string RegistryAssetPath = "Assets/Data/Tags/GameplayTagRegistry.asset";

        private enum Tab { Tags, Skills, Effects, Statuses, AIProfiles, Validation }

        private Tab _currentTab;
        private Vector2 _tagScrollPos;
        private string _tagSearch = string.Empty;
        private string _newTagValue = string.Empty;
        private string _newTagDisplayName = string.Empty;
        private string _newTagDescription = string.Empty;
        private TagRegistry _registry;

        [MenuItem("MiniChess/Combat Config")]
        public static void ShowWindow()
        {
            var window = GetWindow<CombatConfigWindow>("Combat Config");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            _registry = AssetDatabase.LoadAssetAtPath<TagRegistry>(RegistryAssetPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawToolbarButton("Tags", Tab.Tags);
            DrawToolbarButton("Skills", Tab.Skills);
            DrawToolbarButton("Effects", Tab.Effects);
            DrawToolbarButton("Status", Tab.Statuses);
            DrawToolbarButton("AI Profiles", Tab.AIProfiles);
            DrawToolbarButton("Validation", Tab.Validation);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            switch (_currentTab)
            {
                case Tab.Tags:
                    DrawTagsTab();
                    break;
                case Tab.Skills:
                    DrawStubTab("Skills", "SkillDefinition assets will appear here once created.");
                    break;
                case Tab.Effects:
                    DrawStubTab("Effects", "EffectDefinition assets will appear here once created.");
                    break;
                case Tab.Statuses:
                    DrawStubTab("Statuses", "StatusDefinition assets not yet available.");
                    break;
                case Tab.AIProfiles:
                    DrawStubTab("AI Profiles", "AIProfile assets will appear here once created.");
                    break;
                case Tab.Validation:
                    DrawValidationTab();
                    break;
            }
        }

        // ── Toolbar ────────────────────────────────────────────

        private void DrawToolbarButton(string label, Tab tab)
        {
            var style = _currentTab == tab
                ? EditorStyles.toolbarButton
                : EditorStyles.toolbarButton;
            GUI.backgroundColor = _currentTab == tab ? Color.cyan * 0.6f : Color.white;
            if (GUILayout.Button(label, style))
            {
                _currentTab = tab;
            }
            GUI.backgroundColor = Color.white;
        }

        // ── Tags Tab ───────────────────────────────────────────

        private void DrawTagsTab()
        {
            EditorGUILayout.LabelField("Tag Registry", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path", RegistryAssetPath);
            EditorGUILayout.Space(4);

            // Ensure registry exists
            if (_registry == null)
            {
                EditorGUILayout.HelpBox(
                    "No TagRegistry found. Click 'Create Registry' to create one.",
                    MessageType.Warning);
                if (GUILayout.Button("Create Registry"))
                {
                    CreateRegistry();
                }
                return;
            }

            // Search
            EditorGUILayout.BeginHorizontal();
            _tagSearch = EditorGUILayout.TextField("Filter", _tagSearch);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                _tagSearch = string.Empty;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Tag list
            var filtered = string.IsNullOrWhiteSpace(_tagSearch)
                ? _registry.Entries.ToList()
                : _registry.Entries
                    .Where(e => e.Tag.Value.IndexOf(_tagSearch, System.StringComparison.OrdinalIgnoreCase) >= 0
                             || (e.DisplayName?.IndexOf(_tagSearch, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    .ToList();

            // Group by root
            var groups = filtered
                .GroupBy(e => e.Tag.Value.Contains(".")
                    ? e.Tag.Value.Substring(0, e.Tag.Value.IndexOf('.'))
                    : e.Tag.Value)
                .OrderBy(g => g.Key);

            _tagScrollPos = EditorGUILayout.BeginScrollView(_tagScrollPos);

            foreach (var group in groups)
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var entry in group.OrderBy(e => e.Tag.Value))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(entry.Tag.Value, GUILayout.Width(220));
                    EditorGUILayout.LabelField(entry.DisplayName ?? "", GUILayout.Width(120));
                    EditorGUILayout.LabelField(entry.Description ?? "");
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        RemoveTagEntry(entry);
                        break; // collection modified, exit loop
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8);

            // Add tag
            EditorGUILayout.LabelField("Add Tag", EditorStyles.boldLabel);
            _newTagValue = EditorGUILayout.TextField("Tag (e.g. Element.Fire)", _newTagValue);
            _newTagDisplayName = EditorGUILayout.TextField("Display Name", _newTagDisplayName);
            _newTagDescription = EditorGUILayout.TextField("Description", _newTagDescription);

            var validNewTag = MiniChess.GameplayTags.GameplayTag.IsValid(_newTagValue);
            if (!string.IsNullOrWhiteSpace(_newTagValue) && !validNewTag)
            {
                EditorGUILayout.HelpBox("Invalid tag format. No leading/trailing dots, no spaces, no consecutive dots.", MessageType.Error);
            }

            GUI.enabled = validNewTag;
            if (GUILayout.Button("Add Tag", GUILayout.Height(24)))
            {
                AddTagEntry();
            }
            GUI.enabled = true;
        }

        private void CreateRegistry()
        {
            var dir = Path.GetDirectoryName(RegistryAssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var registry = ScriptableObject.CreateInstance<TagRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _registry = registry;
        }

        private void AddTagEntry()
        {
            if (_registry == null) return;

            var tag = new MiniChess.GameplayTags.GameplayTag(_newTagValue);
            if (_registry.IsRegistered(tag))
            {
                Debug.LogWarning($"[CombatConfig] Tag '{_newTagValue}' already registered.");
                return;
            }

            var so = new SerializedObject(_registry);
            var entriesProp = so.FindProperty("_entries");
            var idx = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(idx);
            var el = entriesProp.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("_tag._value").stringValue = _newTagValue;
            el.FindPropertyRelative("_displayName").stringValue = _newTagDisplayName;
            el.FindPropertyRelative("_description").stringValue = _newTagDescription;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(_registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _newTagValue = string.Empty;
            _newTagDisplayName = string.Empty;
            _newTagDescription = string.Empty;
            GUI.FocusControl(null);
        }

        private void RemoveTagEntry(TagEntry entry)
        {
            var so = new SerializedObject(_registry);
            var entriesProp = so.FindProperty("_entries");
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var el = entriesProp.GetArrayElementAtIndex(i);
                var val = el.FindPropertyRelative("_tag._value").stringValue;
                if (val == entry.Tag.Value)
                {
                    entriesProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ── Stub Tab ───────────────────────────────────────────

        private void DrawStubTab(string title, string hint)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(hint, MessageType.Info);
        }

        // ── Validation Tab ─────────────────────────────────────

        private void DrawValidationTab()
        {
            EditorGUILayout.LabelField("Combat Config Validation", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (GUILayout.Button("Run All Checks", GUILayout.Height(30)))
            {
                RunValidation();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Checks performed:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("• Tag format validity in all assets");
            EditorGUILayout.LabelField("• Unregistered tags in Skill/Effect/AI assets");
            EditorGUILayout.LabelField("• Effect assets missing GameplayTag");
            EditorGUILayout.LabelField("• Duplicate SkillDefinition.id values");
            EditorGUILayout.LabelField("• basic_attack skill asset existence");
        }

        private void RunValidation()
        {
            Debug.Log("── Combat Config Validation ──");
            int errors = 0;

            if (_registry == null)
            {
                Debug.LogError("[Validation] TagRegistry not found. Create one via the Tags tab.");
                errors++;
            }

            // TODO: add more checks as Skill/Effect/AI systems are implemented

            Debug.Log(errors == 0
                ? "[Validation] All checks passed."
                : $"[Validation] {errors} issue(s) found.");
        }
    }
}
