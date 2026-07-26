using System.Collections.Generic;
using CardsChaos.Cards;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Decides the order the album lists its sets in.
    ///
    /// By default the order is a shuffle that is fixed per save - it looks arbitrary but does not
    /// change from one opening to the next, or between sessions. Once the Alphabetical Sets
    /// upgrade is claimed the order becomes A to Z instead.
    /// </summary>
    public interface IAlbumSetOrder
    {
        IReadOnlyList<CardSetDefinition> GetOrderedSets();
    }
}
