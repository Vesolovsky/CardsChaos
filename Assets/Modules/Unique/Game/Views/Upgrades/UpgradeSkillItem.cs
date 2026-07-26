using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Upgrades;

namespace Vesolovsky.Game.Views.Upgrades
{
    /// <summary>
    /// One row on the upgrades screen for a leveled upgrade - a permanent upgrade or a skill.
    ///
    /// It shows the upgrade's face and blurb, a track of diamonds for its levels, and a buy button
    /// whose cost updates as levels are bought. Buying is delegated to the view model; the row only
    /// asks and then redraws itself from the result, so the level track and the cost stay honest
    /// without anything else having to refresh it.
    /// </summary>
    [AddComponentMenu("CardsChaos/Upgrades/Skill Item")]
    public class UpgradeSkillItem : MonoBehaviour
    {
        private const string PermanentLabel = "Permanent";
        private const string SkillLabel = "Skill";
        private const string CompletedLabel = "Completed";

        // The dimmed alpha the buy label drops to once the upgrade is maxed - 64 of 255.
        private const float DimLabelAlpha = 64f / 255f;

        [Header("Info")]
        [SerializeField] private Image icon;
        [SerializeField] private VText typeLabel;
        [SerializeField] private VText nameText;
        [SerializeField] private VText descriptionText;
        [SerializeField] private VText levelText;

        [Header("Levels")]
        [Tooltip("The layout group the level diamonds spawn into.")]
        [SerializeField] private Transform skillLevelsContainer;
        [SerializeField] private UpgradeSkillLevel skillLevelPrefab;

        [Header("Buy")]
        [SerializeField] private VText costText;
        [SerializeField] private VButton upgradeButton;

        [Tooltip("The buy button's label. Dimmed once the upgrade is fully bought.")]
        [SerializeField] private VText upgradeButtonLabel;

        private readonly List<UpgradeSkillLevel> _levels = new List<UpgradeSkillLevel>();

        private IUpgradesViewModel _viewModel;
        private LeveledUpgradeDefinition _definition;
        private float _defaultLabelAlpha = 1f;

        public void Bind(IUpgradesViewModel viewModel, LeveledUpgradeDefinition definition, bool isPermanent)
        {
            _viewModel = viewModel;
            _definition = definition;

            ApplyIcon(icon, definition.Icon);

            if (typeLabel != null)
                typeLabel.SetText(isPermanent ? PermanentLabel : SkillLabel);

            if (nameText != null)
                nameText.SetText(definition.DisplayName);

            if (descriptionText != null)
                descriptionText.SetText(definition.Description);

            if (upgradeButtonLabel != null)
                _defaultLabelAlpha = upgradeButtonLabel.color.a;

            BuildLevels(definition.MaxLevel);

            if (upgradeButton != null)
                upgradeButton.Bind(OnUpgradeClicked);

            Refresh(animate: false);
        }

        /// <summary>Redraws the row from the view model. Animates the fill only after a purchase.</summary>
        public void Refresh(bool animate)
        {
            if (_viewModel == null || _definition == null)
                return;

            int level = _viewModel.GetLevel(_definition);
            int max = _viewModel.GetMaxLevel(_definition);

            if (levelText != null)
                levelText.SetText($"Level {level}/{max}");

            for (int i = 0; i < _levels.Count; i++)
            {
                bool owned = i < level;

                // Only the level a purchase just lit fades; the rest are already where they belong.
                bool animateThis = animate && owned && i == level - 1;
                _levels[i].SetFilled(owned, animateThis);
            }

            bool maxed = _viewModel.IsMaxed(_definition);

            if (costText != null)
                costText.SetText(maxed ? CompletedLabel : $"Cost: {_viewModel.GetNextCost(_definition)} skill points");

            if (upgradeButton != null)
                upgradeButton.interactable = !maxed;

            SetLabelAlpha(maxed ? DimLabelAlpha : _defaultLabelAlpha);
        }

        private void OnUpgradeClicked()
        {
            if (_viewModel.TryLevelUp(_definition))
                Refresh(animate: true);
        }

        private void BuildLevels(int count)
        {
            foreach (UpgradeSkillLevel level in _levels)
            {
                if (level != null)
                    Destroy(level.gameObject);
            }

            _levels.Clear();

            if (skillLevelPrefab == null || skillLevelsContainer == null)
            {
                Debug.LogError($"[{nameof(UpgradeSkillItem)}] Missing level prefab or container.", this);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                UpgradeSkillLevel level = Instantiate(skillLevelPrefab, skillLevelsContainer);
                _levels.Add(level);
            }
        }

        private void SetLabelAlpha(float alpha)
        {
            if (upgradeButtonLabel == null)
                return;

            Color color = upgradeButtonLabel.color;
            color.a = alpha;
            upgradeButtonLabel.color = color;
        }

        /// <summary>An Image with no sprite draws a white box; switch it off instead.</summary>
        private static void ApplyIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
