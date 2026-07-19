using Vesolovsky.Core.UISystem.Services;
using RoboRyanTron.SceneReference;
using UnityEngine;
using Zenject;
using Vesolovsky.Game;
using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.UISystem.UIComponents
{
    [AddComponentMenu("Vesolovsky/UI/Scene open button")]
    [RequireComponent(typeof(VButton))]
    public class SceneOpenButton : MonoBehaviour
    {
        [SerializeField] private SceneReference sceneToOpen;
        [SerializeField] private bool hideSceneViewsFirst = true;

        private VButton _button;
        private ISceneViewsService _sceneViewsService;
        private ISceneTransition _sceneTransition;
        private bool _isLoadingScene = false;
        
        [Inject]
        private void Inject(ISceneViewsService sceneViewsService, ISceneTransition sceneTransition)
        {
            _sceneViewsService = sceneViewsService;
            _sceneTransition = sceneTransition;
        }

        private void Awake()
        {
            _button = GetComponent<VButton>();
            _button.Bind(LoadScene);
        }
        
        private async void LoadScene()
        {
            if (_isLoadingScene) return;
            _isLoadingScene = true;
            
            if(hideSceneViewsFirst)
                await _sceneViewsService.HideScene();

            await _sceneTransition.FadeIn();

            var asyncOperation = sceneToOpen.LoadSceneAsync();
            await UniTask.WaitUntil(() => asyncOperation.isDone);
            await _sceneTransition.FadeOut();
            _isLoadingScene = false;
        }
    }
}