using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// The one-time nudge that tells a new player they can turn the camera. A short while after the
    /// game starts it drops in from above the screen, holds a moment, and slides back out - once, and
    /// never again this session.
    ///
    /// It is parked off the top edge to begin with (its hidden y); showing it is just sliding that y
    /// home to nothing, and hiding it is the same move in reverse.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Rotate Camera Hint")]
    public class HudRotateCameraHint : MonoBehaviour
    {
        [Tooltip("The rect that drops in. Its y is driven between the two positions below.")]
        [SerializeField] private RectTransform slider;

        [Header("Positions")]
        [Tooltip("The y where the hint sits parked off the top of the screen, out of sight.")]
        [SerializeField] private float hiddenY = 125f;

        [Tooltip("The y where the hint reads, on screen.")]
        [SerializeField] private float shownY = 0f;

        [Header("Timing")]
        [Tooltip("Seconds after the game starts before the hint drops in.")]
        [SerializeField] private float startDelay = 3f;

        [SerializeField] private float slideInDuration = 0.45f;
        [SerializeField, SearchableEnum] private Ease slideInEase = Ease.OutCubic;

        [Tooltip("Seconds the hint stays on screen before it slides back out.")]
        [SerializeField] private float holdDuration = 3f;

        [SerializeField] private float slideOutDuration = 0.4f;
        [SerializeField, SearchableEnum] private Ease slideOutEase = Ease.InCubic;

        private bool _played;

        /// <summary>Runs the whole drop-in / hold / slide-out once. Later calls do nothing.</summary>
        public void Play()
        {
            if (_played || slider == null)
                return;

            _played = true;
            SetY(hiddenY);
            PlayAsync(destroyCancellationToken).Forget();
        }

        private async UniTask PlayAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: ct);
                await SlideToY(shownY, slideInDuration, slideInEase, ct);
                await UniTask.Delay(TimeSpan.FromSeconds(holdDuration), cancellationToken: ct);
                await SlideToY(hiddenY, slideOutDuration, slideOutEase, ct);
            }
            catch (OperationCanceledException)
            {
                // The scene went away mid-slide; nothing to clean up.
            }
        }

        private async UniTask SlideToY(float y, float duration, Ease ease, CancellationToken ct)
        {
            Vector2 target = new Vector2(slider.anchoredPosition.x, y);
            await Tween.UIAnchoredPosition(slider, target, duration, ease).WithCancellation(ct);
        }

        private void SetY(float y)
        {
            Vector2 position = slider.anchoredPosition;
            slider.anchoredPosition = new Vector2(position.x, y);
        }
    }
}
