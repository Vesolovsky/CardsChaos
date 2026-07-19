using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.UISystem.Init;
using Vesolovsky.Core.UISystem.Services;
using UnityEngine;
using Zenject;

namespace Vesolovsky.Game.Init
{
    /// <summary>
    /// Responsible for triggering the asynchronous initialization of all context initializers within the context and child contexts.
    /// It gathers all components implementing <see cref="IContextInitializator"/> and triggers their initialization process.
    /// This component should be present in both Project and Scene contexts to ensure proper initialization flow.
    /// </summary>
    public class ContextInitializatorTrigger : MonoBehaviour
    {
        private List<IContextInitializator> _contextInitializators;
        private ISceneViewsService _sceneViewsService;
        
        [Inject]
        public void Inject([InjectOptional] ISceneViewsService sceneViewsService)
        {
            _sceneViewsService = sceneViewsService;
        }
        
        private async void Start()
        {
            _contextInitializators = GetComponentsInChildren<IContextInitializator>().ToList();

            try
            {
                await InitContextInitializators();
                _sceneViewsService?.ShowScene().Forget();
            }
            catch (Exception e)
            {
                Debug.LogError($"Init failed! Exception: {e.Message}");
                throw;
            }
        }

        private async UniTask InitContextInitializators()
        {
            foreach (var context in _contextInitializators)
            {
                try
                {
                    await context.InitializeAsync();
                }
                catch (Exception e)
                {
                    var mb = context as MonoBehaviour;
                    var go = mb ? mb.gameObject : null;

                    Debug.LogError($"Could not init context '{(go ? go.name : context?.ToString())}'.", go);

                    Debug.LogException(e, go);

                    if (e is NullReferenceException)
                    {
                        Debug.LogError($"NullReferenceException during InitializeAsync on {mb.GetType().Name}. Dumping missing references…", mb);

#if UNITY_EDITOR
                        UnityEditor.Selection.activeObject = go;
                        UnityEditor.EditorGUIUtility.PingObject(go);
#endif
                    }

                    throw;
                }
            }
        }
    }
}