using System;
using System.Collections.Generic;
using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using UnityEngine;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Game.Services.Progress
{
    /// <summary>
    /// Watches the album and keeps the permanent tally of completed pages and sets, backed by the
    /// save.
    ///
    /// It listens to <see cref="ICardAlbum.PageChanged"/> and, whenever a set's page becomes
    /// correct in full for the first time, records it and announces it. Recording is one-way: a
    /// page that has ever been completed is never taken off the list, so the events fire exactly
    /// once each and the counts only ever grow.
    /// </summary>
    public class CollectionProgress : ICollectionProgress, IDisposable
    {
        public event Action<string, int> PageCompleted;
        public event Action<string> SetCompleted;
        public event Action Changed;

        private readonly ICardAlbum _album;
        private readonly ICardCatalog _catalog;
        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;

        private HashSet<string> _completedPages;

        [Inject]
        public CollectionProgress(
            ICardAlbum album,
            ICardCatalog catalog,
            ISaveService<GameSave> saveService,
            ISaveCoordinator saveCoordinator)
        {
            _album = album;
            _catalog = catalog;
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;

            // Subscribed here, but the tally is not read until the save is loaded - see Completed.
            // Filing a card is the only thing that raises this, and that cannot happen before the
            // player is in the room, long after the save is in.
            _album.PageChanged += OnPageChanged;
        }

        // Built on first use rather than in the constructor: the container assembles this well
        // before the async save load has run, exactly as LocalCardAlbum does with its pages.
        private HashSet<string> Completed => _completedPages ??= LoadCompleted();

        public int CompletedPageCount => Completed.Count;

        public int CompletedSetCount
        {
            get
            {
                int count = 0;
                foreach (CardSetDefinition set in _catalog.Sets)
                {
                    if (set != null && IsSetCompleted(set.SetId))
                        count++;
                }

                return count;
            }
        }

        public bool IsSetCompleted(string setId)
        {
            CardSetDefinition set = _catalog.FindSet(setId);
            if (set == null)
                return false;

            int pages = AlbumLayout.PageCount(set.CardCount);
            for (int page = 0; page < pages; page++)
            {
                if (!Completed.Contains(PageKey(setId, page)))
                    return false;
            }

            return true;
        }

        public void Dispose()
        {
            _album.PageChanged -= OnPageChanged;
        }

        private void OnPageChanged(string setId)
        {
            CardSetDefinition set = _catalog.FindSet(setId);
            if (set == null)
                return;

            bool anyNewPage = false;
            int pages = AlbumLayout.PageCount(set.CardCount);

            for (int page = 0; page < pages; page++)
            {
                string key = PageKey(setId, page);

                if (Completed.Contains(key) || !IsPageCorrect(setId, page, set.CardCount))
                    continue;

                Completed.Add(key);
                _saveService.CurrentSave.CompletedPages.Add(key);
                anyNewPage = true;

                PageCompleted?.Invoke(setId, page);
            }

            if (!anyNewPage)
                return;

            _saveCoordinator.MarkDirty();
            Changed?.Invoke();

            // Announced after the page events and the flush, so a listener that reacts to a set
            // finishing sees a fully written-through tally.
            if (IsSetCompleted(setId))
                SetCompleted?.Invoke(setId);
        }

        /// <summary>
        /// Whether every card slot on a page currently holds the card that belongs in it. The last
        /// page of a set can be short, so it stops at the set's card count rather than the full
        /// page width.
        /// </summary>
        private bool IsPageCorrect(string setId, int page, int cardCount)
        {
            int start = AlbumLayout.FirstSlotOfPage(page);
            int end = Mathf.Min(start + AlbumLayout.CardsPerPage, cardCount);

            // A page with no slots is not a page the player can complete; guarding it keeps an
            // empty or miscounted set from reporting a phantom completed page.
            if (end <= start)
                return false;

            for (int slot = start; slot < end; slot++)
            {
                if (!_album.GetPlacement(setId, slot).BelongsAt(setId, slot))
                    return false;
            }

            return true;
        }

        private HashSet<string> LoadCompleted()
        {
            GameSave save = _saveService.CurrentSave;
            List<string> saved = save?.CompletedPages;

            // A save written before this feature existed has no list at all. Rather than leave it
            // null (which the recorder would trip over) it is created and seeded with whatever is
            // already correctly filed, silently: those pages were finished before there was a
            // reward to earn, so they must not suddenly pay out on the next card the player moves.
            if (save != null && saved == null)
            {
                saved = save.CompletedPages = new List<string>();
                SeedFromCurrentAlbum(saved);
            }

            return saved == null ? new HashSet<string>() : new HashSet<string>(saved);
        }

        private void SeedFromCurrentAlbum(List<string> into)
        {
            foreach (CardSetDefinition set in _catalog.Sets)
            {
                if (set == null)
                    continue;

                int pages = AlbumLayout.PageCount(set.CardCount);
                for (int page = 0; page < pages; page++)
                {
                    if (IsPageCorrect(set.SetId, page, set.CardCount))
                        into.Add(PageKey(set.SetId, page));
                }
            }
        }

        private static string PageKey(string setId, int page) => $"{setId}#{page}";
    }
}
