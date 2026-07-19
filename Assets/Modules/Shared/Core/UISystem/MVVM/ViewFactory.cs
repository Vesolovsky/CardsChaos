using Cysharp.Threading.Tasks;
using Vesolovsky.Core.UISystem.Init;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zenject;

namespace Vesolovsky.Core.UISystem
{
    /// <summary>
    /// This class uses the Addressables system to load view asynchronously and initialize it
    /// </summary>
    public class ViewFactory : IFactory<IViewDefinition, Transform, UniTask<IView>>
    {
        private readonly DiContainer _container;

        public ViewFactory(DiContainer container)
        {
            _container = container;
        }

        /// <summary>
        /// Creates a view instance based on the provided view definition and initializes it.
        /// </summary>
        /// <param name="viewDefinition">The definition containing details about the view to be created.</param>
        public async UniTask<IView> Create(IViewDefinition viewDefinition, Transform viewParent)
        {
            var viewPrefab = await LoadViewPrefabAsync(viewDefinition.Address);
            if (viewPrefab == null)
            {
                Debug.LogError($"Could not load view prefab with address: '{viewDefinition.Address}'.");
                return null;
            }

            var viewInstance = _container.InstantiatePrefabForComponent<IView>(viewPrefab, viewParent);

            await InitializeViewAsync(viewInstance, viewDefinition);

            return viewInstance;
        }

        private async UniTask<GameObject> LoadViewPrefabAsync(string address)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(address);
            await handle.Task;

            return handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
        }


        private async UniTask InitializeViewAsync(IView viewInstance, IViewDefinition viewDefinition)
        {
            var viewObject = (MonoBehaviour)viewInstance;
            
            //TODO: add possibility to set position&scale
            viewObject.transform.localPosition = Vector3.zero;
            viewObject.transform.localScale = Vector3.one;
            
            var viewInitializator = viewObject.GetComponent<IContextInitializator>();
            await viewInitializator.InitializeAsync(viewDefinition);
        }
    }
}
