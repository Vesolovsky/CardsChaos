using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// One skill on the HUD: fires it on click, hides itself until the player owns it, and while it
    /// cools down draws the wind-down two ways at once - a ring that fills as the cooldown runs and
    /// a countdown the player reads by hovering.
    ///
    /// A skill the player has not bought is simply not here - the whole object switches off. Owned,
    /// it fires only when ready; on cooldown the button goes dim and un-clickable, and clicking it
    /// anyway flinches the ring to say "not yet". The moment it comes back it gives the little kick
    /// that says so.
    /// </summary>
    [AddComponentMenu("CardsChaos/HUD/Hud Skill Button")]
    public class HudSkillButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Tooltip("Which skill this button fires and reads its state from.")]
        [SerializeField] private SkillId skillId;

        [SerializeField] private VButton button;

        [Tooltip("The radial-filled ring. Empty when the cooldown starts, full when it ends.")]
        [SerializeField] private Image circleFill;

        [SerializeField] private HudSlideLabel label;

        [Tooltip("What the little ready-kick scales. Usually the ring.")]
        [SerializeField] private RectTransform readyPunchTarget;

        [Tooltip("The hint when ready, with {0} where the trigger key goes. e.g. \"Card magnet [{0}]\".")]
        [SerializeField] private string labelFormat = "Card magnet [{0}]";

        [Header("Ready kick")]
        [Tooltip("The kick the ring takes the moment the skill comes off cooldown. Kept the same " +
                 "as the album's progress kick so the HUD and the album feel of a piece.")]
        [SerializeField] private Vector3 readyPunch = new Vector3(0.12f, 0.12f, 0f);

        [SerializeField] private float readyPunchDuration = 0.3f;
        [SerializeField] private float readyPunchFrequency = 3f;

        [Header("Not-ready flinch")]
        [Tooltip("The flinch the ring gives when the skill is clicked before it is ready - the same " +
                 "shake the album slot gives a card that does not belong.")]
        [SerializeField] private Vector3 notReadyShake = new Vector3(5f, 5f, 0f);

        [SerializeField] private float notReadyShakeDuration = 0.18f;
        [SerializeField] private float notReadyShakeFrequency = 22f;

        private IGameplayHudViewModel _viewModel;
        private bool _hovered;
        private bool _wasReady;
        private int _shownSeconds = -1;

        private Tween _punch;
        private Tween _shake;
        private Vector3 _shakeRest;

        private RectTransform PunchTarget =>
            readyPunchTarget != null ? readyPunchTarget
            : circleFill != null ? circleFill.rectTransform
            : null;

        public void Initialize(IGameplayHudViewModel viewModel)
        {
            _viewModel = viewModel;
            RefreshOwned();
        }

        /// <summary>
        /// Shows or hides the whole button by whether the skill is owned, and squares the ring and
        /// interactability to its current cooldown. Called on wire-up and whenever an upgrade is
        /// bought.
        /// </summary>
        public void RefreshOwned()
        {
            bool owned = _viewModel != null && _viewModel.IsSkillOwned(skillId);

            if (gameObject.activeSelf != owned)
                gameObject.SetActive(owned);

            if (!owned)
                return;

            _wasReady = _viewModel.IsSkillReady(skillId);

            if (button != null)
                button.interactable = _wasReady;

            ApplyFill();
            RefreshLabel();
        }

        private void Update()
        {
            if (_viewModel == null)
                return;

            ApplyFill();

            bool ready = _viewModel.IsSkillReady(skillId);
            if (ready != _wasReady)
            {
                _wasReady = ready;

                if (button != null)
                    button.interactable = ready;

                if (ready)
                    PlayReadyPunch();

                // The label flips between the name and the countdown on this same edge, but only
                // matters while it is actually out; hidden, the next hover rebuilds it.
                if (_hovered)
                    RefreshLabel();
            }

            // The countdown only has to keep up while the player is watching it.
            if (!ready && _hovered)
                UpdateCountdown();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            RefreshLabel();

            if (label != null)
                label.Show();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;

            if (label != null)
                label.Hide();
        }

        // The click is read here rather than off the button because a skill on cooldown is left
        // un-interactable, and an un-interactable button never raises its own click - but the flinch
        // is the whole point of clicking it too early, so the press still has to be heard.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _viewModel == null)
                return;

            if (_viewModel.IsSkillReady(skillId))
                _viewModel.TryActivateSkill(skillId);
            else
                PlayNotReadyShake();
        }

        private void ApplyFill()
        {
            if (circleFill == null)
                return;

            // Normalized is one when the cooldown starts and zero when it ends; the ring shows the
            // opposite, filling up as the wait runs down.
            circleFill.fillAmount = 1f - _viewModel.GetSkillCooldownNormalized(skillId);
        }

        private void RefreshLabel()
        {
            if (label == null)
                return;

            if (_viewModel.IsSkillReady(skillId))
            {
                label.SetText(string.Format(labelFormat, _viewModel.GetSkillKeyDisplay(skillId)));
                _shownSeconds = -1;
            }
            else
            {
                UpdateCountdown();
            }
        }

        private void UpdateCountdown()
        {
            if (label == null)
                return;

            int seconds = Mathf.CeilToInt(_viewModel.GetSkillCooldownRemaining(skillId));
            if (seconds == _shownSeconds)
                return;

            _shownSeconds = seconds;
            label.SetText(FormatCooldown(seconds));
        }

        // 1:30 over a minute, just the seconds under it.
        private static string FormatCooldown(int totalSeconds)
        {
            if (totalSeconds >= 60)
            {
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return $"{minutes}:{seconds:00}";
            }

            return totalSeconds.ToString();
        }

        private void PlayReadyPunch()
        {
            RectTransform target = PunchTarget;
            if (target == null)
                return;

            if (_punch.isAlive)
            {
                _punch.Stop();
                target.localScale = Vector3.one;
            }

            _punch = Tween.PunchScale(target, readyPunch, readyPunchDuration, readyPunchFrequency);
        }

        private void PlayNotReadyShake()
        {
            RectTransform target = PunchTarget;
            if (target == null)
                return;

            // Rest captured while still and restored before a re-triggered shake, so a mashed button
            // whose flinch is cut short never leaves the ring adrift.
            if (_shake.isAlive)
            {
                _shake.Stop();
                target.localPosition = _shakeRest;
            }
            else
            {
                _shakeRest = target.localPosition;
            }

            _shake = Tween.ShakeLocalPosition(target, notReadyShake, notReadyShakeDuration, notReadyShakeFrequency);
        }

        private void OnDestroy()
        {
            if (_punch.isAlive)
                _punch.Stop();

            if (_shake.isAlive)
                _shake.Stop();
        }
    }
}
