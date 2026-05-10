using System.Collections.Generic;
using System.IO;
using System.Linq;
using MiniChess.Combat.Skills;
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
        private Vector2 _validationScrollPos;
        private string _tagSearch = string.Empty;
        private string _newTagValue = string.Empty;
        private string _newTagDisplayName = string.Empty;
        private string _newTagDescription = string.Empty;
        private TagRegistry _registry;
        private List<ValidationIssue> _validationIssues = new List<ValidationIssue>();
        private bool _validationRun;

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

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run All Checks", GUILayout.Height(30), GUILayout.Width(140)))
            {
                _validationIssues.Clear();
                RunValidation();
                _validationRun = true;
            }
            if (GUILayout.Button("Clear", GUILayout.Height(30), GUILayout.Width(80)))
            {
                _validationIssues.Clear();
                _validationRun = false;
            }
            EditorGUILayout.EndHorizontal();

            if (!_validationRun)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Checks performed:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• SkillDefinition.id non-empty / unique");
                EditorGUILayout.LabelField("• apCost, cooldown, range validity");
                EditorGUILayout.LabelField("• Effect references non-null, Effect tags present");
                EditorGUILayout.LabelField("• Unregistered tags in Skill/Effect assets");
                EditorGUILayout.LabelField("• basic_attack skill asset existence");
                EditorGUILayout.LabelField("• basic_attack references valid DamageEffect");
                return;
            }

            EditorGUILayout.Space(4);
            int errorCount = 0;
            int warningCount = 0;
            for (int i = 0; i < _validationIssues.Count; i++)
            {
                if (_validationIssues[i].Severity == ValidationSeverity.Error) errorCount++;
                else if (_validationIssues[i].Severity == ValidationSeverity.Warning) warningCount++;
            }

            var summaryStyle = new GUIStyle(EditorStyles.boldLabel);
            summaryStyle.normal.textColor = errorCount > 0 ? Color.red : Color.green;
            var summary = errorCount > 0
                ? $"{errorCount} error(s), {warningCount} warning(s)"
                : "All checks passed";
            EditorGUILayout.LabelField(summary, summaryStyle);
            EditorGUILayout.Space(4);

            _validationScrollPos = EditorGUILayout.BeginScrollView(_validationScrollPos);
            for (int i = 0; i < _validationIssues.Count; i++)
            {
                var issue = _validationIssues[i];
                var icon = issue.Severity == ValidationSeverity.Error ? "✘" : "⚠";
                var color = issue.Severity == ValidationSeverity.Error ? Color.red : Color.yellow;
                if (issue.Severity == ValidationSeverity.Info)
                {
                    icon = "ℹ";
                    color = Color.white;
                }

                var oldColor = GUI.color;
                GUI.color = color;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = oldColor;
                EditorGUILayout.LabelField($"{icon} {issue.Message}", EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(issue.AssetPath))
                {
                    EditorGUILayout.LabelField(issue.AssetPath, EditorStyles.miniLabel);
                    if (GUILayout.Button("Ping Asset", GUILayout.Width(100)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<Object>(issue.AssetPath);
                        if (obj != null) EditorGUIUtility.PingObject(obj);
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void RunValidation()
        {
            Debug.Log("── Combat Config Validation ──");

            // 1. TagRegistry check
            if (_registry == null)
            {
                _registry = AssetDatabase.LoadAssetAtPath<TagRegistry>(RegistryAssetPath);
            }
            if (_registry == null)
            {
                AddIssue(ValidationSeverity.Error, "TagRegistry not found. Create one via the Tags tab.", RegistryAssetPath);
                Debug.LogError("[Validation] TagRegistry not found.");
            }

            var skillGuids = AssetDatabase.FindAssets("t:SkillDefinition");
            var effectGuids = AssetDatabase.FindAssets("t:EffectDefinition");
            var skillDefs = new List<SkillDefinition>(skillGuids.Length);
            var skillIdMap = new Dictionary<string, SkillDefinition>(System.StringComparer.OrdinalIgnoreCase);

            // Collect all SkillDefinitions
            for (int i = 0; i < skillGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(skillGuids[i]);
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                if (skill != null) skillDefs.Add(skill);
            }

            // 2. Check each SkillDefinition
            for (int i = 0; i < skillDefs.Count; i++)
            {
                var skill = skillDefs[i];
                var path = AssetDatabase.GetAssetPath(skill);

                ValidateSkillDefinition(skill, path, skillIdMap);
            }

            // 3. Check duplicate SkillDefinition ids
            foreach (var kvp in skillIdMap)
            {
                var id = kvp.Key;
                // Count by scanning skillDefs for this id
                int count = 0;
                for (int i = 0; i < skillDefs.Count; i++)
                {
                    if (string.Equals(skillDefs[i].Id, id, System.StringComparison.OrdinalIgnoreCase))
                        count++;
                }
                if (count > 1)
                {
                    var path = AssetDatabase.GetAssetPath(kvp.Value);
                    AddError($"[{id}] Duplicate SkillDefinition.id '{id}' ({count} occurrences)", path);
                }
            }

            // 4. Check each EffectDefinition
            var effectDefs = new Dictionary<string, EffectDefinition>();
            for (int i = 0; i < effectGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(effectGuids[i]);
                var effect = AssetDatabase.LoadAssetAtPath<EffectDefinition>(path);
                if (effect != null) effectDefs[path] = effect;
            }
            foreach (var kvp in effectDefs)
            {
                ValidateEffectDefinition(kvp.Value, kvp.Key);
            }

            // 5. basic_attack existence
            var basicAttack = FindSkillById("basic_attack", skillDefs);
            if (basicAttack == null)
            {
                AddWarning("Skill 'basic_attack' not found. This blocks the basic-attack vertical slice.");
            }
            else
            {
                var baPath = AssetDatabase.GetAssetPath(basicAttack);
                var effects = basicAttack.Effects;
                if (effects.Length == 0)
                {
                    AddError("basic_attack has no Effect assigned.", baPath);
                }
                else
                {
                    bool hasDamage = false;
                    for (int i = 0; i < effects.Length; i++)
                    {
                        if (effects[i] == null)
                        {
                            AddError($"basic_attack.effects[{i}] is null.", baPath);
                        }
                        else if (effects[i] is DamageEffectDefinition)
                        {
                            hasDamage = true;
                        }
                    }
                    if (!hasDamage)
                    {
                        AddWarning("basic_attack does not reference a DamageEffectDefinition. It may not deal damage.", baPath);
                    }
                }
            }

            // 6. Unregistered tags in Skills
            if (_registry != null)
            {
                for (int i = 0; i < skillDefs.Count; i++)
                {
                    var skill = skillDefs[i];
                    var path = AssetDatabase.GetAssetPath(skill);
                    var tags = skill.SkillTags;
                    for (int j = 0; j < tags.Length; j++)
                        CheckTagRegistered(tags[j], $"Skill '{skill.Id}' SkillTags[{j}]", path);
                    var aiTags = skill.AiTags;
                    for (int j = 0; j < aiTags.Length; j++)
                        CheckTagRegistered(aiTags[j], $"Skill '{skill.Id}' AiTags[{j}]", path);
                    var effects = skill.Effects;
                    for (int j = 0; j < effects.Length; j++)
                    {
                        if (effects[j] == null) continue;
                        var effTags = effects[j].Tags;
                        for (int k = 0; k < effTags.Length; k++)
                            CheckTagRegistered(effTags[k], $"Skill '{skill.Id}' Effects[{j}].Tags[{k}]", path);
                    }
                }
                foreach (var kvp in effectDefs)
                {
                    var tags = kvp.Value.Tags;
                    for (int j = 0; j < tags.Length; j++)
                        CheckTagRegistered(tags[j], $"Effect '{kvp.Value.name}' Tags[{j}]", kvp.Key);
                }
            }

            // Summary to Console
            int errCount = 0;
            int warnCount = 0;
            for (int i = 0; i < _validationIssues.Count; i++)
            {
                if (_validationIssues[i].Severity == ValidationSeverity.Error) errCount++;
                else if (_validationIssues[i].Severity == ValidationSeverity.Warning) warnCount++;
            }
            Debug.Log(errCount == 0 && warnCount == 0
                ? "[Validation] All checks passed."
                : $"[Validation] {errCount} error(s), {warnCount} warning(s) found. See the Validation tab for details.");
        }

        private void ValidateSkillDefinition(SkillDefinition skill, string path,
            Dictionary<string, SkillDefinition> idMap)
        {
            var id = skill.Id;

            // id non-empty
            if (string.IsNullOrWhiteSpace(id))
            {
                AddError("SkillDefinition.id is empty.", path);
            }

            // Track for uniqueness check (case-insensitive, first-wins for reporting)
            if (!string.IsNullOrWhiteSpace(id) && !idMap.ContainsKey(id))
            {
                idMap[id] = skill;
            }

            // apCost >= 0
            if (skill.ApCost < 0)
            {
                AddError($"Skill '{id}': apCost = {skill.ApCost} (must be >= 0).", path);
            }

            // cooldown >= 0
            if (skill.Cooldown < 0)
            {
                AddError($"Skill '{id}': cooldown = {skill.Cooldown} (must be >= 0).", path);
            }

            // range >= 0
            if (skill.Range < 0f)
            {
                AddError($"Skill '{id}': range = {skill.Range} (must be >= 0).", path);
            }

            // effects not empty
            var effects = skill.Effects;
            if (effects.Length == 0)
            {
                AddWarning($"Skill '{id}': effects array is empty.", path);
            }
            else
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i] == null)
                    {
                        AddError($"Skill '{id}': effects[{i}] is null.", path);
                    }
                    else if (!effects[i].HasAnyTag())
                    {
                        AddWarning(
                            $"Skill '{id}': Effect '{effects[i].name}' has no GameplayTag.",
                            path);
                    }
                }
            }
        }

        private void ValidateEffectDefinition(EffectDefinition effect, string path)
        {
            if (!effect.HasAnyTag())
            {
                AddWarning(
                    $"Effect '{effect.name}' has no GameplayTag. Every effect should carry at least one tag.",
                    path);
            }
        }

        private void CheckTagRegistered(GameplayTagRef tagRef, string context, string assetPath)
        {
            if (!tagRef.IsValid)
            {
                AddError($"Invalid tag format in {context}: '{tagRef.Value}'", assetPath);
                return;
            }
            if (_registry != null && !_registry.IsRegistered(tagRef))
            {
                AddWarning($"Unregistered tag in {context}: '{tagRef.Value}'", assetPath);
            }
        }

        private SkillDefinition FindSkillById(string id, List<SkillDefinition> skills)
        {
            for (int i = 0; i < skills.Count; i++)
            {
                if (string.Equals(skills[i].Id, id, System.StringComparison.OrdinalIgnoreCase))
                    return skills[i];
            }
            return null;
        }

        private void AddError(string message, string assetPath = null)
        {
            AddIssue(ValidationSeverity.Error, message, assetPath);
            Debug.LogError($"[Validation] {message}" + (assetPath != null ? $" ({assetPath})" : ""));
        }

        private void AddWarning(string message, string assetPath = null)
        {
            AddIssue(ValidationSeverity.Warning, message, assetPath);
            Debug.LogWarning($"[Validation] {message}" + (assetPath != null ? $" ({assetPath})" : ""));
        }

        private void AddIssue(ValidationSeverity severity, string message, string assetPath)
        {
            _validationIssues.Add(new ValidationIssue
            {
                Severity = severity,
                Message = message,
                AssetPath = assetPath ?? string.Empty
            });
        }

        private enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }

        private struct ValidationIssue
        {
            public ValidationSeverity Severity;
            public string Message;
            public string AssetPath;
        }
    }
}
