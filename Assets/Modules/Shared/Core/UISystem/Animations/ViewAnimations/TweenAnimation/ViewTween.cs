using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Animations
{
    public abstract class ViewTween : MonoBehaviour, IViewTween
    {
        //TODO: FIX IN THE CORE
        protected virtual void Awake()
        {
            SetToClosedState();
        }

        public async UniTask PlayOpen(CancellationToken ct, bool immediately = false)
        {
            if (immediately)
            {
                SetToOpenedState();
                return;
            }

            await OpenAnimation(CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, ct).Token);
        }

        public async UniTask PlayClose(CancellationToken ct, bool immediately = false)
        {
            if (immediately)
            {
                SetToClosedState();
                return;
            }

            await CloseAnimation(CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken, ct).Token);
        }

        protected abstract UniTask OpenAnimation(CancellationToken ct);
        protected abstract UniTask CloseAnimation(CancellationToken ct);

        protected abstract void SetToOpenedState();
        protected abstract void SetToClosedState();
    }
}
