using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Pause;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Stats
{
    /// <summary>
    /// Keeps the player's running tally. The discrete counters ride the events that already exist -
    /// a thrown card, a pickup, a fired skill - and the continuous ones (playtime, distance)
    /// accumulate each frame straight into the save object, the way the wallet mutates its balances
    /// in place.
    ///
    /// The collection figures (correctly placed, total, left) are worked out from the album,
    /// duplicate containers and catalog. A snapshot is written into the save and kept in step with
    /// both destinations, so a screen outside gameplay can read progress without scene objects.
    ///
    /// Nothing here forces a write on the per-frame path: the counters live inside the save, so any
    /// save the game already takes carries them, and a light throttle nudges the coordinator dirty
    /// often enough that even a long idle stretch is not lost past the next autosave (a quit always
    /// captures the rest).
    /// </summary>
    public class PlayerStatsService : IPlayerStats, IInitializable, ITickable, IDisposable
    {
        // A stretch of pure playtime this long with nothing else touching the save nudges it dirty,
        // so an idle session that never picks up a card still autosaves its clock before too long.
        private const float DirtyFlushIntervalSeconds = 30f;

        // How often the validation dump is printed while logging is on.
        private const float LogIntervalSeconds = 5f;

        public event Action Changed;

        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ICardCatalog _catalog;
        private readonly ICardAlbum _album;
        private readonly CardHand _hand;
        private readonly CameraPanController _camera;
        private readonly IPauseState _pause;
        private readonly ISkillService _skills;
        private readonly bool _enableLogging;

        private bool _sessionCounted;
        private bool _collectionDirty;
        private float _secondsSinceDirty;
        private float _secondsSinceLog;

        [Inject]
        public PlayerStatsService(
            ISaveService<GameSave> saveService,
            ISaveCoordinator saveCoordinator,
            ICardCatalog catalog,
            ICardAlbum album,
            CardHand hand,
            [InjectOptional] CameraPanController camera,
            [InjectOptional] IPauseState pause,
            [InjectOptional] ISkillService skills,
            bool enableLogging = false)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
            _catalog = catalog;
            _album = album;
            _hand = hand;
            _camera = camera;
            _pause = pause;
            _skills = skills;
            _enableLogging = enableLogging;
        }

        public void Initialize()
        {
            if (_hand != null)
            {
                _hand.Thrown += OnThrown;
                _hand.PickedUp += OnPickedUp;
            }

            if (_skills != null)
                _skills.Activated += OnSkillActivated;

            if (_album != null)
                _album.PageChanged += OnPageChanged;

            CardStackContainer.ContentsChanged += OnContainerContentsChanged;
        }

        public void Dispose()
        {
            if (_hand != null)
            {
                _hand.Thrown -= OnThrown;
                _hand.PickedUp -= OnPickedUp;
            }

            if (_skills != null)
                _skills.Activated -= OnSkillActivated;

            if (_album != null)
                _album.PageChanged -= OnPageChanged;

            CardStackContainer.ContentsChanged -= OnContainerContentsChanged;
        }

        public void Tick()
        {
            // The save loads asynchronously and is not in at Zenject init time; until it lands there
            // is nowhere to write, so hold off. Nothing else can happen in the room before then.
            PlayerStatsData stats = Stats;
            if (stats == null)
                return;

            // Counted here rather than in Initialize because that runs before the save is loaded.
            // The first tick with a save in hand is the first real frame of the session.
            if (!_sessionCounted)
            {
                _sessionCounted = true;
                stats.SessionsPlayed++;

                // Bring the saved collection snapshot up to the room we just loaded, in case content
                // was added since the last save or this save predates the snapshot.
                RefreshCollection(stats);

                _saveCoordinator.MarkDirty();
                Log($"Session #{stats.SessionsPlayed} started — {Summary(stats)}");
                Changed?.Invoke();
            }

            if (_collectionDirty)
            {
                _collectionDirty = false;
                RefreshCollection(stats);
            }

            AccumulateDistance(stats);
            AccumulatePlaytime(stats);
            TickLog(stats);
        }

        private void AccumulateDistance(PlayerStatsData stats)
        {
            if (_camera == null)
                return;

            float distance = _camera.LastMoveDistance;
            if (distance <= 0f)
                return;

            stats.DistanceTraveled += distance;
            if (_camera.IsSprinting)
                stats.DistanceSprinted += distance;
        }

        private void AccumulatePlaytime(PlayerStatsData stats)
        {
            if (_pause != null && _pause.IsPaused)
                return;

            float delta = Time.deltaTime;
            if (delta <= 0f)
                return;

            stats.PlaytimeSeconds += delta;

            // Stamped on every counted frame rather than on a timer, so the value in memory is
            // always the last instant the player was really playing. It does not mark the save
            // dirty on its own - the throttle below does that for both figures - but the quit path
            // serializes the live save, so a session that ends by closing the game still records
            // the moment it ended rather than the last throttle tick before it.
            stats.LastPlayedAt = DateTime.Now;

            // The continuous figures do not mark the save dirty every frame - that would thrash
            // autosave - so a periodic nudge makes sure a session that only ever stood still and
            // watched the clock still gets flushed on the next autosave rather than only at quit.
            _secondsSinceDirty += delta;
            if (_secondsSinceDirty >= DirtyFlushIntervalSeconds)
            {
                _secondsSinceDirty = 0f;
                _saveCoordinator.MarkDirty();
            }
        }

        private void TickLog(PlayerStatsData stats)
        {
            if (!_enableLogging)
                return;

            _secondsSinceLog += Time.deltaTime;
            if (_secondsSinceLog < LogIntervalSeconds)
                return;

            _secondsSinceLog = 0f;
            Log(Summary(stats));
        }

        private void OnThrown(Card _)
        {
            PlayerStatsData stats = Stats;
            if (stats == null)
                return;

            stats.CardsThrown++;
            Bump();
            Log($"Card thrown → {stats.CardsThrown}");
        }

        private void OnPickedUp(Card _)
        {
            PlayerStatsData stats = Stats;
            if (stats == null)
                return;

            stats.CardsPickedUp++;
            Bump();
            Log($"Card picked up → {stats.CardsPickedUp}");
        }

        private void OnSkillActivated(SkillId id)
        {
            PlayerStatsData stats = Stats;
            if (stats == null)
                return;

            stats.SkillsUsed++;
            Bump();
            Log($"Skill '{id}' used → {stats.SkillsUsed}");
        }

        // Any album move changes what is filed; re-snapshot here where the count has just settled.
        private void OnPageChanged(string setId)
        {
            PlayerStatsData stats = Stats;
            if (stats == null)
                return;

            RefreshCollection(stats);
        }

        private void OnContainerContentsChanged()
        {
            // Parenting can change many cards in one save-restore frame. Coalesce the burst into
            // one scan on Tick instead of rescanning the whole room for every restored child.
            _collectionDirty = true;
        }

        /// <summary>
        /// Recomputes the collection snapshot from the live album and writes it into the save when it
        /// has moved. Also lifts the all-time peak. A no-op when the album or catalog is absent, so
        /// the last saved snapshot simply stands.
        /// </summary>
        private void RefreshCollection(PlayerStatsData stats)
        {
            if (_catalog == null || _album == null)
                return;

            // Every counted catalog card earns a point by sitting correctly in the album, and the
            // share of them authored into the room twice earns a second one by having that copy put
            // away in the duplicate box. Sets flagged out of the collection (the endgame set) count
            // toward neither side.
            int total = 0;
            int correct = 0;
            foreach (CardSetDefinition set in _catalog.Sets)
            {
                if (set == null || !set.CountsTowardCollection)
                    continue;

                total += set.CardCount + set.DuplicateCount;
                correct += _album.CountCorrect(set.SetId);
            }

            // A boxed card is put away whether or not its twin is in the album yet - the two halves
            // are earned independently, in whichever order the player gets to them. The containers
            // keep their own tally, so this does not sweep the room; they also refuse a second copy
            // of the same card, and the count is clamped in case a hand-edited save slipped one in.
            int duplicates = 0;
            foreach (KeyValuePair<CardRef, int> stored in CardStackContainer.StoredCards)
            {
                if (!stored.Key.IsValid || stored.Value <= 0)
                    continue;

                CardSetDefinition set = _catalog.FindSet(stored.Key.SetId);
                if (set != null && set.CountsTowardCollection)
                    duplicates++;
            }

            // Kept before the duplicates are folded in: the album milestones are counted in filed
            // cards alone, so they need the album half on its own as well as the combined figure.
            int albumCorrect = correct;

            correct += duplicates;

            bool peakRose = correct > stats.PeakCorrectlyPlaced;
            bool duplicatePeakRose = duplicates > stats.PeakDuplicatesStored;
            bool albumPeakRose = albumCorrect > stats.PeakAlbumCorrect;
            bool snapshotMoved = correct != stats.CorrectlyPlacedCards || total != stats.TotalCards;
            if (!peakRose && !duplicatePeakRose && !albumPeakRose && !snapshotMoved)
                return;

            stats.CorrectlyPlacedCards = correct;
            stats.TotalCards = total;
            if (peakRose)
                stats.PeakCorrectlyPlaced = correct;

            // The same one-way rule as the other two peaks: taking a card back out of the album
            // does not undo the milestone it crossed on the way in.
            if (albumPeakRose)
                stats.PeakAlbumCorrect = albumCorrect;

            // The duplicate task is measured against the high-water mark, so emptying a box later
            // cannot take a claimed reward - or a nearly finished task - back off the player.
            if (duplicatePeakRose)
                stats.PeakDuplicatesStored = duplicates;

            _saveCoordinator.MarkDirty();
            Log($"Collection {correct}/{total} (remaining {Mathf.Max(0, total - correct)}, " +
                $"peak {stats.PeakCorrectlyPlaced}, album {albumCorrect}, " +
                $"peak {stats.PeakAlbumCorrect}, boxed duplicates {duplicates}, " +
                $"peak {stats.PeakDuplicatesStored})");
            Changed?.Invoke();
        }

        private void Bump()
        {
            _saveCoordinator.MarkDirty();
            Changed?.Invoke();
        }

        private void Log(string message)
        {
            if (_enableLogging)
                Debug.Log($"[PlayerStats] {message}");
        }

        private string Summary(PlayerStatsData s)
        {
            return $"thrown={s.CardsThrown} pickedUp={s.CardsPickedUp} skills={s.SkillsUsed} " +
                   $"sessions={s.SessionsPlayed} playtime={s.PlaytimeSeconds:F1}s " +
                   $"dist={s.DistanceTraveled:F2} sprint={s.DistanceSprinted:F2} " +
                   $"correct={s.CorrectlyPlacedCards}/{s.TotalCards} " +
                   $"remaining={Mathf.Max(0, s.TotalCards - s.CorrectlyPlacedCards)} " +
                   $"peak={s.PeakCorrectlyPlaced}";
        }

        // --- IPlayerStats: cumulative reads ---

        public long CardsThrown => Stats?.CardsThrown ?? 0L;
        public long CardsPickedUp => Stats?.CardsPickedUp ?? 0L;
        public long SkillsUsed => Stats?.SkillsUsed ?? 0L;
        public long SessionsPlayed => Stats?.SessionsPlayed ?? 0L;
        public double PlaytimeSeconds => Stats?.PlaytimeSeconds ?? 0d;
        public double DistanceTraveled => Stats?.DistanceTraveled ?? 0d;
        public double DistanceSprinted => Stats?.DistanceSprinted ?? 0d;
        public int PeakCorrectlyPlaced => Stats?.PeakCorrectlyPlaced ?? 0;
        public int PeakAlbumCorrect => Stats?.PeakAlbumCorrect ?? 0;
        public int PeakDuplicatesStored => Stats?.PeakDuplicatesStored ?? 0;

        // --- IPlayerStats: collection snapshot reads (straight from the save) ---

        public int TotalCards => Stats?.TotalCards ?? 0;
        public int CorrectlyPlacedCards => Stats?.CorrectlyPlacedCards ?? 0;

        public int CardsRemainingToPlace
        {
            get
            {
                PlayerStatsData stats = Stats;
                return stats == null ? 0 : Mathf.Max(0, stats.TotalCards - stats.CorrectlyPlacedCards);
            }
        }

        /// <summary>
        /// The live stats block in the save, or null before the save has loaded. Created on first
        /// use for a save written before stats existed, the same lazy-fill the album and progress
        /// tally use, so an old save simply starts its counters from zero.
        /// </summary>
        private PlayerStatsData Stats
        {
            get
            {
                GameSave save = _saveService.CurrentSave;
                if (save == null)
                    return null;

                return save.PlayerStats ??= new PlayerStatsData();
            }
        }
    }
}
