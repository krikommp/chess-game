using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.EditorTools
{
    public static class MiniChessNavMeshTools
    {
        private const string k_SurfaceObjectName = "[NavMeshSurface]";
        private const string k_MenuPath = "MiniChess/NavMesh/Rebuild Surface NavMesh";

        [MenuItem(k_MenuPath)]
        public static void RebuildSurfaceNavMesh()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("[MiniChessNavMesh] Save the active scene before baking NavMesh.");
                return;
            }

            ClearLegacySceneNavMesh();

            var surface = EnsureSurface();
            surface.RemoveData();
            surface.navMeshData = null;
            surface.BuildNavMesh();

            if (surface.navMeshData == null)
            {
                Debug.LogError("[MiniChessNavMesh] NavMeshSurface.BuildNavMesh produced no NavMeshData.");
                return;
            }

            SaveSurfaceDataAsset(surface, scene.path);
            surface.RemoveData();
            surface.AddData();

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MiniChessNavMesh] Rebuilt Surface NavMesh: {AssetDatabase.GetAssetPath(surface.navMeshData)}");
        }

        private static NavMeshSurface EnsureSurface()
        {
            var existing = GameObject.Find(k_SurfaceObjectName);
            if (existing == null)
            {
                existing = new GameObject(k_SurfaceObjectName);
                Undo.RegisterCreatedObjectUndo(existing, "Create NavMeshSurface");
            }

            var surface = existing.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = Undo.AddComponent<NavMeshSurface>(existing);
            }

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.defaultArea = 0;
            return surface;
        }

        private static void ClearLegacySceneNavMesh()
        {
            var settings = UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject;
            if (settings == null)
            {
                return;
            }

            var serializedSettings = new SerializedObject(settings);
            var navMeshData = serializedSettings.FindProperty("m_NavMeshData");
            if (navMeshData == null || navMeshData.objectReferenceValue == null)
            {
                return;
            }

            navMeshData.objectReferenceValue = null;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SaveSurfaceDataAsset(NavMeshSurface surface, string scenePath)
        {
            var sceneDirectory = Path.GetDirectoryName(scenePath);
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var targetDirectory = Path.Combine(sceneDirectory, sceneName).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var assetPath = $"{targetDirectory}/NavMesh-{k_SurfaceObjectName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        }
    }
}

