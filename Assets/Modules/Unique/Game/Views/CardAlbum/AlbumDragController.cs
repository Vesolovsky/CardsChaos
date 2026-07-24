using CardsChaos.Cards.Album;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// What the album lets the player do with a card: pick one off the pile and file it, or pull
    /// a filed one back out.
    ///
    /// Implemented by the view model, because both moves are questions about ownership - whether
    /// the hand has room, what happens to the card out in the room - rather than about pixels.
    /// </summary>
    public interface IAlbumMoves
    {
        /// <summary>
        /// Files a card into an empty slot. Any card goes in any slot; putting the wrong one down
        /// is a move the player is allowed to make. False only when the move could not be
        /// completed at all.
        /// </summary>
        bool TryFile(IAlbumCardSource source, AlbumCardSlot slot);

        /// <summary>
        /// Hands a filed card back to the player. False when there is no room in the hand, in
        /// which case the card stays exactly where it was.
        /// </summary>
        bool TryReturnToHand(AlbumCardSlot slot);
    }

    /// <summary>
    /// Owns the card the player is dragging, from the moment it leaves the pile or a slot until
    /// it lands somewhere or gives up and goes home.
    ///
    /// The card being dragged is a copy floating on its own layer, never the original. That keeps
    /// it clear of every mask and layout group between where it started and where it is going -
    /// dragging the real object out of a clipped, grid-laid page means fighting both on every
    /// frame of the drag.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Drag Controller")]
    public class AlbumDragController : MonoBehaviour
    {
        [Tooltip("Full-rect transform above everything else in the album. The dragged card lives " +
                 "here, so it draws over the pages and the set list.")]
        [SerializeField] private RectTransform dragLayer;

        [Tooltip("The floating card. Just an Image - its raycast target must be off, or it " +
                 "shadows the pointer and no slot ever sees the drop.")]
        [SerializeField] private Image ghostPrefab;

        [Header("Lift")]
        [Tooltip("How much the card grows while carried, so it reads as being above the page.")]
        [SerializeField] private float liftScale = 1.06f;
        [SerializeField] private float liftDuration = 0.12f;

        [Header("Going home")]
        [Tooltip("A card let go of over nothing. It travels back rather than vanishing, so the " +
                 "player can see where it went.")]
        [SerializeField] private float returnDuration = 0.28f;
        [SerializeField, SearchableEnum] private Ease returnEase = Ease.OutQuad;

        [Header("Landing")]
        [Tooltip("Short and accelerating - the card should look dropped onto the page, not " +
                 "lowered onto it.")]
        [SerializeField] private float dropDuration = 0.16f;
        [SerializeField, SearchableEnum] private Ease dropEase = Ease.InQuad;

        [Header("Screen shake")]
        [Tooltip("What gets shaken when a card lands - the album's Root. It has to be the UI " +
                 "and not the camera: the album is drawn on a screen-space overlay canvas, so a " +
                 "camera shake would rattle the room behind a panel nobody can see past.")]
        [SerializeField] private RectTransform shakeTarget;

        [Tooltip("Pixels. Small - this is meant to be felt rather than noticed, and it fires on " +
                 "every card the player files.")]
        [SerializeField] private Vector3 shakeStrength = new Vector3(5f, 5f, 0f);

        [SerializeField] private float shakeDuration = 0.22f;
        [SerializeField] private float shakeFrequency = 20f;

        [Header("Correct placement")]
        [Tooltip("Spawned on the slot when a card lands where it actually belongs. Optional - " +
                 "leave it empty until the effect exists. The prefab is expected to clean itself " +
                 "up; see ParticleDestroyer.")]
        [SerializeField] private GameObject correctPlacementVfx;

        private DiContainer _container;
        private IAlbumMoves _moves;
        private CardArtworkResolver _artwork;
        private Canvas _canvas;

        private IAlbumCardSource _source;
        private Image _ghost;
        private Vector2 _grabOffset;
        private AlbumCardSlot _droppedOnSlot;
        private bool _droppedOnPile;
        private Tween _shakeTween;
        private Vector3 _shakeRestPosition;
        private bool _shakeRestCaptured;

        /// <summary>True while a card is off the page and following the pointer.</summary>
        public bool IsDragging => _source != null;

        [Inject]
        private void Inject(DiContainer container) => _container = container;

        public void Initialize(IAlbumMoves moves, CardArtworkResolver artwork)
        {
            _moves = moves;
            _artwork = artwork;

            // The root canvas, not the nearest one: a nested Canvas inherits the render mode and
            // the camera from the top of the stack, and those two are what every screen-space
            // conversion below turns on.
            _canvas = dragLayer.GetComponentInParent<Canvas>().rootCanvas;
        }

        /// <summary>
        /// The camera screen positions are relative to - null under a screen-space overlay
        /// canvas, which is what the conversion helpers expect there.
        /// </summary>
        private Camera UICamera =>
            _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

        public void Begin(IAlbumCardSource source, PointerEventData eventData)
        {
            // A second drag while one is in flight would orphan the first card's ghost, and with
            // it the source that is still waiting to hear what happened.
            if (IsDragging)
                return;

            Sprite artwork = source.Artwork;
            if (artwork == null)
                return;

            _source = source;
            _droppedOnSlot = null;
            _droppedOnPile = false;

            _ghost = _container.InstantiatePrefabForComponent<Image>(ghostPrefab, dragLayer);
            _ghost.sprite = artwork;

            RectTransform ghostRect = _ghost.rectTransform;
            ghostRect.sizeDelta = source.Rect.rect.size;
            ghostRect.localScale = Vector3.one;

            // Grabbed where it was grabbed: snapping the card's centre to the cursor makes a card
            // picked up by its corner jump, which reads as the drag having missed.
            Vector2 pointer = ToLayer(eventData.position);
            ghostRect.anchoredPosition = ToLayer(source.Rect);
            _grabOffset = ghostRect.anchoredPosition - pointer;

            source.OnCardLifted();

            Tween.Scale(ghostRect, liftScale, liftDuration, Ease.OutQuad);
        }

        public void Move(IAlbumCardSource source, PointerEventData eventData)
        {
            if (_source != source || _ghost == null)
                return;

            _ghost.rectTransform.anchoredPosition = ToLayer(eventData.position) + _grabOffset;
        }

        public void End(IAlbumCardSource source, PointerEventData eventData)
        {
            if (_source != source)
                return;

            AlbumCardSlot slot = _droppedOnSlot;
            bool ontoPile = _droppedOnPile;

            _droppedOnSlot = null;
            _droppedOnPile = false;

            // Read before anything is told the card has moved: a slot that gives its card up
            // forgets which one it was, and the landing animation still needs the name.
            CardRef card = source.Card;

            if (slot != null && _moves.TryFile(source, slot))
            {
                LandOn(slot, card);
                return;
            }

            // Only a card that came out of a slot has a hand to go back to; one already in the
            // hand dropped onto the pile is just going home the long way.
            if (ontoPile && source is AlbumCardSlot origin && _moves.TryReturnToHand(origin))
            {
                // The pile rebuilds itself off the hand, so the card is already drawn there by
                // the time the ghost would arrive. Anything else would show it twice.
                Release(taken: true);
                return;
            }

            GoHome();
        }

        /// <summary>Called by a slot the pointer was released over.</summary>
        public void TryDropOn(AlbumCardSlot slot)
        {
            if (IsDragging)
                _droppedOnSlot = slot;
        }

        /// <summary>Called by the hand pile when the pointer was released over it.</summary>
        public void TryDropOnPile()
        {
            if (IsDragging)
                _droppedOnPile = true;
        }

        /// <summary>
        /// Drops the floating card onto the slot it was let go of over. The album already knows
        /// the card is there - only the picture is still catching up - so the flight is left to
        /// finish on its own and the drag is handed back immediately. A player who starts the
        /// next drag mid-flight gets it; this one still lands.
        /// </summary>
        private void LandOn(AlbumCardSlot slot, CardRef card)
        {
            // Captured before Release() clears the fields: the callback runs several frames
            // later, by which time the drag has moved on to whatever came next.
            Image ghost = _ghost;
            RectTransform ghostRect = ghost.rectTransform;

            Tween.Scale(ghostRect, 1f, dropDuration, dropEase);

            // No warning when the target goes: closing the album mid-flight destroys the ghost,
            // which is a normal end to the drag rather than something to report.
            Tween.UIAnchoredPosition(ghostRect, ToLayer(slot.Rect), dropDuration, dropEase)
                .OnComplete(() => Settle(slot, card, ghost), warnIfTargetDestroyed: false);

            Release(taken: true, destroyGhost: false);
        }

        private void Settle(AlbumCardSlot slot, CardRef card, Image ghost)
        {
            if (ghost != null)
                Destroy(ghost.gameObject);

            // The album can have been closed and rebuilt underneath a landing card.
            if (slot == null)
                return;

            slot.Fill(card, _artwork.Resolve(card));
            PlayImpactShake();

            if (card.BelongsAt(slot.PageSetId, slot.SlotIndex))
                SpawnCorrectPlacementVfx(slot);
        }

        /// <summary>
        /// The knock that runs through the whole album when a card lands, on top of the slot's
        /// own punch. Every card gets it, right or wrong - the shake is about the card hitting
        /// the page, not about it hitting the right square.
        /// </summary>
        private void PlayImpactShake()
        {
            if (shakeTarget == null || shakeDuration <= 0f)
                return;

            // Captured the first time rather than at Initialize: the layout has settled by the
            // time a card can be filed, and reading it earlier can catch the rect mid-build.
            if (!_shakeRestCaptured)
            {
                _shakeRestPosition = shakeTarget.localPosition;
                _shakeRestCaptured = true;
            }

            // Stopping a shake leaves the target wherever the last frame put it, and starting the
            // next one from there makes it the new rest position. Two cards filed in quick
            // succession would walk the album off centre and it would never come back, so the
            // rest position is restored by hand first.
            if (_shakeTween.isAlive)
                _shakeTween.Stop();

            shakeTarget.localPosition = _shakeRestPosition;

            _shakeTween = Tween.ShakeLocalPosition(
                shakeTarget, shakeStrength, shakeDuration, shakeFrequency);
        }

        private void SpawnCorrectPlacementVfx(AlbumCardSlot slot)
        {
            if (correctPlacementVfx == null)
                return;

            _container.InstantiatePrefab(correctPlacementVfx, slot.VfxAnchor);
        }

        /// <summary>
        /// Nobody took the card, so it travels back to where it was picked up rather than
        /// blinking out of the drag layer - the player let go somewhere for a reason, and seeing
        /// the card go back is what says the album refused it.
        /// </summary>
        private void GoHome()
        {
            IAlbumCardSource source = _source;
            Image ghost = _ghost;

            if (ghost == null)
            {
                source?.OnCardReturned();
                Release(taken: false);
                return;
            }

            Tween.Scale(ghost.rectTransform, 1f, returnDuration, returnEase);
            Tween.UIAnchoredPosition(
                    ghost.rectTransform, ToLayer(source.Rect), returnDuration, returnEase)
                .OnComplete(() =>
                {
                    if (ghost != null)
                        Destroy(ghost.gameObject);

                    source.OnCardReturned();
                }, warnIfTargetDestroyed: false);

            Release(taken: false, destroyGhost: false);
        }

        /// <summary>
        /// Lets go of the drag so a new one can start, independently of whatever animation is
        /// still playing out. The card's fate is already decided by the time this runs.
        /// </summary>
        private void Release(bool taken, bool destroyGhost = true)
        {
            if (taken)
                _source?.OnCardTaken();

            if (destroyGhost && _ghost != null)
                Destroy(_ghost.gameObject);

            _source = null;
            _ghost = null;
        }

        /// <summary>A screen point in the drag layer's own space.</summary>
        private Vector2 ToLayer(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer, screenPosition, UICamera, out Vector2 local);

            return local;
        }

        /// <summary>
        /// Another rect's centre in the drag layer's space, which is the only space the ghost's
        /// anchored position means anything in. Used to aim the card at a slot on a page that is
        /// masked, offset and grid-laid, none of which the ghost knows or wants to know about.
        /// </summary>
        private Vector2 ToLayer(RectTransform rect)
        {
            return ToLayer(RectTransformUtility.WorldToScreenPoint(UICamera, rect.position));
        }
    }
}
