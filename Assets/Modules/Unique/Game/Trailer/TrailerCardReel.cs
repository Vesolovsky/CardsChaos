using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UnityEngine;
using UnityEngine.InputSystem;
using VInspector;
using Zenject;

namespace Vesolovsky.Game.Trailer
{
    /// <summary>
    /// Steps the close-up through a chosen run of cards, one key press per card.
    ///
    /// Recording the close-up off the hand as it happens to be means scrolling past whatever the
    /// player picked up; this loads a <see cref="TrailerCardList"/> into the hand instead, in the
    /// order it was authored, and then walks the close-up along it.
    ///
    /// Loading is deliberately not destructive. Cards already in hand are thrown back onto the
    /// floor rather than deleted, and a card on the list that already exists in the room is taken
    /// from where it lies rather than duplicated - a second copy of the same card would break the
    /// save, which keys every card by set and number. Only a card the room does not have (filed
    /// away in the album, say) is built from its prefab.
    ///
    /// Drop it beside <see cref="TrailerCameraShots"/> in the gameplay scene. Alt+L loads the list,
    /// Alt+Right and Alt+Left walk it (the first press opens the close-up), Alt+Backspace leaves,
    /// and Alt+Insert appends whatever card is in hand to the list while you hunt for good ones.
    /// </summary>
    [AddComponentMenu("CardsChaos/Trailer/Trailer Card Reel")]
    public class TrailerCardReel : MonoBehaviour
    {
        [Tooltip("The run of cards to step through. Create one with " +
                 "Assets > Create > CardsChaos > Trailer > Card List.")]
        [SerializeField] private TrailerCardList list;

        [Header("Hotkeys")]
        [Tooltip("Empties the hand back onto the floor and fills it with the list.")]
        [SerializeField] private TrailerHotkey loadKey = new TrailerHotkey(Key.L);

        [Tooltip("Opens the close-up, then steps forward through the list.")]
        [SerializeField] private TrailerHotkey nextKey = new TrailerHotkey(Key.RightArrow);

        [SerializeField] private TrailerHotkey previousKey = new TrailerHotkey(Key.LeftArrow);

        [SerializeField] private TrailerHotkey closeKey = new TrailerHotkey(Key.Backspace);

        [Tooltip("Appends the card currently selected in hand to the list.")]
        [SerializeField] private TrailerHotkey addSelectedKey = new TrailerHotkey(Key.Insert);

        private CardHand _hand;
        private ICardInspector _inspector;
        private ICardCatalog _catalog;
        private ICardFactory _factory;

        [Inject]
        public void Construct(
            [InjectOptional] CardHand hand,
            [InjectOptional] ICardInspector inspector,
            [InjectOptional] ICardCatalog catalog,
            [InjectOptional] ICardFactory factory)
        {
            _hand = hand;
            _inspector = inspector;
            _catalog = catalog;
            _factory = factory;
        }

        /// <summary>
        /// Puts the list in hand, in order, ready for the close-up to walk. The close-up is shut
        /// first and whatever was being held is thrown back onto the floor, so the hand holds the
        /// list and nothing else.
        /// </summary>
        [Button("Load List Into Hand")]
        public void LoadIntoHand()
        {
            if (!Ready() || list == null || list.Count == 0)
            {
                Warn("nothing to load - assign a card list with cards in it");
                return;
            }

            _inspector.Close();
            EmptyHand();

            Dictionary<CardRef, Card> sceneCards = MapSceneCards();
            var loaded = new List<Card>();

            foreach (Card prefab in list.Cards)
            {
                Card card = Take(prefab, sceneCards);

                if (card != null)
                    loaded.Add(card);
            }

            // Restore rather than PickUp: it is the one path that takes a whole hand at once and
            // ignores the slot limit, which a list longer than the player's hand would otherwise
            // run straight into.
            _hand.Restore(loaded, _hand.Layout);

            Debug.Log($"[{nameof(TrailerCardReel)}] Loaded {loaded.Count} of {list.Count} card(s) " +
                      $"from '{list.name}' into the hand.", this);
        }

        /// <summary>
        /// Opens the close-up if it is shut, and steps to the next card if it is already open - so
        /// one key both starts the take and carries it along.
        /// </summary>
        [Button("Next Card")]
        public void Next() => Step(1);

        [Button("Previous Card")]
        public void Previous() => Step(-1);

        [Button("Close Close-Up")]
        public void Close()
        {
            if (Ready())
                _inspector.Close();
        }

