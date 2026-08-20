using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Vesolovsky.Core.Services.Input;

namespace Vesolovsky.Game.CommonUI
{
    /// <summary>
    /// Custom cursor drawn as a UI overlay that follows the mouse. We hide the OS cursor and move a
    /// RawImage to the pointer every frame instead of using Cursor.SetCursor: Unity's software cursor
    /// (ForceSoftware) can freeze / stop tracking the mouse depending on the render pipeline, and the
    /// hardware cursor is capped at ~32px and blurry. This overlay always follows the mouse, renders
    /// crisply, and can be any size. Put this on a persistent object (ProjectContext) and assign the
    /// texture; it survives scene loads because ProjectContext does.
    /// </summary>
    public class CustomCursor : MonoBehaviour
    {
        [SerializeField] private Texture2D cursorTexture;

        [Tooltip("On-screen cursor height in pixels; width scales to keep aspect ratio. Lower = smaller.")]
        [SerializeField] private float height = 32f;

        [Tooltip("Click point in texture pixels, measured from the top-left. For an arrow this is the tip.")]
        [SerializeField] private Vector2 hotspot = new Vector2(4, 3);

        [Tooltip("Logs each warp and how many frames Mouse.position took to agree with it. For " +
                 "diagnosing a cursor that still jumps; leave off otherwise.")]
        [SerializeField] private bool logWarpSettling;

        // How close the live reading has to get to the warp target to count as having caught up.
        // A couple of pixels, because the OS lands the pointer on a whole pixel and the position
        // asked for came from a float reading.
        private const float WarpSettleTolerance = 2f;

        // How long to keep drawing at a warp target that the live reading never agrees with. Only
        // a safety valve - the settle normally happens within a frame or two - but without it a
        // warp that the platform quietly refused would freeze the cursor in place for good.
        private const float WarpSettleTimeoutSeconds = 0.25f;

        private Canvas _canvas;
        private RectTransform _cursorRect;
        private bool _hasFocus = true;

        // Where the pointer has been sent but Mouse.position does not yet say so. Null whenever the
        // live reading can simply be believed, which is almost always.
        private Vector2? _warpTarget;
        private float _warpAge;
        private int _warpFrames;

        private void Start()
        {
            if (cursorTexture == null)
            {
                Debug.LogWarning($"{nameof(CustomCursor)}: no cursor texture assigned; keeping the OS cursor.", this);
                return;
            }

            Cursor.visible = false;
            BuildCursor();
        }

        private void OnEnable() => PointerWarp.Warped += OnPointerWarped;

        private void OnDisable() => PointerWarp.Warped -= OnPointerWarped;

        private void OnDestroy()
        {
            Cursor.visible = true;
        }

        /// <summary>
        /// Something has sent the pointer somewhere - the camera drag putting it back where the
        /// player left it. Draw there from the next frame rather than waiting for the input system,
        /// which is a frame or two behind and until then still reports where the pointer used to be.
        /// </summary>
        private void OnPointerWarped(Vector2 position)
        {
            _warpTarget = position;
            _warpAge = 0f;
            _warpFrames = 0;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
        }

        private void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || _canvas == null)
                return;

            // While a camera-look drag locks the pointer (CameraLookController) there is no real
            // pointer position to draw at - the OS has taken it away and the reading left behind
            // means nothing. Hide our overlay for the duration rather than drawing it somewhere
            // arbitrary; likewise hide it when the window isn't focused.
            bool shouldShow = Cursor.lockState == CursorLockMode.None && _hasFocus;

            if (!shouldShow)
            {
                if (_canvas.enabled)
                    _canvas.enabled = false;

                return;
            }

            // Other systems flip the OS cursor back on (e.g. CameraLookController.EndDrag restores
            // it); re-hide it every frame so ours stays the only cursor.
            Cursor.visible = false;

            _cursorRect.position = ResolvePosition(mouse);

            // Enabled only after the rect has been moved. The other order is what produced the
            // flicker: the canvas would light up and draw one frame at wherever it still sat.
            if (!_canvas.enabled)
                _canvas.enabled = true;
        }

        /// <summary>
        /// Where to draw this frame: the live reading, unless the pointer has just been warped and
        /// that reading has not caught up.
        ///
        /// <see cref="Vesolovsky.Core.Services.CameraLookController"/> ends a camera drag by warping
        /// the pointer back to where the drag began. Mouse.position keeps reporting the old reading
        /// for a frame or two afterwards, and drawing that is the jump: the cursor appears wherever
        /// the pointer had drifted to under the lock, then snaps into place when the warp lands.
        ///
        /// Rather than guess when the reading is trustworthy - which is what an earlier attempt got
        /// wrong, because what the control reports under a lock is not the fixed value it looked
        /// like - the warp says where it went, and this simply draws there until the two agree.
        /// </summary>
        private Vector2 ResolvePosition(Mouse mouse)
        {
            Vector2 live = mouse.position.ReadValue();

            if (!_warpTarget.HasValue)
                return live;

            Vector2 target = _warpTarget.Value;
            _warpAge += Time.unscaledDeltaTime;
            _warpFrames++;

            bool settled = (live - target).sqrMagnitude <= WarpSettleTolerance * WarpSettleTolerance;
            bool gaveUp = _warpAge >= WarpSettleTimeoutSeconds;

            if (settled || gaveUp)
            {
                if (logWarpSettling)
                {
                    Debug.Log(
                        $"{nameof(CustomCursor)}: warp to {target} " +
                        $"{(settled ? "settled" : "TIMED OUT")} after {_warpFrames} frame(s), " +
                        $"{_warpAge * 1000f:F0} ms; live reads {live}.");
                }

                _warpTarget = null;
                return live;
            }

            // Still stale - draw where the pointer is going, not where it has been.
            return target;
        }

        private void BuildCursor()
        {
            if (cursorTexture == null)
                return;

            var canvasGo = new GameObject("CustomCursorCanvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue; // draw above every other UI
            // Deliberately no GraphicRaycaster: the cursor must never intercept clicks.

            var imageGo = new GameObject("CursorImage");
            imageGo.transform.SetParent(canvasGo.transform, false);

            var image = imageGo.AddComponent<RawImage>();
            image.texture = cursorTexture;
            image.raycastTarget = false;

            _cursorRect = image.rectTransform;

            float aspect = (float)cursorTexture.width / cursorTexture.height;
            _cursorRect.sizeDelta = new Vector2(height * aspect, height);

            // Pivot at the hotspot so the tip sits exactly on the mouse point. The hotspot uses a
            // top-left origin while RectTransform pivots are bottom-left, so the Y is flipped.
            _cursorRect.pivot = new Vector2(
                hotspot.x / cursorTexture.width,
                1f - hotspot.y / cursorTexture.height);
        }
    }
}
