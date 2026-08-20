using System;
using System.Collections.Generic;
using UnityEngine;
using VInspector;

namespace Vesolovsky.Game.Views.MainMenu
{
    /// <summary>
    /// The menu laid out as a hand of cards: spread along a shallow arc, bowed up in the middle,
    /// each one leaning a little further out than the last.
    ///
    /// The cards themselves are authored by hand in the prefab - there are exactly as many as
    /// there are menu entries, and each is its own object with its own icon and label. Only where
    /// they sit is worked out here, and for one reason: Continue is not always there. Hand-placing
    /// seven cards means either a hole on the left of the fan on a fresh save, or a second layout
    /// authored and kept in step by hand. Computing the arc closes the fan back up around the gap
    /// for free, and the numbers below stay tunable from the Inspector with the preview button.
    ///
    /// Only one card is ever raised. The cards report the cursor arriving and leaving; the fan is
    /// what decides which of them that means is up, so a cursor swept along the row hands the lift
    /// from one card to the next rather than leaving a trail of them standing.
    /// </summary>
    [AddComponentMenu("CardsChaos/Main Menu/Card Fan")]
    public class MainMenuCardFan : MonoBehaviour
    {
        [Tooltip("Every menu card, in the order they are spread from left to right. They should " +
                 "be the only children of this object - the fan sets their draw order, and " +
                 "anything else parented here would be shuffled along with them.")]
        [SerializeField] private List<MainMenuCard> cards = new List<MainMenuCard>();

        [Header("Arc")]
        [Tooltip("Horizontal gap between neighbouring cards, in pixels. Smaller than a card is " +
                 "wide, so they overlap the way a held hand does.")]
        [SerializeField] private float spacing = 196f;

        [Tooltip("Degrees between neighbouring cards. The fan leans out from the middle, so the " +
                 "two ends end up tilted this much times half the count.")]
        [SerializeField] private float anglePerCard = 7f;

        [Tooltip("How far the middle of the fan bows above its two ends, in pixels. Zero lays " +
                 "the cards along a straight line.")]
        [SerializeField] private float arcHeight = 54f;

        [Tooltip("Where the middle of the fan sits, relative to this object.")]
        [SerializeField] private Vector2 centre = Vector2.zero;

        /// <summary>A card was clicked. Raised for whichever card it was; the view reads its action.</summary>
        public event Action<MainMenuCard> CardClicked;

        private readonly List<MainMenuCard> _shown = new List<MainMenuCard>();
        private readonly List<int> _drawOrder = new List<int>();

        private MainMenuCard _hovered;
        private bool _laidOut;

        // The fan's own CanvasGroup - the same one the deal animation dims - reused as the switch
        // that decides whether the cards answer the cursor. Resolved in Awake, which is long
        // before anything asks.
        private CanvasGroup _raycastBlocker;

        /// <summary>The cards actually in the fan right now, left to right.</summary>
        public IReadOnlyList<MainMenuCard> ShownCards
        {
            get
            {
                EnsureLayout();
                return _shown;
            }
        }

        private void Awake()
        {
            _raycastBlocker = GetComponent<CanvasGroup>();

            if (_raycastBlocker == null)
            {
                Debug.LogWarning(
                    $"[{nameof(MainMenuCardFan)}] No {nameof(CanvasGroup)} on '{name}', so the " +
                    "cards can still be clicked while they are being dealt.", this);
            }

            foreach (MainMenuCard card in cards)
            {
                if (card == null)
                    continue;

                card.Clicked += OnCardClicked;
                card.HoverEntered += OnCardHoverEntered;
                card.HoverExited += OnCardHoverExited;
            }

            EnsureLayout();
        }

        public MainMenuCard Find(MainMenuAction action)
        {
            foreach (MainMenuCard card in cards)
            {
                if (card != null && card.Action == action)
                    return card;
            }

            return null;
        }

        /// <summary>
        /// Takes a card out of the menu, or puts it back. The arc is re-struck around what is left,
        /// so a missing card closes the fan up rather than leaving a hole where it was.
        /// </summary>
        public void SetCardShown(MainMenuAction action, bool shown)
        {
            MainMenuCard card = Find(action);
            if (card == null || card.IsShown == shown)
                return;

            if (_hovered == card)
                ClearHover();

            card.SetShown(shown);
            _laidOut = false;
        }

        /// <summary>
        /// Whether the cards can be pointed at. Held shut while they are still being dealt - the
        /// card under the cursor halfway through the deal is not the card the player is aiming at.
        ///
        /// Done by taking the fan out of the raycast rather than by turning the buttons off. A
        /// disabled <see cref="UnityEngine.UI.Selectable"/> is not merely unclickable: it repaints
        /// its target graphic in its disabled colour, and the stock value for that is grey at half
        /// alpha, cross-faded over the button's own fade duration. Held that way through the deal,
        /// every card is dealt washed out and then visibly brightens the instant the last one
        /// lands - which reads as a broken fade, because nothing here is meant to look disabled in
        /// the first place. The CanvasGroup blocks exactly what needs blocking and paints nothing.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (_raycastBlocker != null)
                _raycastBlocker.blocksRaycasts = interactable;