        /// <summary>
        /// Appends the card in hand to the list, so a good-looking card found while playing can be
        /// added to the reel on the spot. The list is an asset, so the addition outlives play mode.
        /// </summary>
        [Button("Add Selected Card To List")]
        public void AddSelectedCard()
        {
            if (!Ready() || list == null)
            {
                Warn("no card list assigned");
                return;
            }

            Card selected = _hand.SelectedCard;
            CardIdentity identity = IdentityOf(selected);

            if (identity == null)
            {
                Warn("no card is selected in hand");
                return;
            }

            Card prefab = FindPrefab(identity.SetId, identity.Number);

            if (prefab == null)
            {
                Warn($"the catalog has no prefab for {identity.SetId} #{identity.Number}");
                return;
            }

            if (!list.Add(prefab))
            {
                Debug.Log($"[{nameof(TrailerCardReel)}] '{prefab.name}' is already on '{list.name}'.",
                    this);

                return;
            }

            Debug.Log($"[{nameof(TrailerCardReel)}] Added '{prefab.name}' to '{list.name}' " +
                      $"({list.Count} card(s)).", list);
        }

        private void Step(int delta)
        {
            if (!Ready())
                return;

            // The first press opens rather than steps: at that point there is nothing to step from,
            // and having to reach for a second key to start a take is a fumble waiting to happen.
            if (!_inspector.IsInspecting)
            {
                if (!_inspector.TryOpen())
                    Warn("the close-up has nothing to open on - load the list into the hand first");

                return;
            }

            _inspector.Step(delta);
        }

        private void Update()
        {
            if (loadKey.WasPressed())
                LoadIntoHand();

            if (nextKey.WasPressed())
                Next();

            if (previousKey.WasPressed())
                Previous();

            if (closeKey.WasPressed())
                Close();

            if (addSelectedKey.WasPressed())
                AddSelectedCard();
        }

        /// <summary>Throws everything in hand back onto the floor, one card at a time.</summary>
        private void EmptyHand()
        {
            // ThrowSelected acts on the selection, so each pass selects the top card and throws
            // that. The count falls by one every time, so this cannot run away.
            while (_hand.Cards.Count > 0)
            {
                _hand.Select(_hand.Cards[0]);
                _hand.ThrowSelected();
            }
        }

        /// <summary>
        /// The room's own copy of a card if it has one, otherwise a fresh one built from the
        /// prefab. Taking the existing card is what keeps set/number unique across the scene, which
        /// the save depends on.
        /// </summary>
        private Card Take(Card prefab, Dictionary<CardRef, Card> sceneCards)
        {
            CardIdentity identity = IdentityOf(prefab);

            if (identity == null)
            {
                Warn("a list entry is empty or has no card identity; skipped");
                return null;
            }

            CardRef key = CardRef.From(identity);

            if (key.IsValid && sceneCards.TryGetValue(key, out Card existing) && existing != null)
            {
                sceneCards.Remove(key);

                // Lifting a card out of a standing house brings the house down, exactly as picking
                // it up by hand would. Better that than a house left standing on a card that has
                // quietly moved into the player's hand.
                existing.House?.OnMemberPickedUp(existing);
                existing.StopAnimation();

                return existing;
            }

            if (_factory == null)
                return null;

            // Only reached for a card the room does not have - one filed into the album, or content
            // added since the scene was authored.
            return _factory.Create(prefab, _hand.transform.position, _hand.transform.rotation);
        }

        private static Dictionary<CardRef, Card> MapSceneCards()
        {
            var map = new Dictionary<CardRef, Card>();

            foreach (Card card in FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                CardIdentity identity = IdentityOf(card);

                if (identity == null)
                    continue;

                CardRef key = CardRef.From(identity);

                if (key.IsValid)
                    map.TryAdd(key, card);
            }

            return map;
        }

        private Card FindPrefab(string setId, int number)
        {
            if (_catalog == null)
                return null;

            foreach (Card prefab in _catalog.Cards)
            {
                CardIdentity identity = IdentityOf(prefab);

                if (identity != null && identity.SetId == setId && identity.Number == number)
                    return prefab;
            }

            return null;
        }

        /// <summary>
        /// A card's identity, asked for in a way that works on a prefab too: the cached property is
        /// filled in Awake, which a prefab asset never runs.
        /// </summary>
        private static CardIdentity IdentityOf(Card card)
        {
            if (card == null)
                return null;

            return card.Identity != null ? card.Identity : card.GetComponent<CardIdentity>();
        }

        private bool Ready()
        {
            if (_hand != null && _inspector != null)
                return true;

            Warn("the card hand and close-up are not available - is this in the gameplay scene, " +
                 "and is the game running?");

            return false;
        }

        private void Warn(string reason)
        {
            Debug.LogWarning($"[{nameof(TrailerCardReel)}] {reason}.", this);
        }
    }
}
