using UnityEngine;
using UnityEngine.Rendering;
using Vesolovsky.Core.Services;
using Zenject;

namespace CardsChaos.Cards
{
    public interface ICardOutlinePresenter
    {
        void SetTarget(Card card);
        void Clear();
    }

    /// <summary>
    /// Submits one inverted-hull draw for the floor card currently under the cursor. The outline
    /// is deliberately separate from the card material: an idle room therefore owns no outline
    /// passes or per-renderer property blocks, while a hover costs exactly one small mesh draw.
    /// </summary>
    public sealed class CardOutlinePresenter : ICardOutlinePresenter, ILateTickable
    {
        private const string MaterialResourcePath = "M_Card_Outline";

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private readonly ICameraService _cameraService;
        private readonly IWorldInteractionLock _worldLock;
        private readonly Material _material;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();

        private Card _target;

        [Inject]
        public CardOutlinePresenter(ICameraService cameraService, IWorldInteractionLock worldLock)
        {
            _cameraService = cameraService;
            _worldLock = worldLock;
            _material = Resources.Load<Material>(MaterialResourcePath);

            if (_material == null)
            {
                Debug.LogError(
                    $"[{nameof(CardOutlinePresenter)}] Resources/{MaterialResourcePath}.mat is " +
                    "missing; hovered cards cannot be outlined.");
            }
        }

        public void SetTarget(Card card) => _target = card;

        public void Clear() => _target = null;

        public void LateTick()
        {
            Card card = _target;

            // A modal can acquire the world lock after CardInputController's early Tick but
            // before this LateTick. Recheck here so the opening frame cannot submit stale hover.
            if (_worldLock.IsLocked || card == null || card.IsHeld)
            {
                _target = null;
                return;
            }

            Camera camera = _cameraService.MainCamera;
            MeshRenderer source = card.OutlineRenderer;
            Mesh mesh = card.OutlineMesh;

            if (_material == null || camera == null || source == null || mesh == null
                || !source.enabled || source.forceRenderingOff || !source.gameObject.activeInHierarchy)
            {
                return;
            }

            int layer = source.gameObject.layer;
            if ((camera.cullingMask & (1 << layer)) == 0)
                return;

            _properties.Clear();
            _properties.SetColor(OutlineColorId, card.OutlineColor);
            _properties.SetFloat(OutlineWidthId, card.OutlineWidth);

            Graphics.DrawMesh(
                mesh,
                source.localToWorldMatrix,
                _material,
                layer,
                camera,
                0,
                _properties,
                ShadowCastingMode.Off,
                receiveShadows: false,
                probeAnchor: null,
                lightProbeUsage: LightProbeUsage.Off);
        }
    }
}
