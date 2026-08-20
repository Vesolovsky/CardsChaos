using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using Vesolovsky.Core.Audio;
using Vesolovsky.Core.UISystem.Animations;
using Vesolovsky.Core.Utils;
using Zenject;

namespace Vesolovsky.Game.Views.MainMenu
{
    /// <summary>
    /// The menu's way in and out: the cards are dealt onto the table.
    ///
    /// Every card starts off screen, turned further out than it will end up, and flies to its
    /// place in the fan - one after another rather than all at once, which is the whole trick. A
    /// row of cards arriving together reads as a panel sliding in; the same row arriving a beat
    /// apart reads as a deck being spread, and that is what the menu is meant to be.
    ///
    /// It is an <see cref="IViewAnimation"/>, so the view system plays it: the scene shows itself
    /// and the deal runs, and leaving for another scene sweeps the cards back off the same way.
    ///
    /// The whole fan sits behind a CanvasGroup that starts at zero, because the cards are only
    /// laid out once the menu knows whether Continue is part of it - and until that is settled
    /// nothing should be on screen at all.
    /// </summary>
    [AddComponentMenu("CardsChaos/Main Menu/Fan Animation")]
    public class MainMenuFanAnimation : MonoBehaviour, IViewAnimation
    {
        [SerializeField] private MainMenuCardFan fan;

        [Tooltip("CanvasGroup over the fan. Held at zero until the deal starts, so the cards are " +
                 "never seen sitting at their places for the frame before they fly in. It is also " +
                 "what swallows clicks while the cards are in the air.")]
        [SerializeField] private CanvasGroup group;

        [Header("Deal in")]
        [Tooltip("Where a card starts, measured from where it ends up. The default sends them off " +
                 "the bottom-right corner, so they sweep up and across into the fan.")]
        [SerializeField] private Vector2 enterOffset = new Vector2(1400f, -900f);

        [Tooltip("Extra turn a card carries while it is off screen, straightened out as it lands.")]
        [SerializeField] private float enterAngle = -40f;

        [Tooltip("How long one card takes to fly in.")]
        [SerializeField] private float cardDuration = 0.55f;

        [Tooltip("Seconds between one card setting off and the next. This is what makes it a deal " +
                 "rather than a slide - keep it well under the card duration so they overlap.")]
        [SerializeField] private float stagger = 0.07f;

        [SerializeField, SearchableEnum] private Ease enterEase = Ease.OutCubic;

        [Tooltip("Deal the right-hand end of the fan first. They come in from the right, so " +
                 "dealing that end first means each card lands on top of the gap the next one " +
                 "will fill instead of flying over cards already down.")]
        [SerializeField] private bool dealFromRight = true;

        [Header("Deal out")]
        [Tooltip("Where the cards leave to when the menu hands over to another scene.")]
        [SerializeField] private Vector2 exitOffset = new Vector2(1400f, -900f);

        [SerializeField] private float exitAngle = -40f;
        [SerializeField] private float exitCardDuration = 0.35f;
        [SerializeField] private float exitStagger = 0.04f;
        [SerializeField, SearchableEnum] private Ease exitEase = Ease.InCubic;

        [Header("Sound")]
        [Tooltip("Played once, as the deal sets off. One sound for the whole spread rather than " +
                 "one per card: seven of anything this close together fuse into a single crack " +
                 "however they are spaced, and the hand already has a sound for exactly this - a " +
                 "fistful of cards being spread out. Set to None to deal silently.")]
        [SerializeField, SearchableEnum] private AudioSFXKey dealStartSound = AudioSFXKey.HandToFan;

        private IAudioService _audioService;

        private Sequence _sequence;
        private bool _isOpening;
        private bool _isClosing;

        [Inject]
        private void Inject(IAudioService audioService) => _audioService = audioService;

        private void Awake()
        {
            if (fan == null)
            {
                Debug.LogError($"[{nameof(MainMenuFanAnimation)}] No fan assigned; the menu will " +
                               "have nothing to deal.", this);

                return;
            }

            SetToClosedState();
        }

