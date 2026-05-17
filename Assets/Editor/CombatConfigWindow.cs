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
        private const string k_RegistryAssetPath = "Assets/Data/Tags/GameplayTagRegistry.asset";

        private enum ETab { Tags, Skills, Effects, Statuses, AIProfiles, Validation }

        private ETab m_currentTab;
        private Vector2 m_tagScrollPos;
        private Vector2 m_validationScrollPos;
        private string m_tagSearch = string.Empty;
        private string m_newTagValue = string.Empty;
        private string m_newTagDisplayName = string.Empty;
        private string m_newTagDescription = string.Empty;
        private TagRegistry m_registry;
        private List<ValidationIssue> m_validationIssues = new List<ValidationIssue>();
        private bool m_validationRun;

        [MenuItem("MiniChess/Combat Config")]
        public static void ShowWindow()
        {
            var window = GetWindow<CombatConfigWindow>("Combat Config");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            m_registry = AssetDatabase.LoadAssetAtPath<TagRegistry>(k_RegistryAssetPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawToolbarButton("Tags", ETab.Tags);
            DrawToolbarButton("Skills", ETab.Skills);
            DrawToolbarButton("Effects", ETab.Effects);
            DrawToolbarButton("Status", ETab.Statuses);
            DrawToolbarButton("AI Profiles", ETab.AIProfiles);
            DrawToolbarButton("Validation", ETab.Validation);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            switch (m_currentTab)
            {
                case ETab.Tags:
                    DrawTagsTab();
                    break;
                case ETab.Skills:
                    DrawStubTab("Skills", "SkillAbility assets will appear here once created.");
                    break;
                case ETab.Effects:
                    DrawStubTab("Effects", "SkillEffect assets will appear here once created.");
                    break;
                case ETab.Statuses:
                    DrawStubTab("Statuses", "StatusDefinition assets not yet available.");
                    break;
                case ETab.AIProfiles:
                    DrawStubTab("AI Profiles", "AIProfile assets will appear here once created.");
                    break;
                case ETab.Validation:
                    DrawValidationTab();
                    break;
            }
        }

        // ── Toolbar ────────────────────────────────────────────

        private void DrawToolbarButton(string label, ETab ETab)
        {
            var style = m_currentTab == ETab
                ? EditorStyles.toolbarButton
                : EditorStyles.toolbarButton;
            GUI.backgroundColor = m_currentTab == ETab ? Color.cyan * 0.6f : Color.white;
            if (GUILayout.Button(label, style))
            {
                m_currentTab = ETab;
            }
            GUI.backgroundColor = Color.white;
        }

        // ── Tags ETab ───────────────────────────────────────────

        private void DrawTagsTab()
        {
            EditorGUILayout.LabelField("Gameplay Tags", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path", k_RegistryAssetPath);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Regenerate Code", GUILayout.Width(130)))
            {
                TagCodeGenerator.Regenerate();
            }
            EditorGUILayout.HelpBox("Auto-generates GameplayTagConstants.g.cs from this registry.", MessageType.None);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Ensure registry exists
            if (m_registry == null)
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
            m_tagSearch = EditorGUILayout.TextField("Filter", m_tagSearch);
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                m_tagSearch = string.Empty;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            var filtered = string.IsNullOrWhiteSpace(m_tagSearch)
                ? m_registry.Entries.ToList()
                : m_registry.Entries
                    .Where(e => e.Tag.Value.IndexOf(m_tagSearch, System.StringComparison.OrdinalIgnoreCase) >= 0
                             || (e.DisplayName?.IndexOf(m_tagSearch, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    .ToList();

            // Group by root
            var groups = filtered
                .GroupBy(e => e.Tag.Value.Contains(".")
                    ? e.Tag.Value.Substring(0, e.Tag.Value.IndexOf('.'))
                    : e.Tag.Value)
                .OrderBy(g => g.Key);

            m_tagScrollPos = EditorGUILayout.BeginScrollView(m_tagScrollPos);

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
            m_newTagValue = EditorGUILayout.TextField("Tag (e.g. Element.Fire)", m_newTagValue);
            m_newTagDisplayName = EditorGUILayout.TextField("Display Name", m_newTagDisplayName);
            m_newTagDescription = EditorGUILayout.TextField("Description", m_newTagDescription);

            var validNewTag = MiniChess.GameplayTags.GameplayTag.IsValid(m_newTagValue);
            if (!string.IsNullOrWhiteSpace(m_newTagValue) && !validNewTag)
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
            var dir = Path.GetDirectoryName(k_RegistryAssetPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var registry = ScriptableObject.CreateInstance<TagRegistry>();
            AssetDatabase.CreateAsset(registry, k_RegistryAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            m_registry = registry;
        }

        private void AddTagEntry()
        {
            if (m_registry == null) return;

            var tag = new MiniChess.GameplayTags.GameplayTag(m_newTagValue);
            if (m_registry.IsRegistered(tag))
            {
                Debug.LogWarning($"[CombatConfig] Tag '{m_newTagValue}' already registered.");
                return;
            }

            var so = new SerializedObject(m_registry);
            var entriesProp = so.FindProperty("m_entries");
            var idx = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(idx);
            var el = entriesProp.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("m_tag.m_value").stringValue = m_newTagValue;
            el.FindPropertyRelative("m_tag.m_id").intValue = MiniChess.GameplayTags.GameplayTag.ComputeTagHash(m_newTagValue);
            el.FindPropertyRelative("m_displayName").stringValue = m_newTagDisplayName;
            el.FindPropertyRelative("m_description").stringValue = m_newTagDescription;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(m_registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameplayTagEditorSources.Reload();

            m_newTagValue = string.Empty;
            m_newTagDisplayName = string.Empty;
            m_newTagDescription = string.Empty;
            GUI.FocusControl(null);
        }

        private void RemoveTagEntry(TagEntry entry)
        {
            var so = new SerializedObject(m_registry);
            var entriesProp = so.FindProperty("m_entries");
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var el = entriesProp.GetArrayElementAtIndex(i);
                var val = el.FindPropertyRelative("m_tag.m_value").stringValue;
                if (val == entry.Tag.Value)
                {
                    entriesProp.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(m_registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameplayTagEditorSources.Reload();
        }

        // ── Stub ETab ───────────────────────────────────────────

        private void DrawStubTab(string title, string hint)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(hint, MessageType.Info);
        }

        // ── Validation ETab ─────────────────────────────────────

        private void DrawValidationTab()
        {
            EditorGUILayout.LabelField("Combat Config Validation", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run All Checks", GUILayout.Height(30), GUILayout.Width(140)))
            {
                m_validationIssues.Clear();
                RunValidation();
                m_validationRun = true;
            }
            if (GUILayout.Button("Clear", GUILayout.Height(30), GUILayout.Width(80)))
            {
                m_validationIssues.Clear();
                m_validationRun = false;
            }
            EditorGUILayout.EndHorizontal();

            if (!m_validationRun)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Checks performed:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• SkillAbility.id non-empty / unique");
                EditorGUILayout.LabelField("• Effect references non-null, Effect tags present");
                EditorGUILayout.LabelField("• Unregistered tags in Skill/Effect assets");
                EditorGUILayout.LabelField("• basic_attack skill asset existence");
                EditorGUILayout.LabelField("• basic_attack references valid DamageEffect");
                return;
            }

            EditorGUILayout.Space(4);
            int errorCount = 0;
            int warningCount = 0;
            for (int i = 0; i < m_validationIssues.Count; i++)
            {
                if (m_validationIssues[i].Severity == EValidationSeverity.Error) errorCount++;
                else if (m_validationIssues[i].Severity == EValidationSeverity.Warning) warningCount++;
            }

            var summaryStyle = new GUIStyle(EditorStyles.boldLabel);
            summaryStyle.normal.textColor = errorCount > 0 ? Color.red : Color.green;
            var summary = errorCount > 0
                ? $"{errorCount} error(s), {warningCount} warning(s)"
                : "All checks passed";
            EditorGUILayout.LabelField(summary, summaryStyle);
            EditorGUILayout.Space(4);

            m_validationScrollPos = EditorGUILayout.BeginScrollView(m_validationScrollPos);
            for (int i = 0; i < m_validationIssues.Count; i++)
            {
                var issue = m_validationIssues[i];
                var icon = issue.Severity == EValidationSeverity.Error ? "✘" : "⚠";
                var color = issue.Severity == EValidationSeverity.Error ? Color.red : Color.yellow;
                if (issue.Severity == EValidationSeverity.Info)
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
            if (m_registry == null)
            {
                m_registry = AssetDatabase.LoadAssetAtPath<TagRegistry>(k_RegistryAssetPath);
            }
            if (m_registry == null)
            {
                AddIssue(EValidationSeverity.Error, "TagRegistry not found. Create one via the Tags ETab.", k_RegistryAssetPath);
                Debug.LogError("[Validation] TagRegistry not found.");
            }

            var skillGuids = AssetDatabase.FindAssets("t:SkillAbility");
            var effectGuids = AssetDatabase.FindAssets("t:SkillEffect");
            var skillDefs = new List<SkillAbility>(skillGuids.Length);
            var skillIdMap = new Dictionary<string, SkillAbility>(System.StringComparer.OrdinalIgnoreCase);

            // Collect all SkillAbilitys
            for (int i = 0; i < skillGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(skillGuids[i]);
                var skill = AssetDatabase.LoadAssetAtPath<SkillAbility>(path);
                if (skill != null) skillDefs.Add(skill);
            }

            // 2. Check each SkillAbility
            for (int i = 0; i < skillDefs.Count; i++)
            {
                var skill = skillDefs[i];
                var path = AssetDatabase.GetAssetPath(skill);

                ValidateSkillAbility(skill, path, skillIdMap);
            }

            // 3. Check duplicate SkillAbility ids
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
                    AddError($"[{id}] Duplicate SkillAbility.id '{id}' ({count} occurrences)", path);
                }
            }

            // 4. Check each SkillEffect
            var effectDefs = new Dictionary<string, SkillEffect>();
            for (int i = 0; i < effectGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(effectGuids[i]);
                var effect = AssetDatabase.LoadAssetAtPath<SkillEffect>(path);
                if (effect != null) effectDefs[path] = effect;
            }
            foreach (var kvp in effectDefs)
            {
                ValidateSkillEffect(kvp.Value, kvp.Key);
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
                        else if (effects[i] != null && effects[i].Function is ModifyAttributeFunction)
                        {
                            hasDamage = true;
                        }
                    }
                    if (!hasDamage)
                    {
                        AddWarning("basic_attack may not deal damage (no ModifyAttribute effect assigned).", baPath);
                    }
                }
            }

            // 6. Unregistered tags in Skills
            if (m_registry != null)
            {
                for (int i = 0; i < skillDefs.Count; i++)
                {
                    var skill = skillDefs[i];
                    var path = AssetDatabase.GetAssetPath(skill);
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
            for (int i = 0; i < m_validationIssues.Count; i++)
            {
                if (m_validationIssues[i].Severity == EValidationSeverity.Error) errCount++;
                else if (m_validationIssues[i].Severity == EValidationSeverity.Warning) warnCount++;
            }
            Debug.Log(errCount == 0 && warnCount == 0
                ? "[Validation] All checks passed."
                : $"[Validation] {errCount} error(s), {warnCount} warning(s) found. See the Validation ETab for details.");
        }

        private void ValidateSkillAbility(SkillAbility skill, string path,
            Dictionary<string, SkillAbility> idMap)
        {
            var id = skill.Id;

            // id non-empty
            if (string.IsNullOrWhiteSpace(id))
            {
                AddError("SkillAbility.id is empty.", path);
            }

            // Track for uniqueness check (case-insensitive, first-wins for reporting)
            if (!string.IsNullOrWhiteSpace(id) && !idMap.ContainsKey(id))
            {
                idMap[id] = skill;
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

        private void ValidateSkillEffect(SkillEffect effect, string path)
        {
            if (!effect.HasAnyTag())
            {
                AddWarning(
                    $"SkillEffect '{effect.name}' has no GameplayTag. Every effect should carry at least one tag.",
                    path);
            }
        }

        private void CheckTagRegistered(GameplayTag tag, string context, string assetPath)
        {
            if (string.IsNullOrEmpty(tag.Value) || !GameplayTag.IsValid(tag.Value))
            {
                AddError($"Invalid tag format in {context}: '{tag.Value}'", assetPath);
                return;
            }
            if (!GameplayTagEditorSources.IsKnown(tag.Value))
            {
                AddWarning($"Unregistered tag in {context}: '{tag.Value}'", assetPath);
            }
        }

        private SkillAbility FindSkillById(string id, List<SkillAbility> skills)
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
            AddIssue(EValidationSeverity.Error, message, assetPath);
            Debug.LogError($"[Validation] {message}" + (assetPath != null ? $" ({assetPath})" : ""));
        }

        private void AddWarning(string message, string assetPath = null)
        {
            AddIssue(EValidationSeverity.Warning, message, assetPath);
            Debug.LogWarning($"[Validation] {message}" + (assetPath != null ? $" ({assetPath})" : ""));
        }

        private void AddIssue(EValidationSeverity severity, string message, string assetPath)
        {
            m_validationIssues.Add(new ValidationIssue
            {
                Severity = severity,
                Message = message,
                AssetPath = assetPath ?? string.Empty
            });
        }

        private enum EValidationSeverity
        {
            Info,
            Warning,
            Error
        }

        private struct ValidationIssue
        {
            public EValidationSeverity Severity { get; set; }
            public string Message { get; set; }
            public string AssetPath { get; set; }
        }
    }
}



