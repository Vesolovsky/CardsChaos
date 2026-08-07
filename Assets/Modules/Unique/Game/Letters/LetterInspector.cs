using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.Services;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Views;
using Zenject;

namespace Vesolovsky.Game.Letters
{
    public interface ILetterInspector
    {
        bool IsInspecting { get; }

        /// <summary>Opens the read mode on a letter. False when one is already open, or on a null letter.</summary>
        bool TryOpen(Letter letter);
    }

    /// <summary>
    /// The read-a-letter mode. Opening it suspends the room - it holds the world-interaction lock, so
    /// the camera stops and cards cannot be picked up - switches the physical letter off (once read a
    /// letter is gone), and spawns the <see cref="LetterView"/> with the letter's story, its author's
    /// signature and their handwriting. Escape is the only way out; every other click or key while it
    /// is open is deliberately ignored, so nothing in the room reacts behind the note.
    /// </summary>
    public class LetterInspector : ITickable, ILetterInspector, IDisposable
    {
        private readonly IWorldInteractionLock _worldLock;
        private readonly ILetterCollection _collection;
        private readonly LetterSettings _settings;
        private readonly ISceneViewsService _sceneViews;
        private readonly DynamicViewsCanvas _canvas;

        private Letter _letter;
        private LetterView _view;
        private IDisposable _worldHandle;
        private int _openedFrame = -1;

        // Bumped on every open and every close. A view still loading when its session ends sees the
        // number has moved and unloads itself on arrival instead of lingering on screen.
        private int _generation;

        // Set from the view's backdrop click (off the UI event), acted on in Tick so the close runs
        // on our own update rather than re-entrant inside the view's click handler.
        private bool _closeRequested;

        public bool IsInspecting => _letter != null;

        [Inject]
        public LetterInspector(
            IWorldInteractionLock worldLock,
            ILetterCollection collection,
            LetterSettings settings,
            ISceneViewsService sceneViews,
            [InjectOptional] DynamicViewsCanvas canvas)
        {
            _worldLock = worldLock;
            _collection = collection;
            _settings = settings;
            _sceneViews = sceneViews;
            _canvas = canvas;
        }

        public bool TryOpen(Letter letter)
        {
            if (IsInspecting || letter == null)
                return false;

            _letter = letter;
            _openedFrame = Time.frameCount;
            _closeRequested = false;

            // Remember this letter as read and take it out of the room. Marked before the object is
            // switched off, while its id is still readable.
            _collection.MarkCollected(letter.Id);
            letter.Collect();

            // Take the room, so the camera and the card table fall quiet behind the open letter.
            _worldHandle = _worldLock.Acquire(this);

            // Resolve who left it: their name signs the note, their handwriting is the font.
            LetterAuthorInfo authorInfo = _settings?.GetAuthor(letter.Author);
            string signature = authorInfo != null ? authorInfo.DisplayName : string.Empty;
            TMP_FontAsset font = authorInfo != null ? authorInfo.Font : null;

            var initData = new LetterViewModelInitData(letter.Body, signature, font, RequestClose);
            OpenViewAsync(++_generation, initData).Forget();

            return true;
        }

        public void Tick()
        {
            if (!IsInspecting)
                return;

            // The click that opened the letter is still being reported this frame; ignore this frame
            // entirely so that same press cannot also be read as a close.
            if (Time.frameCount == _openedFrame)
                return;

            // Escape, or a click on the dimmed backdrop, closes the letter. Nothing else is read: a
            // click anywhere else in the room does nothing while a letter is open.
            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;

            if (escape || _closeRequested)
                Exit();
        }

        // Handed to the view as its close callback; the view raises it from the backdrop click. Only
        // flags the intent - Tick does the actual close on our own update.
        private void RequestClose() => _closeRequested = true;

        private async UniTaskVoid OpenViewAsync(int generation, LetterViewModelInitData initData)
        {
            if (_sceneViews == null || _canvas == null)
            {
                Debug.LogError(
                    $"[{nameof(LetterInspector)}] No DynamicViewsCanvas or scene views service; the " +
                    "letter view cannot be shown. The letter is still marked read - press Escape to " +
                    "hand the room back.");

                return;
            }

            var definition = new LetterViewDefinition { ViewModelInitData = initData };

            // AddView does not hand back the view it makes, so note which LetterViews exist before
            // the spawn and take whichever one is new afterwards.
            var before = new HashSet<IView>(_sceneViews.LoadedViews);
            await _sceneViews.AddView(definition, _canvas.transform, throughQueue: false);

            LetterView created = null;
            foreach (IView view in _sceneViews.LoadedViews)
            {
                if (view is LetterView letterView && !before.Contains(letterView))
                {
                    created = letterView;
                    break;
                }
            }

            if (created == null)
                return;

            // Closed (or superseded by another letter) while this one was still loading - drop it.
            if (generation != _generation)
            {
                created.Unload().Forget();
                return;
            }

            _view = created;
        }

        private void Exit()
        {
            // Invalidate any view still loading for this session so it unloads itself on arrival.
            _generation++;

            _letter = null;
            _closeRequested = false;

            if (_view != null)
            {
                _view.Unload().Forget();
                _view = null;
            }

            _worldHandle?.Dispose();
            _worldHandle = null;
        }

        public void Dispose()
        {
            _generation++;

            if (_view != null)
            {
                _view.Unload().Forget();
                _view = null;
            }

            _worldHandle?.Dispose();
            _worldHandle = null;
        }
    }
}
