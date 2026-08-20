using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UniRx;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Views.Album;

namespace Vesolovsky.Game.Views
{
    public interface ICardAlbumViewModel : IViewModel, IAlbumMoves
    {
        /// <summary>
        /// Whether the album has the room. While it is true the player cannot walk, turn the
        /// camera, or pick anything up off the floor.
        /// </summary>
        IReadOnlyReactiveProperty<bool> IsOpen { get; }

        /// <summary>Raised with the id of the set whose page just changed.</summary>
        event Action<string> AlbumChanged;

        /// <summary>Every set, in the order they are listed down the left-hand side.</summary>
        IReadOnlyList<CardSetDefinition> Sets { get; }

        /// <summary>
        /// The endgame set - the one flagged out of the collection, holding the single final card.
        /// Null when the game has no such set.
        /// </summary>
        CardSetDefinition EndgameSet { get; }

        /// <summary>
        /// Whether the player is holding the final card right now. When they open the album holding
        /// it, the album opens straight into its endgame state.
        /// </summary>
        bool HoldsEndgameCard { get; }

        /// <summary>
        /// The album as a display case: cards can be looked at and turned over, never moved. What
        /// the main menu's album is, and what the album falls back to wherever the room's half of
        /// it - the hand, the card factory - is not present to make a move with.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>The final card as filed, or <see cref="CardRef.None"/> while it is still out there.</summary>
        CardRef EndgameCard { get; }

        /// <summary>
        /// Whether the final card has been filed - the game finished. The read-only album opens on
        /// the closing spread when it has.
        /// </summary>
        bool IsEndgameCardFiled { get; }

        /// <summary>The hand the pile is a view of.</summary>
        CardHand Hand { get; }

        ICardAlbum Album { get; }

        CardArtworkResolver Artwork { get; }

        void Open();

        void Close();

        /// <summary>The X of a set button's "X / Y".</summary>
        int CountFiled(string setId);
    }
}
