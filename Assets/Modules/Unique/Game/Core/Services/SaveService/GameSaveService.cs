using System.Collections.Generic;
using UnityEngine;
using Vesolovsky.Core;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Core.Services.Wallet;

namespace Vesolovsky.Game.Services.Save
{
    public class GameSave : IGameSave
    {
        public Dictionary<CurrencyType, long> Currencies { get; set; }
        public bool IsAnalyticsAllowed { get; set; }
        public bool IsFirstLaunch { get; set; }
        public string BuildVersion { get; set; }
    }

    public class GameSaveService : SaveService<GameSave>
    {
        protected override GameSave CreateInitialSave()
        {
            return new GameSave()
            {
                Currencies = new Dictionary<CurrencyType, long>()
                {
                    { CurrencyType.Coins, 0 }
                },
                IsAnalyticsAllowed = false,
                IsFirstLaunch = true,
                BuildVersion = BuildVersion.CURRENT_VERSION,
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
            CurrentSave.Currencies[CurrencyType.Coins] = 0;
        }
    }
}
