using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Zenject;

namespace CardsChaos.Cards
{
    public interface ICardInspector
    {
        bool IsInspecting { get; }
    }

    [System.Serializable]
    public class CardInspectSettings
    {
        [Tooltip("Degrees of tilt when the cursor sits at the very edge of the screen.")]
        public float Tilt = 12f;

        [Tooltip("How sharply the card chases the cursor and settles into the inspect pose.")]
        public float Smoothing = 14f;
    }

    /// <summary>
    /// Close-up view of the selected card: E opens it, LMB closes it, RMB turns it over and
    /// the cursor tilts it. Scrolling swaps to the neighbouring card without leaving.
    ///
    /// While it is open the camera is suspended and the table is not interactive, so this
    /// type owns the whole input for as long as it is running.
    /// </summary>
    public class CardInspector : ITickable, ICardInspector
    {
        private const float ScrollDeadzone = 0.01f;

        // The mesh front is +Z and the anchor's +Z points away from the viewer, so the card
        // has to be turned around to face the camera; another half turn shows the back.
        private static readonly Quaternion FaceFront = Quaternion.AngleAxis(180f, Vector3.up);
        private static readonly Quaternion FaceBack = Quaternion.identity;

        private readonly CardHand _hand;
        private readonly ICameraPanControl _cameraPan;
        private readonly CardInspectSettings _settings;

        private Card _card;
        private bool _showingBack;

        public bool IsInspecting => _card != null;

        [Inject]
        public CardInspector(CardHand hand, ICameraPanControl cameraPan, CardInspectSettings settings)
        {
            _hand = hand;
            _cameraPan = cameraPan;
            _settings = settings;
        }

        public void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null || mouse == null)
                return;

            if (!IsInspecting)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                    Enter();

                return;
            }

            // The hand can drop the card underneath us - thrown out to make room, say.
            if (_hand.SelectedCard != _card)
            {
                Exit();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                Exit();
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
                _showingBack = !_showingBack;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > ScrollDeadzone)
            {
                Switch(scroll > 0f ? -1 : 1);
                return;
            }

            Drive(mouse);
        }

        private void Enter()
        {
            Card card = _hand.PresentForInspect();
            if (card == null)
                return;

            _card = card;
            _card.SetInspected(true);
            _showingBack = false;
            _cameraPan.Enabled = false;
        }

        private void Exit()
        {
            if (_card != null)
                _card.SetInspected(false);

            _hand.ReturnFromInspect(_card);

            _card = null;
            _showingBack = false;
            _cameraPan.Enabled = true;
        }

        private void Switch(int delta)
        {
            _card.SetInspected(false);
            _hand.ReturnFromInspect(_card);
            _hand.MoveSelection(delta);

            _showingBack = false;
            _card = _hand.PresentForInspect();

            // Nothing left to look at - fall back out rather than sit in a dead mode.
            if (_card == null)
            {
                Exit();
                return;
            }

            _card.SetInspected(true);
        }

        private void Drive(Mouse mouse)
        {
            Transform card = _card.transform;

            Vector2 offset = ScreenOffset(mouse);
            Quaternion target =
                Quaternion.Euler(-offset.y * _settings.Tilt, offset.x * _settings.Tilt, 0f)
                * (_showingBack ? FaceBack : FaceFront);

            // Framerate independent approach, same easing shape as the camera pan.
            float t = 1f - Mathf.Exp(-_settings.Smoothing * Time.deltaTime);

            card.localPosition = Vector3.Lerp(card.localPosition, Vector3.zero, t);
            card.localRotation = Quaternion.Slerp(card.localRotation, target, t);
        }

        /// <summary>Cursor position as -1..1 from the centre of the screen.</summary>
        private static Vector2 ScreenOffset(Mouse mouse)
        {
            var half = new Vector2(Screen.width, Screen.height) * 0.5f;
            if (half.x <= 0f || half.y <= 0f)
                return Vector2.zero;

            Vector2 position = mouse.position.ReadValue();

            return Vector2.ClampMagnitude(
                new Vector2((position.x - half.x) / half.x, (position.y - half.y) / half.y), 1f);
        }
    }
}
