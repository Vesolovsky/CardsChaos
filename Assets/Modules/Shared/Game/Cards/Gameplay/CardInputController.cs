using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Zenject;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Mouse driver for the card table: hover highlights, LMB picks up,
    /// RMB throws the selected card, scroll moves the selection.
    /// </summary>
    public class CardInputController : ITickable
    {
        private const float MaxPickDistance = 50f;
        private const float ScrollDeadzone = 0.01f;

        private readonly ICameraService _cameraService;
        private readonly CardHand _hand;
        private readonly ICardInspector _inspector;

        private Card _hovered;

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
            if (mouse == null)
                return;

            // The inspector owns the mouse while it is open. This runs before it (see the
            // execution order in CardsInstaller), so the click that closes the inspector is
            // swallowed here instead of also picking up whatever sits under the cursor.
            if (_inspector.IsInspecting)
            {
                SetHovered(null);
                return;
            }

            SetHovered(FindCardUnderCursor(mouse));

            if (mouse.leftButton.wasPressedThisFrame && _hovered != null)
            {
                Card card = _hovered;
                SetHovered(null);
                _hand.PickUp(card);
            }

            if (mouse.rightButton.wasPressedThisFrame)
                _hand.ThrowSelected();

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > ScrollDeadzone)
                _hand.MoveSelection(scroll > 0f ? -1 : 1);
        }

        private Card FindCardUnderCursor(Mouse mouse)
        {
            Ray ray = _cameraService.SceenPointToRay(mouse.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, MaxPickDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return null;

            Card card = hit.collider.GetComponentInParent<Card>();
            return card != null && !card.IsHeld ? card : null;
        }

        private void SetHovered(Card card)
        {
            if (_hovered == card)
                return;

            if (_hovered != null)
                _hovered.SetHighlight(CardHighlight.None);

            _hovered = card;

            if (_hovered != null)
                _hovered.SetHighlight(CardHighlight.Hovered);
        }
    }
}
