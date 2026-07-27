using CardsChaos.Cards;
using UnityEngine;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Views.GameplayHud;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The always-on overlay for the room: how full the hand is, the two screens the player can
    /// open, the pile/fan switch, and a row of skills. It owns none of what those things mean - it
    /// counts the hand the view model hands it, forwards a click, and draws state back.
    ///
    /// Each button is its own little component (see the Hud* behaviours); the view's job is only to
    /// hand each one what it needs and to translate the hand's comings and goings into the counter's
    /// kick, its flinch, and the one-time throw hint.
    /// </summary>
    public class GameplayHudView : View<IGameplayHudViewModel>
    {
        [Header("Hand counter")]
        [SerializeField] private HudHandCounter handCounter;

        [Header("Screen buttons")]
        [SerializeField] private HudPanelButton albumButton;
        [SerializeField] private HudPanelButton upgradesButton;

        [Header("Hand switch")]
        [SerializeField] private HudHandSwitchButton handSwitchButton;

        [Header("Throw hint")]
        [SerializeField] private HudThrowHint throwHint;

        [Header("Rotate-camera hint")]
        [SerializeField] private HudRotateCameraHint rotateCameraHint;

        [Header("Skills")]
        [SerializeField] private HudSkillButton[] skillButtons;

        private int _shownCount = -1;
        private int _shownMax = -1;
        private bool _throwHintPlayed;
        private bool _wired;

        protected override void InitialViewSetup(IViewInitData viewInitData)
        {
            base.InitialViewSetup(viewInitData);

            if (albumButton != null)
                albumButton.Initialize(
                    ViewModel.ToggleAlbum, ViewModel.GetActionKeyDisplay(GameInputActions.ToggleAlbum));

            if (upgradesButton != null)
                upgradesButton.Initialize(
                    ViewModel.ToggleUpgrades, ViewModel.GetActionKeyDisplay(GameInputActions.ToggleUpgrades));

            if (handSwitchButton != null)
                handSwitchButton.Initialize(
                    ViewModel.Hand, ViewModel.ToggleHandLayout,
                    ViewModel.GetActionKeyDisplay(GameInputActions.ToggleHand));

            if (throwHint != null)
                throwHint.Initialize(ViewModel.GetActionKeyDisplay(GameInputActions.Throw));

            // Its own 3-second wait starts here, at the top of the session.
            if (rotateCameraHint != null)
                rotateCameraHint.Play();

            if (skillButtons != null)
            {
                foreach (HudSkillButton skill in skillButtons)
                {
                    if (skill != null)
                        skill.Initialize(ViewModel);
                }
            }

            RefreshCounts(punch: false);

            CardHand hand = ViewModel.Hand;
            if (hand != null)
            {
                hand.Changed += OnHandChanged;
                hand.PickUpRejected += OnPickUpRejected;
            }

            ViewModel.SkillsChanged += OnSkillsChanged;

            _wired = true;
        }

        private void Update()
        {
            // The max is the only count with no event behind it - the Extra Card Slot upgrade just
            // sets it - so it is polled. It moves seldom, and the check is a single int compare.
            if (_wired && ViewModel.Hand != null && ViewModel.Hand.SlotCount != _shownMax)
                RefreshCounts(punch: false);
        }

        private void OnHandChanged()
        {
            CardHand hand = ViewModel.Hand;
            if (hand == null)
                return;

            int count = hand.Cards.Count;

            // Order-only changes - the wheel, a sort - leave the count where it was and should not
            // kick the readout. A real gain or loss does.
            bool countMoved = count != _shownCount;
            bool gained = count > _shownCount;

            RefreshCounts(punch: countMoved);

            // The very first card off the floor earns the throw hint, once for the scene's life.
            if (gained && !_throwHintPlayed)
            {
                _throwHintPlayed = true;
                throwHint?.Play();
            }
        }

        private void OnPickUpRejected() => handCounter?.PlayShake();

        private void OnSkillsChanged()
        {
            if (skillButtons == null)
                return;

            foreach (HudSkillButton skill in skillButtons)
            {
                if (skill != null)
                    skill.RefreshOwned();
            }
        }

        private void RefreshCounts(bool punch)
        {
            CardHand hand = ViewModel.Hand;
            if (hand == null || handCounter == null)
                return;

            _shownCount = hand.Cards.Count;
            _shownMax = hand.SlotCount;

            handCounter.SetCounts(_shownCount, _shownMax);

            if (punch)
                handCounter.PlayPunch();
        }

        protected override void OnDestroy()
        {
            if (_wired && ViewModel != null && ViewModel.Hand != null)
            {
                ViewModel.Hand.Changed -= OnHandChanged;
                ViewModel.Hand.PickUpRejected -= OnPickUpRejected;
            }

            if (ViewModel != null)
                ViewModel.SkillsChanged -= OnSkillsChanged;

            base.OnDestroy();
        }
    }
}
