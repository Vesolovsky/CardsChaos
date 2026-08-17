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
    /// picks a card up off the floor or opens the one in hand, Throw discards the selected card (or
    /// stores it while a container slot is targeted), Toggle Hand spreads the hand out and the
    /// wheel walks through it.
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

        // Optional: the duplicate rewards live in the game module, and the card table is expected
        // to work in a test scene that has no upgrade system at all.
        private readonly IDuplicateCards _duplicates;

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
            IInputActions input,
            [InjectOptional] IDuplicateCards duplicates)
        {
            _cameraService = cameraService;
            _hand = hand;
            _inspector = inspector;
            _outline = outline;
            _worldLock = worldLock;
            _duplicates = duplicates;

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
            bool interactPressed = _interact != null && _interact.WasPressedThisFrame();
            bool throwPressed = _throw != null && _throw.WasPressedThisFrame();

            Ray cursorRay = !pointerOverUi
                ? _cameraService.SceenPointToRay(mouse.position.ReadValue())
                : default;
            RaycastHit cursorHit = default;
            bool hasWorldHit = !pointerOverUi && TryFindWorldHit(cursorRay, out cursorHit);
            Card cursorCard = hasWorldHit
                ? cursorHit.collider.GetComponentInParent<Card>()
                : null;

            bool containerOwnsPointer = false;
            Card selected = _hand.SelectedCard;

            if (!pointerOverUi && selected != null)
            {
                // The open part of a basket has no collider of its own. Its component intersects
                // the same cursor ray at each stack's actual landing height, while the ordinary
                // physics hit guards against selecting a container hidden behind another object.
                // A hit on the basket or one of its child cards belongs to that basket and is not
                // an obstruction.
                float obstructionDistance = hasWorldHit
                    ? cursorHit.distance
                    : float.PositiveInfinity;
                CardStackContainer obstructionOwner = hasWorldHit
                    ? cursorHit.collider.GetComponentInParent<CardStackContainer>()
                    : null;

                if (CardStackContainer.TryFindTarget(
                        cursorRay,
                        obstructionDistance,
                        obstructionOwner,
                        out CardStackContainer container,
                        out CardStackContainer.SlotTarget slot))
                {
                    containerOwnsPointer = true;

                    // A stack remains an ordinary source of cards even while another card is
                    // selected in hand. Physics gives us its topmost collider, so LMB naturally
                    // peels the stack one card at a time instead of the container swallowing the
                    // click after the first pickup.
                    bool pointsAtStoredCard = cursorCard != null &&
                                              !cursorCard.IsHeld &&
                                              cursorCard.transform.IsChildOf(container.transform);
                    Aim(pointsAtStoredCard ? cursorCard : null);

                    bool pickedFromStack = false;
                    if (!throwPressed && interactPressed && pointsAtStoredCard)
                    {
                        pickedFromStack = _hand.PickUp(cursorCard);
                        if (pickedFromStack)
                            Aim(null);
                    }

                    if (slot.IsValid)
                    {
                        // F is contextual: over a legal container slot it stores the selected
                        // card; everywhere else the same action keeps its ordinary throw meaning.
                        if (throwPressed)
                            container.TryStore(_hand, selected, slot);
                        else if (!pickedFromStack)
                            container.ShowGhost(selected, slot, _cameraService.MainCamera);
                    }
                }
            }

            if (!containerOwnsPointer)
                Aim(pointerOverUi ? null : cursorCard);

            if (!containerOwnsPointer && interactPressed && _target != null)
            {
                if (_target.IsHeld)
                    _inspector.TryOpen();
                else if (_hand.PickUp(_target))
                    Aim(null);
            }

            if (throwPressed && !containerOwnsPointer)
            {
                // A free throw of a duplicate is filed for the player once the reward is owned -
                // but only a free one. Aiming at a box is handled above, and there the player is
                // choosing the slot themselves, which is a choice the reward must not take away.
                if (_duplicates == null || !_duplicates.TryAutoStore(_hand, _hand.SelectedCard))
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

        private static bool TryFindWorldHit(Ray ray, out RaycastHit hit)
        {
            // Triggers count here: a card in hand is one, so that it can be pointed at without
            // barging the floor around as it rides along with the camera.
            return Physics.Raycast(
                ray,
                out hit,
                MaxPickDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
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
