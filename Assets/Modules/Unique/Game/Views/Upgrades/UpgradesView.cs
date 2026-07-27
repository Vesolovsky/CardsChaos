using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game.Services.Hud;
using Vesolovsky.Game.Views.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The upgrades screen: the skill-point count up top, a row per upgrade in the middle, and a
    /// back button that hides it again.
    ///
    /// The view spawns a skill row for each permanent upgrade and skill and a task row for each
    /// one-time upgrade, then leaves each row to talk to the view model on its own. Task rows are
    /// refreshed every time the screen opens - the only moment their progress can have moved, since
    /// nothing can be completed while the screen is up.
    /// </summary>
    public class UpgradesView : View<IUpgradesViewModel>
    {
        [Header("Header")]
        [SerializeField] private VText skillPointsText;
        [SerializeField] private VButton backButton;

        [Header("Content")]
        [Tooltip("The layout group the rows are spawned into.")]
        [SerializeField] private Transform contentLayoutGroup;

        [SerializeField] private UpgradeSkillItem skillItemPrefab;
        [SerializeField] private UpgradeTaskItem taskItemPrefab;

        private readonly List<UpgradeSkillItem> _skillItems = new List<UpgradeSkillItem>();
        private readonly List<UpgradeTaskItem> _taskItems = new List<UpgradeTaskItem>();

        private DiContainer _container;
        private IGameplayPanels _panels;
        private IInputActions _input;
        private InputAction _toggleAction;

        [Inject]
        private void InjectContainer(
            DiContainer container,
            [InjectOptional] IGameplayPanels panels,
            [InjectOptional] IInputActions input)
        {
            _container = container;
            _panels = panels;
            _input = input;
        }

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            if (skillPointsText != null)
                skillPointsText.Bind(ViewModel.SkillPoints);

            if (backButton != null)
                backButton.Bind(ViewModel.Close);

            BuildItems();

            // The HUD's upgrades button pulls the same lever the toggle key does.
            if (_panels != null)
                _panels.UpgradesToggleRequested += Toggle;

            if (_input != null)
                _toggleAction = _input.Find(GameInputActions.ToggleUpgrades);

            ViewModel.IsOpen
                .Subscribe(OnIsOpenChanged)
                .AddTo(this);
        }

        protected override void OnDestroy()
        {
            if (_panels != null)
                _panels.UpgradesToggleRequested -= Toggle;

            base.OnDestroy();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || ViewModel == null)
                return;

            if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
            {
                Toggle();
                return;
            }

            // Escape only ever closes, so it never fights other panels for the key.
            if (ViewModel.IsOpen.Value && keyboard.escapeKey.wasPressedThisFrame)
                ViewModel.Close();
        }

        /// <summary>Opens the screen if it is shut and shuts it if it is open - the U key and the
        /// HUD's upgrades button both land here.</summary>
        private void Toggle()
        {
            if (ViewModel == null)
                return;

            if (ViewModel.IsOpen.Value)
                ViewModel.Close();
            else
                ViewModel.Open();
        }

        private void BuildItems()
        {
            if (contentLayoutGroup == null || skillItemPrefab == null || taskItemPrefab == null)
            {
                Debug.LogError($"[{nameof(UpgradesView)}] Content parent or item prefabs not assigned.", this);
                return;
            }

            foreach (var permanent in ViewModel.Permanents)
            {
                if (permanent == null)
                    continue;

                UpgradeSkillItem item = _container.InstantiatePrefabForComponent<UpgradeSkillItem>(
                    skillItemPrefab, contentLayoutGroup);

                item.Bind(ViewModel, permanent, isPermanent: true);
                _skillItems.Add(item);
            }

            foreach (var skill in ViewModel.Skills)
            {
                if (skill == null)
                    continue;

                UpgradeSkillItem item = _container.InstantiatePrefabForComponent<UpgradeSkillItem>(
                    skillItemPrefab, contentLayoutGroup);

                item.Bind(ViewModel, skill, isPermanent: false);
                _skillItems.Add(item);
            }

            foreach (var oneTime in ViewModel.OneTimes)
            {
                if (oneTime == null)
                    continue;

                UpgradeTaskItem item = _container.InstantiatePrefabForComponent<UpgradeTaskItem>(
                    taskItemPrefab, contentLayoutGroup);

                item.Bind(ViewModel, oneTime);
                _taskItems.Add(item);
            }
        }

        private void OnIsOpenChanged(bool isOpen)
        {
            if (isOpen)
            {
                RefreshItems();
                Show(destroyCancellationToken).Forget();
            }
            else
            {
                Hide(destroyCancellationToken).Forget();
            }
        }

        /// <summary>
        /// Re-reads every row on open. Task rows may have moved since last time; skill rows will
        /// not have, but they are cheap to re-assert and it keeps the screen honest if points were
        /// spent elsewhere.
        /// </summary>
        private void RefreshItems()
        {
            foreach (UpgradeSkillItem item in _skillItems)
                item.Refresh(animate: false);

            foreach (UpgradeTaskItem item in _taskItems)
                item.Refresh();
        }
    }
}
