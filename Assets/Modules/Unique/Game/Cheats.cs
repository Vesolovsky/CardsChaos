using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;
// F11's bulk-claim complements the per-task "Debug Force Unlock" button on each task row.

namespace Vesolovsky.Game
{
    /// <summary>
    /// Temporary testing helper. Drop it on any empty object in the scene; pull it back out before
    /// shipping.
    ///
    /// F12 drops 100 skill points into the wallet; F11 claims every one-time reward; Ctrl+F1 clears
    /// every skill's running cooldown. For the collection/letter systems, each fill key tops the
    /// album up to a milestone-minus-one so the next card is yours to file and watch the arrival:
    /// F9 = 299, F8 = 599, F7 = 999, F6 = 1199. F5 files every card of the Unique Wands set but the
    /// last, so completing it is your move. F4 completes Unique Wands AND fills to 300 at once, to
    /// see two arrivals queue one after another. F3 files every card but one, so filing that last one
    /// finishes the collection and slides the endgame card out. Filing through the cheat also removes
    /// the card from the floor - the same as filing it by hand - and raises the same album event, so
    /// the triggers fire exactly as in play.
    /// </summary>
    [AddComponentMenu("CardsChaos/Debug/Cheats")]
    public class Cheats : MonoBehaviour
    {
        private const int SkillPointsPerPress = 100;

        [Header("Collection cheats")]
        [Tooltip("The Unique Wands set's folder id, used by F5 and F4.")]
        [SerializeField] private string uniqueWandsSetId = "UniqueWands";

        private IWalletService _wallet;
        private IUpgradeService _upgrades;
        private UpgradeCatalog _catalog;
        private ISkillService _skills;
        private ICardAlbum _album;
        private ICardCatalog _cardCatalog;
        private CardHand _hand;

        [Inject]
        public void Construct(
            IWalletService wallet,
            [InjectOptional] IUpgradeService upgrades,
            [InjectOptional] UpgradeCatalog catalog,
            [InjectOptional] ISkillService skills,
            [InjectOptional] ICardAlbum album,
            [InjectOptional] ICardCatalog cardCatalog,
            [InjectOptional] CardHand hand)
        {
            _wallet = wallet;
            _upgrades = upgrades;
            _catalog = catalog;
            _skills = skills;
            _album = album;
            _cardCatalog = cardCatalog;
            _hand = hand;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f12Key.wasPressedThisFrame && _wallet != null)
                _wallet.AddRealCurrency(CurrencyType.SkillPoints, SkillPointsPerPress);

            if (keyboard.f11Key.wasPressedThisFrame && _upgrades != null && _catalog != null)
            {
                foreach (OneTimeUpgradeDefinition oneTime in _catalog.OneTimes)
                    _upgrades.DebugForceUnlock(oneTime);
            }

            bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (keyboard.f1Key.wasPressedThisFrame && ctrlHeld && _skills != null)
                _skills.DebugResetCooldowns();

            if (keyboard.f9Key.wasPressedThisFrame)
                FillCorrectTo(299);

            if (keyboard.f8Key.wasPressedThisFrame)
                FillCorrectTo(599);

            if (keyboard.f7Key.wasPressedThisFrame)
                FillCorrectTo(999);

            if (keyboard.f6Key.wasPressedThisFrame)
                FillCorrectTo(1199);

            if (keyboard.f5Key.wasPressedThisFrame)
                FillSetExceptLast(uniqueWandsSetId);

            if (keyboard.f4Key.wasPressedThisFrame)
                CompleteSetAndFillTo(uniqueWandsSetId, 300);

            if (keyboard.f3Key.wasPressedThisFrame)
                FillAllExceptLast();
        }

        // Files correct cards, set by set, until the collection holds this many - so the milestone
        // triggers fire exactly as in real play. Only tops up: a target below the current count does
        // nothing. If the counting sets hold fewer than the target, it fills them all (which trips
        // the endgame card).
        private void FillCorrectTo(int target)
        {
            if (!Ready())
                return;

            Dictionary<CardRef, Card> sceneCards = BuildSceneCards();
            int total = CountingCorrectTotal();

            foreach (CardSetDefinition set in _cardCatalog.Sets)
            {
                if (set == null || !set.CountsTowardCollection)
                    continue;

                for (int slot = 0; slot < set.CardCount; slot++)
                {
                    if (total >= target)
                    {
                        Debug.Log($"[Cheats] Album filled to {total} correct card(s).");
                        return;
                    }

                    if (FileCorrect(set.SetId, slot, sceneCards))
                        total++;
                }
            }

            Debug.Log($"[Cheats] Album filled to {total} correct card(s) (all counting cards).");
        }

