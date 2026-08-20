using System;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.UISystem;

namespace Vesolovsky.Game.Views
{
    public interface IMainMenuViewModel : IViewModel
    {
        /// <summary>
        /// Whether there is a game to go back to. This is what decides if the Continue card is
        /// part of the fan at all, and whether New Game has anything to warn about.
        /// </summary>
        bool HasStartedGame { get; }

        /// <summary>
        /// Whether the save carries a usable collection count. Saves written before the collection
        /// snapshot existed do not, and the Album card then simply shows no progress line rather
        /// than an invented "0/0".
        /// </summary>
        bool HasCollectionProgress { get; }

        /// <summary>Cards correctly placed - the X of the Album card's "X / Y".</summary>
        int CardsCollected { get; }

        /// <summary>Every card and duplicate there is to place - the Y.</summary>
        int TotalCards { get; }

        /// <summary>
        /// When the save was last played, for the line on the Continue card. Null on a save
        /// written before that was recorded, which shows no line rather than a made-up date.
        /// </summary>
        DateTime? LastPlayedAt { get; }

        /// <summary>
        /// Throws the save away and writes the empty one straight to disk, so the fresh start
        /// survives even if the game is killed before it reaches the room.
        /// </summary>
        UniTask StartNewGame();
    }
}
