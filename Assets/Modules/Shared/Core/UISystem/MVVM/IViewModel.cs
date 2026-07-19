using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.UISystem
{
    public interface IViewModel
    {
        /// <summary>
        /// Initializes the ViewModel with the provided initialization data.
        /// </summary>
        /// <param name="viewModelInitData">Data required to initialize the ViewModel.
        /// Use <see cref="ViewModelInitDataDefaults.Default"/> if no specific data is needed.</param>
        public UniTask Initialize(IViewModelInitData viewModelInitData);
    }
}