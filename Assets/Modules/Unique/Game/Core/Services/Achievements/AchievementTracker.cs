using System;
using System.Collections.Generic;
using System.Threading;
using CardsChaos.Cards;
using Cysharp.Threading.Tasks;
using Vesolovsky.Core.Services.Achievements;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Letters;
using Vesolovsky.Game.Services.Progress;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Services.Stats;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Achievements
{
    /// <summary>
    /// Watches the game for the conditions behind the achievements and awards them.
    ///
    /// Nothing here keeps state of its own. Every condition is a question asked of a service that
    /// already knows the answer and already persists it - the progress tally for finished sets, the
    /// player stats for the filed and boxed high-water marks, the upgrade service for claimed tasks
    /// and bought levels, the letter collection for what has been read. The two that are moments
    /// rather than states - a house of cards coming down - ride the collapse event.
    ///
    /// That is why re-checking everything is safe and is in fact what happens on every load: Steam
    /// is the record of what has been earned, so a condition already met just reads back as earned
    /// and awards nothing. It is also what lets this ship after players already have saves - their
    /// existing progress is picked up the first time they launch the new build.
    /// </summary>
    public class AchievementTracker : IInitializable, IDisposable
    {
        private readonly IAchievementService _achievements;
        private readonly ISaveService<GameSave> _saveService;
        private readonly ICollectionProgress _progress;
        private readonly ICardCatalog _cardCatalog;
        private readonly IPlayerStats _stats;
        private readonly IUpgradeService _upgrades;
        private readonly UpgradeCatalog _upgradeCatalog;
        private readonly ILetterCollection _letters;
        private readonly IEndgameRecord _endgame;

        private readonly CancellationTokenSource _loadCts = new CancellationTokenSource();

        private bool _subscribed;

        [Inject]
        public AchievementTracker(
            IAchievementService achievements,
            ISaveService<GameSave> saveService,
            ICollectionProgress progress,
            ICardCatalog cardCatalog,
            IPlayerStats stats,
            IUpgradeService upgrades,
            UpgradeCatalog upgradeCatalog,
            [InjectOptional] ILetterCollection letters,
            [InjectOptional] IEndgameRecord endgame)
        {
            _achievements = achievements;
            _saveService = saveService;
            _progress = progress;
            _cardCatalog = cardCatalog;
            _stats = stats;
            _upgrades = upgrades;
            _upgradeCatalog = upgradeCatalog;
            _letters = letters;
            _endgame = endgame;
        }

        public void Initialize()
        {
            // The house collapse is the one condition that can fire before the save is in - a house
            // knocked over in the first second of a session - so it is subscribed straight away
            // rather than behind the load. Awarding without a save is fine: it asks nothing of one.
            CardHouse.Collapsed += OnHouseCollapsed;

            SubscribeWhenLoaded(_loadCts.Token).Forget();
        }

        public void Dispose()
        {
            _loadCts.Cancel();
            _loadCts.Dispose();

            CardHouse.Collapsed -= OnHouseCollapsed;

            if (!_subscribed)
                return;

            _progress.SetCompleted -= OnSetCompleted;
            _stats.Changed -= OnStatsChanged;
            _upgrades.Changed -= OnUpgradesChanged;
            if (_letters != null) _letters.Collected -= OnLetterCollected;
            if (_endgame != null) _endgame.Recorded -= OnEndgameRecorded;
        }

        private async UniTaskVoid SubscribeWhenLoaded(CancellationToken token)
        {
            // The save loads asynchronously and is not in at Zenject init time, the same as the
            // world restore and the letter collection - wait for it before reading any tally.
            bool canceled = await UniTask
                .WaitUntil(() => _saveService?.CurrentSave != null, cancellationToken: token)
                .SuppressCancellationThrow();

            if (canceled)
                return;

            GameAchievements.Validate(setId => _cardCatalog?.FindSet(setId) != null);

            _progress.SetCompleted += OnSetCompleted;
            _stats.Changed += OnStatsChanged;
            _upgrades.Changed += OnUpgradesChanged;
            if (_letters != null) _letters.Collected += OnLetterCollected;
            if (_endgame != null) _endgame.Recorded += OnEndgameRecorded;
            _subscribed = true;

            // Catch up on everything this save already satisfies. Anything Steam has recorded is
            // skipped, so a returning player is not showered in toasts for old work; anything they
            // finished while Steam was closed, or before this build shipped, lands now.
            EvaluateCollections();
            EvaluateCounts();
            EvaluateLetters();
            EvaluateUpgrades();
            EvaluateEndgame(_endgame?.Summary);
        }

        // --- Live sources ---

        private void OnSetCompleted(string setId) => EvaluateCollections();

        private void OnStatsChanged() => EvaluateCounts();

        private void OnUpgradesChanged(UpgradeDefinition _) => EvaluateUpgrades();

        private void OnLetterCollected(LetterId _) => EvaluateLetters();

        private void OnEndgameRecorded(EndgameSummary summary) => EvaluateEndgame(summary);

        private void OnHouseCollapsed(CardHouse house, HouseCollapseCause cause)
        {
            switch (cause)
            {
                case HouseCollapseCause.Levitate:
                    _achievements.Unlock(AchievementId.HouseByLevitate);
                    break;

                case HouseCollapseCause.StruckByCard:
                    _achievements.Unlock(AchievementId.HouseByThrow);
                    break;
            }
        }

        // --- Conditions ---

        /// <summary>
        /// The set-group achievements, and the endgame card. Each group asks for every one of its
        /// sets to be finished; the tally behind that is permanent, so a group once satisfied stays
        /// satisfied even if the player empties a page afterwards.
        /// </summary>
        private void EvaluateCollections()
        {
            foreach (KeyValuePair<AchievementId, string[]> group in GameAchievements.BySetGroup)
            {
                if (AreAllCompleted(group.Value))
                    _achievements.Unlock(group.Key);
            }

            // The one-card endgame set. Filing its card is the last move in the game, which is what
            // this marks - not the "all cards filed" moment that slid the card out in the first place.
            if (_progress.IsSetCompleted(GameAchievements.EndgameSetId))
                _achievements.Unlock(AchievementId.TheCollector);
        }

        private bool AreAllCompleted(string[] setIds)
        {
            foreach (string setId in setIds)
            {
                if (!_progress.IsSetCompleted(setId))
                    return false;
            }

            return setIds.Length > 0;
        }

        /// <summary>
        /// The counted milestones. Both are measured against high-water marks rather than the live
        /// count, so a milestone crossed and then walked back - a card lifted out, a box emptied -
        /// still counts, exactly as the tasks measured on the same numbers do.
        /// </summary>
        private void EvaluateCounts()
        {
            int filed = _stats.PeakAlbumCorrect;
            int boxed = _stats.PeakDuplicatesStored;

            // Every duplicate authored into the room, worked out from the catalog rather than
            // written down, so content added or removed cannot leave this asking for a number the
            // game can no longer reach.
            int allDuplicates = CardDuplicates.TotalQuota(_cardCatalog);

            Award(AchievementId.AlbumHundred, filed, GameAchievements.AlbumHundredTarget);
            Award(AchievementId.AlbumThousand, filed, GameAchievements.AlbumThousandTarget);
            Award(AchievementId.DuplicatesHundred, boxed, GameAchievements.DuplicatesHundredTarget);

            if (allDuplicates > 0)
                Award(AchievementId.AllDuplicates, boxed, allDuplicates);
        }

        /// <summary>Every letter in the game read.</summary>
        private void EvaluateLetters()
        {
            if (_letters == null)
                return;

            foreach (LetterId id in (LetterId[])Enum.GetValues(typeof(LetterId)))
            {
                if (!_letters.IsCollected(id))
                    return;
            }

            _achievements.Unlock(AchievementId.AllLetters);
        }

        /// <summary>
        /// Every task claimed, and every buyable skill at its top level. Both read the upgrade
        /// service, which is the only thing that owns either answer.
        /// </summary>
        private void EvaluateUpgrades()
        {
            if (_upgradeCatalog == null)
                return;

            if (AreAllTasksClaimed())
                _achievements.Unlock(AchievementId.AllTasks);

            if (AreAllSkillsMaxed())
                _achievements.Unlock(AchievementId.AllSkillsMaxed);
        }

        private bool AreAllTasksClaimed()
        {
            IReadOnlyList<OneTimeUpgradeDefinition> tasks = _upgradeCatalog.OneTimes;
            if (tasks == null || tasks.Count == 0)
                return false;

            foreach (OneTimeUpgradeDefinition task in tasks)
            {
                if (task != null && !_upgrades.IsUnlocked(task))
                    return false;
            }

            return true;
        }

        private bool AreAllSkillsMaxed()
        {
            IReadOnlyList<SkillDefinition> skills = _upgradeCatalog.Skills;
            if (skills == null || skills.Count == 0)
                return false;

            bool anyBuyable = false;

            foreach (SkillDefinition skill in skills)
            {
                // A task-unlocked skill is not bought and has nothing to max - it is either owned or
                // not, which is what the "every task claimed" achievement already covers. A skill
                // with no levels authored is not something the player can act on either.
                if (skill == null || skill.IsTaskUnlocked || skill.MaxLevel <= 0)
                    continue;

                anyBuyable = true;

                if (_upgrades.GetLevel(skill) < skill.MaxLevel)
                    return false;
            }

            return anyBuyable;
        }

        /// <summary>
        /// The speed run. Read off the frozen ending rather than the live clock, because the
        /// playtime counter keeps running after the last card goes in - the player is free to walk
        /// back into the room, and the credits themselves take a while. Only the tally as it stood
        /// at the moment the game was finished can answer how long it took to finish.
        ///
        /// Called both when an ending is recorded and once on load, so a game finished in time
        /// while Steam was closed - or finished before this achievement existed - is still awarded.
        /// </summary>
        private void EvaluateEndgame(EndgameSummary summary)
        {
            if (summary?.Stats == null)
                return;

            if (summary.Stats.PlaytimeSeconds < GameAchievements.SwiftCollectorMaxSeconds)
                _achievements.Unlock(AchievementId.SwiftCollector);
        }

        // --- Awarding ---

        /// <summary>
        /// Awards a counted achievement the moment its number is reached, and does nothing at all
        /// before then. Every achievement in the game is a single moment, never a running total -
        /// so a condition still short of its target is not news, and reporting it would put a
        /// notification on screen for every card the player files.
        /// </summary>
        private void Award(AchievementId id, int current, int required)
        {
            if (required <= 0 || current < required)
                return;

            _achievements.Unlock(id);
        }
    }
}
