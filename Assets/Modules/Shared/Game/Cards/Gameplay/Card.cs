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

        private Rigidbody _body;
        private BoxCollider _collider;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;

        private Tween _positionTween;
        private Tween _rotationTween;
        private CardHighlight _highlight = CardHighlight.None;

        public bool IsHeld { get; private set; }

        public bool IsInspected { get; private set; }

        public float FaceLuminance => faceLuminance;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<BoxCollider>();
            _renderer = GetComponent<MeshRenderer>();
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
            IsHeld = true;
            // Only cards on the table can be hovered, so one entering the hand must not
            // carry the ring in with it.
            _highlight = CardHighlight.None;
            ApplyMaterialOverrides();

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
            // A card in hand hangs right in front of the camera, so its shadow sweeps across
            // the whole table for no gain.
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _collider.isTrigger = false;
            _body.detectCollisions = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.isKinematic = false;
            _body.velocity = velocity;
            _body.angularVelocity = angularVelocity;
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
