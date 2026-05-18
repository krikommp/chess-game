using UnityEditor;

namespace MiniChess.EditorTools
{
    public class TagRegistryAssetProcessor : AssetPostprocessor
    {
        private const string k_RegistryPath = "Assets/Data/Tags/GameplayTagRegistry.asset";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool registryChanged = false;

            foreach (var path in importedAssets)
            {
                if (path == k_RegistryPath) { registryChanged = true; break; }
            }

            if (!registryChanged)
            {
                foreach (var path in deletedAssets)
                {
                    if (path == k_RegistryPath) { registryChanged = true; break; }
                }
            }

            if (!registryChanged)
            {
                foreach (var path in movedAssets)
                {
                    if (path == k_RegistryPath) { registryChanged = true; break; }
                }
            }

            if (!registryChanged)
            {
                foreach (var path in movedFromAssetPaths)
                {
                    if (path == k_RegistryPath) { registryChanged = true; break; }
                }
            }

            if (registryChanged)
            {
                GameplayTagEditorSources.Reload();
                EditorApplication.delayCall += TagCodeGenerator.Generate;
            }
        }
    }
}
