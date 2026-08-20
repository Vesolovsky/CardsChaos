using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Stats;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Vesolovsky.Game.Views.Album;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// How the album was asked for. Left off entirely - which is what the gameplay scene does -
    /// the album is the full thing the player files cards into.
    /// </summary>
    public sealed class CardAlbumViewModelInitData : IViewModelInitData
    {
        /// <summary>
        /// The album as a display case rather than a workspace: every card that has been filed is
        /// there to look at and turn over, and nothing can be moved. The main menu opens it this
        /// way, where there is no room, no hand and nowhere for a card taken out to go.
        /// </summary>
        public bool ReadOnly { get; set; }
    }

    /// <summary>
    /// The album's side of every move the player can make in it, and the one place that knows
    /// what filing a card actually costs.
    ///
    /// A card exists in exactly one of two places: the player's hand, as a real object out in the
    /// room, or the album, as a name in the save. Moving between them is a handover, not a copy -
    /// filing destroys the object, taking one back out builds a new one from the same prefab. The
    /// alternative, parking the object somewhere invisible, means a card that is in the album and
    /// in the room at once, and every bug that follows from that is a duplicate card.
    /// </summary>
    public class CardAlbumViewModel : ViewModel, ICardAlbumViewModel
    {
        private readonly ICardCatalog _catalog;
        private readonly ICardAlbum _album;
        private readonly CardHand _hand;
        private readonly ICardFactory _cardFactory;
        private readonly IWorldInteractionLock _worldLock;
        private readonly IAlbumSetOrder _setOrder;
        private readonly IPlayerStats _stats;
        private readonly IUpgradeService _upgrades;
        private readonly PermanentUpgradeDefinition _setSense;

        private readonly ReactiveProperty<bool> _isOpen = new ReactiveProperty<bool>(false);

        private IDisposable _worldHandle;
        private bool _readOnlyRequested;

        public event Action<string> AlbumChanged;

        public IReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

        /// <summary>
        /// Whether the album may be changed at all.
        ///
        /// True either because it was asked for that way (the main menu) or because the pieces a
        /// move needs are simply not in this scene: filing a card destroys an object in the room
        /// and taking one back out builds a new one, so without a hand and a card factory there is
        /// no honest way to do either. Read as a belt-and-braces check rather than trusting the
        /// flag alone, because the failure it prevents is a null reference mid-drag.
        /// </summary>
        public bool IsReadOnly => _readOnlyRequested || _hand == null || _cardFactory == null;

        /// <summary>
        /// The final card as it sits in the endgame set's one slot, or <see cref="CardRef.None"/>
        /// while it is still out in the world. Filed, it means the game has been finished.
        /// </summary>
        public CardRef EndgameCard
        {
            get
            {
                CardSetDefinition endgame = EndgameSet;
                return endgame == null ? CardRef.None : _album.GetPlacement(endgame.SetId, 0);
            }
        }

        public bool IsEndgameCardFiled => EndgameCard.IsValid;

        // Drawn from the order service rather than straight off the catalog, so the album lists its
        // sets shuffled by default and A-to-Z once Alphabetical Sets is claimed. The order is read
        // afresh each time the buttons are built. Falls back to the plain catalog order while the
        // upgrade system is not yet wired in.
        public IReadOnlyList<CardSetDefinition> Sets =>
            _setOrder != null ? _setOrder.GetOrderedSets() : _catalog.Sets;

        // The one set flagged out of the collection - the endgame set. Found by the flag rather than
        // by id so nothing here has to be told which set it is.
        public CardSetDefinition EndgameSet
        {
            get
            {
                foreach (CardSetDefinition set in _catalog.Sets)
                {
                    if (set != null && !set.CountsTowardCollection && set.CardCount > 0)
                        return set;
                }

                return null;
            }
        }

        public bool HoldsEndgameCard
        {
            get
            {
                CardSetDefinition endgame = EndgameSet;
                if (endgame == null || _hand == null)
                    return false;

                foreach (Card card in _hand.Cards)
                {
                    if (card != null && card.Identity != null && card.Identity.SetId == endgame.SetId)
                        return true;
                }

                return false;
            }
        }

        public CardHand Hand => _hand;

        public ICardAlbum Album => _album;

        public CardArtworkResolver Artwork { get; }

        // The room's half of the album - the hand a card is filed from, the factory that rebuilds
        // one taken back out, the lock that holds the room still - is optional so the same view
        // model can serve the menu's read-only album, where none of it exists. In the gameplay
        // scene all three are bound and the album behaves exactly as it always has.
        [Inject]
        public CardAlbumViewModel(
            ICardCatalog catalog,
            ICardAlbum album,
            [InjectOptional] CardHand hand,
            [InjectOptional] ICardFactory cardFactory,
            [InjectOptional] IWorldInteractionLock worldLock,
            [InjectOptional] IAlbumSetOrder setOrder,
            [InjectOptional] IPlayerStats stats,
            [InjectOptional] IUpgradeService upgrades,
            [InjectOptional] UpgradeCatalog upgradeCatalog)
        {
            _catalog = catalog;
            _album = album;
            _hand = hand;
            _cardFactory = cardFactory;
            _worldLock = worldLock;
            _setOrder = setOrder;
            _stats = stats;
            _upgrades = upgrades;

            // Optional the same way the set order is: the menu's display-case album is built in a
            // context with no upgrade system, and there it simply never pulses.
            _setSense = upgradeCatalog != null
                ? upgradeCatalog.FindPermanent(PermanentUpgradeKind.HandSetSense)
                : null;

            Artwork = new CardArtworkResolver(catalog);
            _album.PageChanged += OnAlbumPageChanged;
        }

        public override UniTask Initialize(IViewModelInitData viewModelInitData)
        {
            if (viewModelInitData is CardAlbumViewModelInitData initData)
                _readOnlyRequested = initData.ReadOnly;

            return base.Initialize(viewModelInitData);
        }

        public void Open()
        {
            if (_isOpen.Value)
                return;

            // No room to take in the menu, so nothing to ask for and nothing to hold.
            if (_worldLock != null)
            {
                // Do not open on top of something that already holds the room - a card close-up,
                // the upgrades screen, the pause menu. Matches the upgrades screen's own guard.
                if (_worldLock.IsLocked)
                    return;

                // Taken before the view is told, so the frame the album appears on is already a
                // frame the room is not listening in.
                _worldHandle = _worldLock.Acquire(this);
            }

            _isOpen.Value = true;
        }

        public void Close()
        {
            if (!_isOpen.Value)
                return;

            _isOpen.Value = false;
            ReleaseWorld();
        }

        public int CountFiled(string setId) => _album.CountCorrect(setId);

        /// <summary>
        /// Whether a set's button should breathe - "Set sense", bought once, marking the sets the
        /// player is carrying a card from. Any card from the set counts, filed twin or not: the
        /// point is to find where what is in hand belongs, not to grade it.
        /// </summary>
        public bool ShouldPulseSet(string setId)
        {
            if (_hand == null || string.IsNullOrEmpty(setId) || !HasSetSense)
                return false;

            foreach (Card card in _hand.Cards)
            {
                if (card != null && card.Identity != null && card.Identity.SetId == setId)
                    return true;
            }

            return false;
        }

        // Read live rather than cached: the upgrade can be bought while the album is closed, and
        // the answer is one dictionary lookup behind a definition found once at construction.
        private bool HasSetSense =>
            _upgrades != null && _setSense != null && _upgrades.GetLevel(_setSense) >= 1;

        public bool TryFile(IAlbumCardSource source, AlbumCardSlot slot)
        {
            // A display-case album refuses every move. The drag controller is switched off in that
            // mode as well, so this is the second lock on the same door rather than the only one.
            if (IsReadOnly)
                return false;

            if (slot == null || !slot.IsUsable)
                return false;

            // The slot's own picture and the album are kept in step, but the album is the one
            // that decides - a slot that has fallen behind must not be talked into a second card.
            if (_album.GetPlacement(slot.PageSetId, slot.SlotIndex).IsValid)
                return false;

            switch (source)
            {
                case AlbumHandCard handCard:
                    return FileFromHand(handCard, slot);

                case AlbumCardSlot origin:
                    return MoveBetweenSlots(origin, slot);

                default:
                    Debug.LogError(
                        $"[{nameof(CardAlbumViewModel)}] Nothing knows how to file a card from " +
                        $"'{source?.GetType().Name ?? "null"}'.");

                    return false;
            }
        }

        public bool TryReturnToHand(AlbumCardSlot slot)
        {
            if (IsReadOnly)
                return false;

            // Once originals and duplicate boxes are both complete the album is sealed - the game
            // is ending, so no card is ever lifted back out.
            if (IsCollectionComplete())
                return false;

            if (slot == null || slot.IsEmpty)
                return false;

            // The hand is the constraint, and it is a real one: ten cards is ten cards, and the
            // album is not allowed to hand over an eleventh. The card stays filed.
            if (!_hand.HasRoom)
                return false;

            CardRef card = slot.Card;
            Card prefab = Artwork.ResolvePrefab(card);

            if (prefab == null)
                return false;

            Card spawned = _cardFactory.Create(
                prefab, _hand.transform.position, _hand.transform.rotation);

            if (spawned == null)
                return false;

            if (!_hand.PickUp(spawned))
            {
                // Only reachable if the hand filled up between the check above and here. Cleaned
                // up rather than left on the floor: the player asked for it in their hand, and a
                // card quietly appearing at their feet is not that.
                UnityEngine.Object.Destroy(spawned.gameObject);
                return false;
            }

            _album.Take(slot.PageSetId, slot.SlotIndex);
            return true;
        }

        public void PromoteToTop(Card worldCard)
        {
            if (worldCard != null && _hand != null)
                _hand.BringToTop(worldCard);
        }

        public override void Dispose()
        {
            _album.PageChanged -= OnAlbumPageChanged;
            ReleaseWorld();

            base.Dispose();
        }

        private bool FileFromHand(AlbumHandCard handCard, AlbumCardSlot slot)
        {
            Card worldCard = handCard.WorldCard;
            CardRef card = handCard.Card;

            if (worldCard == null || !card.IsValid)
                return false;

            // Out of the hand first. If this fails the card was never really held and nothing
            // has happened yet, which is the only ordering that cannot lose a card.
            if (!_hand.TryRemove(worldCard))
                return false;

            _album.Place(slot.PageSetId, slot.SlotIndex, card);
            UnityEngine.Object.Destroy(worldCard.gameObject);

            return true;
        }

        private bool MoveBetweenSlots(AlbumCardSlot origin, AlbumCardSlot slot)
        {
            if (origin == slot)
                return false;

            CardRef card = _album.Take(origin.PageSetId, origin.SlotIndex);
            if (!card.IsValid)
                return false;

            _album.Place(slot.PageSetId, slot.SlotIndex, card);
            return true;
        }

        // Every counting card sitting in its slot. Read straight off the album and catalog rather
        // than the stats snapshot, so the seal takes effect the instant the last card lands.
        private bool IsCollectionComplete()
        {
            // The stats snapshot includes both destinations and updates on album/container changes.
            // Keep the old album-only calculation as a fallback for isolated UI test scenes where
            // the gameplay stats service is intentionally absent.
            if (_stats != null && _stats.TotalCards > 0)
                return _stats.CorrectlyPlacedCards >= _stats.TotalCards;

            int total = 0;
            int correct = 0;

            foreach (CardSetDefinition set in _catalog.Sets)
            {
                if (set == null || !set.CountsTowardCollection)
                    continue;

                total += set.CardCount;
                correct += _album.CountCorrect(set.SetId);
            }

            return total > 0 && correct >= total;
        }

        private void OnAlbumPageChanged(string setId) => AlbumChanged?.Invoke(setId);

        private void ReleaseWorld()
        {
            _worldHandle?.Dispose();
            _worldHandle = null;
        }
    }
}
