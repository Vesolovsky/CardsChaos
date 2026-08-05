using System.Collections.Generic;
using CardsChaos.Cards;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// Decides the order the album lists its sets in - always alphabetical, A to Z.
    /// </summary>
    public interface IAlbumSetOrder
    {
        IReadOnlyList<CardSetDefinition> GetOrderedSets();
    }
}
