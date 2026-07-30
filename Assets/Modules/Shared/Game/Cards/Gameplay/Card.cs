using System.Collections;
using PrimeTween;
using UnityEngine;

namespace CardsChaos.Cards
{
    public enum CardHighlight
    {
        None,
        Hovered,
    }

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("CardsChaos/Card")]
    public class Card : MonoBehaviour
    {
        [SerializeField] private Color hoverColor = Color.white;

        // Only the rim sweeps the silhouette outwards, and its widest lateral component is
        // cos(18 degrees), so the ring on screen is a touch narrower than this number.
        [SerializeField] private float hoverWidth = 0.002f;

        [Tooltip("Smoothness while the card is in hand. The material value is restored on release.")]
        [SerializeField] private float heldSmoothness = 0f;

        [Header("Inspect")]
        [Tooltip("Material look while this card is held up for inspection. CardSetBuilder " +
                 "writes these per variant from the measured brightness of the face, so " +
                 "edits here are overwritten by the next Build All Card Sets - retune the " +
                 "luminance constants in the builder instead.")]
        [SerializeField] private float inspectSmoothness = 0.5f;
        [SerializeField] private float inspectMetallic = 0.845f;

        [Tooltip("Perceived brightness of the face, 0 to 1, measured from the artwork at build " +
                 "time. Written by the same pass as the two values above; the close-up lighting " +
                 "reads it so a pale card is not lit as hard as a dark one.")]
        [SerializeField, Range(0f, 1f)] private float faceLuminance = 0.5f;

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        // A thrown card is thin and moving fast at a table of other thin cards - exactly the case
        // speculative contacts were found to tunnel through (see CardSetBuilder). Continuous
        // Dynamic sweeps instead, and it costs next to nothing here: only the handful of cards in
        // flight are simulated at all, since the hundreds at rest are frozen kinematic below.
        private const CollisionDetectionMode FlightCollision =
            CollisionDetectionMode.ContinuousDynamic;

        private Rigidbody _body;
        private BoxCollider _collider;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;

        private Tween _positionTween;
        private Tween _rotationTween;
        private CardHighlight _highlight = CardHighlight.None;

        // Runs only while a card is falling, and only ever for the handful in flight at once. It
        // waits for the body to fall asleep and then freezes it out of the simulation for good.
        private Coroutine _settleWatch;

        public bool IsHeld { get; private set; }

        public bool IsInspected { get; private set; }

        public float FaceLuminance => faceLuminance;

        /// <summary>
        /// Which card this is - set, number, face. Cached because the album asks every card in
        /// hand for it every time it redraws the pile.
        /// </summary>
        public CardIdentity Identity { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<BoxCollider>();
            _renderer = GetComponent<MeshRenderer>();
            Identity = GetComponent<CardIdentity>();
        }

        private void Start()
        {
            // A card that begins in the air - scattered by the spawner, or dropped in by hand and
            // left dynamic - falls under the cheap flight settings and freezes itself the moment it
            // lands. One placed already frozen (see FreezeInPlace) is left alone, so a hand-laid
            // table pays nothing on load.
            if (!_body.isKinematic)
                EnterFlight();
        }

        public void SetHighlight(CardHighlight highlight)
        {
            if (_highlight == highlight)
                return;

            _highlight = highlight;
            ApplyMaterialOverrides();
        }

        public void SetInspected(bool inspected)
        {
            if (IsInspected == inspected)
                return;

            IsInspected = inspected;
            ApplyMaterialOverrides();
        }

        public void AttachTo(Transform parent)
        {
            // Picked up before it had settled, the card must not freeze itself while in hand.
            StopSettleWatch();

            IsHeld = true;
            // Only cards on the table can be hovered, so one entering the hand must not
            // carry the ring in with it.
            _highlight = CardHighlight.None;
            ApplyMaterialOverrides();

            // A card grabbed before it settled is still in a continuous mode, which is illegal on
            // a kinematic body - drop it to Discrete before freezing.
            _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _body.isKinematic = true;
            // Stays in the physics scene, but as a trigger. The mouse has to be able to find a
            // card in hand in order to select it, while a solid collider riding along in front
            // of the camera would shove the cards on the floor aside as the player walks.
            _body.detectCollisions = true;
            _collider.isTrigger = true;
            // The hand drives the transform directly from Update. Leaving interpolation on
            // would have the body keep writing its own one-step-old pose over the tween,
            // which shows up as a card twitching in place.
            _body.interpolation = RigidbodyInterpolation.None;

            transform.SetParent(parent, worldPositionStays: true);
        }

        public void MoveTo(Vector3 localPosition, Quaternion localRotation, float duration, Ease ease)
        {
            StopTweens();

            if (duration <= 0f)
            {
                transform.SetLocalPositionAndRotation(localPosition, localRotation);
                return;
            }

            // A relayout re-issues every slot, so most cards are asked to move to where they
            // already are. Those tweens animate nothing, spend a tween slot and make
            // PrimeTween warn about a redundant end value.
            if (transform.localPosition != localPosition)
                _positionTween = Tween.LocalPosition(transform, localPosition, duration, ease);

            if (transform.localRotation != localRotation)
                _rotationTween = Tween.LocalRotation(transform, localRotation, duration, ease);
        }

        /// <summary>
        /// Same as <see cref="MoveTo"/>, but bulged out along the way by <paramref name="arc"/>.
        ///
        /// Used by the card cycling from one end of the pile to the other: taken in a straight
        /// line it would slide through every card it is meant to be passing, and the whole point
        /// of the move is watching where the card went.
        /// </summary>
        public void ArcTo(Vector3 localPosition, Quaternion localRotation, Vector3 arc,
            float duration, Ease ease)
        {
            StopTweens();

            if (duration <= 0f)
            {
                transform.SetLocalPositionAndRotation(localPosition, localRotation);
                return;
            }

            Vector3 start = transform.localPosition;
            Vector3 control = (start + localPosition) * 0.5f + arc;
            Transform cardTransform = transform;

            // Quadratic bezier. The control point is only ever approached, never reached, so the
            // swing reads as softer than the offset suggests.
            _positionTween = Tween.Custom(0f, 1f, duration, t =>
            {
                float inverse = 1f - t;

                cardTransform.localPosition = inverse * inverse * start
                                              + 2f * inverse * t * control
                                              + t * t * localPosition;
            }, ease);

            if (transform.localRotation != localRotation)
                _rotationTween = Tween.LocalRotation(transform, localRotation, duration, ease);
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            StopTweens();

            // Every override drops at the same moment, so apply the block just once.
            _highlight = CardHighlight.None;
            IsHeld = false;
            IsInspected = false;
            ApplyMaterialOverrides();

            transform.SetParent(null, worldPositionStays: true);

            _body.detectCollisions = true;
            EnterFlight();
            _body.velocity = velocity;
            _body.angularVelocity = angularVelocity;
            // It has been kinematic the whole time it sat in hand; without this the throw would
            // not take hold until physics next chose to wake the body on its own.
            _body.WakeUp();
        }

        /// <summary>
        /// Puts the body back into the simulation under the settings a falling card wants and
        /// starts watching for it to come to rest. Shared by a thrown card and by one that begins
        /// life in the air.
        /// </summary>
        private void EnterFlight()
        {
            _body.isKinematic = false;
            _collider.isTrigger = false;
            // Set only once the body is dynamic again - a continuous mode is illegal on a
            // kinematic body and Unity warns if it is written while one still is.
            _body.collisionDetectionMode = FlightCollision;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            BeginSettleWatch();
        }

        /// <summary>
        /// Freezes the card where it stands without waiting for it to settle, for cards laid out by
        /// hand that are already at rest. Spares the scene hundreds of bodies waking on load only
        /// to fall straight back asleep.
        /// </summary>
        public void FreezeInPlace()
        {
            StopSettleWatch();
            EnterResting();
        }

        /// <summary>
        /// Takes a card out of the simulation once it has come to rest. Hundreds of kinematic cards
        /// cost nothing to keep standing; only the few in flight are simulated at all.
        /// </summary>
        private void EnterResting()
        {
            _settleWatch = null;

            _collider.isTrigger = false;
            _body.interpolation = RigidbodyInterpolation.None;
            // Step down off the continuous mode before freezing: it is illegal on a kinematic body.
            _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _body.isKinematic = true;
        }

        private void BeginSettleWatch()
        {
            StopSettleWatch();

            if (isActiveAndEnabled)
                _settleWatch = StartCoroutine(FreezeWhenAsleep());
        }

        private void StopSettleWatch()
        {
            if (_settleWatch == null)
                return;

            StopCoroutine(_settleWatch);
            _settleWatch = null;
        }

        private IEnumerator FreezeWhenAsleep()
        {
            var step = new WaitForFixedUpdate();

            // PhysX reports a body asleep only after it has held still for a stretch, so this
            // fires when the card has genuinely come to rest rather than between two bounces.
            while (!_body.IsSleeping())
                yield return step;

            EnterResting();
        }

        private void ApplyMaterialOverrides()
        {
            bool outlined = _highlight == CardHighlight.Hovered;

            // Clearing the block rather than zeroing the values matters: a renderer carrying
            // any property block drops out of SRP batching for good.
            if (!outlined && !IsHeld && !IsInspected)
            {
                _renderer.SetPropertyBlock(null);
                return;
            }

            // Rebuilt from scratch every time: whatever is left out falls back to the
            // material, which is how the card gets its normal smoothness back on release.
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.Clear();

            if (outlined)
            {
                _propertyBlock.SetColor(OutlineColorId, hoverColor);
                _propertyBlock.SetFloat(OutlineWidthId, hoverWidth);
            }

            if (IsInspected)
            {
                // Under inspection the card is the whole point, so let it catch the light.
                _propertyBlock.SetFloat(SmoothnessId, inspectSmoothness);
                _propertyBlock.SetFloat(MetallicId, inspectMetallic);
            }
            else if (IsHeld)
            {
                // Fanned out in hand a glossy card catches a specular sweep that sits right
                // on top of the artwork; matte it out until it is looked at or put down.
                _propertyBlock.SetFloat(SmoothnessId, heldSmoothness);
            }

            _renderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>Cancels the slot tweens so an external driver can own the transform.</summary>
        public void StopAnimation() => StopTweens();

        private void StopTweens()
        {
            if (_positionTween.isAlive)
                _positionTween.Stop();

            if (_rotationTween.isAlive)
                _rotationTween.Stop();
        }

        private void OnDestroy() => StopTweens();
    }
}
