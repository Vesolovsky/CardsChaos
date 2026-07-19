using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace Vesolovsky.Core.UISystem.Animations
{
    /// <summary>
    /// Handles view animations using the Animator component, implementing state control and triggering animations.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class ViewAnimatorAnimation : MonoBehaviour, IViewAnimation, IViewAnimatorStateController
    {
        private static readonly int SHOWED_STATE = Animator.StringToHash("Showed");
        private static readonly int HIDDEN_STATE = Animator.StringToHash("Hidden");
        private static readonly int SHOW_TRIGGER = Animator.StringToHash("Show");
        private static readonly int HIDE_TRIGGER = Animator.StringToHash("Hide");
        
        private Animator _animator;
        private bool _fullyOpened = false;
        private bool _fullyClosed = false;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnDestroy()
        {
            // Ensure to finish waiting in Open/Close methods in case the view is destroyed mid-animation.
            _fullyOpened = true;
            _fullyClosed = true;
        }

        /// <summary>
        /// Opens the view, playing the appropriate animation.
        /// </summary>
        /// <param name="immediately">If true, skips the animation and directly sets the view to the opened state.</param>
        public async UniTask Open(CancellationToken ct, bool immediately = false)
        {
            if (_fullyOpened) return;
            
            if (immediately)
            {
                _animator.Play(SHOWED_STATE);
                EnterViewOpenedState();
                return;
            }
            
            _animator.SetTrigger(SHOW_TRIGGER);

            try
            {
                await UniTask.WaitUntil(() => _fullyOpened, cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                EnterViewOpenedState();
            }
        }

        /// <summary>
        /// Closes the view, playing the appropriate animation.
        /// </summary>
        /// <param name="immediately">If true, skips the animation and directly sets the view to the closed state.</param>
        public async UniTask Close(CancellationToken ct, bool immediately = false)
        {
            if (_fullyClosed) return;
            
            if (immediately)
            {
                _animator.Play(HIDDEN_STATE);
                EnterViewClosedState();
                return;
            }
            
            _animator.SetTrigger(HIDE_TRIGGER);

            try
            {
                await UniTask.WaitUntil(() => _fullyClosed, cancellationToken: ct);
            }
            catch(OperationCanceledException)
            {
                EnterViewClosedState();
            }
        }

        /// <summary>
        /// Called when the view's open animation has completed.
        /// </summary>
        public void EnterViewOpenedState()
        {
            _animator.ResetTrigger(SHOW_TRIGGER);
            _fullyOpened = true;
            _fullyClosed = false;
        }

        /// <summary>
        /// Called when the view's close animation has completed.
        /// </summary>
        public void EnterViewClosedState()
        {
            _animator.ResetTrigger(HIDE_TRIGGER);
            _fullyOpened = false;
            _fullyClosed = true;
        }
    }
}
