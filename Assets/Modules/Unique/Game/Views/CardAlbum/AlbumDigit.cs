using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// One digit of a card number: the glyph and the inner shadow that rides on top of it, the
    /// same two-image pairing the set icon and the card face already use.
    ///
    /// This is also the unit <see cref="AlbumCardNumber"/> clones when a number needs a second
    /// digit, which is why the pair is a component with its own references rather than something
    /// found by walking the children - a clone has to arrive complete.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Digit")]
    public class AlbumDigit : MonoBehaviour
    {
        [SerializeField] private Image glyph;

        [Tooltip("Must be a child of this object, so cloning the digit brings it along.")]
        [SerializeField] private Image innerShadow;

        public RectTransform Rect => (RectTransform)transform;

        public void Show(CardDigitSprites.Digit digit)
        {
            Apply(glyph, digit.Glyph);
            Apply(innerShadow, digit.InnerShadow);
        }

        /// <summary>
        /// An Image with no sprite draws a white box, so a digit whose art is missing is switched
        /// off instead - a gap in the number reads as a gap, not as a blank tile.
        /// </summary>
        private static void Apply(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
