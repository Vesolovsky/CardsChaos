using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Zenject;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Mouse and keyboard driver for everything outside the close-up: LMB either picks a card up
    /// off the floor or opens the one in hand, F throws the selected card away, TAB spreads the
    /// hand out and the wheel walks through it.
    ///
    /// A card in hand can be chosen two ways - pointed at, or reached with the wheel - and the
    /// choice sticks either way, so the cursor is free to wander off without the hand forgetting
    /// what was picked.
    /// </summary>
    public class CardInputController : ITickable
    {
        private const float MaxPickDistance = 50f;
        private const float ScrollDeadzone = 0.01f;

        private readonly ICameraService _cameraService;
        private readonly CardHand _hand;
        private readonly ICardInspector _inspector;

        private Card _target;
        private Card _outlined;

        [Inject]
        public CardInputController(ICameraService cameraService, CardHand hand, ICardInspector inspector)
        {
            _cameraService = cameraService;
            _hand = hand;
            _inspector = inspector;
        }

        public void Tick()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (mouse == null || keyboard == null)
                return;

            // The inspector owns the mouse while it is open. This runs before it (see the
            // execution order in CardsInstaller), so the click that closes the inspector is
            // swallowed here instead of also grabbing whatever sits under the cursor.
            if (_inspector.IsInspecting)
            {
                Aim(null);
                return;
            }

            // The right button is the camera's. While it is down the pointer is parked in the
            // middle of the screen, so anything it appears to be over is an accident.
            if (mouse.rightButton.isPressed)
            {
                Aim(null);
                return;
            }

            Aim(FindCardUnderCursor(mouse));

            if (mouse.leftButton.wasPressedThisFrame && _target != null)
            {
                if (_target.IsHeld)
                    _inspector.TryOpen();
                else
                    _hand.PickUp(_target);
            }

            if (keyboard.fKey.wasPressedThisFrame)
            {
                _hand.ThrowSelected();

                // The thrown card is very likely still under the cursor, and it drops its own
                // outline on the way out. Forgetting it here is what lets the next frame notice
                // it again and light it back up as a card on the floor.
                Aim(null);
            }

            if (keyboard.tabKey.wasPressedThisFrame)
                _hand.ToggleLayout();

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > ScrollDeadzone)
            {
                // Read the way a stack of paper is thumbed through: pushing the wheel away sends
                // the card on top under the rest and brings up the next one.
                _hand.Step(scroll > 0f ? 1 : -1);
            }
        }

        private Card FindCardUnderCursor(Mouse mouse)
        {
            Ray ray = _cameraService.SceenPointToRay(mouse.position.ReadValue());

            // Triggers count here: a card in hand is one, so that it can be pointed at without
            // barging the floor around as it rides along with the camera.
            if (!Physics.Raycast(ray, out RaycastHit hit, MaxPickDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide))
                return null;

            return hit.collider.GetComponentInParent<Card>();
        }

        private void Aim(Card card)
        {
            _target = card;

            // Cards in hand wear the ring for as long as they stay selected, and the hand puts it
            // there. Only the floor is lit by the cursor alone, and only while it rests on it.
            Card floorCard = card != null && !card.IsHeld ? card : null;

            if (_outlined != floorCard)
            {
                if (_outlined != null)
                    _outlined.SetHighlight(CardHighlight.None);

                _outlined = floorCard;

                if (_outlined != null)
                    _outlined.SetHighlight(CardHighlight.Hovered);
            }

            // Pointing at a card in hand claims the selection. Pointing away deliberately does
            // not give it back, so a card reached with the wheel survives the cursor drifting off
            // it on the way to pressing F.
            if (card != null && card.IsHeld)
                _hand.Select(card);
        }
    }
}
