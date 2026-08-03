using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// The one-time nudge that tells a new player how to get rid of a card. It fades in the first
    /// time a card is picked up, holds a moment, fades out, and is never seen again this session.
    ///
    /// "Session" is just the scene's life - nothing is written down. Close the scene and open it and
    /// a first pickup will show the nudge again, which is the right behaviour for a hint that only
    /// ever means to teach the control once.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Throw Hint")]
    public class HudThrowHint : MonoBehaviour
    {
        [Tooltip("Faded to show and hide the whole hint.")]
        [SerializeField] private CanvasGroup group;

        [SerializeField] private TMP_Text text;

        [Tooltip("The nudge, with {0} where the throw key goes. e.g. \"Press <b>[{0}]</b> To throw a card\".")]
        [SerializeField] private string labelFormat = "Press <b>[{0}]</b> To throw a card";

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.4f;
        [SerializeField] private float holdDuration = 3f;
        [SerializeField] private float fadeOutDuration = 0.5f;

        private bool _played;

        /// <summary>Writes the nudge with the given throw-key text and parks it hidden.</summary>
        public void Initialize(string keyDisplay)
        {
            SetKeyDisplay(keyDisplay);

            if (group != null)
                group.alpha = 0f;
        }

        public void SetKeyDisplay(string keyDisplay)
        {
            if (text != null)
                text.SetText(string.Format(labelFormat, keyDisplay));
        }

        /// <summary>Plays the nudge once. Every call after the first does nothing.</summary>
        public void Play()
        {
            if (_played || group == null)
                return;

            _played = true;
            PlayAsync(destroyCancellationToken).Forget();
        }

        private async UniTask PlayAsync(CancellationToken ct)
        {
            try
            {
                await Tween.Alpha(group, 1f, fadeInDuration).WithCancellation(ct);
                await UniTask.Delay(TimeSpan.FromSeconds(holdDuration), cancellationToken: ct);
                await Tween.Alpha(group, 0f, fadeOutDuration).WithCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                // The scene went away mid-fade; nothing to clean up.
            }
        }
    }
}
