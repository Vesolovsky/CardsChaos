using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// Writes a card's number across a slot, one image per digit.
    ///
    /// The digit already placed in the prefab is both the first digit and the template the rest
    /// are cloned from, so wherever it was positioned is exactly where a single-digit number
    /// appears - nothing here moves it. Longer numbers spread symmetrically around that same
    /// point, which is what keeps 7 and 27 looking like they belong on the same page.
    ///
    /// Digits are spaced by a fixed advance rather than by their own widths. The art is drawn on
    /// square canvases, so measuring would give a 1 a narrower cell than an 8 and the numbers
    /// would stop lining up between one slot and the next - the same reason ledgers are set in
    /// monospace.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Card Number")]
    public class AlbumCardNumber : MonoBehaviour
    {
        [SerializeField] private CardDigitSprites digitSprites;

        [Tooltip("The digit that is already in the prefab. It stays where it is, and every " +
                 "further digit is a clone of it parented alongside.")]
        [SerializeField] private AlbumDigit firstDigit;

        [Tooltip("Distance between the centres of neighbouring digits, in pixels. Smaller than " +
                 "the digit is wide, because the glyphs sit inside square canvases with air " +
                 "around them.")]
        [SerializeField] private float advance = 42f;

        private readonly List<AlbumDigit> _digits = new List<AlbumDigit>();
        private readonly List<int> _values = new List<int>(4);

        private Vector2 _origin;
        private bool _originCaptured;
        private int _shownCount;
        private bool _visible = true;
        private bool _reportedMissingArt;

        public void SetNumber(int number)
        {
            Split(number);

            EnsureDigits(_values.Count);
            _shownCount = _values.Count;

            for (int i = 0; i < _values.Count; i++)
            {
                AlbumDigit digit = _digits[i];

                if (digitSprites != null && digitSprites.TryGet(_values[i], out CardDigitSprites.Digit art))
                {
                    digit.Show(art);
                }
                else
                {
                    digit.Show(default);
                    ReportMissingArt(number);
                }

                // Centred on the template's own position: half a step either side for two
                // digits, dead on it for one.
                digit.Rect.anchoredPosition =
                    Origin + new Vector2((i - (_values.Count - 1) * 0.5f) * advance, 0f);
            }

            ApplyVisibility();
        }

        /// <summary>
        /// Shows or hides the whole number. Driven by the slot, which only labels a square while
        /// it is still waiting for its card - a filled slot shows the card, and the card has its
        /// number printed on it already.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility();
        }

        // Read lazily rather than in Awake so it cannot be captured before a layout pass has
        // settled the prefab's own placement.
        private Vector2 Origin
        {
            get
            {
                if (!_originCaptured)
                {
                    _origin = firstDigit.Rect.anchoredPosition;
                    _originCaptured = true;
                }

                return _origin;
            }
        }

        private void Split(int number)
        {
            _values.Clear();

            if (number <= 0)
            {
                // Cards are numbered from one, so this is a bug elsewhere rather than a real
                // number - draw a zero so it is visible instead of an empty square.
                _values.Add(0);
                return;
            }

            while (number > 0)
            {
                _values.Add(number % 10);
                number /= 10;
            }

            _values.Reverse();
        }

        private void EnsureDigits(int count)
        {
            if (_digits.Count == 0)
                _digits.Add(firstDigit);

            while (_digits.Count < count)
            {
                // A plain instantiate: a digit is a pair of images with nothing injected into
                // it. Parented alongside the original and told not to keep its world position,
                // so it inherits the template's anchors and size rather than being reflowed.
                AlbumDigit clone = Instantiate(firstDigit, firstDigit.Rect.parent, false);
                clone.name = $"{firstDigit.name}_{_digits.Count}";

                _digits.Add(clone);
            }
        }

        private void ApplyVisibility()
        {
            for (int i = 0; i < _digits.Count; i++)
                _digits[i].gameObject.SetActive(_visible && i < _shownCount);
        }

        /// <summary>Once per slot, not once per digit - a missing set would otherwise fill the console.</summary>
        private void ReportMissingArt(int number)
        {
            if (_reportedMissingArt)
                return;

            _reportedMissingArt = true;

            Debug.LogError(
                $"[{nameof(AlbumCardNumber)}] No digit art for {number}. Assign a " +
                $"{nameof(CardDigitSprites)} asset and fill it from its context menu.", this);
        }
    }
}
