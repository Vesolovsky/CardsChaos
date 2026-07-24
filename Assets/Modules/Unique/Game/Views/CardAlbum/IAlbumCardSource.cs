using CardsChaos.Cards.Album;
using UnityEngine;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// Something inside the album the player can pick a card up from - the hand pile, or a slot
    /// that already holds one.
    ///
    /// The drag itself is never the source's business. It hands over what the card is and where
    /// it lives, then hears back exactly once: either the card went somewhere, or it did not.
    /// </summary>
    public interface IAlbumCardSource
    {
        CardRef Card { get; }

        Sprite Artwork { get; }

        /// <summary>
        /// The rect the dragged card is torn off - it sets the size the floating copy is drawn
        /// at, and the place it flies back to if the drag comes to nothing.
        /// </summary>
        RectTransform Rect { get; }

        /// <summary>
        /// The card has left, and a floating copy is now standing in for it. The original should
        /// stop being drawn, or the player sees two of the same card.
        /// </summary>
        void OnCardLifted();

        /// <summary>Nothing took the card. Show it again, exactly where it was.</summary>
        void OnCardReturned();

        /// <summary>Something took the card. It is gone from here for good.</summary>
        void OnCardTaken();
    }
}
