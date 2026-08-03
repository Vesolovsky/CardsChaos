using System;
using System.Collections.Generic;
using CardsChaos.Cards.Album;
using UnityEngine;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Game.Services.Album
{
    /// <summary>
    /// The album, backed by the local save.
    ///
    /// The in-memory index is the working copy and the save list is rewritten from it after every
    /// move. Rewriting the whole list rather than patching the one row that changed is a few
    /// hundred objects on a move the player makes once every few seconds, and it buys the one
    /// thing worth having here: there is no second code path that could let the index and the
    /// file drift apart.
    /// </summary>
    public class LocalCardAlbum : ICardAlbum
    {
        public event Action<string> PageChanged;

        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;

        private Dictionary<string, Dictionary<int, CardRef>> _pages;

        [Inject]
        public LocalCardAlbum(ISaveService<GameSave> saveService, ISaveCoordinator saveCoordinator)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
        }

        // Built on first use rather than in the constructor: the save is filled in by an async
        // initialization pass, and the container is free to build this well before that has run.
        private Dictionary<string, Dictionary<int, CardRef>> Pages
        {
            get
            {
                if (_pages != null)
                    return _pages;

                // If the async save load has not run yet, serve an empty view but do NOT cache it:
                // caching here would freeze the album empty for the whole session even after the
                // save lands. Rebuild on the next access, once CurrentSave is in.
                if (_saveService.CurrentSave == null)
                    return new Dictionary<string, Dictionary<int, CardRef>>();

                _pages = LoadPages();
                return _pages;
            }
        }

        public CardRef GetPlacement(string pageSetId, int slotIndex)
        {
            return Pages.TryGetValue(pageSetId, out Dictionary<int, CardRef> slots)
                   && slots.TryGetValue(slotIndex, out CardRef card)
                ? card
                : CardRef.None;
        }

        public void Place(string pageSetId, int slotIndex, CardRef card)
        {
            if (!card.IsValid)
            {
                Debug.LogError($"[LocalCardAlbum] Refused to place an empty card in '{pageSetId}' slot {slotIndex}.");
                return;
            }

            if (slotIndex < 0)
            {
                Debug.LogError($"[LocalCardAlbum] Slot {slotIndex} is not a slot.");
                return;
            }

            if (!Pages.TryGetValue(pageSetId, out Dictionary<int, CardRef> slots))
            {
                slots = new Dictionary<int, CardRef>();
                Pages[pageSetId] = slots;
            }

            if (slots.TryGetValue(slotIndex, out CardRef occupant))
            {
                // Displacing silently would leave the old card nowhere - not in the album, not in
                // the player's hand. The caller is expected to Take() first and deal with it.
                Debug.LogError(
                    $"[LocalCardAlbum] '{pageSetId}' slot {slotIndex} already holds {occupant}; " +
                    $"{card} was not placed.");

                return;
            }

            slots[slotIndex] = card;
            Flush(pageSetId);
        }

        public CardRef Take(string pageSetId, int slotIndex)
        {
            if (!Pages.TryGetValue(pageSetId, out Dictionary<int, CardRef> slots)
                || !slots.Remove(slotIndex, out CardRef card))
            {
                return CardRef.None;
            }

            // An emptied page is dropped so it stops being written out, and so a set the player
            // has never touched and one they have emptied look the same on reload.
            if (slots.Count == 0)
                Pages.Remove(pageSetId);

            Flush(pageSetId);
            return card;
        }

        public int CountCorrect(string pageSetId)
        {
            if (!Pages.TryGetValue(pageSetId, out Dictionary<int, CardRef> slots))
                return 0;

            int correct = 0;
            foreach (KeyValuePair<int, CardRef> slot in slots)
            {
                if (slot.Value.BelongsAt(pageSetId, slot.Key))
                    correct++;
            }

            return correct;
        }

        private Dictionary<string, Dictionary<int, CardRef>> LoadPages()
        {
            var pages = new Dictionary<string, Dictionary<int, CardRef>>();
            List<AlbumPlacement> saved = _saveService.CurrentSave?.Album;

            if (saved == null)
                return pages;

            foreach (AlbumPlacement placement in saved)
            {
                if (placement == null || string.IsNullOrEmpty(placement.PageSetId))
                    continue;

                var card = new CardRef(placement.CardSetId, placement.CardNumber);
                if (!card.IsValid)
                {
                    Debug.LogWarning(
                        $"[LocalCardAlbum] Dropped an unreadable saved placement on page " +
                        $"'{placement.PageSetId}' slot {placement.Slot}.");

                    continue;
                }

                if (!pages.TryGetValue(placement.PageSetId, out Dictionary<int, CardRef> slots))
                {
                    slots = new Dictionary<int, CardRef>();
                    pages[placement.PageSetId] = slots;
                }

                // Two rows for one slot means a corrupt or hand-edited file. Keeping the first is
                // arbitrary but stable, and losing the duplicate is better than throwing here.
                if (!slots.TryAdd(placement.Slot, card))
                {
                    Debug.LogWarning(
                        $"[LocalCardAlbum] Save has two cards in '{placement.PageSetId}' slot " +
                        $"{placement.Slot}; kept {slots[placement.Slot]}, dropped {card}.");
                }
            }

            return pages;
        }

        private void Flush(string pageSetId)
        {
            GameSave save = _saveService.CurrentSave;
            if (save == null)
            {
                Debug.LogError("[LocalCardAlbum] There is no save to write the album into.");
                return;
            }

            List<AlbumPlacement> placements = save.Album ??= new List<AlbumPlacement>();
            placements.Clear();

            foreach (KeyValuePair<string, Dictionary<int, CardRef>> page in Pages)
            {
                foreach (KeyValuePair<int, CardRef> slot in page.Value)
                {
                    placements.Add(new AlbumPlacement
                    {
                        PageSetId = page.Key,
                        Slot = slot.Key,
                        CardSetId = slot.Value.SetId,
                        CardNumber = slot.Value.Number,
                    });
                }
            }

            _saveCoordinator.MarkDirty();
            PageChanged?.Invoke(pageSetId);
        }
    }
}
