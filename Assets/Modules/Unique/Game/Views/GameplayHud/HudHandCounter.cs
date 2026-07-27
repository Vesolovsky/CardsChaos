using PrimeTween;
using TMPro;
using UnityEngine;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// The "held / max" readout for the hand. It kicks when the number moves - a card picked up or
    /// let go - and flinches when a pickup is turned away because the hand is full, so the reason a
    /// card would not come up off the floor is answered right where the count is.
    ///
    /// The kick is the album's progress kick and the flinch is the album slot's wrong-card shake,
    /// on purpose: the same two feelings mean the same two things across the game.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Hand Counter")]
    public class HudHandCounter : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        [Tooltip("What the kick and flinch move. Defaults to the text's own rect.")]
        [SerializeField] private RectTransform target;

        [Header("Kick (count changed)")]
        [SerializeField] private Vector3 punchStrength = new Vector3(0.12f, 0.12f, 0f);
        [SerializeField] private float punchDuration = 0.3f;
        [SerializeField] private float punchFrequency = 3f;

        [Header("Flinch (hand full)")]
        [SerializeField] private Vector3 shakeStrength = new Vector3(5f, 5f, 0f);
        [SerializeField] private float shakeDuration = 0.18f;
        [SerializeField] private float shakeFrequency = 22f;

        private Tween _punch;
        private Tween _shake;
        private Vector3 _shakeRest;

        private RectTransform Target =>
            target != null ? target : text != null ? text.rectTransform : null;

        public void SetCounts(int held, int max)
        {
            if (text != null)
                text.SetText($"{held}/{max}");
        }

        /// <summary>The kick a moved count takes - a card picked up or thrown.</summary>
        public void PlayPunch()
        {
            RectTransform t = Target;
            if (t == null)
                return;

            if (_punch.isAlive)
            {
                _punch.Stop();
                t.localScale = Vector3.one;
            }

            _punch = Tween.PunchScale(t, punchStrength, punchDuration, punchFrequency);
        }

        /// <summary>The flinch a full hand gives when it cannot take the card being picked up.</summary>
        public void PlayShake()
        {
            RectTransform t = Target;
            if (t == null)
                return;

            // Rest captured while still and restored before a re-triggered shake, so a flinch cut
            // short never leaves the readout adrift.
            if (_shake.isAlive)
            {
                _shake.Stop();
                t.localPosition = _shakeRest;
            }
            else
            {
                _shakeRest = t.localPosition;
            }

            _shake = Tween.ShakeLocalPosition(t, shakeStrength, shakeDuration, shakeFrequency);
        }

        private void OnDestroy()
        {
            if (_punch.isAlive)
                _punch.Stop();

            if (_shake.isAlive)
                _shake.Stop();
        }
    }
}
