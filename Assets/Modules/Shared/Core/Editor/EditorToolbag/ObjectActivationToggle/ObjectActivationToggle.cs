#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vesolovsky.Core.EditorToolbag
{
    [InitializeOnLoad]
    public static class ObjectActivationToggle
    {
        private const float TOGGLE_X_OFFSET = 27f;
        private const float TOGGLE_WIDTH = 13f;

        static ObjectActivationToggle()
        {
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            DrawActivationToggle(instanceID, selectionRect);
        }

        public static void DrawActivationToggle(int instanceID, Rect selectionRect)
        {
            var gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null) return;

            var toggleRect = new Rect(selectionRect);
            toggleRect.x -= TOGGLE_X_OFFSET;
            toggleRect.width = TOGGLE_WIDTH;
            bool active = EditorGUI.Toggle(toggleRect, gameObject.activeSelf);

            if (active != gameObject.activeSelf)
            {
                Undo.RecordObject(gameObject, "Changed game object active state");
                gameObject.SetActive(active);
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
    }
}
#endif