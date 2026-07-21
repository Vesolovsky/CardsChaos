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
        [SerializeField] private float hoverWidth = 0.0018f;
        [SerializeField] private float selectedWidth = 0.0026f;

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private Rigidbody _body;
        private BoxCollider _collider;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;

        private Tween _positionTween;
        private Tween _rotationTween;
        private CardHighlight _highlight = CardHighlight.None;

        public bool IsHeld { get; private set; }

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
            ApplyHighlight();
        }

        public void AttachTo(Transform parent)
        {
            IsHeld = true;

            _body.isKinematic = true;
            _body.detectCollisions = false;
            _collider.enabled = false;

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

            _positionTween = Tween.LocalPosition(transform, localPosition, duration, ease);
            _rotationTween = Tween.LocalRotation(transform, localRotation, duration, ease);
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            StopTweens();
            SetHighlight(CardHighlight.None);

            IsHeld = false;
            transform.SetParent(null, worldPositionStays: true);

            _collider.enabled = true;
            _body.detectCollisions = true;
            _body.isKinematic = false;
            _body.velocity = velocity;
            _body.angularVelocity = angularVelocity;
        }

        private void ApplyHighlight()
        {
            // Clearing the block rather than zeroing the width matters: a renderer carrying
            // any property block drops out of SRP batching for good.
            if (_highlight == CardHighlight.None)
            {
                _renderer.SetPropertyBlock(null);
                return;
            }

            bool selected = _highlight == CardHighlight.Selected;

            _propertyBlock ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(OutlineColorId, selected ? selectedColor : hoverColor);
            _propertyBlock.SetFloat(OutlineWidthId, selected ? selectedWidth : hoverWidth);
            _renderer.SetPropertyBlock(_propertyBlock);
        }

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
