using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

namespace Vesolovsky.Core.EditorToolbag
{
    [Overlay(typeof(SceneView), "Views")]
    [Icon("Assets/Modules/Shared/Core/UISystem/Editor/ViewOverlay/ViewIcon.png")]
    public class ViewOpenOverlay : IMGUIOverlay
    {
        private const float OVERLAY_WIDTH = 200f;
        private const float SCROLL_WIDTH = 20f;
        private const float MAX_HEIGHT = 200f;
        private const float ITEM_HEIGHT = 25f;
        private const float BUTTON_WIDTH_OFFSET = 6;
        private const float PING_BUTTON_SIZE = 20;

        private Vector2 _scrollPosition;
        private string _searchQuery = string.Empty;

        public override void OnGUI()
        {
            GUILayout.Label("Open view prefab", EditorStyles.boldLabel);
            _searchQuery = GUILayout.TextField(_searchQuery, GUILayout.Width(OVERLAY_WIDTH));

            var _viewPrefabPaths = FindPrefabsWithLabel("View");

            var filteredPaths = _viewPrefabPaths.Where(path => Path.GetFileNameWithoutExtension(path).ToLower().Contains(_searchQuery.ToLower())).ToList();

            if (filteredPaths.Count == 0)
            {
                GUILayout.Label("No prefabs found.", EditorStyles.helpBox);
                return;
            }

            float dynamicHeight = filteredPaths.Count * ITEM_HEIGHT;
            float scrollHeight = Mathf.Min(dynamicHeight, MAX_HEIGHT);

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                false,
                false,
                GUILayout.Height(scrollHeight),
                GUILayout.Width(OVERLAY_WIDTH + BUTTON_WIDTH_OFFSET)
            );

            foreach (var path in filteredPaths)
            {
                string name = Path.GetFileNameWithoutExtension(path);

                GUILayout.BeginHorizontal();
                var buttonWidth = 
                    dynamicHeight > MAX_HEIGHT ?
                    OVERLAY_WIDTH - SCROLL_WIDTH - PING_BUTTON_SIZE
                    : OVERLAY_WIDTH - BUTTON_WIDTH_OFFSET - PING_BUTTON_SIZE;

                if (GUILayout.Button(EditorGUIUtility.IconContent("d_Selectable Icon"), GUILayout.Width(PING_BUTTON_SIZE), GUILayout.Height(PING_BUTTON_SIZE)))
                {
                    PingPrefab(path);
                }

                if (GUILayout.Button(name, GUILayout.Width(buttonWidth)))
                {
                    OpenPrefabInIsolation(path);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
        }

        private void OpenPrefabInIsolation(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                PrefabUtility.LoadPrefabContents(path);
                AssetDatabase.OpenAsset(prefab);
            }
        }

        private List<string> FindPrefabsWithLabel(string label)
        {
            List<string> prefabPaths = new List<string>();
            string[] allPrefabGUIDs = AssetDatabase.FindAssets("t:Prefab");

            foreach (string guid in allPrefabGUIDs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && HasLabel(prefab, label))
                {
                    prefabPaths.Add(path);
                }
            }

            return prefabPaths;
        }

        private bool HasLabel(GameObject prefab, string label)
        {
            var labels = AssetDatabase.GetLabels(prefab);
            return labels.Contains(label);
        }

        private void PingPrefab(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }
        }
    }
}
