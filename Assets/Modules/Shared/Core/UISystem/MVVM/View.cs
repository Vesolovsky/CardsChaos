using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.UISystem.Animations;
using Vesolovsky.Core.UISystem.Services;
using JetBrains.Annotations;
using RoboRyanTron.QuickButtons;
using UnityEngine;
using UnityEngine.Assertions;
using Zenject;
using System.Threading;
using Vesolovsky.Core.Audio;

namespace Vesolovsky.Core.UISystem
{
    public interface IPopup { }

    /// <summary>
    /// Base class for all views in the UI system, providing common functionality such as view initialization,
    /// showing/hiding animations, and managing nested views.
    /// </summary>
    /// <typeparam name="T">Type of the ViewModel associated with this view.</typeparam>
    [SelectionBase]
    public abstract class View<T> : MonoBehaviour, IView, IInitializable, IDisposable, IViewParent where T : IViewModel
    {
        [SerializeField] private QuickButton findRoot = new QuickButton("FindRoot");
        [SerializeField] private Transform root;
        [SerializeField] private bool stayHidden = false;
        
        protected ISceneViewsService SceneViewsService;
        protected IAudioService AudioService;
        protected T ViewModel;
        
        private IViewAnimation _viewAnimation;
        private IViewParent _parentView;

        private readonly List<IView> _nestedViews = new();

        public bool StayHidden => stayHidden;

        private bool _isShown = false;

        public bool IsShown => _isShown;
        
        /// <summary>
        /// Ensures the root Transform is assigned, which is crucial for proper view functionality.
        /// </summary>
        protected virtual void Awake()
        {
            Assert.IsNotNull(root,
                $"The 'root' object in the view of type '{GetType().Name}' (GameObject: '{gameObject.name}') is missing. This is required for proper functionality.");
        }

        protected virtual void Start() { }
        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }
        protected virtual void OnDestroy() { }

        [Inject]
        public void Inject(
            T viewModel,
            ISceneViewsService sceneViewsService,
            [InjectOptional(Source = InjectSources.Local)] IViewAnimation viewAnimation,
            [InjectOptional(Source = InjectSources.Parent)] IViewParent parentView,
            IAudioService audioService)
        {
            ViewModel = viewModel;
            SceneViewsService = sceneViewsService;
            _viewAnimation = viewAnimation;
            _parentView = parentView;
            AudioService = audioService;
        }

        public void Initialize()
        {
            _parentView?.RegisterNestedView(this);
        }

        public virtual void Dispose()
        {
            SceneViewsService.UnregisterView(this);
            _parentView?.UnregisterNestedView(this);
        }

        /// <summary>
        /// Initializes the view and its ViewModel using the provided definition data.
        /// </summary>
        /// <param name="viewDefinition">The data required to initialize the view and ViewModel. For default use: <see cref="ViewDefaultDefinitionFactory"/></param>
        public async UniTask Initialize(IViewDefinition viewDefinition)
        {
            await ViewModel.Initialize(viewDefinition.ViewModelInitData);
            InitialViewSetup(viewDefinition.ViewInitData);

            SceneViewsService.RegisterView(this);
        }

        /// <summary>
        /// Sets up the view with initial values from the ViewModel before it is shown.
        /// </summary>
        protected virtual void InitialViewSetup(IViewInitData viewInitData)
        {
            // Custom setup logic for the view, using ViewModel and viewInitData
        }

        public void RegisterNestedView(IView view)
        {
            if (_nestedViews.Contains(view))
            {
                Debug.LogError($"Trying to register nested view of type: '{view.GetType()}' that was already registered before!");
                return;
            }
            _nestedViews.Add(view);
        }

        public void UnregisterNestedView(IView view)
        {
            if (!_nestedViews.Contains(view))
            {
                Debug.LogError($"Trying to unregister nested view of type: '{view.GetType()}' that is not in the nested views list!");
                return;
            }
            _nestedViews.Remove(view);
        }

        public virtual async UniTask Show(CancellationToken ct, bool immediately = false)
        {
            if (_isShown) return;

            if(this is IPopup)
            {
                AudioService.Play(AudioSFXKey.PopupShow);
            }

            if (!immediately && _viewAnimation == null)
            {
                Debug.Log(
                    $"Failed to play view '{GetType().Name}' (GameObject: '{gameObject.name}') open animation because '{nameof(IViewAnimation)}' is not assigned. If that's not intended ensure it is injected and present in the view object.", gameObject);
                return;
            }

            if (_viewAnimation != null)
            {
                await _viewAnimation.Open(CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, ct).Token, immediately);
            }
            _isShown = true;
        }

        public virtual async UniTask Hide(CancellationToken ct, bool immediately = false)
        {
            if(_isShown == false) return;

            if (this is IPopup)
            {
                AudioService.Play(AudioSFXKey.PopupHide);
            }

            if (!immediately && _viewAnimation == null)
            {
                Debug.Log(
                    $"Failed to play view '{GetType().Name}' (GameObject: '{gameObject.name}') hide animation because '{nameof(IViewAnimation)}' is not assigned. If that's not intended ensure it is injected and present in the view object.", gameObject);
                return;
            }

            if (_viewAnimation != null)
            {
                await _viewAnimation.Close(CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, ct).Token, immediately);
            }
            _isShown = false;
        }

        public async UniTask Unload(bool immediately = false)
        {
            await Hide(CancellationToken.None, immediately);
            
            Destroy(gameObject);
        }
        
        #region Editor
        [UsedImplicitly]
        private void FindRoot()
        {
            root = transform.Find("Root");
        }
        #endregion
    }
}
