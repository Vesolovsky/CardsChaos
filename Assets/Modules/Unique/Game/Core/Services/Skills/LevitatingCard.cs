using CardsChaos.Cards;
using UnityEngine;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Runs one card's spell of levitation: it rises off the table, turns to face the camera and
    /// hovers there, and then - unless the player picks it up first - falls back down under gravity.
    ///
    /// Added to a card by <see cref="LevitateSkill"/> and driven from its own Update, so each raised
    /// card keeps its own timer and its own facing. It drives the transform directly while the body
    /// sits kinematic (see <see cref="Card.BeginLevitate"/>); the moment the hand claims the card it
    /// bows out and lets the hand take over, and when its time runs out it hands the card to
    /// <see cref="Card.BeginFlight"/> and removes itself.
    /// </summary>
    [AddComponentMenu("")]
    public class LevitatingCard : MonoBehaviour
    {
        private Card _card;
        private Camera _camera;
        private LevitateSettings _settings;

        private Vector3 _restPosition;
        private float _hoverHeight;
        private float _elapsed;
        private bool _running;

        /// <summary>
        /// Begins the float. Safe to call once per card - the skill checks a card is not already
        /// levitating before adding this. The camera is the one the card turns to face;
        /// <paramref name="hoverHeight"/> is the world Y the card settles at, shared across the cast
        /// so every raised card floats on one line regardless of the height it started from.
        /// </summary>
        public void Begin(Card card, Camera camera, LevitateSettings settings, float hoverHeight)
        {
            _card = card;
            _camera = camera;
            _settings = settings;
            _hoverHeight = hoverHeight;

            if (_card == null || _settings == null)
            {
                Destroy(this);
                return;
            }

            // Raised out of a house of cards: bring the rest of it down now - the very collapse a
            // pickup fires - and cut this card loose from the house. The card holding the house up
            // still levitates; it just takes the house down with it.
            if (_card.House != null)
            {
                _card.House.OnMemberPickedUp(_card, HouseCollapseCause.Levitate);
                _card.House = null;
            }

            // Float free of any parent (a house root, say) so the rise and the later fall move a
            // plain scene card, the way a thrown or collapsed card lives.
            if (_card.transform.parent != null)
                _card.transform.SetParent(null, worldPositionStays: true);

            _restPosition = _card.transform.position;
            _elapsed = 0f;
            _running = true;

            // Hand the card into its floating physics state up front, so the raise below moves a
            // kinematic body the caller owns rather than fighting a resting static collider.
            _card.BeginLevitate();
        }

        private void Update()
        {
            if (!_running)
                return;

            // The hand grabbed it: AttachTo already took the body and reparented the card, so stop
            // touching the transform this instant and let the hand drive it.
            if (_card == null || _card.IsHeld)
            {
                _running = false;
                Destroy(this);
                return;
            }

            _elapsed += Time.deltaTime;

            if (_elapsed >= _settings.HoverDuration)
            {
                Fall();
                return;
            }

            DriveFloat();
        }

        private void DriveFloat()
        {
            float rise = _settings.RiseDuration > 0f
                ? Mathf.Clamp01(_elapsed / _settings.RiseDuration)
                : 1f;

            // Ease from wherever the card rested to the cast's shared hover line - so a floor card
            // lifts up to it and a card off a house drifts down to it, both landing on the same line.
            float y = Mathf.SmoothStep(_restPosition.y, _hoverHeight, rise);

            // The bob only joins once the card has arrived, so the move reads as one clean glide into
            // a hover rather than starting to wobble on the way.
            if (rise >= 1f && _settings.BobAmplitude > 0f)
            {
                y += Mathf.Sin(_elapsed * _settings.BobFrequency * 2f * Mathf.PI)
                     * _settings.BobAmplitude;
            }

            _card.transform.position = new Vector3(_restPosition.x, y, _restPosition.z);

            Quaternion target = FaceCameraRotation();

            // Ease the turn over the same rise so it settles facing the camera as it reaches the top,
            // then keep tracking the camera in case the player walks around it while it hovers.
            float turn = 1f - Mathf.Exp(-12f * Time.deltaTime);
            _card.transform.rotation = Quaternion.Slerp(_card.transform.rotation, target, turn);
        }

        /// <summary>
        /// The rotation that shows the card's face to the camera. The mesh front is +Z, so +Z is
        /// aimed at the camera; when the camera is almost straight overhead the usual up-hint would
        /// be degenerate, so a sideways hint is used instead.
        /// </summary>
        private Quaternion FaceCameraRotation()
        {
            Vector3 toCamera = _camera != null
                ? _camera.transform.position - _card.transform.position
                : Vector3.up;

            if (toCamera.sqrMagnitude < 1e-6f)
                return _card.transform.rotation;

            toCamera.Normalize();

            Vector3 up = Mathf.Abs(Vector3.Dot(toCamera, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up;

            return Quaternion.LookRotation(toCamera, up);
        }

        private void Fall()
        {
            _running = false;

            // Back into the simulation from wherever it is hovering; gravity brings it down and the
            // card's own settle watch freezes it again once it lands.
            if (_card != null && !_card.IsHeld)
                _card.BeginFlight();

            Destroy(this);
        }
    }
}
