using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Vesolovsky.Core.EditorToolbag;
using Vesolovsky.Core.Utils;

namespace Vesolovsky.Core.UISystem.Editor
{
    public class ViewCreatorWindow : EditorWindow
    {
        private const string SCRIPT_TEMPLATES_PATH = "Assets/Modules/Shared/Core/UISystem/Editor/ViewCreatorWindow/ScriptTemplates/";
        private const string VIEW_NAME_ENUM_FILE_PATH = "Assets/Modules/Unique/Game/Core/UISystem/ViewName.cs";

        [SerializeField] private VisualTreeAsset visualTreeAsset = default;

        private HelpBox _errorBox;
        private TextField _namespaceTextField;
        private TextField _viewNameTextField;
        private Toggle _isSharedViewToggle;
        private Button _createButton;
        private Button _createViewPrefabButton;

        private string _inputData;
        private bool _isSharedViewData;

        [MenuItem("Vesolovsky/UI System/View Creator")]
        public static void ShowExample()
        {
            ViewCreatorWindow wnd = GetWindow<ViewCreatorWindow>();
            wnd.titleContent = new GUIContent("View Creator");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            visualTreeAsset.CloneTree(root);

            _namespaceTextField = root.Q<TextField>("NamespaceNameTextField");
            _viewNameTextField = root.Q<TextField>("ViewNameTextField");
            _createButton = root.Q<Button>("CreateButton");
            _createViewPrefabButton = root.Q<Button>("CreateViewPrefabButton");
            _isSharedViewToggle = root.Q<Toggle>("IsSharedViewToggle");

            var content = root.Q<VisualElement>("Content");
            _errorBox = new HelpBox("", HelpBoxMessageType.Error);
            content.Add(_errorBox);

            _namespaceTextField.SetEnabled(false);
            _createViewPrefabButton.SetEnabled(false);

            _createButton.clicked += CreateFiles;
            _createViewPrefabButton.clicked += CreateViewPrefab;

            _viewNameTextField.RegisterValueChangedCallback(evt =>
            {
                _inputData = evt.newValue;
            });

            _isSharedViewToggle.RegisterValueChangedCallback(evt =>
            {
                _isSharedViewData = evt.newValue;
            });
        }

        private void OnDestroy()
        {
            _createButton.clicked -= CreateFiles;
            _createViewPrefabButton.clicked -= CreateViewPrefab;
        }

        private void Update()
        {
            _viewNameTextField.value = _inputData;
            _isSharedViewToggle.value = _isSharedViewData;

            if (_viewNameTextField == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_viewNameTextField.text))
            {
                ShowError("View name can't be empty!");
                return;
            }

            if (_viewNameTextField.text.Contains("View"))
            {
                ShowError("'View' in View name is redundant. Remove it.");
                return;
            }

            if (VAssetDatabase.DoesAssetExist($"{_viewNameTextField.text}View", VAssetDatabase.AssetType.Script))
            {
                ShowError("View with this name already exists!");
                _createViewPrefabButton.SetEnabled(true);
                return;
            }

            _createViewPrefabButton.SetEnabled(false);

            _errorBox.style.display = DisplayStyle.None;
            _createButton.SetEnabled(true);
        }

        private void ShowError(string errorMessage)
        {
            _errorBox.text = errorMessage;
            _errorBox.style.display = DisplayStyle.Flex;
            _createButton.SetEnabled(false);
        }

        private async void CreateFiles()
        {
            var viewName = _viewNameTextField.text;

            List<UniTask> fileCreationTasks = new()
        {
            CreateFileFromTemplate($"I{_viewNameTextField.text}ViewModel", "IViewModel"),
            CreateFileFromTemplate($"{_viewNameTextField.text}ViewModel", "ViewModel"),
            CreateFileFromTemplate($"{_viewNameTextField.text}View", "View"),
            CreateFileFromTemplate($"{_viewNameTextField.text}ViewDefinition", "ViewDefinition"),
            CreateFileFromTemplate($"{_viewNameTextField.text}ViewInstaller", "ViewInstaller"),
        };

            await UniTask.WhenAll(fileCreationTasks);

            EnumFileModifier.AddEntryToEnum(VIEW_NAME_ENUM_FILE_PATH, viewName);

            ViewFactoryModifier.AddViewToFactory(viewName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void CreateViewPrefab()
        {
            var prefabSaveDirectory = _isSharedViewToggle.value ? "Shared" : "Unique";
            var prefabSaveFile = $"Assets/Modules/{prefabSaveDirectory}/Game/Views/{_viewNameTextField.text}/";

            ViewPrefabFileCreator.CreateViewFromPrefab($"{_viewNameTextField.text}View", prefabSaveFile, _namespaceTextField.text);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private async UniTask CreateFileFromTemplate(string fileName, string fileTemplateName)
        {
            string fullReadPath = $"{SCRIPT_TEMPLATES_PATH}{fileTemplateName}Template.template";

            var templateFileText = await FileHandler.ReadFile(fullReadPath);
            var newFileText = templateFileText.Replace("#ViewName#", _viewNameTextField.text);
            newFileText = newFileText.Replace("#Namespace#", _namespaceTextField.text);

            string viewGroup = _isSharedViewToggle.value ? "Shared" : "Unique";
            string fullSavePath = $"Assets/Modules/{viewGroup}/Game/Views/{_viewNameTextField.text}/{fileName}.cs";

            await FileHandler.WriteFile(fullSavePath, newFileText);
        }
    }
}
