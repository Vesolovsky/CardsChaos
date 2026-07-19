using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.UISystem.Animations;
using UnityEngine;
using Zenject;
using System.Threading;
using System;

namespace Vesolovsky.Core.UISystem.Services
{
    public class SceneViewsService : ISceneViewsService
    {
        public event Action SceneShown;
        public event Action SceneShowStarted;

        private readonly List<IView> _loadedViews = new();

        private readonly Queue<(IViewDefinition definition, Transform parent)> _loadingQueue = new();

        private bool _isLoading;

        private readonly IMultiViewAnimation _multiViewAnimation;
        private readonly ViewFactory _viewFactory;

        public IReadOnlyList<IView> LoadedViews => _loadedViews;

        [Inject]
        public SceneViewsService(
            [InjectOptional] IMultiViewAnimation multiViewAnimation,
            ViewFactory viewFactory)
        {
            _multiViewAnimation = multiViewAnimation;
            _viewFactory = viewFactory;
        }

        public async UniTask ShowScene()
        {
            if (_multiViewAnimation == null)
            {
                var viewShowTasks = new List<UniTask>();
                foreach (var view in _loadedViews)
                {
                    viewShowTasks.Add(view.StayHidden ? view.Hide(CancellationToken.None, immediately: true) : view.Show(CancellationToken.None));
                }

                SceneShowStarted?.Invoke();
                await UniTask.WhenAll(viewShowTasks);
            }
            else
            {
                SceneShowStarted?.Invoke();
                await _multiViewAnimation.Open(_loadedViews);
            }
            SceneShown?.Invoke();
        }

        public async UniTask HideScene()
        {
            if (_multiViewAnimation == null)
            {
                var viewsHideTasks = Enumerable.Select(_loadedViews, view => view.Hide(CancellationToken.None)).ToList();
                await UniTask.WhenAll(viewsHideTasks);
                return;
            }

            await _multiViewAnimation.Close(_loadedViews);
        }

        public void RegisterView(IView view)
        {
            _loadedViews.Add(view);
        }

        public void UnregisterView(IView view)
        {
            if (!_loadedViews.Contains(view))
            {
                Debug.LogError($"Can't unregister a view of type: '{view.GetType()}' as it's not on the registered views list");
                return;
            }

            _loadedViews.Remove(view);
        }

        #region Adding view

        public async UniTask AddView(IViewDefinition viewDefinition, Transform parent,  bool throughQueue)
        {
            if (!throughQueue)
            {
                await AddView(viewDefinition, parent);
                return;
            }

            _loadingQueue.Enqueue((viewDefinition, parent));

            await ProcessLoadingQueue();
        }

        private async UniTask ProcessLoadingQueue()
        {
            if (_isLoading) return;

            _isLoading = true;

            while (_loadingQueue.Count > 0)
            {
                var viewDefinition = _loadingQueue.Dequeue();
                await AddView(viewDefinition.definition, viewDefinition.parent);
            }

            _isLoading = false;
        }

        private async UniTask AddView(IViewDefinition viewDefinition, Transform parent)
        {
            IView view = await _viewFactory.Create(viewDefinition, parent);
            await view.Show(CancellationToken.None);
        }
        #endregion
    }
}