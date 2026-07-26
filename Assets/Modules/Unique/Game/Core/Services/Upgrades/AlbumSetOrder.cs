using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Upgrades
{
    /// <summary>
    /// The save-backed <see cref="IAlbumSetOrder"/>.
    ///
    /// The default shuffle is deterministic: it is seeded from a number stored in the save, chosen
    /// once and kept, so the same save always lays the sets out the same way while different saves
    /// differ. Claiming Alphabetical Sets switches to a plain name sort and ignores the seed.
    /// </summary>
    public class AlbumSetOrder : IAlbumSetOrder
    {
        private readonly ICardCatalog _catalog;
        private readonly IUpgradeService _upgrades;
        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly OneTimeUpgradeDefinition _alphabetical;

        [Inject]
        public AlbumSetOrder(
            ICardCatalog catalog,
            IUpgradeService upgrades,
            UpgradeCatalog upgradeCatalog,
            ISaveService<GameSave> saveService,
            ISaveCoordinator saveCoordinator)
        {
            _catalog = catalog;
            _upgrades = upgrades;
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
            _alphabetical = upgradeCatalog.FindOneTime(OneTimeUpgradeKind.AlphabeticalSets);
        }

        public IReadOnlyList<CardSetDefinition> GetOrderedSets()
        {
            var sets = new List<CardSetDefinition>();
            foreach (CardSetDefinition set in _catalog.Sets)
            {
                if (set != null)
                    sets.Add(set);
            }

            if (_alphabetical != null && _upgrades.IsUnlocked(_alphabetical))
            {
                sets.Sort((a, b) =>
                    string.Compare(a.SetName, b.SetName, StringComparison.CurrentCultureIgnoreCase));

                return sets;
            }

            Shuffle(sets, Seed());
            return sets;
        }

        /// <summary>
        /// The saved shuffle seed, chosen the first time it is needed. Zero is the "not chosen yet"
        /// marker, so a fresh non-zero number is drawn, written and reused from then on.
        /// </summary>
        private int Seed()
        {
            GameSave save = _saveService.CurrentSave;
            if (save == null)
                return 1;

            if (save.SetOrderSeed == 0)
            {
                // Guid gives a well-spread number, and the loop is only there to reject the one
                // value that means "unchosen".
                int seed;
                do
                {
                    seed = Guid.NewGuid().GetHashCode();
                }
                while (seed == 0);

                save.SetOrderSeed = seed;
                _saveCoordinator.MarkDirty();
            }

            return save.SetOrderSeed;
        }

        private static void Shuffle(List<CardSetDefinition> sets, int seed)
        {
            var random = new System.Random(seed);

            for (int i = sets.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (sets[i], sets[j]) = (sets[j], sets[i]);
            }
        }
    }
}
