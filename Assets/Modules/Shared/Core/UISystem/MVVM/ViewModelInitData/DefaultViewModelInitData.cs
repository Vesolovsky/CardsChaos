namespace Vesolovsky.Core.UISystem
{
    /// <summary>
    /// Interface for ViewModel initialization data.
    /// </summary>
    public interface IViewModelInitData
    {
    }

    /// <summary>
    /// Represents default initialization data for a ViewModel.
    /// </summary>
    public class DefaultViewModelInitData : IViewModelInitData
    {
    }

    /// <summary>
    /// Provides default instances of initialization data for ViewModels.
    /// </summary>
    public static class ViewModelInitDataDefaults
    {
        /// <summary>
        /// Gets a default instance of <see cref="IViewModelInitData"/>.
        /// </summary>
        public static IViewModelInitData Default => new DefaultViewModelInitData();
    }
}