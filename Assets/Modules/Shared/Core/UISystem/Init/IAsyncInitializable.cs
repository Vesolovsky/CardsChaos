using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.UISystem.Init
{
    /// <summary>
    /// All implementations of this interface are used by the initialization system to initialize context-related services
    /// and views. This ensures that components can be properly set up before being used.
    /// </summary>
    public interface IAsyncInitializable
    {
        /// <summary>
        /// Asynchronously initializes the component.
        /// </summary>
        UniTask Initialize();
    }
}