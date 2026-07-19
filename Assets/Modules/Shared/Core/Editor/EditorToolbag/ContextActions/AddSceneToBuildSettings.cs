#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
namespace Vesolovsky.Core.EditorToolbag
{
    //TODO: Add to the core
    public static class AddSceneToBuildSettings
    {
        [MenuItem("Assets/Add Scene to Build Settings", true)]
        private static bool ValidateAddSceneToBuildSettings()
        {
            string path = GetSelectedScenePath();
            if (string.IsNullOrEmpty(path)) return false;

            return !IsSceneInBuildSettings(path);
        }

        [MenuItem("Assets/Add Scene to Build Settings")]
        private static void AddScene()
        {
            string path = GetSelectedScenePath();
            if (string.IsNullOrEmpty(path)) return;

            var scenes = EditorBuildSettings.scenes.ToList();
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"Added scene to build settings: {path}");
        }

        private static string GetSelectedScenePath()
        {
            var selected = Selection.activeObject;
            string path = AssetDatabase.GetAssetPath(selected);

            if (!path.EndsWith(".unity")) return null;
            return path;
        }

        private static bool IsSceneInBuildSettings(string scenePath)
        {
            return EditorBuildSettings.scenes.Any(scene => scene.path == scenePath);
        }
    }
}
#endif