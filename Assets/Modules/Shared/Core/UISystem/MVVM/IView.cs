using Cysharp.Threading.Tasks;
using System.Threading;

namespace Vesolovsky.Core.UISystem
{
    public interface IView
    {
        public UniTask Initialize(IViewDefinition viewDefinition);

        public UniTask Unload(bool immediately = false);

        public UniTask Show(CancellationToken ct, bool immediately = false);

        public UniTask Hide(CancellationToken ct, bool immediately = false);

        /// <summary>
        /// Views marked as StayHidden will not be shown when the scene loads. Instead it will stay in the hidden state.
        /// </summary>
        public bool StayHidden { get; }

        /// <summary>
        /// True while the view is currently shown (between <see cref="Show"/> and
        /// <see cref="Hide"/>). Reused views (destroyOnClose = false) stay
        /// registered in the scene's loaded-views list even while hidden, so
        /// callers that need to know whether a view is actually visible should
        /// check this rather than mere presence in that list.
        /// </summary>
        public bool IsShown { get; }
    }
}
