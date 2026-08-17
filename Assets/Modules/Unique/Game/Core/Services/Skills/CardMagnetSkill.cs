using System.Collections.Generic;
using CardsChaos.Cards;
using UnityEngine;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.Services;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Pulls floor cards of the selected card's set into the hand.
    ///
    /// It takes the nearest ones first and never more than the hand has room for - asked for three
    /// with one slot free, it brings one. Picking a card up already flies it in from wherever it
    /// lay, so the pull needs no animation of its own; it just claims the cards and lets the hand
    /// draw them in. The "Helping Hands" task, once claimed, lets it reach for one card more.
    /// </summary>
    public class CardMagnetSkill : ISkillHandler
    {
        private readonly CardHand _hand;
        private readonly ICameraService _cameraService;
        private readonly IWorldInteractionLock _worldLock;
        private readonly IUpgradeService _upgrades;
        private readonly IAudioService _audioService;
        private readonly OneTimeUpgradeDefinition _bonus;

        [Inject]
        public CardMagnetSkill(
            CardHand hand, ICameraService cameraService, IWorldInteractionLock worldLock,
            IUpgradeService upgrades, UpgradeCatalog catalog, IAudioService audioService)
        {
            _hand = hand;
            _cameraService = cameraService;
            _worldLock = worldLock;
            _upgrades = upgrades;
            _audioService = audioService;
            _bonus = catalog.FindOneTime(OneTimeUpgradeKind.CardMagnetBonus);
        }

        public SkillId Id => SkillId.CardMagnet;

        public bool CanActivate()
        {
            // Not while the album (or a close-up) holds the room, and only when there is a card to
            // read a set from and somewhere to put what is pulled.
            return !_worldLock.IsLocked && _hand.SelectedCard != null && _hand.FreeSlots > 0;
        }

        public bool Activate(SkillDefinition definition, int level)
        {
            Card selected = _hand.SelectedCard;
            if (selected == null || selected.Identity == null)
                return false;

            string setId = selected.Identity.SetId;
            int want = Mathf.RoundToInt(definition.GetValue(level)) + BonusCards();
            int pull = Mathf.Min(want, _hand.FreeSlots);
            if (pull <= 0)
                return false;

            List<Card> nearest = FindNearest(setId, pull);

            int pulled = 0;
            foreach (Card card in nearest)
            {
                if (_hand.PickUp(card))
                    pulled++;
            }

            if (pulled > 0)
                _audioService?.Play(AudioSFXKey.SkillCardMagnet);

            return pulled > 0;
        }

        /// <summary>
        /// Extra cards the "Helping Hands" reward adds to the pull once claimed - its authored value,
        /// or none while it is not owned. Read live so claiming it takes effect on the next magnet.
        /// </summary>
        private int BonusCards()
        {
            if (_bonus == null || !_upgrades.IsUnlocked(_bonus))
                return 0;

            return Mathf.Max(0, Mathf.RoundToInt(_bonus.Value));
        }

        /// <summary>
        /// The <paramref name="count"/> floor cards of the set that are closest to the camera. A
        /// full scene scan, but the skill fires seconds apart at most, so it is far cheaper than
        /// keeping a live registry of every card in step through pickups, throws and filing.
        /// </summary>
        private List<Card> FindNearest(string setId, int count)
        {
            Vector3 eye = _cameraService.MainCamera != null
                ? _cameraService.MainCamera.transform.position
                : Vector3.zero;

            var candidates = new List<Card>();
            foreach (Card card in Object.FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                if (card == null || card.IsHeld || CardStackContainer.IsStored(card) ||
                    card.Identity == null || card.Identity.SetId != setId)
                    continue;

                candidates.Add(card);
            }

            candidates.Sort((a, b) =>
            {
                float da = (a.transform.position - eye).sqrMagnitude;
                float db = (b.transform.position - eye).sqrMagnitude;
                return da.CompareTo(db);
            });

            if (candidates.Count > count)
                candidates.RemoveRange(count, candidates.Count - count);

            return candidates;
        }
    }
}
