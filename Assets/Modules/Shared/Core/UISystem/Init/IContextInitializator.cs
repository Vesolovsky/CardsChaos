using System;
using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.UISystem.Init
{
    /// <summary>
    /// This interface is used by the initialization system to set up all services implementing <see cref="IAsyncInitializable"/> 
    /// and views within a context. It ensures that the initialization process is completed before proceeding.
    /// </summary>
    public interface IContextInitializator
    {
        /// <summary>
        /// Initializes the context by setting up all services and views asynchronously.
        /// </summary>
        /// <param name="viewDefinition">The definition to initialize the view with. If null, a default definition is created.</param>
        UniTask InitializeAsync(IViewDefinition viewDefinition = null);

        /// <summary>
        /// Indicates whether the initialization process has been completed.
        /// </summary>
        bool InitializeCompleted { get; }

        /// <summary>
        /// Event triggered when the initialization process is completed.
        /// </summary>
        event Action Initialized;
    }
}