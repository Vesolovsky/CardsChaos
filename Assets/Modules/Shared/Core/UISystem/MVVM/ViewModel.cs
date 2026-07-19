using System;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Vesolovsky.Core.UISystem
{
    public abstract class ViewModel : IViewModel, IDisposable
    {
        protected readonly CompositeDisposable Disposables = new CompositeDisposable();

        /// <summary>
        /// Initializes the ViewModel with the provided initialization data.
        /// Derived classes should override this method to provide specific initialization logic.
        /// </summary>
        /// <param name="viewModelInitData">Data required to initialize the ViewModel.</param>
        public virtual UniTask Initialize(IViewModelInitData viewModelInitData)
        {
            return UniTask.CompletedTask;
        }

        public virtual void Dispose()
        {
            Disposables.Dispose();
        }
    }
}