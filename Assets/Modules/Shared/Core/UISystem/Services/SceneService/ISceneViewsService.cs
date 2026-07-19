using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Services
{
    /// <summary>
    /// Manages views within a specific scene, handling their lifecycle including loading, showing, hiding, and unloading.
    /// </summary>
    public interface ISceneViewsService
    {
        public event Action SceneShown;
        public event Action SceneShowStarted;

        /// <summary>
        /// Gets the list of all currently loaded views in the scene.
        /// </summary>
        IReadOnlyList<IView> LoadedViews { get; }
        
        /// <summary>
        /// Shows all currently loaded views in the scene, triggering the scene's open animation if present,
        /// or the views' show animations in bulk.
        /// </summary>
        UniTask ShowScene();
        
        /// <summary>
        /// Hides all currently loaded views in the scene, triggering the scene's close animation if present,
        /// or the views' hide animations in bulk.
        /// </summary>
        UniTask HideScene();

        /// <summary>
        /// Registers a view as loaded, typically called after the view's initialization process.
        /// </summary>
        /// <param name="view">The initialized view to register.</param>
        void RegisterView(IView view);
        
        /// <summary>
        /// Unregisters a view, typically called when the view is unloaded.
        /// </summary>
        /// <param name="view">The view to unregister.</param>
        void UnregisterView(IView view);

        /// <summary>
        /// Adds a view to the loading queue. Once loaded, the view is created, initialized, and shown.
        /// </summary>
        /// <param name="viewDefinition">The definition of the view to add.</param>
        /// <param name="parent">Parent transform for new view.</param>
        /// <param name="throughQueue">If true, the view is processed through a loading queue; otherwise, it is added directly.</param>
        UniTask AddView(IViewDefinition viewDefinition, Transform parent, bool throughQueue = true);

    }
}
