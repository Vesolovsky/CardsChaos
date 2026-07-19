namespace Vesolovsky.Core.UISystem
{
    /// <summary>
    /// Interface for views that can manage nested views. It provides methods to register and unregister child views.
    /// This allows a parent view to keep track of its nested views, which can be useful for coordinated animations,
    /// state management, and cleanup processes.
    /// </summary>
    public interface IViewParent
    {
        /// <summary>
        /// Registers a nested view under this view parent. This is useful for managing the lifecycle and interactions
        /// of nested views within a composite view structure.
        /// </summary>
        /// <param name="view">The view to be registered as a nested view.</param>
        void RegisterNestedView(IView view);

        /// <summary>
        /// Unregisters a nested view from this view parent. This method is typically called when a nested view is being
        /// unloaded or removed from the parent view structure.
        /// </summary>
        /// <param name="view">The view to be unregistered from the nested views list.</param>
        void UnregisterNestedView(IView view);
    }
}