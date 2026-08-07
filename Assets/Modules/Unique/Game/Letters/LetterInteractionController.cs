using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Input;
using Zenject;

namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// The cursor's side of a letter: it draws a hover outline over whatever letter is under the
    /// pointer, and a click (the Interact action) opens that letter. It stays out of the way whenever
    /// the room is not the player's to drive - a letter already open, the album, the card close-up -
    /// by honouring the world-interaction lock, exactly as the card table does.
    ///
    /// The outline cannot reuse the cards' depth-tested inverted hull: a letter is a static prop with
    /// a custom-render-queue material lying flat on furniture, so a depth-based ring gets hidden. So
    /// it is drawn ON TOP (ZTest Always) as two passes - a stencil mask over the letter's silhouette,
    /// then the enlarged hull everywhere the mask did not mark - which leaves a ring immune to depth
    /// and to the letter's own shader. Input runs in Tick; the outline is submitted in LateTick.
    /// </summary>
    public sealed class LetterInteractionController : ITickable, ILateTickable
    {
        private const float MaxPickDistance = 50f;

        // Shader assets living in a Letters/Resources folder, loaded by file name.
        private const string MaskShaderResourcePath = "LetterOutlineMask";
        private const string FillShaderResourcePath = "LetterOutlineFill";

        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        private readonly ICameraService _cameraService;
        private readonly IWorldInteractionLock _worldLock;
        private readonly ILetterInspector _inspector;
        private readonly LetterSettings _settings;
        private readonly InputAction _interact;

        // The mask stamps the letter's silhouette into the stencil; the fill draws the ring where the
        // mask did not. Built once from the two Resources shaders.
        private readonly Material _maskMaterial;
        private readonly Material _fillMaterial;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();

        private Letter _hovered;

        [Inject]
        public LetterInteractionController(
            ICameraService cameraService,
            IWorldInteractionLock worldLock,
            ILetterInspector inspector,
            LetterSettings settings,
            IInputActions input)
        {
            _cameraService = cameraService;
            _worldLock = worldLock;
            _inspector = inspector;
            _settings = settings;
            _interact = input.Find(GameInputActions.Interact);

            _maskMaterial = LoadMaterial(MaskShaderResourcePath);
            _fillMaterial = LoadMaterial(FillShaderResourcePath);
        }

        private static Material LoadMaterial(string shaderResourcePath)
        {
            Shader shader = Resources.Load<Shader>(shaderResourcePath);
            if (shader == null)
            {
                Debug.LogError(
                    $"[{nameof(LetterInteractionController)}] Resources/{shaderResourcePath}.shader is " +
                    "missing; hovered letters cannot be outlined.");

                return null;
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Tick()
        {
            Mouse mouse = Mouse.current;

            // Whoever holds the room owns the mouse - a letter already open, the album, the close-up.
            // The right button is the camera's, and while it is down the pointer is parked, so
            // anything it appears to be over is an accident.
            if (mouse == null || _worldLock.IsLocked || mouse.rightButton.isPressed)
            {
                _hovered = null;
                return;
            }

            // A HUD button can sit right over a letter; when the pointer rests on interactable UI the
            // click belongs to the button, so aim at nothing - that hides the outline and stops the
            // open below, which needs a target.
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            _hovered = pointerOverUi ? null : FindLetterUnderCursor(mouse);

            if (_hovered != null && _interact != null && _interact.WasPressedThisFrame())
            {
                _inspector.TryOpen(_hovered);
                _hovered = null;
            }
        }

        public void LateTick()
        {
            Letter letter = _hovered;

            // A modal can take the lock between Tick and here; recheck so the opening frame cannot
            // submit a stale outline over the letter now being read.
            if (_worldLock.IsLocked || letter == null)
            {
                _hovered = null;
                return;
            }

            Camera camera = _cameraService.MainCamera;
            MeshRenderer source = letter.OutlineRenderer;
            Mesh mesh = letter.OutlineMesh;

            if (_maskMaterial == null || _fillMaterial == null || camera == null
                || source == null || mesh == null
                || !source.enabled || source.forceRenderingOff || !source.gameObject.activeInHierarchy)
            {
                return;
            }

            int layer = source.gameObject.layer;
            if ((camera.cullingMask & (1 << layer)) == 0)
                return;

            Matrix4x4 matrix = source.localToWorldMatrix;

            // Pass 1: stamp the letter's silhouette into the stencil (lower queue, so it runs first).
            Graphics.DrawMesh(
                mesh, matrix, _maskMaterial, layer, camera, 0, null,
                ShadowCastingMode.Off, receiveShadows: false,
                probeAnchor: null, lightProbeUsage: LightProbeUsage.Off);

            // Pass 2: the ring, drawn on top everywhere the mask did not mark.
            _properties.Clear();
            _properties.SetColor(OutlineColorId, _settings.HoverColor);
            _properties.SetFloat(OutlineWidthId, _settings.HoverWidth);

            Graphics.DrawMesh(
                mesh, matrix, _fillMaterial, layer, camera, 0, _properties,
                ShadowCastingMode.Off, receiveShadows: false,
                probeAnchor: null, lightProbeUsage: LightProbeUsage.Off);
        }

        private Letter FindLetterUnderCursor(Mouse mouse)
        {
            Ray ray = _cameraService.SceenPointToRay(mouse.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, MaxPickDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide))
                return null;

            return hit.collider.GetComponentInParent<Letter>();
        }
    }
}
