using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Views.Upgrades
{
    /// <summary>
    /// One row for a one-time upgrade. It has two faces, held on separate canvas groups: the task
    /// - what to finish and how far along - and, once claimed, a trimmed skill face for the ability
    /// it granted.
    ///
    /// While the task is unmet the task face is up and the skill face hidden. Finishing the task
    /// lights the Unlock button; claiming it fades the task out and the ability in. On a later open
    /// an already-claimed upgrade simply shows its skill face from the start.
    /// </summary>
    [AddComponentMenu("CardsChaos/Upgrades/Task Item")]
    public class UpgradeTaskItem : MonoBehaviour
    {
        private const string ProgressLabel = "Progress";
        private const string CompletedLabel = "Completed";

        [Header("Info")]
        [SerializeField] private Image icon;

        [Header("Faces")]
        [SerializeField] private CanvasGroup taskItemsGroup;
        [SerializeField] private CanvasGroup skillItemsGroup;

        [Header("Task")]
        [Tooltip("The task face's heading. Was a fixed \"HOW TO UNLOCK\" label; now names the " +
                 "ability the task grants, taken from the upgrade's Display Name.")]
        [SerializeField] private VText unlockTitle;

        [SerializeField] private VText unlockTaskDescription;
        [SerializeField] private VText remainingText;

        [Tooltip("The progress bar fill. Anchored from the left; its width is scaled to the ratio.")]
        [SerializeField] private RectTransform fill;

        [SerializeField] private VText progressText;
        [SerializeField] private VButton unlockButton;

        [Header("Unlocked ability (trimmed skill face)")]
        [SerializeField] private VText abilityName;
        [SerializeField] private VText abilityDescription;

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.35f;

        private IUpgradesViewModel _viewModel;
        private OneTimeUpgradeDefinition _definition;
        private float _fullFillWidth;
        private Tween _claimTween;

        public void Bind(IUpgradesViewModel viewModel, OneTimeUpgradeDefinition definition)
        {
            _viewModel = viewModel;
            _definition = definition;

            // The icon is left as the prefab placeholder here on purpose - it is only swapped to the
            // ability's own icon once the upgrade is unlocked (see Refresh and PlayUnlockAnimation).

            if (fill != null)
                _fullFillWidth = fill.sizeDelta.x;

            // Both faces name the ability: the task heading tells the player what finishing the
            // task earns, and the skill face names it again once claimed.
            if (unlockTitle != null)
                unlockTitle.SetText(definition.DisplayName);

            if (abilityName != null)
                abilityName.SetText(definition.DisplayName);

            if (abilityDescription != null)
                abilityDescription.SetText(definition.Description);

            if (unlockButton != null)
                unlockButton.Bind(OnUnlockClicked);

            Refresh();
        }

        /// <summary>Reads the task's state and snaps the row to it. Called each time the screen opens.</summary>
        public void Refresh()
        {
            if (_viewModel == null || _definition == null)
                return;

            StopClaimTween();

            UpgradeTaskProgress progress = _viewModel.GetTaskProgress(_definition);

            if (progress.IsUnlocked)
            {
                // Already claimed on an earlier open: show the ability face and its own icon outright.
                SetTaskShown(false);
                ApplyIcon(icon, _definition.Icon);
                SetIconAlpha(1f);
                return;
            }

            // Still locked: the task face is up and the icon stays the placeholder, fully visible.
            SetTaskShown(true);
            SetIconAlpha(1f);

            if (unlockTaskDescription != null)
                unlockTaskDescription.SetText(progress.Description);

            if (remainingText != null)
                remainingText.SetText(progress.RemainingText);

            SetFill(progress.FillRatio);

            if (progressText != null)
                progressText.SetText(progress.IsComplete ? CompletedLabel : ProgressLabel);

            // The Unlock button only exists once the task is done; before that its object is off.
            if (unlockButton != null)
            {
                unlockButton.gameObject.SetActive(progress.IsComplete);
                unlockButton.interactable = progress.IsComplete;
            }
        }

        private void OnUnlockClicked()
        {
            if (_viewModel.TryClaim(_definition))
                PlayUnlockAnimation();
        }

        /// <summary>
        /// Fades the task face out - the icon along with it - then swaps in the ability's icon and
        /// fades the ability face and that new icon in together.
        /// </summary>
        private void PlayUnlockAnimation()
        {
            StopClaimTween();

            if (unlockButton != null)
                unlockButton.interactable = false;

            if (taskItemsGroup != null)
                taskItemsGroup.blocksRaycasts = false;

            _claimTween = Tween.Custom(1f, 0f, fadeDuration, FadeOutTaskAndIcon)
                .OnComplete(() =>
                {
                    if (taskItemsGroup != null)
                        taskItemsGroup.interactable = false;

                    // The placeholder has faded out; put the ability's icon in, hidden, so it rises
                    // together with the ability face.
                    ApplyIcon(icon, _definition.Icon);
                    SetIconAlpha(0f);

                    if (skillItemsGroup != null)
                        skillItemsGroup.blocksRaycasts = true;

                    // Kept in the same field so a teardown mid-fade stops whichever half is running.
                    _claimTween = Tween.Custom(0f, 1f, fadeDuration, FadeInSkillAndIcon);
                }, warnIfTargetDestroyed: false);
        }

        private void SetTaskShown(bool taskShown)
        {
            SetGroup(taskItemsGroup, taskShown);
            SetGroup(skillItemsGroup, !taskShown);
        }

        private static void SetGroup(CanvasGroup group, bool shown)
        {
            if (group == null)
                return;

            group.alpha = shown ? 1f : 0f;
            group.blocksRaycasts = shown;
            group.interactable = shown;
        }

        private void SetFill(float ratio)
        {
            if (fill == null)
                return;

            fill.sizeDelta = new Vector2(_fullFillWidth * Mathf.Clamp01(ratio), fill.sizeDelta.y);
        }

        private void FadeOutTaskAndIcon(float alpha)
        {
            if (taskItemsGroup != null)
                taskItemsGroup.alpha = alpha;

            SetIconAlpha(alpha);
        }

        private void FadeInSkillAndIcon(float alpha)
        {
            if (skillItemsGroup != null)
                skillItemsGroup.alpha = alpha;

            SetIconAlpha(alpha);
        }

        private void SetIconAlpha(float alpha)
        {
            if (icon == null)
                return;

            Color color = icon.color;
            color.a = alpha;
            icon.color = color;
        }

        private void StopClaimTween()
        {
            if (_claimTween.isAlive)
                _claimTween.Stop();
        }

        private static void ApplyIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void OnDestroy() => StopClaimTween();
    }
}