        // Fills every slot of a set but the last, and leaves that last slot (and its floor card)
        // untouched - so filing it, and completing the set, is the player's own move.
        private void FillSetExceptLast(string setId)
        {
            if (!Ready() || string.IsNullOrEmpty(setId))
                return;

            CardSetDefinition set = _cardCatalog.FindSet(setId);
            if (set == null || set.CardCount <= 0)
            {
                Debug.LogWarning($"[Cheats] No set '{setId}' (or it has no cards) to fill.");
                return;
            }

            Dictionary<CardRef, Card> sceneCards = BuildSceneCards();
            int last = set.CardCount - 1;

            for (int slot = 0; slot < last; slot++)
                FileCorrect(setId, slot, sceneCards);

            // Make sure the last slot is empty; leave its floor card be so the player can file it.
            if (_album.GetPlacement(setId, last).IsValid)
                _album.Take(setId, last);

            Debug.Log($"[Cheats] '{setId}' filled except its last card (#{last + 1}) - file it to " +
                      "complete the set.");
        }

        // Files every counting card but one - the last slot of the last counting set - and leaves
        // that card on the floor. Filing it yourself takes the collection to complete, which slides
        // the endgame card out: the way to test the final sequence.
        private void FillAllExceptLast()
        {
            if (!Ready())
                return;

            CardSetDefinition lastSet = null;
            foreach (CardSetDefinition set in _cardCatalog.Sets)
            {
                if (set != null && set.CountsTowardCollection && set.CardCount > 0)
                    lastSet = set;
            }

            if (lastSet == null)
            {
                Debug.LogWarning("[Cheats] No counting set with cards to fill.");
                return;
            }

            Dictionary<CardRef, Card> sceneCards = BuildSceneCards();
            int lastSlot = lastSet.CardCount - 1;

            foreach (CardSetDefinition set in _cardCatalog.Sets)
            {
                if (set == null || !set.CountsTowardCollection)
                    continue;

                for (int slot = 0; slot < set.CardCount; slot++)
                {
                    if (set == lastSet && slot == lastSlot)
                    {
                        // The one card left for the player: keep its slot empty and its floor card.
                        if (_album.GetPlacement(set.SetId, slot).IsValid)
                            _album.Take(set.SetId, slot);

                        continue;
                    }

                    FileCorrect(set.SetId, slot, sceneCards);
                }
            }

            Debug.Log($"[Cheats] Filled every counting card except {lastSet.SetId} #{lastSlot + 1} - " +
                      "file it to finish the collection and trigger the endgame card.");
        }

        // Completes a set AND tops the whole album up to a count, in one press - so a set-completion
        // arrival and a card-count arrival queue together, to watch them show one after another.
        // (Run on a fresh save: a set completed in a past session will not re-announce.)
        private void CompleteSetAndFillTo(string setId, int target)
        {
            if (!Ready() || string.IsNullOrEmpty(setId))
                return;

            Dictionary<CardRef, Card> sceneCards = BuildSceneCards();

            CardSetDefinition set = _cardCatalog.FindSet(setId);
            if (set != null)
            {
                for (int slot = 0; slot < set.CardCount; slot++)
                    FileCorrect(setId, slot, sceneCards);
            }

            int total = CountingCorrectTotal();
            foreach (CardSetDefinition other in _cardCatalog.Sets)
            {
                if (other == null || !other.CountsTowardCollection)
                    continue;

                for (int slot = 0; slot < other.CardCount; slot++)
                {
                    if (total >= target)
                    {
                        Debug.Log($"[Cheats] Completed '{setId}' and filled to {total}.");
                        return;
                    }

                    if (FileCorrect(other.SetId, slot, sceneCards))
                        total++;
                }
            }

            Debug.Log($"[Cheats] Completed '{setId}' and filled to {total}.");
        }

        // Puts the card that belongs in a slot into it and takes its twin off the floor (or out of
        // the hand), the same as filing it by hand. Returns whether this was a fresh correct
        // placement; a slot already correct is left alone.
        private bool FileCorrect(string setId, int slot, Dictionary<CardRef, Card> sceneCards)
        {
            CardRef current = _album.GetPlacement(setId, slot);
            if (current.BelongsAt(setId, slot))
                return false;

            if (current.IsValid)
                _album.Take(setId, slot);

            var placed = new CardRef(setId, slot + 1);
            _album.Place(setId, slot, placed);
            RemovePhysicalCard(placed, sceneCards);
            return true;
        }

        // Destroys the physical card matching a placement, so the cheat does not leave a duplicate
        // lying on the floor. Detaches it from the hand first when it is being held.
        private void RemovePhysicalCard(CardRef card, Dictionary<CardRef, Card> sceneCards)
        {
            if (!sceneCards.TryGetValue(card, out Card instance) || instance == null)
                return;

            if (instance.IsHeld)
                _hand?.TryRemove(instance);

            Destroy(instance.gameObject);
            sceneCards.Remove(card);
        }

        private Dictionary<CardRef, Card> BuildSceneCards()
        {
            var map = new Dictionary<CardRef, Card>();

            foreach (Card card in FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                if (card == null)
                    continue;

                CardRef key = CardRef.From(card.Identity);
                if (key.IsValid)
                    map[key] = card;
            }

            return map;
        }

        private int CountingCorrectTotal()
        {
            int total = 0;

            foreach (CardSetDefinition set in _cardCatalog.Sets)
            {
                if (set != null && set.CountsTowardCollection)
                    total += _album.CountCorrect(set.SetId);
            }

            return total;
        }

        private bool Ready() => _album != null && _cardCatalog != null;
    }
}
