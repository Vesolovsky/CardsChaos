using PrimeTween;
using UnityEngine;

namespace CardsChaos.Cards
{
    public enum CardHighlight
    {
        None,
        Hovered,
        Selected,
    }

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("CardsChaos/Card")]
    public class Card : MonoBehaviour
    {
        [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.99f, 0.88f, 1f);
        // Only the rim sweeps the silhouette outwards, and its widest lateral component is
        // cos(18 degrees), so the ring on screen is a touch narrower than these numbers.
        [SerializeField] private float hoverWidth = 0.004f;
        [SerializeField] private float selectedWidth = 0.006f;

        [Tooltip("Smoothness while the card is in hand. The material value is restored on release.")]
        [SerializeField] private float heldSmoothness = 0f;

        [Header("Inspect")]
        [Tooltip("Material look while this card is held up for inspection. CardSetBuilder " +
                 "writes these per variant from the measured brightness of the face, so " +
                 "edits here are overwritten by the next Build All Card Sets - retune the " +
                 "luminance constants in the builder instead.")]
        [SerializeField] private float inspectSmoothness = 0.5f;
        [SerializeField] private float inspectMetallic = 0.845f;

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
            ApplyMaterialOverrides();

            _body.isKinematic = true;
            _body.detectCollisions = false;
            _collider.enabled = false;
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
            _collider.enabled = true;
            _body.detectCollisions = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.isKinematic = false;
            _body.velocity = velocity;
            _body.angularVelocity = angularVelocity;
        }

        private void ApplyMaterialOverrides()
        {
            bool outlined = _highlight != CardHighlight.None;

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
                bool selected = _highlight == CardHighlight.Selected;
                _propertyBlock.SetColor(OutlineColorId, selected ? selectedColor : hoverColor);
                _propertyBlock.SetFloat(OutlineWidthId, selected ? selectedWidth : hoverWidth);
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
