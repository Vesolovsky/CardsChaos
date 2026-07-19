using Cysharp.Threading.Tasks;
using System.Threading;

namespace Vesolovsky.Core.UISystem.Animations
{
    public interface IViewTween
    {
        public UniTask PlayOpen(CancellationToken ct, bool immediately = false);
        public UniTask PlayClose(CancellationToken ct, bool immediately = false);
    }
}
