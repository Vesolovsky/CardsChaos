#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vesolovsky.Core.EditorToolbag
{
    [Overlay(typeof(SceneView), "Scenes")]
    [Icon(EditorIcons.SCENE_ICON)]
    public class SceneOpenOverlay : IMGUIOverlay
    {
        private const float OVERLAY_WIDTH = 200f;
        private const float SCROLL_WIDTH = 20f;
        private const float MAX_HEIGHT = 200f;
        private const float ITEM_HEIGHT = 25f;
        private const float BUTTON_WIDTH_OFFSET = 6;

        private Vector2 _scrollPosition;

        public override void OnGUI()
        {
            GUILayout.Label("Scenes", EditorStyles.boldLabel);

            Scene currentScene = EditorSceneManager.GetActiveScene();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled == true).ToList();

            float dynamicHeight = scenes.Count * ITEM_HEIGHT;
            float scrollHeight = Mathf.Min(dynamicHeight, MAX_HEIGHT);

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                false,
                GUILayout.Height(scrollHeight),
                GUILayout.Width(OVERLAY_WIDTH)
            );

            foreach (var scene in scenes)
            {
                string path = scene.path;
                string name = Path.GetFileNameWithoutExtension(path);

                bool isActive = string.Compare(currentScene.name, name) == 0;
                EditorGUI.BeginDisabledGroup(isActive);

                var buttonWidth = dynamicHeight > MAX_HEIGHT ? OVERLAY_WIDTH - SCROLL_WIDTH : OVERLAY_WIDTH - BUTTON_WIDTH_OFFSET;

                if (GUILayout.Button(name, GUILayout.Width(buttonWidth)))
                {
                    OpenScene(currentScene, path);
                }
                EditorGUI.EndDisabledGroup();
            }

            GUILayout.EndScrollView();
        }

        private void OpenScene(Scene currentScene, string path)
        {
            if (currentScene.isDirty)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(path);
                }
            }
            else
            {
                EditorSceneManager.OpenScene(path);
            }
        }
    }
}
#endif