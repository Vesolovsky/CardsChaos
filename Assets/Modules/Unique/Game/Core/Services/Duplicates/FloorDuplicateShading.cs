using System;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Duplicates
{
    /// <summary>
    /// Greys the cards lying in the room whose place in the album is already taken - the "They
    /// sense more..." reward, and the floor's half of what Déjà vu does in the hand.
    ///
    /// The rule is deliberately the plainest one that is always true: a card whose album slot is
    /// filled is a card the collection has no further use for, so it can go in the box. That is a
    /// fact about the card face, not about which copy of it this is, so the grey never has to pick
    /// between two identical cards on the floor and never wanders from one to the other. A card
    /// still needed for the album stays in colour even when its twin is lying beside it, which is
    /// right: at that point the player needs one of them.
    ///
    /// Nothing is polled. The answer only moves when the album gains or loses a card, when a card
    /// joins or leaves the room, when one is picked up, boxed or thrown, or when the reward is
    /// claimed - so each of those marks the pass dirty and the next frame re-states the answer for
    /// every card. The pass itself is a dictionary lookup per card over a list in the low
    /// thousands, and it re-asserts rather than tracks, so there is no second copy of the state
    /// here to drift out of step with the album.
    /// </summary>
    public class FloorDuplicateShading : IInitializable, IDisposable, ITickable
    {
        private readonly ICardAlbum _album;
        private readonly CardHand _hand;
        private readonly IUpgradeService _upgrades;
        private readonly OneTimeUpgradeDefinition _reward;

        // Cached so walking the room does not allocate a delegate per pass.
        private readonly Action<CardRef, Card> _applyToCard;

        private bool _dirty = true;

        // Read once at the top of a pass rather than per card: it is the same answer for all of
        // them, and the pass runs over the room's whole card list.
        private bool _ownedThisPass;

        // Whether the last pass left anything grey. With the reward unclaimed and nothing greyed,
        // there is provably nothing to say, and the walk is skipped outright - which is most of the
        // game, since the reward is late and the hand raises a dirty flag constantly.
        private bool _anyShaded;

        [Inject]
        public FloorDuplicateShading(
            ICardAlbum album,
            CardHand hand,
            UpgradeCatalog catalog,
            IUpgradeService upgrades)
        {
            _album = album;
            _hand = hand;
            _upgrades = upgrades;
            _reward = catalog != null
                ? catalog.FindOneTime(OneTimeUpgradeKind.FloorDuplicateSight)
                : null;

            _applyToCard = ApplyToCard;
        }

        public void Initialize()
        {
            if (_album != null)
                _album.PageChanged += OnPageChanged;

            if (_hand != null)
                _hand.Changed += MarkDirty;

            if (_upgrades != null)
                _upgrades.Changed += OnUpgradeChanged;

            CardRegistry.Changed += MarkDirty;
            CardStackContainer.ContentsChanged += MarkDirty;

            MarkDirty();
        }

        public void Dispose()
        {
            if (_album != null)
                _album.PageChanged -= OnPageChanged;

            if (_hand != null)
                _hand.Changed -= MarkDirty;

            if (_upgrades != null)
                _upgrades.Changed -= OnUpgradeChanged;

            // Both are static events, so leaving them subscribed would outlive the scene - and with
            // domain reload off, the play session too.
            CardRegistry.Changed -= MarkDirty;
            CardStackContainer.ContentsChanged -= MarkDirty;
        }

        public void Tick()
        {
            if (!_dirty)
                return;

            _dirty = false;
            Apply();
        }

        /// <summary>
        /// Coalesces every reason the answer might have moved into one pass on the next frame:
        /// filing a card raises the album's event and the registry's within the same frame, and
        /// picking up a handful of cards raises the hand's once per card.
        /// </summary>
        private void MarkDirty() => _dirty = true;

        private void OnPageChanged(string setId) => MarkDirty();

        private void OnUpgradeChanged(UpgradeDefinition definition) => MarkDirty();

        private void Apply()
        {
            _ownedThisPass = _upgrades != null && _upgrades.IsUnlocked(_reward);

            // Nothing to turn on and nothing left on from before: the answer is already right for
            // every card in the room, so there is no reason to visit them.
            if (!_ownedThisPass && !_anyShaded)
                return;

            _anyShaded = false;
            CardRegistry.ForEach(_applyToCard);
        }

        private void ApplyToCard(CardRef key, Card card)
        {
            // Held cards are the in-hand wash's business, and a card already in a box is put away -
            // greying the box's contents would say nothing the box does not already say.
            bool grey = _ownedThisPass
                        && _album != null
                        && _album.Contains(key)
                        && !card.IsHeld
                        && !CardStackContainer.IsStored(card);

            card.SetFloorShaded(grey);

            if (grey)
                _anyShaded = true;
        }
    }
}
