using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Vesolovsky.Game.Views.Album
{
    /// <summary>
    /// The open set's pages, laid end to end in one long strip that slides left and right behind
    /// a window.
    ///
    /// Every page of the set is built at once. The largest set in the game is fifty cards, which
    /// is five pages and a few hundred UI objects - nothing next to what recycling ten slots
    /// underneath a running tween would cost in bugs, with a drag possibly in flight over the
    /// very slots being renumbered.
    ///
    /// Pages are positioned by anchor rather than by measured width: page i is anchored to the
    /// slice [i, i+1] of the strip, so it is exactly one window wide whatever the window turns
    /// out to be, and nothing has to wait for a layout pass before it can be placed.
    /// </summary>
    [AddComponentMenu("CardsChaos/Album/Page Strip")]
    public class AlbumPageStrip : MonoBehaviour
    {
        [Tooltip("The window the strip slides behind. Gets a RectMask2D so the pages either " +
                 "side of the current one are clipped away.")]
        [SerializeField] private RectTransform viewport;

        [Tooltip("The strip itself, stretched to fill the window. This is what moves. It must " +
                 "not carry a layout group - the pages place themselves.")]
        [SerializeField] private RectTransform content;

        [Tooltip("An empty page: a stretched RectTransform with the grid layout on it. The grid " +
                 "wants two fixed rows, which with ten slots to a page gives five columns.")]
        [SerializeField] private RectTransform pagePrefab;

        [SerializeField] private AlbumCardSlot slotPrefab;

        [Tooltip("Cards to a page. Changing this changes what the grid on the page prefab has " +
                 "to be set to.")]
        [SerializeField] private int slotsPerPage = 10;

        [Header("Paging")]
        [SerializeField] private float pageDuration = 0.45f;
        [SerializeField, SearchableEnum] private Ease pageEase = Ease.OutQuint;

        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private readonly List<AlbumCardSlot> _slots = new List<AlbumCardSlot>();

        private DiContainer _container;
        private AlbumDragController _drag;
        private ICardAlbum _album;
        private CardArtworkResolver _artwork;
        private CardSetDefinition _set;
        private Tween _slideTween;

        /// <summary>Raised after the page index changes, so the paging buttons can re-enable.</summary>
        public event Action PageChanged;

        public int PageIndex { get; private set; }

        public int PageCount { get; private set; }

        public bool CanGoNext => PageIndex < PageCount - 1;

        public bool CanGoPrevious => PageIndex > 0;

        /// <summary>Guarded, because a zero here divides by zero rather than showing nothing.</summary>
        private int SlotsPerPage => Mathf.Max(1, slotsPerPage);

        [Inject]
        private void Inject(DiContainer container) => _container = container;

        public void Initialize(AlbumDragController drag, ICardAlbum album, CardArtworkResolver artwork)
        {
            _drag = drag;
            _album = album;
            _artwork = artwork;

            EnsureViewportClips();
        }

        /// <summary>
        /// Swaps the strip over to a set and jumps to its first page. Pages and slots are reused
        /// across sets - only their contents change - so flipping down the set list does not
        /// churn through a few hundred objects each time.
        /// </summary>
        public void Show(CardSetDefinition set)
        {
            _set = set;
            PageCount = Mathf.Max(1, Mathf.CeilToInt(set.CardCount / (float)SlotsPerPage));

            EnsurePages(PageCount);
            EnsureSlots(PageCount * SlotsPerPage);

            for (int i = 0; i < _slots.Count; i++)
            {
                AlbumCardSlot slot = _slots[i];

                // The slots past the end of a short set are padding: they hold the grid's shape
                // and take no cards. See AlbumCardSlot.IsUsable.
                if (i >= set.CardCount)
                {
                    slot.MakeUnused();
                    continue;
                }

                slot.Initialize(_drag, set.SetId, i, set);
                RefreshSlot(i);
            }

            GoToPage(0, immediately: true);
        }

        /// <summary>Redraws one slot from what the album says is in it.</summary>
        public void RefreshSlot(int slotIndex)
        {
            if (_set == null || slotIndex < 0 || slotIndex >= _slots.Count)
                return;

            AlbumCardSlot slot = _slots[slotIndex];
            if (!slot.IsUsable)
                return;

            CardRef card = _album.GetPlacement(_set.SetId, slotIndex);

            if (card.IsValid)
                slot.Fill(card, _artwork.Resolve(card));
            else
                slot.Clear();
        }

        public void GoToNextPage() => GoToPage(PageIndex + 1);

        public void GoToPreviousPage() => GoToPage(PageIndex - 1);

        /// <summary>
        /// Slides to a page. Out-of-range indices are ignored rather than wrapped: there is no
        /// page before the first one, and pretending there is would fake a set the player has
        /// already reached the end of.
        /// </summary>
        public void GoToPage(int index, bool immediately = false)
        {
            if (index < 0 || index >= PageCount)
                return;

            PageIndex = index;

            if (_slideTween.isAlive)
                _slideTween.Stop();

            // Read now rather than cached: the window is free to have been resized since the
            // last turn of the page.
            var target = new Vector2(-index * content.rect.width, content.anchoredPosition.y);

            if (immediately || pageDuration <= 0f)
                content.anchoredPosition = target;
            else if (content.anchoredPosition != target)
                _slideTween = Tween.UIAnchoredPosition(content, target, pageDuration, pageEase);

            PageChanged?.Invoke();
        }

        private void EnsurePages(int count)
        {
            while (_pages.Count < count)
            {
                RectTransform page = _container.InstantiatePrefabForComponent<RectTransform>(
                    pagePrefab, content);

                int index = _pages.Count;

                // Anchored to its own slice of the strip: page 0 covers [0,1] - exactly the
                // window - page 1 covers [1,2] and sits immediately off its right edge.
                page.anchorMin = new Vector2(index, 0f);
                page.anchorMax = new Vector2(index + 1f, 1f);
                page.offsetMin = Vector2.zero;
                page.offsetMax = Vector2.zero;

                _pages.Add(page);
            }

            for (int i = 0; i < _pages.Count; i++)
                _pages[i].gameObject.SetActive(i < count);
        }

        private void EnsureSlots(int count)
        {
            while (_slots.Count < count)
            {
                RectTransform page = _pages[_slots.Count / SlotsPerPage];

                AlbumCardSlot slot = _container.InstantiatePrefabForComponent<AlbumCardSlot>(
                    slotPrefab, page);

                _slots.Add(slot);
            }

            for (int i = 0; i < _slots.Count; i++)
                _slots[i].gameObject.SetActive(i < count);
        }

        /// <summary>
        /// Without a mask the strip draws all five pages across the whole album at once, which is
        /// unmistakable but leaves the view unusable. Adding it is cheap and reversible, so the
        /// mask is put on and the omission reported rather than only reported.
        /// </summary>
        private void EnsureViewportClips()
        {
            if (viewport.TryGetComponent(out RectMask2D _))
                return;

            viewport.gameObject.AddComponent<RectMask2D>();

            Debug.LogWarning(
                $"[{nameof(AlbumPageStrip)}] '{viewport.name}' had no {nameof(RectMask2D)}, so " +
                "one was added at runtime. Add it to the prefab - the pages either side of the " +
                "open one are only hidden by that mask.", viewport);
        }

        private void OnDestroy()
        {
            if (_slideTween.isAlive)
                _slideTween.Stop();
        }
    }
}
