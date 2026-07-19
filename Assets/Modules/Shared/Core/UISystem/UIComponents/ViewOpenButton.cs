using Vesolovsky.Core.UISystem.Services;
using UnityEngine;
using UnityEngine.Assertions;
using Zenject;
using Vesolovsky.Game.UISystem;
using RoboRyanTron.SearchableEnum;

namespace Vesolovsky.Core.UISystem.UIComponents
{

    [AddComponentMenu("Vesolovsky/UI/View open button")]
    [RequireComponent(typeof(VButton))]
    public class ViewOpenButton : MonoBehaviour
    {
        [SerializeField, SearchableEnum] private ViewName viewToOpen;

        private ISceneViewsService _sceneViewsService;
        private VButton _button;

        private Transform _viewParent;
        private bool _isOpening = false;
        
        [Inject]
        private void Inject(ISceneViewsService sceneViewsService, DynamicViewsCanvas dynamicViewsCanvas)
        {
            _sceneViewsService = sceneViewsService;
            _viewParent = dynamicViewsCanvas.transform;
        }
        
        private void Awake()
        {
            _button = GetComponent<VButton>();
            Assert.IsNotNull(_button);
        }

        private void OnEnable()
        {
            _button.Bind(OpenView);
        }

        private async void OpenView()
        {
            if (_isOpening) return;
            
            _isOpening = true;
            var viewDefinition = ViewDefaultDefinitionFactory.CreateDefaultViewDefinition(viewToOpen);
            await _sceneViewsService.AddView(viewDefinition, _viewParent);
            _isOpening = false;
        }
    }
}