using System.Collections.Generic;
using CardsChaos.Cards;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Raises the selected card's set-mates off the floor to hover, turned to the camera, for a
    /// spell before they fall - unless the player plucks them out of the air first.
    ///
    /// Finding the cards and deciding what still counts is the targeting service's job; this only
    /// starts a <see cref="LevitatingCard"/> on each, which then runs its own float and fall. With
    /// nothing in reach it declines, so - like the magnet with nothing to pull - the skill charges
    /// no cooldown for a cast that raised nothing.
    ///
    /// It is unlocked by the "Is this magic?..." task rather than bought, and has a single level;
    /// the skill service reads its unlock off that task (see the skill definition's UnlockedBy).
    /// </summary>
    public class LevitateSkill : ISkillHandler
    {
        private readonly CardHand _hand;
        private readonly ICameraService _cameraService;
        private readonly IWorldInteractionLock _worldLock;
        private readonly ILevitateTargeting _targeting;
        private readonly LevitateSettings _settings;

        [Inject]
        public LevitateSkill(
            CardHand hand,
            ICameraService cameraService,
            IWorldInteractionLock worldLock,
            ILevitateTargeting targeting,
            LevitateSettings settings)
        {
            _hand = hand;
            _cameraService = cameraService;
            _worldLock = worldLock;
            _targeting = targeting;
            _settings = settings;
        }

        public SkillId Id => SkillId.Levitate;

        public bool CanActivate()
        {
            // Not while the album or a close-up holds the room, and only with a card to read a set
            // from. Whether anything is actually near enough is left to Activate, so an empty cast
            // simply raises nothing and costs no cooldown.
            return !_worldLock.IsLocked && _hand.SelectedCard != null;
        }

        public bool Activate(SkillDefinition definition, int level)
        {
            List<Card> targets = _targeting.FindTargets();
            if (targets.Count == 0)
                return false;

            Camera camera = _cameraService.MainCamera;

            // One shared hover line for the whole cast, measured off the lowest card in reach, so
            // every raised card floats level with the rest whatever height it began at - a card off
            // the top of a house of cards eases down to the same line a floor card lifts up to.
            float hoverHeight = LowestRestHeight(targets) + _settings.RiseHeight;

            int raised = 0;
            foreach (Card card in targets)
            {
                if (card == null || card.IsHeld)
                    continue;

                // The targeting already skips cards mid-float, but guard once more so a double-add
                // can never stack two drivers on one card.
                if (card.GetComponent<LevitatingCard>() != null)
                    continue;

                LevitatingCard driver = card.gameObject.AddComponent<LevitatingCard>();
                driver.Begin(card, camera, _settings, hoverHeight);
                raised++;
            }

            return raised > 0;
        }

        /// <summary>
        /// The world height of the lowest card among <paramref name="targets"/> - the floor the whole
        /// cast's shared hover line is built on. Nulls are ignored; an all-null list falls back to 0.
        /// </summary>
        private static float LowestRestHeight(List<Card> targets)
        {
            float lowest = float.PositiveInfinity;
            foreach (Card card in targets)
            {
                if (card == null)
                    continue;

                float y = card.transform.position.y;
                if (y < lowest)
                    lowest = y;
            }

            return float.IsPositiveInfinity(lowest) ? 0f : lowest;
        }
    }
}
