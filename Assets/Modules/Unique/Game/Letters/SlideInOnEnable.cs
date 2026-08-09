using PrimeTween;
using UnityEngine;

namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// Slides an object into its authored place whenever it is switched on. The letter-arrival
    /// service brings a triggered letter in by activating it (<c>SetActive(true)</c>); this is the
    /// ready-made "how it slides" - drop it on the letter, place the letter where it should come to
    /// rest, and set <see cref="fromOffset"/> to where it starts (under the door). Every enable plays
    /// the glide, so it also replays on a reload of a letter that had already arrived.
    ///
    /// Optional - author your own Animator or Timeline instead if you want more than a straight
    /// slide; either way the trigger is simply the object being enabled.
    /// </summary>
    [AddComponentMenu("CardsChaos/Slide In On Enable")]
    public class SlideInOnEnable : MonoBehaviour
    {
        [Tooltip("Where the object starts, relative to its resting local position - e.g. tucked back " +
                 "under the door. It glides from resting + this offset to resting.")]
        [SerializeField] private Vector3 fromOffset = new Vector3(0f, 0f, -0.3f);

        [Tooltip("How long the slide takes, in seconds.")]
        [SerializeField] private float duration = 0.8f;

        [SerializeField] private Ease ease = Ease.OutQuint;

        // The authored place the object comes to rest, captured before any offset is applied.
        private Vector3 _restingLocalPosition;
        private bool _captured;

        private void Awake() => Capture();

        private void OnEnable()
        {
            // Awake runs the first time the object is activated, just before this; captured there so
            // the resting pose is the authored one, not the offset one a re-enable would otherwise read.
            Capture();

            transform.localPosition = _restingLocalPosition + fromOffset;
            Tween.LocalPosition(transform, _restingLocalPosition, duration, ease);
        }

        private void Capture()
        {
            if (_captured)
                return;

            _restingLocalPosition = transform.localPosition;
            _captured = true;
        }
    }
}
