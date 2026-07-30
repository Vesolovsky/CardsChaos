using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Services.Pause;
using Vesolovsky.Game.Services.Skills;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The pause menu. Its contents are wired elsewhere; this half is how Escape brings it up and
    /// takes it away, and what pausing does to the rest of the game.
    ///
    /// Escape is shared. While the player is inside something that owns it - a card close-up, the
    /// album, the upgrades screen - Escape backs out of that, and only once none of them holds the
    /// room does Escape mean "pause". The world-interaction lock is that "something owns the room"
    /// signal, so the pause comes up only while it is free; and it is read a frame in arrears as
    /// well, so the very Escape that closes a section can never also raise the pause behind it.
    ///
    /// While it is up it takes the room itself - freezing the camera and the card table and shutting
    /// the album and upgrades screen out - silences the skills, and stops the clock so cooldowns
    /// hold exactly where they were.
    /// </summary>
    public class PauseView : View<IPauseViewModel>
    {
        [Tooltip("Closes the pause menu. Escape does the same thing.")]
        [SerializeField] private VButton resumeButton;

        private IWorldInteractionLock _worldLock;
        private ISkillGate _skillGate;
        private IPauseState _pauseState;

        private IDisposable _worldHandle;
        private bool _isOpen;

        // The room was free on the previous frame. Required as well as "free now" so a section that
        // releases the room on this very Escape does not hand the same press straight to the pause.
        private bool _worldFreeLastFrame = true;

        [Inject]
        private void InjectPause(
            IWorldInteractionLock worldLock,
            [InjectOptional] ISkillGate skillGate,
            [InjectOptional] IPauseState pauseState)
        {
            _worldLock = worldLock;
            _skillGate = skillGate;
            _pauseState = pauseState;
        }

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            if (resumeButton != null)
                resumeButton.Bind(Close);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            bool worldFree = _worldLock == null || !_worldLock.IsLocked;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_isOpen)
                    Close();
                else if (worldFree && _worldFreeLastFrame)
                    Open();
            }

            _worldFreeLastFrame = worldFree;
        }

        /// <summary>Brings the pause menu up and pauses the game behind it.</summary>
        public void Open()
        {
            if (_isOpen)
                return;

            _isOpen = true;

            // Take the room the way the album and upgrades screen do, so the camera, the cards and
            // both of those panels fall quiet behind the menu. The gate silences even the skills
            // that ignore the room lock, and the pause state stops the clock.
            _worldHandle = _worldLock?.Acquire(this);

            if (_skillGate != null)
                _skillGate.Blocked = true;

            if (_pauseState != null)
                _pauseState.IsPaused = true;

            Show(destroyCancellationToken).Forget();
        }

        /// <summary>Takes the pause menu away and hands the game back. Wired to the Resume button.</summary>
        public void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            ReleaseRoom();

            Hide(destroyCancellationToken).Forget();
        }

        private void ReleaseRoom()
        {
            _worldHandle?.Dispose();
            _worldHandle = null;

            if (_skillGate != null)
                _skillGate.Blocked = false;

            if (_pauseState != null)
                _pauseState.IsPaused = false;
        }

        protected override void OnDestroy()
        {
            // A menu torn down while it is up must not leave the room locked, the skills gated or
            // the clock stopped.
            if (_isOpen)
                ReleaseRoom();

            base.OnDestroy();
        }
    }
}
