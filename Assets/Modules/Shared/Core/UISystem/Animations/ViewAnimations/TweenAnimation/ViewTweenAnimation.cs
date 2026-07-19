using Cysharp.Threading.Tasks;
using RoboRyanTron.QuickButtons;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TNRD;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Animations
{
    public class ViewTweenAnimation : MonoBehaviour, IViewAnimation
    {
        [SerializeField] private List<SerializableInterface<IViewTween>> openViewAnimationParts;
        [SerializeField] private List<SerializableInterface<IViewTween>> closeViewAnimationParts;

        [SerializeField] private QuickButton OpenButton = new QuickButton("Open", CancellationToken.None, false);
        [SerializeField] private QuickButton CloseButton = new QuickButton("Close", CancellationToken.None, false);

        private bool _isOpening = false;
        private bool _isClosing = false;

        public async UniTask Open(CancellationToken ct, bool immediately = false)
        {
            if (_isOpening) return;
            _isOpening = true;

            List<UniTask> openTasks = new();

            foreach(var animationPart in  openViewAnimationParts)
            {
                openTasks.Add(animationPart.Value.PlayOpen(ct, immediately));
            }

            await UniTask.WhenAll(openTasks);
            _isOpening = false;
        }

        public async UniTask Close(CancellationToken ct, bool immediately = false)
        {
            if (_isClosing) return;
            _isClosing = true;

            List<UniTask> closeTasks = new();

            foreach (var animationPart in closeViewAnimationParts)
            {
                closeTasks.Add(animationPart.Value.PlayClose(ct, immediately));
            }

            await UniTask.WhenAll(closeTasks);
            _isClosing = false;
        }
    }
}
