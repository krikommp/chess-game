using System.Collections.Generic;
using System.Linq;
using MiniChess.GameplayTags;
using UnityEditor;

namespace MiniChess.EditorTools
{
    internal static class GameplayTagEditorSources
    {
        private const string k_RegistryPath = "Assets/Data/Tags/GameplayTagRegistry.asset";

        private static List<string> s_tagValues;

        public static IReadOnlyList<string> KnownTagValues
        {
            get
            {
                EnsureLoaded();
                return s_tagValues;
            }
        }

        public static bool IsKnown(string value)
        {
            EnsureLoaded();
            return s_tagValues.Contains(value, System.StringComparer.OrdinalIgnoreCase);
        }

        public static void Reload()
        {
            s_tagValues = null;
        }

        private static void EnsureLoaded()
        {
            if (s_tagValues != null)
            {
                return;
            }

            s_tagValues = LoadRegistryTagValues()
                .Where(v => !string.IsNullOrEmpty(v) && GameplayTag.IsValid(v))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> LoadRegistryTagValues()
        {
            var registry = AssetDatabase.LoadAssetAtPath<TagRegistry>(k_RegistryPath);
            if (registry == null)
            {
                yield break;
            }

            foreach (var entry in registry.Entries)
            {
                string value = entry.Tag.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    yield return value;
                }
            }
        }

    }
}
