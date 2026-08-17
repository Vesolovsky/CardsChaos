using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Input;
using Zenject;

namespace CardsChaos.Cards
{
    public interface ICardInspector
    {
        bool IsInspecting { get; }

        /// <summary>Opens the close-up on the selected card. False when there is nothing to show.</summary>
        bool TryOpen();

        /// <summary>
        /// Steps the close-up onto the neighbouring card in hand, the same move the wheel makes.
        /// False when the close-up is not open - there is nothing to step from.
        /// </summary>
        bool Step(int delta);

        /// <summary>Leaves the close-up and puts the card back in hand. Harmless when not open.</summary>
        void Close();
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
    /// Close-up view of the selected card: clicking a card in hand opens it, clicking the card
    /// itself turns it over, clicking off it leaves, RMB and Escape also leave, the Flip Card
    /// action turns it over from the keyboard, and the wheel swaps to the neighbouring card.
    ///
    /// The left button doing two things by where it lands - flip on the card, close off it -
    /// matches the album's close-up, so a card reads the same however the player opened it.
    ///
    /// While it is open the camera is suspended and the rest of the hand is not interactive, so
    /// this type owns the whole input for as long as it is running.
    /// </summary>
    public class CardInspector : ITickable, ICardInspector, IDisposable
    {
        private const float ScrollDeadzone = 0.01f;

        // The card hangs right in front of the camera, so the ray never has far to travel.
        private const float MaxPickDistance = 50f;

        // The mesh front is +Z and the anchor's +Z points away from the viewer, so the card
        // has to be turned around to face the camera; another half turn shows the back.
        private static readonly Quaternion FaceFront = Quaternion.AngleAxis(180f, Vector3.up);
        private static readonly Quaternion FaceBack = Quaternion.identity;

        private readonly CardHand _hand;
        private readonly ICardCatalog _catalog;
        private readonly ICameraService _cameraService;
        private readonly IWorldInteractionLock _worldLock;
        private readonly ICardInspectLight _light;
        private readonly CardInspectSettings _settings;
        private readonly InputAction _flipCard;

        private Card _card;
        private bool _showingBack;
        private int _openedFrame = -1;
        private System.IDisposable _worldHandle;
        private IDisposable _frontMipRequest;
        private IDisposable _backMipRequest;

        public bool IsInspecting => _card != null;

        [Inject]
        public CardInspector(
            CardHand hand,
            ICardCatalog catalog,
            ICameraService cameraService,
            IWorldInteractionLock worldLock,
            CardInspectSettings settings,
            IInputActions input,
            [InjectOptional] ICardInspectLight light)
        {
            _hand = hand;
            _catalog = catalog;
            _cameraService = cameraService;
            _worldLock = worldLock;
            _settings = settings;
            _flipCard = input.Find(GameInputActions.FlipCard);
            _light = light;
        }

        public bool TryOpen()
        {
            if (IsInspecting)
                return false;

            Card card = _hand.PresentForInspect();
            if (card == null)
                return false;

            _card = card;
            _card.SetInspected(true);
            RequestFullResolution(_card);
            _showingBack = false;
            _openedFrame = Time.frameCount;
            _worldHandle = _worldLock.Acquire(this);
            _light?.Show(_card.FaceLuminance);

            return true;
        }

        public bool Step(int delta)
        {
            if (!IsInspecting || delta == 0)
                return false;

            Switch(delta > 0 ? 1 : -1);
            return true;
        }

        public void Close()
        {
            if (IsInspecting)
                Exit();
        }

        public void Tick()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null || mouse == null || !IsInspecting)
                return;

            // The hand can drop the card underneath us - thrown out from somewhere else, say.
            if (_hand.SelectedCard != _card)
            {
                Exit();
                return;
            }

            // The click that opened the close-up is still being reported this frame. Reading it
            // again would turn the card over the instant it arrived.
            if (Time.frameCount == _openedFrame)
            {
                Drive(mouse);
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            {
                Exit();
                return;
            }

            // The rebindable flip action turns the card over wherever the cursor is.
            if (_flipCard != null && _flipCard.WasPressedThisFrame())
            {
                _showingBack = !_showingBack;
                Drive(mouse);
                return;
            }

            // The left button turns the card over when it lands on the card, and leaves when it
            // lands anywhere else - the card is the thing you are looking at, so clicking off it
            // means you are done.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (PointerOnCard(mouse))
                {
                    _showingBack = !_showingBack;
                }
                else
                {
                    Exit();
                    return;
                }
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > ScrollDeadzone)
            {
                Switch(scroll > 0f ? 1 : -1);
                return;
            }

            Drive(mouse);
        }

        private void Exit()
        {
            if (_card != null)
                _card.SetInspected(false);

            _hand.ReturnFromInspect(_card);
            ReleaseMipRequests();

            _card = null;
            _showingBack = false;

            _worldHandle?.Dispose();
            _worldHandle = null;

            // Switch() routes through here only when the hand ran out, so stepping from one card
            // to the next never flickers the lamps off and back on.
            _light?.Hide();
        }

        private void Switch(int delta)
        {
            _card.SetInspected(false);
            _hand.ReturnFromInspect(_card);
            _hand.SelectNeighbour(delta);

            _showingBack = false;
            _card = _hand.PresentForInspect();

            // Nothing left to look at - fall back out rather than sit in a dead mode.
            if (_card == null)
            {
                Exit();
                return;
            }

            _card.SetInspected(true);
            RequestFullResolution(_card);

            // The new face is its own brightness, so the lamps have to be re-aimed at it. Show()
            // eases across from where they are rather than starting over.
            _light?.Show(_card.FaceLuminance);
        }

        private void RequestFullResolution(Card card)
        {
            ReleaseMipRequests();

            CardIdentity identity = card != null ? card.Identity : null;
            if (identity == null)
                return;

            _frontMipRequest = CardMipStreaming.RequestFullResolution(identity.ArtworkTexture);

            CardSetDefinition set = _catalog.FindSet(identity.SetId);
            _backMipRequest = CardMipStreaming.RequestFullResolution(
                set != null ? set.BackTexture : null);
        }

        private void ReleaseMipRequests()
        {
            _frontMipRequest?.Dispose();
            _frontMipRequest = null;

            _backMipRequest?.Dispose();
            _backMipRequest = null;
        }

        public void Dispose()
        {
            ReleaseMipRequests();
            _worldHandle?.Dispose();
            _worldHandle = null;
        }

        /// <summary>
        /// Whether the cursor is over the card being inspected. The card follows the cursor only
        /// by tilting - it stays centred - so its collider is where the ray looks, and it is a
        /// trigger while held, hence <see cref="QueryTriggerInteraction.Collide"/>.
        /// </summary>
        private bool PointerOnCard(Mouse mouse)
        {
            Ray ray = _cameraService.SceenPointToRay(mouse.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, MaxPickDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            return hit.collider.GetComponentInParent<Card>() == _card;
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
