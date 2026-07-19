using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Core.UISystem
{
    /// <summary>
    /// Represents a definition for creating and initializing a view within the UI system. 
    /// This interface includes information such as the parent transform, view name, address for loading, 
    /// and initialization data for both the view and its ViewModel.
    /// </summary>
    public interface IViewDefinition
    {
        /// <summary>
        /// Name of the view which may be used for identification or logging.
        /// </summary>
        ViewName Name { get; set; }

        /// <summary>
        /// Addressable address of a view prefab.
        /// </summary>
        string Address { get; set; }

        /// <summary>
        /// Unique identifier for the view instance.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Optional. Initialization data specific to the view, used during the view's setup process.
        /// </summary>
        IViewInitData ViewInitData { get; set; }

        /// <summary>
        /// Optional. Initialization data for the ViewModel, which provides initial state and data for the view.
        /// </summary>
        IViewModelInitData ViewModelInitData { get; set; }
    }
}