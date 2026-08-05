using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

        private Canvas _canvas;
        private RectTransform _cursorRect;
        private bool _hasFocus = true;

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

        private void OnDestroy()
        {
            Cursor.visible = true;
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

            // While a camera-look drag locks the pointer (CameraLookController), the OS hides the
            // cursor and Mouse.position pins to screen centre. Hide our overlay too instead of
            // parking it in the middle; likewise hide it when the window isn't focused.
            bool shouldShow = Cursor.lockState == CursorLockMode.None && _hasFocus;

            if (_canvas.enabled != shouldShow)
                _canvas.enabled = shouldShow;

            if (!shouldShow)
                return;

            // Other systems flip the OS cursor back on (e.g. CameraLookController.EndDrag sets
            // Cursor.visible = true); re-hide it every frame so ours stays the only cursor.
            Cursor.visible = false;
            _cursorRect.position = mouse.position.ReadValue();
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
