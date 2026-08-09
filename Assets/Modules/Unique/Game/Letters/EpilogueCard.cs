using PrimeTween;
using UnityEngine;
using UnityEngine.Events;

namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// Marks the one-card endgame set's card in the scene. The card is an ordinary <see cref="Card"/>
    /// authored active but out of reach behind the unenterable door, so the world save persists it
    /// like any card; this component carries the one-time "slide it into reach" cue.
    ///
    /// <see cref="LetterAppearanceService"/> calls <see cref="Release"/> once every counted card has
    /// been filed. Release slides the card by <see cref="slideOffset"/> - set that to the world-space
    /// move that carries it out from under the door into reach. Guarded so it fires at most once, and
    /// never re-slides on a reload where the card is already out. Hook extras (a sound, say) to
    /// <c>onRelease</c> if you want them.
    /// </summary>
    [AddComponentMenu("CardsChaos/Epilogue Card")]
    public class EpilogueCard : MonoBehaviour
    {
        [Tooltip("World-space move that carries the card from behind the door into reach. Set it to " +
                 "the direction and distance the card should slide out.")]
        [SerializeField] private Vector3 slideOffset = new Vector3(0f, 0f, 0.4f);

        [Tooltip("How long the slide takes, in seconds. 0 snaps it into place.")]
        [SerializeField] private float slideDuration = 1f;

        [SerializeField] private Ease slideEase = Ease.OutQuint;

        [Tooltip("Optional extras to fire alongside the slide - a sound, a particle burst.")]
        [SerializeField] private UnityEvent onRelease;

        private bool _released;

        public bool IsReleased => _released;

        /// <summary>Slides the card into reach. Safe to call more than once - only the first takes.</summary>
        public void Release()
        {
            if (_released)
                return;

            _released = true;

            if (slideOffset != Vector3.zero)
            {
                Vector3 target = transform.position + slideOffset;

                if (slideDuration > 0f)
                    Tween.Position(transform, target, slideDuration, slideEase);
                else
                    transform.position = target;
            }

            onRelease?.Invoke();
        }

        /// <summary>
        /// Marks the card already released without sliding it - used on load when the save says it
        /// was released in a past session, so the world save's restored pose stands as-is.
        /// </summary>
        public void MarkReleasedSilently() => _released = true;
    }
}
