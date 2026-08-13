using System.Collections.Generic;
using CardsChaos.Cards;
using UnityEngine;
using Vesolovsky.Core.Services;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Finds the floor cards Levitate can raise: set-mates of the selected card lying near the
    /// player and not already in the air or in hand.
    ///
    /// Both the skill (which raises them) and the HUD pulse (which only asks whether any are there)
    /// read from here, so the two never disagree about what counts as a target. The pulse asks every
    /// frame, so <see cref="HasTargets"/> is cached for a short poll interval rather than scanning
    /// the room each time.
    /// </summary>
    public interface ILevitateTargeting
    {
        /// <summary>
        /// Every raisable set-mate near the player, nearest first and capped by the settings' max.
        /// Empty when nothing selected or nothing qualifies. Allocates a fresh list for the caller.
        /// </summary>
        List<Card> FindTargets();

        /// <summary>
        /// Whether at least one card could be raised right now. Cached for the settings' poll
        /// interval so the pulse can ask it every frame without re-scanning the room.
        /// </summary>
        bool HasTargets();
    }

    public class LevitateTargeting : ILevitateTargeting
    {
        private readonly CardHand _hand;
        private readonly ICameraService _cameraService;
        private readonly LevitateSettings _settings;

        private float _cachedAt = float.NegativeInfinity;
        private bool _cachedHasTargets;

        [Inject]
        public LevitateTargeting(CardHand hand, ICameraService cameraService, LevitateSettings settings)
        {
            _hand = hand;
            _cameraService = cameraService;
            _settings = settings;
        }

        public List<Card> FindTargets()
        {
            var targets = new List<Card>();

            if (!TryGetContext(out string setId, out Vector3 eye))
                return targets;

            float radiusSqr = _settings.Radius * _settings.Radius;

            foreach (Card card in Object.FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                if (!IsEligible(card, setId, eye, radiusSqr))
                    continue;

                targets.Add(card);
            }

            targets.Sort((a, b) =>
                HorizontalSqrDistance(a, eye).CompareTo(HorizontalSqrDistance(b, eye)));

            int max = _settings.MaxCards;
            if (max > 0 && targets.Count > max)
                targets.RemoveRange(max, targets.Count - max);

            return targets;
        }

        public bool HasTargets()
        {
            // Unscaled so the pulse keeps polling at its own rate even while the game clock is
            // stopped for the pause menu; the scan does not care about game time.
            float now = Time.unscaledTime;
            if (now - _cachedAt < _settings.TargetPollInterval)
                return _cachedHasTargets;

            _cachedAt = now;
            _cachedHasTargets = AnyTarget();
            return _cachedHasTargets;
        }

        private bool AnyTarget()
        {
            if (!TryGetContext(out string setId, out Vector3 eye))
                return false;

            float radiusSqr = _settings.Radius * _settings.Radius;

            foreach (Card card in Object.FindObjectsByType<Card>(FindObjectsSortMode.None))
            {
                if (IsEligible(card, setId, eye, radiusSqr))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The set to match and the point to measure from, or false when there is nothing selected
        /// to read a set from.
        /// </summary>
        private bool TryGetContext(out string setId, out Vector3 eye)
        {
            setId = null;
            eye = Vector3.zero;

            Card selected = _hand.SelectedCard;
            if (selected == null || selected.Identity == null)
                return false;

            setId = selected.Identity.SetId;
            eye = _cameraService.MainCamera != null
                ? _cameraService.MainCamera.transform.position
                : Vector3.zero;

            return !string.IsNullOrEmpty(setId);
        }

        private static bool IsEligible(Card card, string setId, Vector3 eye, float radiusSqr)
        {
            if (card == null || card.IsHeld || CardStackContainer.IsStored(card) ||
                card.Identity == null || card.Identity.SetId != setId)
                return false;

            // A card holding up a house of cards is fair game: raising it collapses the house, the
            // same as lifting it by hand would (see LevitatingCard.Begin). So it stays a target.

            // A card already floating is not a fresh target - re-casting must not re-raise it, and
            // the pulse should go quiet once everything nearby is already up.
            if (card.GetComponent<LevitatingCard>() != null)
                return false;

            return HorizontalSqrDistance(card, eye) <= radiusSqr;
        }

        /// <summary>
        /// Distance on the floor plane, so the radius reads as "how far across the room" and does
        /// not shrink or grow with how high the camera happens to sit above the table.
        /// </summary>
        private static float HorizontalSqrDistance(Card card, Vector3 eye)
        {
            Vector3 delta = card.transform.position - eye;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }
    }
}
