using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Game.UISystem;
using Zenject;

namespace Vesolovsky.Core.UISystem.Init
{
    /// <summary>
    /// Manages the initialization of context-related services and view.
    /// <para>
    /// This class initializes all services implementing <see cref="IAsyncInitializable"/> and context view in a specified order.
    /// It waits for the initialization of the parent context initializator, if present, before proceeding.
    /// </para>
    /// <para>
    /// Should be binded and present in every context
    /// </para>
    /// </summary>
    public class ContextInitializator : MonoBehaviour, IContextInitializator, IDisposable
    {
        public bool InitializeCompleted { get; private set; }
        public event Action Initialized;

        private List<IAsyncInitializable> _initializables;
        private IView _contextView;
        private IContextInitializator _parentInitializator;

        /// <summary>
        /// Defines the order in which services should be initialized.
        /// </summary>
        protected virtual List<Type> InitializeOrder { get; } = new List<Type>();

        [Inject]
        public void Inject(
            [InjectOptional(Source = InjectSources.Local)] List<IAsyncInitializable> initializables,
            [InjectOptional(Source = InjectSources.Local)] IView contextView,
            [InjectOptional(Source = InjectSources.Parent)] IContextInitializator parentInitializator)
        {
            _initializables = initializables?.OrderBy(service => GetInitializationOrder(service.GetType())).ToList()
                ?? new List<IAsyncInitializable>();

            _contextView = contextView;
            _parentInitializator = parentInitializator;
        }

        /// <summary>
        /// Initializes the context by setting up all services and views asynchronously.
        /// </summary>
        /// <param name="viewDefinition">The definition to initialize the view with. If null, a default definition is created.</param>
        public async UniTask InitializeAsync(IViewDefinition viewDefinition)
        {
            if (_contextView != null && viewDefinition == null)
            {
                viewDefinition = ViewDefaultDefinitionFactory.CreateDefaultViewDefinition(_contextView);
            }

            if (_parentInitializator == null)
            {
                await InitializeAllAsync(viewDefinition);
            }
            else
            {
                await UniTask.WaitUntil(() => _parentInitializator.InitializeCompleted);
                await InitializeAllAsync(viewDefinition);

                //_parentInitializator.Initialized += OnParentInitialized;
                //await UniTask.WaitUntil(() => InitializeCompleted);
            }
        }

        /// <summary>
        /// Cleans up by unsubscribing from the parent initializator's events.
        /// </summary>
        public void Dispose()
        {
            if (_parentInitializator != null)
            {
                _parentInitializator.Initialized -= OnParentInitialized;
            }
        }

        /// <summary>
        /// Determines the initialization order for a given service type.
        /// </summary>
        /// <param name="serviceType">The type of the service.</param>
        /// <returns>The order index, or <c>int.MaxValue</c> if the service type is not in the initialization order list.</returns>
        private int GetInitializationOrder(Type serviceType)
        {
            int index = InitializeOrder.IndexOf(serviceType);
            return index == -1 ? int.MaxValue : index;
        }

        /// <summary>
        /// Called when the parent context initializator has completed its initialization.
        /// </summary>
        private void OnParentInitialized()
        {
            var viewDefinition = _contextView != null ? ViewDefaultDefinitionFactory.CreateDefaultViewDefinition(_contextView) : null;
            InitializeAllAsync(viewDefinition).Forget();
        }

        /// <summary>
        /// Initializes all services and the context view asynchronously.
        /// </summary>
        /// <param name="viewDefinition">The definition to initialize the view with.</param>
        private async UniTask InitializeAllAsync(IViewDefinition viewDefinition)
        {
            foreach (var initializable in _initializables)
            {
                try
                {
                    await initializable.Initialize();
                    Debug.Log($"Init step: '{initializable.GetType()}' initialized.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Could not initialize: {initializable.GetType()}. Exception: {e.Message}");
                    throw;
                }
            }

            if (_contextView != null)
            {
                await _contextView.Initialize(viewDefinition);
                Debug.Log($"Context view: '{((MonoBehaviour)_contextView).gameObject}' initialized");
            }
            
            InitializeCompleted = true;
            Initialized?.Invoke();
        }
    }
}