            // A card left raised when the fan stops answering the cursor would stay up: the
            // pointer-exit it is waiting for never arrives once the raycast passes it by.
            if (!interactable)
                ClearHover();
        }

        public void ClearHover() => SetHovered(null);

        /// <summary>Lays the fan out if anything has changed since the last time. Cheap to call.</summary>
        public void EnsureLayout()
        {
            if (_laidOut)
                return;

            ApplyLayout();
        }

        /// <summary>
        /// Spreads the shown cards along the arc and settles who draws over whom.
        ///
        /// The offset runs -0.5 at the left end to +0.5 at the right, so the spread is centred on
        /// this object instead of grown from one edge - which is what lets a card leave the fan
        /// without dragging the whole row sideways.
        /// </summary>
        public void ApplyLayout()
        {
            _shown.Clear();

            foreach (MainMenuCard card in cards)
            {
                if (card != null && card.IsShown)
                    _shown.Add(card);
            }

            int count = _shown.Count;
            float last = Mathf.Max(1, count - 1);

            for (int i = 0; i < count; i++)
            {
                float offset = count > 1 ? i / last - 0.5f : 0f;

                // A parabola through the two ends, peaking in the middle - squaring the offset is
                // what makes the row a bow rather than a ramp.
                float arc = count > 1 ? arcHeight * (1f - 4f * offset * offset) : 0f;

                var position = centre + new Vector2(spacing * offset * last, arc);
                float angle = -anglePerCard * offset * last;

                _shown[i].SetRestPose(position, angle);
            }

            ApplyDrawOrder();
            _laidOut = true;
        }

        /// <summary>
        /// The middle of the fan is drawn on top and each card outward from it goes one layer
        /// further back, so the spread reads as one hand held out rather than a row of cards each
        /// eating the next.
        /// </summary>
        private void ApplyDrawOrder()
        {
            int count = _shown.Count;
            float middle = (count - 1) * 0.5f;

            _drawOrder.Clear();
            for (int i = 0; i < count; i++)
                _drawOrder.Add(i);

            // Furthest from the middle first: sibling order is draw order, so the last one placed
            // is the one on top.
            _drawOrder.Sort((a, b) =>
                Mathf.Abs(b - middle).CompareTo(Mathf.Abs(a - middle)));

            for (int i = 0; i < _drawOrder.Count; i++)
                _shown[_drawOrder[i]].transform.SetSiblingIndex(i);
        }

        /// <summary>
        /// Hands the lift to a card, taking it off whichever card had it. The raised card is also
        /// pulled to the front, because a card that rises but stays buried under its neighbour
        /// looks like a bug rather than a selection.
        /// </summary>
        private void SetHovered(MainMenuCard card)
        {
            if (_hovered == card)
                return;

            _hovered?.SetHovered(false);
            _hovered = card;

            // Back to the fan's own order first, so the card that has just been let go of drops
            // back into its layer instead of staying stuck on top from its own turn.
            ApplyDrawOrder();

            if (_hovered == null)
                return;

            _hovered.SetHovered(true);
            _hovered.transform.SetAsLastSibling();
        }

        private void OnCardClicked(MainMenuCard card) => CardClicked?.Invoke(card);

        private void OnCardHoverEntered(MainMenuCard card) => SetHovered(card);

        /// <summary>
        /// Only the card that actually holds the lift may put it down. Without that check, moving
        /// the cursor from one card straight onto the next would arrive at the new card and then
        /// immediately hear the old one leaving, and the fan would drop the lift it had just given.
        /// </summary>
        private void OnCardHoverExited(MainMenuCard card)
        {
            if (_hovered == card)
                SetHovered(null);
        }

        private void OnDestroy()
        {
            foreach (MainMenuCard card in cards)
            {
                if (card == null)
                    continue;

                card.Clicked -= OnCardClicked;
                card.HoverEntered -= OnCardHoverEntered;
                card.HoverExited -= OnCardHoverExited;
            }
        }

        /// <summary>
        /// Strikes the arc in the editor so the numbers above can be dialled in against the real
        /// art instead of guessed at and tested in Play mode.
        /// </summary>
        [Button]
        private void PreviewLayout()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                foreach (MainMenuCard card in cards)
                {
                    if (card != null)
                        UnityEditor.Undo.RecordObject(card.transform, "Main menu fan layout");
                }
            }
#endif

            ApplyLayout();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                foreach (MainMenuCard card in cards)
                {
                    if (card != null)
                        UnityEditor.EditorUtility.SetDirty(card);
                }
            }
#endif
        }
    }
}