        public async UniTask Open(CancellationToken ct, bool immediately = false)
        {
            if (fan == null || _isOpening)
                return;

            _isOpening = true;

            try
            {
                // The layout may have changed since Awake - Continue is added or dropped once the
                // save has been read - so the resting poses are settled here, right before they
                // are flown to.
                fan.EnsureLayout();

                // Nothing may be clicked while it is still in the air: the card under the cursor
                // at the halfway mark is not the card the player is aiming at.
                fan.SetInteractable(false);

                // The deal is asked for on the scene's very first frames - the view system shows
                // the scene the moment initialization finishes - and those are the frames that
                // cost the most. Running on unscaled time, the sequence would be handed that whole
                // cost as one delta and spend most of itself before anything was drawn, which
                // looks exactly like the cards simply appearing. The cards are still at zero alpha
                // here, so nothing shows while we wait.
                if (!immediately && await Core.Utils.FrameTiming.WaitForSettledFrame(ct))
                    return;

                if (group != null)
                    group.alpha = 1f;

                if (immediately || cardDuration <= 0f || fan.ShownCards.Count == 0)
                {
                    SetToOpenedState();
                    return;
                }

                StopSequence();

                // Once, as the whole spread sets off - the cards are one hand being fanned out,
                // and that is one gesture rather than seven.
                if (dealStartSound != AudioSFXKey.None)
                    _audioService?.Play(dealStartSound);

                _sequence = BuildSequence(entering: true);

                await _sequence.WithCancellation(ct);

                // Whether it played out or was cut short, the fan ends where it belongs.
                SetToOpenedState();
            }
            finally
            {
                _isOpening = false;

                // The deal can be cut short by the menu being torn down - a scene change lands
                // mid-flight - and there is nothing left to hand back to the player then.
                if (fan != null)
                    fan.SetInteractable(true);
            }
        }

        public async UniTask Close(CancellationToken ct, bool immediately = false)
        {
            if (fan == null || _isClosing)
                return;

            _isClosing = true;

            try
            {
                fan.SetInteractable(false);

                if (immediately || exitCardDuration <= 0f || fan.ShownCards.Count == 0)
                {
                    SetToClosedState();
                    return;
                }

                StopSequence();
                _sequence = BuildSequence(entering: false);

                await _sequence.WithCancellation(ct);

                SetToClosedState();
            }
            finally
            {
                _isClosing = false;
            }
        }

        /// <summary>
        /// One sequence for the whole fan rather than a tween per card awaited in turn: the cards
        /// have to overlap in the air, and a single sequence is also a single thing to stop when
        /// the menu is cut short mid-deal.
        /// </summary>
        private Sequence BuildSequence(bool entering)
        {
            Vector2 offset = entering ? enterOffset : exitOffset;
            float angle = entering ? enterAngle : exitAngle;
            float duration = entering ? cardDuration : exitCardDuration;
            float step = entering ? stagger : exitStagger;
            Ease ease = entering ? enterEase : exitEase;

            IReadOnlyList<MainMenuCard> shown = fan.ShownCards;

            // Unscaled throughout - the deal must play even if the game's clock arrived stopped
            // from a paused room. The sequence and its tweens have to agree on that.
            Sequence sequence = Sequence.Create(useUnscaledTime: true);

            for (int i = 0; i < shown.Count; i++)
            {
                MainMenuCard card = shown[i];

                // Dealt from one end, so the order is the card's place in the fan counted from
                // whichever end is going first.
                int order = dealFromRight ? shown.Count - 1 - i : i;
                float delay = order * step;

                Vector2 awayPosition = card.RestPosition + offset;
                float awayAngle = card.RestAngle + angle;

                Vector2 targetPosition = entering ? card.RestPosition : awayPosition;
                float targetAngle = entering ? card.RestAngle : awayAngle;

                if (entering)
                    card.SetPose(awayPosition, awayAngle);

                sequence.Insert(delay,
                    Tween.UIAnchoredPosition(card.Rect, targetPosition, duration, ease,
                        useUnscaledTime: true));

                sequence.Insert(delay,
                    Tween.LocalRotation(card.Rect, Quaternion.Euler(0f, 0f, targetAngle), duration,
                        ease, useUnscaledTime: true));
            }

            return sequence;
        }

        private void SetToOpenedState()
        {
            StopSequence();

            if (group != null)
                group.alpha = 1f;

            foreach (MainMenuCard card in fan.ShownCards)
                card.SetPose(card.RestPosition, card.RestAngle);
        }

        private void SetToClosedState()
        {
            StopSequence();

            if (group != null)
                group.alpha = 0f;

            foreach (MainMenuCard card in fan.ShownCards)
                card.SetPose(card.RestPosition + exitOffset, card.RestAngle + exitAngle);
        }

        private void StopSequence()
        {
            if (_sequence.isAlive)
                _sequence.Stop();
        }

        private void OnDestroy() => StopSequence();
    }
}
