using System;
using System.Threading;
using CardsChaos.Cards;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Services.Stats;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// The album's endgame state. It has one fixed slot for the final card and a run of stat lines
    /// that type themselves out one after another once that card is filed. Filing the card seals the
    /// album - it can no longer be closed - and the last line hands off to whatever ends the game.
    ///
    /// It reuses the album's shared drag controller and card inspector, so the final card is dragged
    /// out of the hand and filed exactly like any other; only the layout around it differs.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Final Card Layout")]
    public class AlbumFinalCardLayout : MonoBehaviour
    {
        [Header("Slot")]
        [Tooltip("The single fixed slot the final card is filed into.")]
        [SerializeField] private AlbumCardSlot slot;

        [Header("Stat lines, revealed in this order")]
        [SerializeField] private TypewriterText collectedCardsText;
        [SerializeField] private TypewriterText cardsPickedUpText;
        [SerializeField] private TypewriterText thrownCardsText;
        [SerializeField] private TypewriterText skillsUsedText;
        [SerializeField] private TypewriterText timePlayedText;
        [SerializeField] private TypewriterText sessionsPlayedText;
        [SerializeField] private TypewriterText distanceTraveledText;
        [SerializeField] private TypewriterText peakCorrectlyPlacedText;
        [SerializeField] private TypewriterText completionDateText;

        [Header("Timing")]
        [Tooltip("Seconds to wait after the final card lands before the first line types out.")]
        [SerializeField] private float startDelay = 1.5f;

        [Tooltip("Seconds between the start of one line and the next. Keep it above each line's own " +
                 "type-out time (set per line on the Typewriter Text) so the lines do not overlap.")]
        [SerializeField] private float betweenLines = 1.5f;

        [Header("Seal")]
        [Tooltip("A full-screen raycast blocker switched on the moment the card is filed, so nothing " +
                 "in the album can be touched for the rest of the finale.")]
        [SerializeField] private GameObject sealBlocker;

        private IPlayerStats _stats;
        private AlbumDragController _drag;
        private bool _initialized;
        private bool _sealed;

        /// <summary>True from the moment the final card is filed - the album may no longer be closed.</summary>
        public bool IsSealed => _sealed;

        /// <summary>
        /// Wires the fixed slot to the album's shared drag and inspector and clears the lines. Safe
        /// to call on every open; only the first takes. The stats service may be null (the finale
        /// then reads zeros) since it comes from the scene the album is opened in.
        /// </summary>
        public void Initialize(
            AlbumDragController drag, IAlbumCardInspector inspector, CardSetDefinition endgameSet,
            IPlayerStats stats)
        {
            if (_initialized)
                return;

            _initialized = true;
            _drag = drag;
            _stats = stats;

            if (slot != null && endgameSet != null)
                slot.Initialize(drag, inspector, endgameSet.SetId, 0, endgameSet);

            ClearLines();

            if (sealBlocker != null)
                sealBlocker.SetActive(false);

            if (_drag != null)
                _drag.CardFiledCorrectly += OnCardFiledCorrectly;
        }

        private void OnCardFiledCorrectly(AlbumCardSlot filled)
        {
            // Only the final card landing in this layout's own slot begins the finale.
            if (_sealed || filled != slot)
                return;

            _sealed = true;

            if (sealBlocker != null)
                sealBlocker.SetActive(true);

            RunSequence(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid RunSequence(CancellationToken token)
        {
            if (await Wait(startDelay, token))
                return;

            IPlayerStats s = _stats;

            (TypewriterText Text, string Value)[] lines =
            {
                (collectedCardsText, $"Collected cards: {s?.CorrectlyPlacedCards ?? 0}/{s?.TotalCards ?? 0}"),
                (cardsPickedUpText, $"Cards picked up: {s?.CardsPickedUp ?? 0L}"),
                (thrownCardsText, $"Cards thrown: {s?.CardsThrown ?? 0L}"),
                (skillsUsedText, $"Skills used: {s?.SkillsUsed ?? 0L}"),
                (timePlayedText, $"Playtime: {FormatTime(s?.PlaytimeSeconds ?? 0d)}"),
                (sessionsPlayedText, $"Sessions played: {s?.SessionsPlayed ?? 0L}"),
                (distanceTraveledText, $"Distance traveled: {FormatDistance(s?.DistanceTraveled ?? 0d)}"),
                (peakCorrectlyPlacedText, $"Correctly placed streak: {s?.PeakCorrectlyPlaced ?? 0}"),
                (completionDateText, $"Completion date: {DateTime.Now:dd:MM:yyyy}"),
            };

            foreach ((TypewriterText Text, string Value) line in lines)
            {
                line.Text?.Play(line.Value);

                if (await Wait(betweenLines, token))
                    return;
            }

            OnSequenceComplete();
        }

        private void OnSequenceComplete()
        {
            // ─────────────────────────────────────────────────────────────────────────────────────
            //  ▼▼▼  END OF GAME HANDOFF GOES HERE  ▼▼▼
            //  The stat finale has finished. Kick off the ending here - transition to Credits, then
            //  to the main menu. The album is sealed (cannot be closed), so this is the only way out.
            //  For now, just a log.
            // ─────────────────────────────────────────────────────────────────────────────────────
            Debug.Log("[AlbumFinalCardLayout] Endgame stat sequence complete - trigger Credits here.");
        }

        private void ClearLines()
        {
            SetEmpty(collectedCardsText);
            SetEmpty(cardsPickedUpText);
            SetEmpty(thrownCardsText);
            SetEmpty(skillsUsedText);
            SetEmpty(timePlayedText);
            SetEmpty(sessionsPlayedText);
            SetEmpty(distanceTraveledText);
            SetEmpty(peakCorrectlyPlacedText);
            SetEmpty(completionDateText);
        }

        private static void SetEmpty(TypewriterText text) => text?.SetImmediate(string.Empty);

        private static string FormatTime(double seconds)
        {
            int total = Mathf.Max(0, (int)seconds);
            return $"{total / 3600:00}:{total % 3600 / 60:00}:{total % 60:00}";
        }

        private static string FormatDistance(double units)
        {
            int total = Mathf.Max(0, (int)units);
            return $"{total / 1000}km {total % 1000}m";
        }

        private static async UniTask<bool> Wait(float seconds, CancellationToken token)
        {
            return await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token)
                .SuppressCancellationThrow();
        }

        private void OnDestroy()
        {
            if (_drag != null)
                _drag.CardFiledCorrectly -= OnCardFiledCorrectly;
        }
    }
}
