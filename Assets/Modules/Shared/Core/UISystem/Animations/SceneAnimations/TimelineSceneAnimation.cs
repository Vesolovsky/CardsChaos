using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace Vesolovsky.Core.UISystem.Animations
{
    /// <summary>
    /// Manages scene animations using PlayableDirectors for opening and closing animations,
    /// and ensures view states are correctly updated after timeline playback.
    /// </summary>
    public class TimelineSceneAnimation : MonoBehaviour, IMultiViewAnimation
    {
        [SerializeField] private PlayableDirector openDirector;
        [SerializeField] private PlayableDirector closeDirector;

        private void Awake()
        {
            Assert.IsNotNull(openDirector, "Open PlayableDirector is not assigned.");
            Assert.IsNotNull(closeDirector, "Close PlayableDirector is not assigned.");

            Assert.AreEqual(DirectorWrapMode.None, openDirector.extrapolationMode, "Open PlayableDirector's extrapolation mode is not set to None.");
            Assert.AreEqual(DirectorWrapMode.None, closeDirector.extrapolationMode, "Close PlayableDirector's extrapolation mode is not set to None.");
        }

        /// <summary>
        /// Plays the open animation for all relevant views and updates their state once the animation is finished.
        /// </summary>
        /// <param name="views">The list of views to open.</param>
        /// <param name="immediately">If true, skips animation and directly opens the views.</param>
        public async UniTask Open(List<IView> views, bool immediately = false)
        {
            if (immediately)
            {
                SetViewsToOpenedState(views);
                return;
            }

            openDirector.Play();
            await UniTask.WaitUntil(() => openDirector.state != PlayState.Playing);
            SetViewsToOpenedState(views);
        }

        /// <summary>
        /// Plays the close animation for all relevant views and updates their state once the animation is finished.
        /// </summary>
        /// <param name="views">The list of views to close.</param>
        /// <param name="immediately">If true, skips animation and directly closes the views.</param>
        public async UniTask Close(List<IView> views, bool immediately = false)
        {
            if (immediately)
            {
                SetViewsToClosedState(views);
                return;
            }

            closeDirector.Play();
            await UniTask.WaitUntil(() => closeDirector.state != PlayState.Playing);
            SetViewsToClosedState(views);
        }

        /// <summary>
        /// Updates the state of all specified views to be shown.
        /// </summary>
        /// <param name="views">The list of views to be shown.</param>
        private void SetViewsToOpenedState(List<IView> views)
        {
            foreach (var view in views)
            {
                if(view.StayHidden) continue;
                view.Show(CancellationToken.None, true);
            }
        }

        /// <summary>
        /// Updates the state of all specified views to be hidden.
        /// </summary>
        /// <param name="views">The list of views to be hidden.</param>
        private void SetViewsToClosedState(List<IView> views)
        {
            foreach (var view in views)
            {
                view.Hide(CancellationToken.None, true);
            }
        }
    }
}
