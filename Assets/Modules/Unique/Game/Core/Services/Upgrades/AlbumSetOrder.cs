using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// The <see cref="IAlbumSetOrder"/>: the album always lists its sets alphabetically, A to Z.
    ///
    /// It used to shuffle per save and only sort once an upgrade was claimed; the sort is now the
    /// only order there is, so there is nothing here to read from the save or the upgrade record.
    /// </summary>
    public class AlbumSetOrder : IAlbumSetOrder
    {
        private readonly ICardCatalog _catalog;

        [Inject]
        public AlbumSetOrder(ICardCatalog catalog)
        {
            _catalog = catalog;
        }

        public IReadOnlyList<CardSetDefinition> GetOrderedSets()
        {
            var sets = new List<CardSetDefinition>();
            foreach (CardSetDefinition set in _catalog.Sets)
            {
                if (set != null)
                    sets.Add(set);
            }

            sets.Sort((a, b) =>
                string.Compare(a.SetName, b.SetName, StringComparison.CurrentCultureIgnoreCase));

            return sets;
        }
    }
}
