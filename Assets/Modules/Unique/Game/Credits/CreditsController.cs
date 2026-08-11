using RoboRyanTron.SceneReference;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Vesolovsky.Game.Credits
{
    /// <summary>
    /// The credits screen. Its content climbs steadily up the screen to reveal the whole roll, a
    /// corner "Press any key to skip" line lets the player cut to the main menu at any moment, and
    /// once the roll has fully passed it goes to the main menu on its own.
    ///
    /// A plain scene script - no DI, no album, nothing but a rect climbing the screen and a key that
    /// ends it. Both the Credits scene and the main menu scene must be in Build Settings.
    /// </summary>
    [AddComponentMenu("CardsChaos/Credits/Credits Controller")]
    public class CreditsController : MonoBehaviour
    {
        [Header("Scroll")]
        [Tooltip("The content that climbs the screen - the whole credits roll as one tall rect. " +
                 "Position it so it starts at (or below) the bottom of its window.")]
        [SerializeField] private RectTransform content;

        [Tooltip("The visible window the content scrolls through. Left empty it uses the content's " +
                 "parent, which is usually the mask/viewport it sits in.")]
        [SerializeField] private RectTransform viewport;

        [Tooltip("How fast the roll climbs, in UI units per second.")]
        [SerializeField] private float scrollSpeed = 60f;

        [Tooltip("Seconds to wait before the roll starts climbing.")]
        [SerializeField] private float startDelay = 1f;

        [Tooltip("Seconds to hold once the whole roll has passed before going to the menu on its own.")]
        [SerializeField] private float endHold = 2f;

        [SerializeField] private SceneReference mainMenuScene;

        private float _startY;
        private float _endDistance;
        private float _elapsed;
        private float _holdTimer;
        private bool _leaving;

        private void Start()
        {
            if (content == null)
            {
                Debug.LogError($"[{nameof(CreditsController)}] No content assigned; nothing to scroll.", this);
                enabled = false;
                return;
            }

            // Force a layout pass so the rects below read their real, settled sizes rather than
            // whatever they were before the canvas first built.
            Canvas.ForceUpdateCanvases();

            _startY = content.anchoredPosition.y;

            RectTransform window = viewport != null ? viewport : content.parent as RectTransform;
            float windowHeight = window != null ? window.rect.height : Screen.height;

            // Far enough that the bottom of the roll climbs clear past the top of the window.
            _endDistance = content.rect.height + windowHeight;
        }

        private void Update()
        {
            if (_leaving)
                return;

            if (AnyKeyPressed())
            {
                GoToMenu();
                return;
            }

            _elapsed += Time.deltaTime;

            float active = _elapsed - startDelay;
            if (active <= 0f)
                return;

            float traveled = Mathf.Min(active * scrollSpeed, _endDistance);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, _startY + traveled);

            if (traveled < _endDistance)
                return;

            // The whole roll has passed; hold a beat, then leave on its own.
            _holdTimer += Time.deltaTime;
            if (_holdTimer >= endHold)
                GoToMenu();
        }

        private void GoToMenu()
        {
            if (_leaving)
                return;

            _leaving = true;
            mainMenuScene.LoadScene();
        }

        private static bool AnyKeyPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame
                                  || mouse.rightButton.wasPressedThisFrame
                                  || mouse.middleButton.wasPressedThisFrame))
                return true;

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame
                                    || gamepad.startButton.wasPressedThisFrame))
                return true;

            return false;
        }
    }
}
