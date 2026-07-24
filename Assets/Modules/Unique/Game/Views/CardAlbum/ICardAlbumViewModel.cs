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
