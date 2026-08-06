using CardsChaos.Cards;
using UnityEngine;
using Vesolovsky.Core.Services.Input;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Upgrades;
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
    /// kick, its flinch, and the hints it raises on the shared <see cref="HudHint"/> queue - camera
    /// rotation at the start, throwing on the first pickup, the wheel once the hand holds more than
    /// one, and each skill as it becomes ready.
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

        [Header("Hints")]
        [SerializeField] private HudHint hint;

        [Header("Skills")]
        [SerializeField] private HudSkillButton[] skillButtons;

        // The skills that can announce themselves ready, and the hint each raises. A skill's ready
        // state is polled; each time it turns ready - unlocked, or a cooldown just ended - its hint
        // is raised. The state a skill loads in is seeded without a hint, so an already-ready skill
        // does not announce itself the instant the scene opens.
        private static readonly (SkillId Skill, HintId Hint)[] SkillHints =
        {
            (SkillId.CardMagnet, HintId.CardMagnetReady),
            (SkillId.SmartAlbumOpen, HintId.SmartAlbumOpenReady),
            (SkillId.HandSort, HintId.HandSortReady),
            (SkillId.Levitate, HintId.LevitateReady),
        };

        private readonly bool[] _skillReady = new bool[SkillHints.Length];

        private int _shownCount = -1;
        private int _shownMax = -1;
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

            if (hint != null)
            {
                hint.Initialize(ViewModel.GetActionKeyDisplay);

                // Honour the "Show hints" setting before anything is raised, so a player who has it
                // off never sees even the first nudge.
                hint.SetEnabled(ViewModel.HintsEnabled);

                // First nudge of the session: how to turn the camera. Its own start delay (authored
                // on the hint) lets the scene settle before it drops in, so this only queues it.
                hint.Show(HintId.RotateCamera);
            }

            SeedSkillReady();

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
            ViewModel.BindingsChanged += RefreshBindings;
            ViewModel.HintsEnabledChanged += OnHintsEnabledChanged;

            _wired = true;
        }

        private void OnHintsEnabledChanged() => hint?.SetEnabled(ViewModel.HintsEnabled);

        private void Update()
        {
            if (!_wired)
                return;

            // The max is the only count with no event behind it - the Extra Card Slot upgrade just
            // sets it - so it is polled. It moves seldom, and the check is a single int compare.
            if (ViewModel.Hand != null && ViewModel.Hand.SlotCount != _shownMax)
                RefreshCounts(punch: false);

            // Skill readiness has no event either - a cooldown simply runs out - so the transition
            // to ready is watched here and turned into the skill's "ready" hint.
            PollSkillReady();
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

            // The very first card off the floor earns the throw hint. The presenter only shows it
            // once, so raising it on every gain is harmless.
            if (gained)
                hint?.Show(HintId.ThrowCard);

            // The first time there is more than one card to choose between, teach the wheel. Same
            // deal - the presenter shows it once, so raising it whenever the hand is deep is fine.
            if (count > 1)
                hint?.Show(HintId.CycleCards);
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

        /// <summary>
        /// Records each skill's ready state as the scene loads, so a skill that is already ready
        /// then does not raise its hint on the first frame - only a fresh turn to ready does.
        /// </summary>
        private void SeedSkillReady()
        {
            for (int i = 0; i < SkillHints.Length; i++)
                _skillReady[i] = ViewModel.IsSkillReady(SkillHints[i].Skill);
        }

        /// <summary>
        /// Raises a skill's "ready" hint on the frame it turns ready - whether that is its unlock or
        /// a cooldown ending. The hint is a recurring one, so it plays each time round.
        /// </summary>
        private void PollSkillReady()
        {
            if (hint == null)
                return;

            for (int i = 0; i < SkillHints.Length; i++)
            {
                bool ready = ViewModel.IsSkillReady(SkillHints[i].Skill);

                if (ready && !_skillReady[i])
                    hint.Show(SkillHints[i].Hint);

                _skillReady[i] = ready;
            }
        }

        private void RefreshBindings()
        {
            albumButton?.SetKeyDisplay(
                ViewModel.GetActionKeyDisplay(GameInputActions.ToggleAlbum));
            upgradesButton?.SetKeyDisplay(
                ViewModel.GetActionKeyDisplay(GameInputActions.ToggleUpgrades));
            handSwitchButton?.SetKeyDisplay(
                ViewModel.GetActionKeyDisplay(GameInputActions.ToggleHand));

            if (skillButtons == null)
                return;

            foreach (HudSkillButton skill in skillButtons)
                skill?.RefreshBinding();
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
            {
                ViewModel.SkillsChanged -= OnSkillsChanged;
                ViewModel.BindingsChanged -= RefreshBindings;
                ViewModel.HintsEnabledChanged -= OnHintsEnabledChanged;
            }

            base.OnDestroy();
        }
    }
}
