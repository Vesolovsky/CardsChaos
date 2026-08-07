using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Game.Letters
{
    public interface ILetterCollection
    {
        /// <summary>Whether this letter has already been read in this or a past session.</summary>
        bool IsCollected(LetterId id);

        /// <summary>Records a letter as read. Persisted in the save; a no-op on an already-known id.</summary>
        void MarkCollected(LetterId id);
    }

    /// <summary>
    /// Remembers which letters the player has read, across sessions. The list itself lives in the
    /// save (<see cref="GameSave.CollectedLetters"/>, stored by <see cref="LetterId"/> name for a
    /// readable file); this is the thin service the letter feature reads and writes it through, plus
    /// the load-time pass that switches off any letter a past session already read, so the room comes
    /// back the way it was left.
    /// </summary>
    public class LetterCollection : ILetterCollection, IInitializable, IDisposable
    {
        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly CancellationTokenSource _applyCts = new CancellationTokenSource();

        [Inject]
        public LetterCollection(ISaveService<GameSave> saveService, ISaveCoordinator saveCoordinator)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
        }

        public void Initialize()
        {
            // The save loads asynchronously and is not in yet at Zenject init time, the same as the
            // world restore - wait for it, then hide the letters already read.
            HideCollectedWhenLoaded(_applyCts.Token).Forget();
        }

        public void Dispose()
        {
            _applyCts.Cancel();
            _applyCts.Dispose();
        }

        public bool IsCollected(LetterId id)
        {
            List<string> collected = Collected;
            return collected != null && collected.Contains(Key(id));
        }

        public void MarkCollected(LetterId id)
        {
            List<string> collected = Collected;
            string key = Key(id);
            if (collected == null || collected.Contains(key))
                return;

            collected.Add(key);
            _saveCoordinator.MarkDirty();
        }

        private async UniTask HideCollectedWhenLoaded(CancellationToken token)
        {
            bool canceled = await UniTask
                .WaitUntil(() => _saveService?.CurrentSave != null, cancellationToken: token)
                .SuppressCancellationThrow();

            if (canceled)
                return;

            var seen = new HashSet<LetterId>();
            foreach (Letter letter in UnityEngine.Object.FindObjectsByType<Letter>(FindObjectsSortMode.None))
            {
                if (letter == null)
                    continue;

                // The save keys reads by id, so two letters sharing one would be read as a single
                // letter - reading either marks both. Surface the clash loudly rather than let it
                // silently swallow a note.
                if (!seen.Add(letter.Id))
                {
                    Debug.LogWarning(
                        $"[{nameof(LetterCollection)}] More than one letter uses the id '{letter.Id}'; " +
                        "reading one would mark them all read. Give each letter its own LetterId.", letter);
                }

                if (IsCollected(letter.Id))
                    letter.Collect();
            }
        }

        // Stored by enum name rather than number: the file stays readable, and the name is stable
        // across enum reordering (only a deliberate rename moves it).
        private static string Key(LetterId id) => id.ToString();

        /// <summary>
        /// The live collected-letter list in the save, or null before the save has loaded. Created on
        /// first use for a save written before letters existed - the same lazy-fill the album and the
        /// stats tally use - so an old save simply starts with nothing read.
        /// </summary>
        private List<string> Collected
        {
            get
            {
                GameSave save = _saveService.CurrentSave;
                if (save == null)
                    return null;

                return save.CollectedLetters ??= new List<string>();
            }
        }
    }
}
