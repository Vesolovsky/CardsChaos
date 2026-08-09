using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UniRx;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Views.Album;
using Zenject;

namespace Vesolovsky.Game.Views
{
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

        private readonly ReactiveProperty<bool> _isOpen = new ReactiveProperty<bool>(false);

        private IDisposable _worldHandle;

        public event Action<string> AlbumChanged;

        public IReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

        // Drawn from the order service rather than straight off the catalog, so the album lists its
        // sets shuffled by default and A-to-Z once Alphabetical Sets is claimed. The order is read
        // afresh each time the buttons are built. Falls back to the plain catalog order while the
        // upgrade system is not yet wired in.
        public IReadOnlyList<CardSetDefinition> Sets =>
            _setOrder != null ? _setOrder.GetOrderedSets() : _catalog.Sets;

        public CardHand Hand => _hand;

        public ICardAlbum Album => _album;

        public CardArtworkResolver Artwork { get; }

        [Inject]
        public CardAlbumViewModel(
            ICardCatalog catalog,
            ICardAlbum album,
            CardHand hand,
            ICardFactory cardFactory,
            IWorldInteractionLock worldLock,
            [InjectOptional] IAlbumSetOrder setOrder)
        {
            _catalog = catalog;
            _album = album;
            _hand = hand;
            _cardFactory = cardFactory;
            _worldLock = worldLock;
            _setOrder = setOrder;

            Artwork = new CardArtworkResolver(catalog);
            _album.PageChanged += OnAlbumPageChanged;
        }

        public void Open()
        {
            if (_isOpen.Value)
                return;

            // Do not open on top of something that already holds the room - a card close-up, the
            // upgrades screen, the pause menu. Matches the upgrades screen's own guard.
            if (_worldLock.IsLocked)
                return;

            // Taken before the view is told, so the frame the album appears on is already a frame
            // the room is not listening in.
            _worldHandle = _worldLock.Acquire(this);
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

        public bool TryFile(IAlbumCardSource source, AlbumCardSlot slot)
        {
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
            // Once the collection is complete the album is sealed - the game is ending, so no card
            // is ever lifted back out.
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
            if (worldCard != null)
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
