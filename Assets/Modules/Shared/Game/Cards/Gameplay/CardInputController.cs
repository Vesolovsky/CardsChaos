using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Input;
using Zenject;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Mouse and keyboard driver for everything outside the close-up: the Interact action either
    /// picks a card up off the floor or opens the one in hand, Throw discards the selected card,
    /// Toggle Hand spreads the hand out and the wheel walks through it.
    ///
    /// A fanned-out card can be chosen two ways - pointed at, or reached with the wheel - and the
    /// choice sticks either way, so the cursor is free to wander off without the hand forgetting
    /// what was picked. A pile is different: its top card is always the one in play, so the cursor
    /// cannot pick a card out of it at all - only the wheel, turning the stack over, can. Either
    /// way clicking a held card opens the close-up on whatever the selection is.
    /// </summary>
    public class CardInputController : ITickable
    {
        private const float MaxPickDistance = 50f;
        private const float ScrollDeadzone = 0.01f;

        private readonly ICameraService _cameraService;
        private readonly CardHand _hand;
        private readonly ICardInspector _inspector;
        private readonly ICardOutlinePresenter _outline;
        private readonly IWorldInteractionLock _worldLock;

        private readonly InputAction _throw;
        private readonly InputAction _toggleHand;
        private readonly InputAction _interact;

        private Card _target;
        private Card _outlined;

        [Inject]
        public CardInputController(
            ICameraService cameraService,
            CardHand hand,
            ICardInspector inspector,
            ICardOutlinePresenter outline,
            IWorldInteractionLock worldLock,
            IInputActions input)
        {
            _cameraService = cameraService;
            _hand = hand;
            _inspector = inspector;
            _outline = outline;
            _worldLock = worldLock;

            _throw = input.Find(GameInputActions.Throw);
            _toggleHand = input.Find(GameInputActions.ToggleHand);
            _interact = input.Find(GameInputActions.Interact);
        }

        public void Tick()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                Aim(null);
                return;
            }

            // Whoever holds the room owns the mouse - the close-up, the album. This runs before
            // the close-up does (see the execution order in CardsInstaller), so the click that
            // closes it is swallowed here instead of also grabbing whatever sits under the cursor.
            if (_worldLock.IsLocked)
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

            // A HUD button (a skill icon, say) can sit right over a floor card. When the pointer
            // rests on an interactable UI element the click belongs to the button, so aim at nothing:
            // that stops the pickup below - it needs a target - and hides the card's hover outline,
            // while Throw, Toggle Hand and the wheel still work off the keyboard as before.
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Aim(pointerOverUi ? null : FindCardUnderCursor(mouse));

            if (_interact != null && _interact.WasPressedThisFrame() && _target != null)
            {
                if (_target.IsHeld)
                    _inspector.TryOpen();
                else if (_hand.PickUp(_target))
                    Aim(null);
            }

            if (_throw != null && _throw.WasPressedThisFrame())
            {
                _hand.ThrowSelected();

                // The thrown card is very likely still under the cursor. Forgetting it here lets
                // the next frame notice it again as a floor card instead of retaining stale aim.
                Aim(null);
            }

            if (_toggleHand != null && _toggleHand.WasPressedThisFrame())
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

            // A card in hand communicates selection by being lifted out of the pile. The outline
            // is a floor-only affordance and exists only while the cursor rests on that card.
            Card floorCard = card != null && !card.IsHeld ? card : null;

            if (_outlined != floorCard)
            {
                _outlined = floorCard;

                if (_outlined != null)
                    _outline.SetTarget(_outlined);
                else
                    _outline.Clear();
            }
            else if (floorCard == null)
            {
                // Keep the presenter authoritative even if its target was changed or invalidated
                // outside this controller between two frames with no floor card under the cursor.
                _outline.Clear();
            }

            // Pointing at a fanned-out card claims the selection. Pointing away deliberately does
            // not give it back, so a card reached with the wheel survives the cursor drifting off
            // it on the way to pressing F. In the pile the cursor has no say at all - the top card
            // is always the selected one, and only the wheel changes which card that is.
            if (card != null && card.IsHeld && _hand.Layout == CardHandLayout.Fan)
                _hand.Select(card);
        }
    }
}
