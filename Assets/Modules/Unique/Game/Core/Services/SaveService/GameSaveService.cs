using System.Collections.Generic;
using UnityEngine;
using Vesolovsky.Core;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Services.Album;

namespace Vesolovsky.Game.Services.Save
{
    public class GameSave : IGameSave
    {
        public Dictionary<CurrencyType, long> Currencies { get; set; }
        public bool IsAnalyticsAllowed { get; set; }
        public bool IsFirstLaunch { get; set; }
        public string BuildVersion { get; set; }

        /// <summary>
        /// Every card the player has filed, flat. Null when read from a save written before the
        /// album existed, which <see cref="LocalCardAlbum"/> reads as an empty album.
        /// </summary>
        public List<AlbumPlacement> Album { get; set; }

        /// <summary>
        /// Purchased level of each permanent upgrade and skill, keyed by upgrade id. A missing key
        /// means level zero - not yet bought - so only touched upgrades take up room here.
        /// </summary>
        public Dictionary<string, int> UpgradeLevels { get; set; }

        /// <summary>
        /// The ids of one-time upgrades the player has claimed. One-time upgrades have no level;
        /// they are either claimed or not, and once claimed they stay claimed for good.
        /// </summary>
        public List<string> UnlockedOneTimeUpgrades { get; set; }

        /// <summary>
        /// Every album page the player has ever completed, as "setId#pageIndex". Kept as a
        /// permanent tally rather than recomputed from <see cref="Album"/> so a page rewards its
        /// skill point exactly once - emptying and refilling it must not pay out again.
        /// </summary>
        public List<string> CompletedPages { get; set; }

        /// <summary>
        /// Seed for the album's default (unsorted) set order. Fixed once on first use and kept, so
        /// the shuffle looks random but does not reshuffle every time the album opens or the game
        /// is relaunched. Zero means "not chosen yet".
        /// </summary>
        public int SetOrderSeed { get; set; }
    }

    public class GameSaveService : SaveService<GameSave>
    {
        protected override GameSave CreateInitialSave()
        {
            return new GameSave()
            {
                Currencies = new Dictionary<CurrencyType, long>()
                {
                    { CurrencyType.SkillPoints, 0 }
                },
                IsAnalyticsAllowed = false,
                IsFirstLaunch = true,
                BuildVersion = BuildVersion.CURRENT_VERSION,
                Album = new List<AlbumPlacement>(),
                UpgradeLevels = new Dictionary<string, int>(),
                UnlockedOneTimeUpgrades = new List<string>(),
                CompletedPages = new List<string>(),
                SetOrderSeed = 0,
            };
        }

        protected override bool SaveRequireReset()
        {
            bool isSaveOutdated = CurrentSave.BuildVersion != BuildVersion.CURRENT_VERSION;

            return isSaveOutdated;
        }

        /// <summary>
        /// Resets the in-memory save only. Persisting is the caller's job, via
        /// <see cref="Vesolovsky.Core.Services.Save.ISaveCoordinator.SaveNow"/>.
        /// </summary>
        public override void ClearSave()
        {
            CurrentSave.Currencies[CurrencyType.SkillPoints] = 0;
            CurrentSave.Album?.Clear();
            CurrentSave.UpgradeLevels?.Clear();
            CurrentSave.UnlockedOneTimeUpgrades?.Clear();
            CurrentSave.CompletedPages?.Clear();

            // Dropped so the next open picks a fresh shuffle rather than the order the wiped
            // collection was last seen in.
            CurrentSave.SetOrderSeed = 0;
        }
    }
}
