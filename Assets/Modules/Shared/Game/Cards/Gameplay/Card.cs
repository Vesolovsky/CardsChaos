using System.Collections;
using PrimeTween;
using UnityEngine;
using Vesolovsky.Core.Audio;
using Zenject;

namespace CardsChaos.Cards
{
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshFilter))]
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

        [Tooltip("Texture LOD correction used only while the card is held or inspected. -0.415 " +
                 "compensates for importing a 3:2 source into the next power-of-two height; " +
                 "resting cards keep the material default of zero.")]
        [SerializeField, Range(-1f, 0f)] private float closeViewMipBias = -0.415f;

        [Tooltip("A conservative five-tap sharpen used only while this card is inspected. It is " +
                 "bounded in the shader and fades out at steep viewing angles to avoid halos " +
                 "and shimmer. Zero disables it completely.")]
        [SerializeField, Range(0f, 0.35f)] private float inspectSharpen = 0.18f;

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

        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int MipBiasId = Shader.PropertyToID("_MipBias");
        private static readonly int InspectSharpenId = Shader.PropertyToID("_InspectSharpen");
        private static readonly int GrayscaleId = Shader.PropertyToID("_Grayscale");

        // Held (and piled) cards ride right in front of the camera and would otherwise clip into the
        // furniture. When a layer of this name exists they are moved onto it so an overlay camera can
        // draw them over the world; if the layer is absent the move is skipped and nothing changes.
        private const string HeldLayerName = "HeldCard";

        // A thrown card is thin and moving fast at a table of other thin cards - exactly the case
        // speculative contacts were found to tunnel through (see CardSetBuilder). Continuous
        // Dynamic sweeps instead, and it costs next to nothing here: only the handful of cards in
        // flight are simulated at all, since cards at rest have no Rigidbody.
        private const CollisionDetectionMode FlightCollision =
            CollisionDetectionMode.ContinuousDynamic;

        // Keep runtime-created bodies identical to the old authored Rigidbody. These values are
        // deliberately applied in one place so editor placement and runtime throws use the same
        // thin-card solver setup.
        private const float BodyMass = 0.005f;
        private const float BodyDrag = 0f;
        private const float BodyAngularDrag = 0.05f;
        private const int BodySolverIterations = 16;
        private const int BodySolverVelocityIterations = 4;
        private const float BodyMaxDepenetrationVelocity = 1f;

        // A card in flight touches down, then bounces and slides through a few more contacts before
        // it settles. Only the first contact hard enough to be heard gets a sound, so one throw is
        // one thwack rather than a rattle of every scrape on the way to rest. Tuned to the light,
        // slow thrown card; smaller taps (a card nudging another as it beds in) stay silent.
        private const float LandImpactSpeed = 0.25f;

        private IAudioService _audioService;

        private Rigidbody _body;
        private BoxCollider _collider;
        private MeshFilter _meshFilter;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private System.IDisposable _heldArtworkMipRequest;

        // The layer the card was authored on, restored whenever it leaves the hand.
        private int _defaultLayer;

        private Tween _positionTween;
        private Tween _rotationTween;
        // Runs only while a card is falling, and only ever for the handful in flight at once. It
        // waits for the body to fall asleep and then freezes it out of the simulation for good.
        private Coroutine _settleWatch;
        private Coroutine _restingBodyRemoval;

        // Cleared each time the card enters flight and set once the landing sound has played, so a
        // single flight only ever produces one impact sound however many contacts it bounces through.
        private bool _landSoundPlayed;

        public bool IsHeld { get; private set; }

        public bool IsInspected { get; private set; }

        /// <summary>
        /// Whether this card is drawn drained of colour while it is in hand - how a copy the player
        /// has already filed away is told apart from a card still worth keeping. Set from outside
        /// (see the duplicate service) and deliberately ignored while the card is inspected: a card
        /// held up close is being read, and the artwork is the point.
        /// </summary>
        public bool IsShaded { get; private set; }

        /// <summary>
        /// The house of cards this card was placed into, if any. Set by <see cref="CardHouse"/> and
        /// read by the hand on pickup, so lifting one card off a standing house brings the rest of
        /// it down. Runtime-only - never serialized - and cleared once the house has come down.
        /// </summary>
        public CardHouse House { get; set; }

        public float FaceLuminance => faceLuminance;

        public Mesh OutlineMesh => _meshFilter != null ? _meshFilter.sharedMesh : null;

        public MeshRenderer OutlineRenderer => _renderer;

        public Color OutlineColor => hoverColor;

        public float OutlineWidth => hoverWidth;

        /// <summary>
        /// Which card this is - set, number, face. Cached because the album asks every card in
        /// hand for it every time it redraws the pile.
        /// </summary>
        public CardIdentity Identity { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<BoxCollider>();
            _meshFilter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            Identity = GetComponent<CardIdentity>();
            _defaultLayer = gameObject.layer;

            // Joined here, left in OnDestroy: the registry is how the room answers "does this card
            // have a second copy", which is what a duplicate box will and will not take.
            CardRegistry.Add(this);
        }

        // Optional: scene-placed cards are injected by the SceneContext and factory-spawned cards by
        // the container. A card that somehow misses injection simply plays no landing sound rather
        // than erroring - the sound is a flourish, never load-bearing.
        [Inject]
        private void Inject([InjectOptional] IAudioService audioService)
        {
            _audioService = audioService;
        }

        private void Start()
        {
            // Runtime spawns enter flight explicitly through BeginFlight. Keep this fallback for
            // intentionally-authored dynamic cards, but do not restart a watch the factory already
            // created between Awake and this first frame. A legacy authored kinematic body is
            // retired here; a card already attached to the hand must retain its kinematic body.
            if (_body == null || _settleWatch != null || IsHeld)
                return;

            if (_body.isKinematic)
                EnterResting();
            else
                BeginFlight();
        }

        public void SetInspected(bool inspected)
        {
            if (IsInspected == inspected)
                return;

            IsInspected = inspected;
            ApplyMaterialOverrides();
        }

        /// <summary>
        /// Raised when the grey wash goes on or off, so a second view of the same card - the album
        /// draws the hand as a flat pile of its own - can follow the room's card rather than work
        /// the rule out again for itself.
        /// </summary>
        public event System.Action<Card> ShadedChanged;

        /// <summary>Turns the in-hand grey wash on or off. See <see cref="IsShaded"/>.</summary>
        public void SetShaded(bool shaded)
        {
            if (IsShaded == shaded)
                return;

            IsShaded = shaded;
            ApplyMaterialOverrides();
            ShadedChanged?.Invoke(this);
        }

        public void AttachTo(Transform parent)
        {
            // Picked up before it had settled, the card must not freeze itself while in hand.
            StopSettleWatch();

            Rigidbody body = EnsureBody();

            IsHeld = true;
            RequestHeldArtworkMip();
            ApplyMaterialOverrides();

            // A card grabbed before it settled is still in a continuous mode, which is illegal on
            // a kinematic body - drop it to Discrete before freezing.
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.useGravity = false;
            // Stays in the physics scene, but as a trigger. The mouse has to be able to find a
            // card in hand in order to select it, while a solid collider riding along in front
            // of the camera would shove the cards on the floor aside as the player walks.
            body.detectCollisions = true;
            _collider.isTrigger = true;
            // The hand drives the transform directly from Update. Leaving interpolation on
            // would have the body keep writing its own one-step-old pose over the tween,
            // which shows up as a card twitching in place.
            body.interpolation = RigidbodyInterpolation.None;

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

        /// <summary>
        /// Flies to a local pose on a quadratic arc and adds a temporary local tilt that is exactly
        /// zero at both ends. Unlike a physics throw this is deterministic and always lands flush;
        /// it is intended for transfers into authored slots such as a card container.
        ///
        /// <paramref name="turns"/> spins the card about its parent's upright axis on the way, the
        /// way a card sailing into place turns as it goes. Whole turns are the point: the spin is
        /// back where it started on landing, so it cannot leave the card square-but-backwards.
        /// </summary>
        public void FlyTo(
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 arc,
            Vector3 flourishEuler,
            float duration,
            Ease ease,
            int turns = 0)
        {
            StopTweens();

            if (duration <= 0f)
            {
                transform.SetLocalPositionAndRotation(localPosition, localRotation);
                return;
            }

            Vector3 startPosition = transform.localPosition;
            Quaternion startRotation = transform.localRotation;
            Transform cardTransform = transform;

            float spinDegrees = turns * 360f;

            // PrimeTween supplies the eased 0..1 value. The sine envelope gives the card a little
            // character in mid-air, then mathematically removes the extra tilt before it lands.
            _positionTween = Tween.Custom(0f, 1f, duration, t =>
            {
                cardTransform.localPosition = FlightPoint(startPosition, localPosition, arc, t);

                Quaternion directRotation = Quaternion.Slerp(startRotation, localRotation, t);
                float flourish = Mathf.Sin(t * Mathf.PI);
                Quaternion rotation = directRotation * Quaternion.Euler(flourishEuler * flourish);

                // Applied on the parent's side of the product, so the card turns about the slot's
                // upright axis - flat and face-up throughout - rather than tumbling end over end.
                cardTransform.localRotation = spinDegrees == 0f
                    ? rotation
                    : Quaternion.AngleAxis(spinDegrees * t, Vector3.up) * rotation;
            }, ease);
        }

        /// <summary>
        /// Where <see cref="FlyTo"/> puts the card at <paramref name="t"/> of the way through, in
        /// the same local space. Public so a caller can look along the path before committing to it
        /// - checking a curve the card does not actually follow would be worse than not checking.
        /// </summary>
        public static Vector3 FlightPoint(Vector3 start, Vector3 end, Vector3 arc, float t)
        {
            // Quadratic bezier. The control point is only ever approached, never reached, so the
            // sweep reads as softer than the offset suggests.
            Vector3 control = (start + end) * 0.5f + arc;
            float inverse = 1f - t;

            return inverse * inverse * start + 2f * inverse * t * control + t * t * end;
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            StopTweens();

            bool wasShaded = IsShaded;
            IsHeld = false;
            IsInspected = false;
            IsShaded = false;
            ReleaseHeldArtworkMip();
            ApplyMaterialOverrides();

            if (wasShaded)
                ShadedChanged?.Invoke(this);

            transform.SetParent(null, worldPositionStays: true);

            BeginFlight();
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
        public void BeginFlight()
        {
            Rigidbody body = EnsureBody();

            // A fresh flight is allowed one landing sound again.
            _landSoundPlayed = false;

            body.detectCollisions = true;
            body.useGravity = true;
            body.isKinematic = false;
            _collider.isTrigger = false;
            // Set only once the body is dynamic again - a continuous mode is illegal on a
            // kinematic body and Unity warns if it is written while one still is.
            body.collisionDetectionMode = FlightCollision;
            body.interpolation = RigidbodyInterpolation.Interpolate;

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
        /// Lifts the card into the hovering state the Levitate skill drives: a kinematic body with
        /// no gravity, its settle watch stopped so it will not freeze itself while it floats, and a
        /// still-solid (non-trigger) collider so the ordinary floor pickup can take it out of the
        /// air. The caller owns the transform from here - the rise and the turn to the camera - the
        /// same way the hand drives a held card. Picking the card up (<see cref="AttachTo"/>) or
        /// letting it drop (<see cref="BeginFlight"/>) both take it back out of this state.
        /// </summary>
        public void BeginLevitate()
        {
            StopTweens();
            StopSettleWatch();

            Rigidbody body = EnsureBody();

            // Same order AttachTo uses: step off any continuous mode before going kinematic, since a
            // continuous mode is illegal on a kinematic body and Unity warns if one is left on it.
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            // The transform is driven straight from Update (see LevitatingCard); interpolation would
            // fight that by writing its own one-step-old pose over it, the same twitch AttachTo avoids.
            body.interpolation = RigidbodyInterpolation.None;

            // Unlike a held card this stays a solid collider, not a trigger: it is still a card lying
            // in the room that the cursor picks up in the ordinary way, only raised off the table.
            _collider.isTrigger = false;
        }

        /// <summary>
        /// Forces the card back to the plain resting state - no body, solid non-trigger collider,
        /// not held or inspected, no leftover physics material - from whatever state it is in.
        /// Used to rebuild a collapsed house of cards for testing; unlike <see cref="FreezeInPlace"/>
        /// it also releases a held card and clears the slick material a collapse left behind.
        /// </summary>
        public void ResetToFrozen()
        {
            StopTweens();
            StopSettleWatch();

            if (_restingBodyRemoval != null)
            {
                StopCoroutine(_restingBodyRemoval);
                _restingBodyRemoval = null;
            }

            bool wasShaded = IsShaded;
            IsHeld = false;
            IsInspected = false;
            IsShaded = false;
            ReleaseHeldArtworkMip();
            ApplyMaterialOverrides();

            if (wasShaded)
                ShadedChanged?.Invoke(this);

            if (_collider != null)
            {
                _collider.isTrigger = false;
                _collider.sharedMaterial = null;
            }

            if (_body != null)
            {
                if (Application.isPlaying)
                    Destroy(_body);
                else
                    DestroyImmediate(_body);

                _body = null;
            }
        }

        /// <summary>
        /// Takes a card out of the simulation once it has come to rest. Its BoxCollider remains as
        /// an immovable static collider; only the few cards in flight retain a Rigidbody.
        /// </summary>
        private void EnterResting()
        {
            _settleWatch = null;

            if (_restingBodyRemoval != null)
            {
                StopCoroutine(_restingBodyRemoval);
                _restingBodyRemoval = null;
            }

            _collider.isTrigger = false;
            if (_body == null)
                return;

            Rigidbody body = _body;
            body.interpolation = RigidbodyInterpolation.None;
            // Step down off the continuous mode before freezing: it is illegal on a kinematic body.
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            // Freeze immediately so the card cannot get one final physics step. Removing the body
            // is delayed through the interaction frame: if the player grabs this card in Update,
            // EnsureBody cancels the removal and safely reuses the already configured body.
            body.isKinematic = true;
            body.useGravity = false;

            if (Application.isPlaying && isActiveAndEnabled)
                _restingBodyRemoval = StartCoroutine(RemoveRestingBody(body));
            else
                RemoveRestingBodyImmediately(body);
        }

        private IEnumerator RemoveRestingBody(Rigidbody body)
        {
            yield return null;
            _restingBodyRemoval = null;

            if (body != null && _body == body && !IsHeld && body.isKinematic)
                RemoveRestingBodyImmediately(body);
        }

        private void RemoveRestingBodyImmediately(Rigidbody body)
        {
            if (_body == body)
                _body = null;

            if (Application.isPlaying)
                Destroy(body);
            else
                DestroyImmediate(body);
        }

        /// <summary>
        /// Returns the card's physics body, creating and tuning one only when the card is about to
        /// be held or simulated. Resting cards intentionally have only a static BoxCollider.
        /// </summary>
        public Rigidbody EnsureBody()
        {
            if (_restingBodyRemoval != null)
            {
                StopCoroutine(_restingBodyRemoval);
                _restingBodyRemoval = null;
            }

            if (_body == null)
                _body = GetComponent<Rigidbody>();

            if (_body == null)
            {
                _body = gameObject.AddComponent<Rigidbody>();
                // A newly added body starts in the cheapest safe state. BeginFlight opts it into
                // dynamics immediately when needed; AttachTo keeps it kinematic for hand motion.
                _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                _body.interpolation = RigidbodyInterpolation.None;
                _body.useGravity = false;
                _body.isKinematic = true;
            }

            _body.mass = BodyMass;
            _body.drag = BodyDrag;
            _body.angularDrag = BodyAngularDrag;
            _body.solverIterations = BodySolverIterations;
            _body.solverVelocityIterations = BodySolverVelocityIterations;
            _body.maxDepenetrationVelocity = BodyMaxDepenetrationVelocity;
            return _body;
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
            while (_body != null && !_body.IsSleeping())
                yield return step;

            if (_body != null)
                EnterResting();
        }

        /// <summary>
        /// Puts held (and piled) cards on the <see cref="HeldLayerName"/> layer and every other card
        /// back on the layer it was authored with. Paired with an overlay camera that renders only
        /// that layer with the depth buffer cleared, this is what draws a card in hand over the room
        /// instead of letting the furniture clip through it. Held state is the trigger, so it rides
        /// along with the material overrides and cannot drift from them. Purely a rendering move that
        /// does nothing while the layer is absent, so the game plays identically until it is set up.
        /// </summary>
        private void ApplyRenderLayer()
        {
            int heldLayer = LayerMask.NameToLayer(HeldLayerName);
            if (heldLayer < 0)
                return;

            int target = IsHeld ? heldLayer : _defaultLayer;
            if (gameObject.layer != target)
                gameObject.layer = target;
        }

        private void ApplyMaterialOverrides()
        {
            ApplyRenderLayer();

            // Clearing the block rather than zeroing values returns resting cards to the SRP
            // Batcher. Only the handful currently held or inspected carry per-renderer state.
            if (!IsHeld && !IsInspected)
            {
                _renderer.SetPropertyBlock(null);
                return;
            }

            // Rebuilt from scratch every time: whatever is left out falls back to the
            // material, which is how the card gets its normal smoothness back on release.
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.Clear();
            _propertyBlock.SetFloat(MipBiasId, closeViewMipBias);

            if (IsInspected)
            {
                // Under inspection the card is the whole point, so let it catch the light.
                _propertyBlock.SetFloat(SmoothnessId, inspectSmoothness);
                _propertyBlock.SetFloat(MetallicId, inspectMetallic);
                _propertyBlock.SetFloat(InspectSharpenId, inspectSharpen);
            }
            else if (IsHeld)
            {
                // Fanned out in hand a glossy card catches a specular sweep that sits right
                // on top of the artwork; matte it out until it is looked at or put down.
                _propertyBlock.SetFloat(SmoothnessId, heldSmoothness);

                // Only in hand, and only outside the close-up: a shaded card goes back to full
                // colour the moment it is held up to be read.
                if (IsShaded)
                    _propertyBlock.SetFloat(GrayscaleId, 1f);
            }

            _renderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// Starts loading mip zero as soon as the card enters the hand, rather than on the frame
        /// inspect opens. At most the hand-size number of fronts are pinned; the inspector can take
        /// a second reference without releasing this one when it closes.
        /// </summary>
        private void RequestHeldArtworkMip()
        {
            if (_heldArtworkMipRequest != null)
                return;

            _heldArtworkMipRequest = CardMipStreaming.RequestFullResolution(
                Identity != null ? Identity.ArtworkTexture : null);
        }

        private void ReleaseHeldArtworkMip()
        {
            _heldArtworkMipRequest?.Dispose();
            _heldArtworkMipRequest = null;
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

        /// <summary>
        /// Plays the impact sound the first time a card in flight hits something hard enough to be
        /// heard. Only dynamic (in-flight) cards report collisions at all - held cards are kinematic
        /// triggers and resting cards have no body - so this cannot fire off a card sitting on the
        /// table; the flag then keeps the rest of the bounce quiet.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (_landSoundPlayed || _audioService == null || IsHeld ||
                _body == null || _body.isKinematic)
                return;

            if (collision.relativeVelocity.magnitude < LandImpactSpeed)
                return;

            _landSoundPlayed = true;

            // Played from the contact point in 3D so the thwack comes from where the card actually
            // hit, off a pooled source parented to the audio root - never to this card - so it
            // survives the card being picked up or filed a moment later.
            _audioService.Play(AudioSFXKey.CardLand, collision.GetContact(0).point);
        }

        private void OnDestroy()
        {
            CardRegistry.Remove(this);
            ReleaseHeldArtworkMip();
            StopTweens();
        }
    }
}
