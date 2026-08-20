using System;
using System.Globalization;
using System.Threading;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using Cysharp.Threading.Tasks;
using PrimeTween;
using RoboRyanTron.SceneReference;
using UnityEngine;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Services.Save;
using Vesolovsky.Game.Services.Stats;
using Zenject;

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

        [Header("Ending")]
        [Tooltip("Full-screen image faded up once the stat lines have all shown; the Credits scene " +
                 "loads when it is fully opaque. Start it transparent - its alpha is driven from here.")]
        [SerializeField] private Image fader;

        [Tooltip("Seconds the fade to the Credits scene takes.")]
        [SerializeField] private float fadeDuration = 1.5f;

        [SerializeField] private SceneReference creditsScene;

        [Header("Looking back")]
        [Tooltip("Only present when the album is opened from the main menu after the game has " +
                 "been finished: it shuts this closing spread and hands the player through to the " +
                 "collection itself. Never shown during the finale, where the ending has not " +
                 "happened yet.")]
        [SerializeField] private VButton seeCollectionButton;

        private IPlayerStats _stats;
        private IEndgameRecord _endgameRecord;
        private AlbumDragController _drag;
        private bool _initialized;
        private bool _sealed;

        // Optional so the album still builds in an isolated UI test scene with no save behind it;
        // the lines then simply come from the live stats, as they used to.
        [Inject]
        private void Inject([InjectOptional] IEndgameRecord endgameRecord)
        {
            _endgameRecord = endgameRecord;
        }

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

            // The ending has not happened yet; there is nothing to look back on.
            if (seeCollectionButton != null)
                seeCollectionButton.gameObject.SetActive(false);

            if (_drag != null)
                _drag.CardFiledCorrectly += OnCardFiledCorrectly;
        }

        /// <summary>
        /// The same spread, but as a memento rather than as a finale: opened from the main menu
        /// once the game has been finished, with the final card already in its slot and the tally
        /// already written out.
        ///
        /// Nothing types itself and nothing fades to the credits. Those beats were the ending, and
        /// the ending has been had - replaying it every time the player looks at their album would
        /// cheapen it and, worse, make them sit through it. The one thing this spread does that
        /// the finale does not is offer a way onward, into the collection itself.
        /// </summary>
        public void ShowCompleted(
            AlbumDragController drag,
            IAlbumCardInspector inspector,
            CardSetDefinition endgameSet,
            CardRef finalCard,
            Sprite artwork,
            IPlayerStats stats,
            Action seeCollection)
        {
            // Claims the same one-shot guard as the finale: whichever of the two set this layout
            // up owns it for the life of the album.
            _initialized = true;
            _stats = stats;

            // Deliberately not subscribed to CardFiledCorrectly. There is no card to file here,
            // and the finale must not be able to fire a second time from a menu.
            _drag = null;

            if (slot != null && endgameSet != null)
            {
                slot.Initialize(drag, inspector, endgameSet.SetId, 0, endgameSet);

                if (finalCard.IsValid)
                    slot.Fill(finalCard, artwork);
            }

            WriteLines();

            if (sealBlocker != null)
                sealBlocker.SetActive(false);

            // The fader is the last step of the finale. Left on it would sit over the album as an
            // opaque sheet the moment this spread is closed.
            if (fader != null)
                fader.gameObject.SetActive(false);

            if (seeCollectionButton == null)
            {
                Debug.LogWarning($"[{nameof(AlbumFinalCardLayout)}] No See Collection button " +
                                 "assigned, so the finished album has no way through to the " +
                                 "collection. Escape still closes it.", this);

                return;
            }

            seeCollectionButton.gameObject.SetActive(true);
            seeCollectionButton.Bind(() => seeCollection?.Invoke());
        }

        private void OnCardFiledCorrectly(AlbumCardSlot filled)
        {
            // Only the final card landing in this layout's own slot begins the finale.
            if (_sealed || filled != slot)
                return;

            _sealed = true;

            if (sealBlocker != null)
                sealBlocker.SetActive(true);

            // Written down now, not at the end of the reveal: this is the instant the game was
            // finished, and leaving it until the credits would risk losing the ending to a quit
            // mid-finale.
            //
            // The tally is settled by this point. Filing a card writes the album first, which
            // raises ICardAlbum.PageChanged - the album's contents changed, not a page turned -
            // and the stats tracker recounts the collection there and then. Only afterwards does
            // the dropped card fly to its slot, and this fires when it lands, a good tenth of a
            // second and several frames later.
            _endgameRecord?.Record();

            RunSequence(destroyCancellationToken).Forget();
        }

        private async UniTaskVoid RunSequence(CancellationToken token)
        {
            if (await Wait(startDelay, token))
                return;

            foreach ((TypewriterText Text, string Value) line in BuildLines())
            {
                line.Text?.Play(line.Value);

                if (await Wait(betweenLines, token))
                    return;
            }

            OnSequenceComplete();
        }

        private void OnSequenceComplete()
        {
            // The finale is over: fade the screen out, then load the Credits scene.
            if (fader == null)
            {
                Debug.LogWarning("[AlbumFinalCardLayout] No fader assigned; loading Credits directly.");
                creditsScene.LoadScene();
                return;
            }

            fader.gameObject.SetActive(true);

            // Start clear whatever the authored alpha, so the fade always runs from transparent.
            Color color = fader.color;
            color.a = 0f;
            fader.color = color;

            Tween.Alpha(fader, 1f, fadeDuration)
                .OnComplete(() => creditsScene.LoadScene());
        }

        /// <summary>
        /// The closing tally, in the order it is revealed.
        ///
        /// Once the game has an ending on record, every line comes from it and from nowhere else -
        /// these are the numbers the game finished on, and the live counters carrying on afterwards
        /// (the playtime clock never stops) must not be able to rewrite them. Only a game with no
        /// ending recorded falls back to the live tally: the finale playing for the very first
        /// time, where the ending has just been written and the two agree anyway, and old saves
        /// finished before any of this was kept.
        /// </summary>
        private (TypewriterText Text, string Value)[] BuildLines()
        {
            EndgameSummary ending = _endgameRecord?.Summary;

            IPlayerStats s = ending != null ? new PlayerStatsSnapshot(ending.Stats) : _stats;
            DateTime completedAt = ending?.CompletedAt ?? DateTime.Now;

            return new (TypewriterText Text, string Value)[]
            {
                (collectedCardsText, $"Collected cards: {s?.CorrectlyPlacedCards ?? 0}/{s?.TotalCards ?? 0}"),
                (cardsPickedUpText, $"Cards picked up: {s?.CardsPickedUp ?? 0L}"),
                (thrownCardsText, $"Cards thrown: {s?.CardsThrown ?? 0L}"),
                (skillsUsedText, $"Skills used: {s?.SkillsUsed ?? 0L}"),
                (timePlayedText, $"Playtime: {FormatTime(s?.PlaytimeSeconds ?? 0d)}"),
                (sessionsPlayedText, $"Sessions played: {s?.SessionsPlayed ?? 0L}"),
                (distanceTraveledText, $"Distance traveled: {FormatDistance(s?.DistanceTraveled ?? 0d)}"),
                (peakCorrectlyPlacedText, $"Correctly placed streak: {s?.PeakCorrectlyPlaced ?? 0}"),
                // Invariant, so the colons stay colons: ':' in a date format is the culture's time
                // separator, and on a machine whose locale uses '.' the line would come out in a
                // shape nobody authored.
                (completionDateText,
                    $"Completion date: {completedAt.ToString("dd:MM:yyyy", CultureInfo.InvariantCulture)}"),
            };
        }

        /// <summary>Puts every line up at once, fully typed - no reveal.</summary>
        private void WriteLines()
        {
            foreach ((TypewriterText Text, string Value) line in BuildLines())
                line.Text?.SetImmediate(line.Value);
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
