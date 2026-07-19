namespace Vesolovsky.Core.UISystem
{
    /// <summary>
    /// Interface for view initialization data.
    /// </summary>
    public interface IViewInitData
    {
    }

    /// <summary>
    /// Represents default initialization data for a view.
    /// </summary>
    public class DefaultViewInitData : IViewInitData
    {
    }

    /// <summary>
    /// Provides default instances of view initialization data.
    /// </summary>
    public static class ViewInitDataDefaults
    {
        /// <summary>
        /// Gets a default instance of <see cref="IViewInitData"/>.
        /// </summary>
        public static IViewInitData Default => new DefaultViewInitData();
    }
}