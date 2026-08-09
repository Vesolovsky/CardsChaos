using System;
using System.Collections.Generic;
using System.Threading;
using CardsChaos.Cards;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Services.Progress;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Stats;
using Vesolovsky.Game.Upgrades;
using Vesolovsky.Game.Views.GameplayHud;
using Zenject;

namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// Brings letters into the room on gameplay milestones and keeps their arrival orderly: a skill
    /// first used, a card-count reached, a set completed - each queues its letter, and the queue
    /// shows one at a time, the next only once the current is read. The queue and the endgame card's
    /// release both live in the save, so a quit mid-queue picks up exactly where it left off.
    ///
    /// Triggered letters are authored inactive at their resting spot; showing one activates it (the
    /// slide-in is the object's own on-enable animation) and calls out "New letter arrived". The
    /// endgame card is an ordinary card sitting out of reach behind the door that this slides in once
    /// every counted card is filed; picking it up queues the certificate letter.
    /// </summary>
    public class LetterAppearanceService : IInitializable, ITickable, IDisposable
    {
        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly LetterSettings _settings;
        private readonly ILetterCollection _collection;
        private readonly IPlayerStats _stats;
        private readonly IWorldInteractionLock _worldLock;
        private readonly ISkillService _skills;
        private readonly ICollectionProgress _progress;
        private readonly IHudHints _hudHints;
        private readonly CardHand _hand;

        private readonly CancellationTokenSource _loadCts = new CancellationTokenSource();

        private Dictionary<LetterId, Letter> _lettersById;
        private LetterId? _shownHead;
        private bool _subscribed;

        // Set when the endgame card is released, cleared when its "final card arrived" hint plays.
        // Deferred through the world lock like a letter arrival, so it calls out after the album shut.
        private bool _finalCardHintPending;

        [Inject]
        public LetterAppearanceService(
            ISaveService<GameSave> saveService,
            ISaveCoordinator saveCoordinator,
            LetterSettings settings,
            ILetterCollection collection,
            IPlayerStats stats,
            IWorldInteractionLock worldLock,
            [InjectOptional] ISkillService skills,
            [InjectOptional] ICollectionProgress progress,
            [InjectOptional] IHudHints hudHints,
            [InjectOptional] CardHand hand)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
            _settings = settings;
            _collection = collection;
            _stats = stats;
            _worldLock = worldLock;
            _skills = skills;
            _progress = progress;
            _hudHints = hudHints;
            _hand = hand;
        }

        public void Initialize() => WhenLoaded(_loadCts.Token).Forget();

        // Retries a deferred arrival: a letter triggered while the room is taken (the album, a panel,
        // reading another letter) waits here until the room is the player's again, so it slides in
        // and calls out only once they are back in the room.
        public void Tick()
        {
            if (!_subscribed)
                return;

            ShowHead(announce: true);

            // The endgame card slid out while the album was open; call it out once the room is back.
            if (_finalCardHintPending && !_worldLock.IsLocked)
            {
                _hudHints?.Raise(HintId.FinalCardArrived);
                _finalCardHintPending = false;
            }
        }

        public void Dispose()
        {
            _loadCts.Cancel();
            _loadCts.Dispose();

            if (!_subscribed)
                return;

            _collection.Collected -= OnLetterCollected;
            if (_skills != null) _skills.Activated -= OnSkillActivated;
            if (_progress != null) _progress.SetCompleted -= OnSetCompleted;
            if (_stats != null) _stats.Changed -= OnStatsChanged;
            if (_hand != null) _hand.PickedUp -= OnCardPickedUp;
        }

        private async UniTaskVoid WhenLoaded(CancellationToken token)
        {
            bool canceled = await UniTask
                .WaitUntil(() => _saveService?.CurrentSave != null, cancellationToken: token)
                .SuppressCancellationThrow();

            if (canceled)
                return;

            // Establish the baseline silently: note the endgame card's released state, catch up any
            // state-based milestone this save already passed, and show whatever letter was waiting
            // when the player last quit - all with no "arrived" call.
            SyncEpilogueOnLoad();
            EvaluateStateTriggers();
            ShowHead(announce: false);

            // Only now react to live events, so the session-start stats refresh below cannot read as
            // a fresh arrival.
            _collection.Collected += OnLetterCollected;
            if (_skills != null) _skills.Activated += OnSkillActivated;
            if (_progress != null) _progress.SetCompleted += OnSetCompleted;
            if (_stats != null) _stats.Changed += OnStatsChanged;
            if (_hand != null) _hand.PickedUp += OnCardPickedUp;
            _subscribed = true;
        }

        // --- Live sources ---

        private void OnSkillActivated(SkillId id)
        {
            foreach (LetterTrigger trigger in _settings.Triggers)
            {
                if (trigger != null && trigger.Kind == LetterTriggerKind.SkillFirstUse
                    && trigger.Skills != null && trigger.Skills.Contains(id))
                    Enqueue(trigger.Letter);
            }

            ShowHead(announce: true);
        }

        private void OnSetCompleted(string setId)
        {
            foreach (LetterTrigger trigger in _settings.Triggers)
            {
                if (trigger != null && trigger.Kind == LetterTriggerKind.SetCompleted
                    && trigger.SetId == setId)
                    Enqueue(trigger.Letter);
            }

            ShowHead(announce: true);
        }

        private void OnStatsChanged()
        {
            EvaluateStateTriggers();
            ReleaseEpilogueIfAllFiled();
            ShowHead(announce: true);
        }

        private void OnCardPickedUp(Card card)
        {
            if (card == null || card.Identity == null || string.IsNullOrEmpty(_settings.EpilogueSetId))
                return;

            if (card.Identity.SetId == _settings.EpilogueSetId)
            {
                Enqueue(_settings.CertificateLetter);
                ShowHead(announce: true);
            }
        }

        private void OnLetterCollected(LetterId id)
        {
            List<string> pending = Pending;
            if (pending == null || pending.Count == 0)
                return;

            // Reading the shown letter (the head) advances the queue to the next.
            if (pending[0] == id.ToString())
            {
                pending.RemoveAt(0);
                _shownHead = null;
                _saveCoordinator.MarkDirty();
                ShowHead(announce: true);
            }
        }

        // --- Triggers -> queue ---

        private void EvaluateStateTriggers()
        {
            foreach (LetterTrigger trigger in _settings.Triggers)
            {
                if (trigger == null)
                    continue;

                switch (trigger.Kind)
                {
                    case LetterTriggerKind.CorrectlyPlacedReached:
                        if (trigger.CorrectlyPlacedThreshold > 0
                            && _stats.CorrectlyPlacedCards >= trigger.CorrectlyPlacedThreshold)
                            Enqueue(trigger.Letter);
                        break;

                    case LetterTriggerKind.SetCompleted:
                        if (_progress != null && !string.IsNullOrEmpty(trigger.SetId)
                            && _progress.IsSetCompleted(trigger.SetId))
                            Enqueue(trigger.Letter);
                        break;
                }
            }
        }

        private void Enqueue(LetterId id)
        {
            List<string> pending = Pending;
            if (pending == null)
                return;

            // Already waiting, or already read - do not queue it again.
            string key = id.ToString();
            if (pending.Contains(key) || _collection.IsCollected(id))
                return;

            pending.Add(key);
            _saveCoordinator.MarkDirty();
        }

        // --- Showing the head ---

        private void ShowHead(bool announce)
        {
            List<string> pending = Pending;
            if (pending == null || pending.Count == 0)
            {
                _shownHead = null;
                return;
            }

            if (!Enum.TryParse(pending[0], out LetterId head))
            {
                Debug.LogWarning($"[{nameof(LetterAppearanceService)}] Pending letter '{pending[0]}' " +
                                 "is not a known LetterId; leaving it be.");
                return;
            }

            if (_shownHead.HasValue && _shownHead.Value == head)
                return;

            // An announced (play-time) arrival waits until the room is the player's again - it must
            // not slide in or call out over the album, a panel, or the letter being read. A silent
            // restore on load shows straight away (nothing is open then). Tick re-runs this so a
            // deferred arrival appears the moment the room is handed back.
            if (announce && _worldLock.IsLocked)
                return;

            Letter letter = FindLetter(head);
            if (letter == null)
            {
                Debug.LogWarning($"[{nameof(LetterAppearanceService)}] No Letter '{head}' in the scene " +
                                 "to bring in; check it is placed (inactive) with a matching id.");
                return;
            }

            _shownHead = head;

            if (!letter.gameObject.activeSelf)
                letter.gameObject.SetActive(true);

            if (announce)
                _hudHints?.Raise(HintId.NewLetterArrived);
        }

        // --- Endgame card ---

        private void ReleaseEpilogueIfAllFiled()
        {
            GameSave save = _saveService.CurrentSave;
            if (save == null || save.EpilogueCardReleased)
                return;

            // TotalCards already excludes the endgame set (flagged out of the collection), so "every
            // counted card filed" is a plain equality - guarded against an empty/unloaded catalog.
            if (_stats.TotalCards <= 0 || _stats.CorrectlyPlacedCards < _stats.TotalCards)
                return;

            FindEpilogueCard()?.Release();

            save.EpilogueCardReleased = true;
            _saveCoordinator.MarkDirty();

            // Announced after the room is handed back (see Tick), the same as a letter arrival.
            _finalCardHintPending = true;
        }

        private void SyncEpilogueOnLoad()
        {
            GameSave save = _saveService.CurrentSave;
            if (save == null || !save.EpilogueCardReleased)
                return;

            // Already released in a past session; the world save restored the card where it ended up,
            // so mark the cue spent rather than sliding it again.
            FindEpilogueCard()?.MarkReleasedSilently();
        }

        // --- Scene lookups ---

        private Letter FindLetter(LetterId id)
        {
            if (_lettersById == null)
            {
                _lettersById = new Dictionary<LetterId, Letter>();
                foreach (Letter letter in UnityEngine.Object.FindObjectsByType<Letter>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (letter != null)
                        _lettersById[letter.Id] = letter;
                }
            }

            return _lettersById.TryGetValue(id, out Letter found) ? found : null;
        }

        private static EpilogueCard FindEpilogueCard()
        {
            EpilogueCard[] found = UnityEngine.Object.FindObjectsByType<EpilogueCard>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            return found.Length > 0 ? found[0] : null;
        }

        private List<string> Pending
        {
            get
            {
                GameSave save = _saveService.CurrentSave;
                if (save == null)
                    return null;

                return save.PendingLetters ??= new List<string>();
            }
        }
    }
}
