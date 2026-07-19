using UnityEditor;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.UIComponents.Editor
{
    public class VUIComponentsCreator
    {
        private const string VBUTTON_PREFAB_PATH = "Assets/Modules/Shared/Core/UISystem/Prototypes/VButton.prefab";
        private const string VTEXT_PREFAB_PATH = "Assets/Modules/Shared/Core/UISystem/Prototypes/VText.prefab";
        private const string VIEW_PROTOTYPE_PREFAB_PATH = "Assets/Modules/Shared/Core/UISystem/Prototypes/ViewPrototype.prefab";
        
        [MenuItem("GameObject/Vesolovsky/UI/VButton", false, 1)]
        public static void CreateVButton(MenuCommand menuCommand)
        {
            CreateElement(VBUTTON_PREFAB_PATH, "VButton", menuCommand);
        }

        [MenuItem("GameObject/Vesolovsky/UI/VText", false, 2)]
        public static void CreateVText(MenuCommand menuCommand)
        {
            CreateElement(VTEXT_PREFAB_PATH, "VText", menuCommand);
        }

        [MenuItem("GameObject/Vesolovsky/UI/New View", false, 3)]
        public static void CreateView(MenuCommand menuCommand)
        {
            CreateElement(VIEW_PROTOTYPE_PREFAB_PATH, "New View", menuCommand);
        }

        private static void CreateElement(string prefabPath, string elementName, MenuCommand menuCommand)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"{elementName} prefab not found at: {prefabPath}");
                return;
            }
            
            var newElement = Object.Instantiate(prefab);
            newElement.name = elementName;

            Undo.RegisterCreatedObjectUndo(newElement, $"Create {elementName}");
            GameObjectUtility.SetParentAndAlign(newElement, menuCommand.context as GameObject);
            Selection.activeObject = newElement;
        }
    }
}