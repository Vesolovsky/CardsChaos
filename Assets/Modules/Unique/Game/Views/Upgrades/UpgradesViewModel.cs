using System;
using System.Collections.Generic;
using System.Globalization;
using CardsChaos.Cards;
using UniRx;
using UnityEngine;
using Vesolovsky.Core.Services;
using Vesolovsky.Core.Services.Wallet;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Services.Progress;
using Vesolovsky.Game.Services.Skills;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Views
{
    /// <summary>
    /// The upgrades screen's brain. It reads the upgrade system, spends skill points through it,
    /// and turns a one-time upgrade's raw objective into the words and the bar the task row shows.
    ///
    /// While it is open it takes the room the way the album does - so the camera and card table go
    /// quiet - and raises the skill gate on top, so even the skills that ignore the room lock fall
    /// silent too.
    /// </summary>
    public class UpgradesViewModel : ViewModel, IUpgradesViewModel
    {
        // Rich-text colour the set names in a task line are written in - the album's warm gold.
        private const string SetNameColor = "#C39258";

        // Between the number in force and the number on offer. Written as an escape rather than the
        // character itself so the literal cannot be mangled by whatever encoding an editor decides
        // to save this file in. If it ever renders as an empty box the row's font atlas has no
        // U+2192 - either add the glyph to it, or make this "->".
        private const string Arrow = "\u2192";

        private readonly IUpgradeService _upgrades;
        private readonly ICollectionProgress _progress;
        private readonly IWalletService _wallet;
        private readonly IWorldInteractionLock _worldLock;
        private readonly ISkillGate _skillGate;
        private readonly UpgradeCatalog _catalog;

        private readonly ReactiveProperty<bool> _isOpen = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<long> _skillPoints = new ReactiveProperty<long>(0);

        // Built once from the catalog: only what can actually be bought. A task-unlocked upgrade
        // (Levitate, Déjà vu) has no price and no shop row - it is earned through its task, which
        // shows in the OneTimes list like every other one-time reward.
        private readonly List<PermanentUpgradeDefinition> _buyablePermanents =
            new List<PermanentUpgradeDefinition>();

        private readonly List<SkillDefinition> _buyableSkills = new List<SkillDefinition>();

        private IDisposable _worldHandle;

        public IReadOnlyReactiveProperty<bool> IsOpen => _isOpen;

        public IReadOnlyReactiveProperty<long> SkillPoints => _skillPoints;

        public IReadOnlyList<PermanentUpgradeDefinition> Permanents => _buyablePermanents;

        public IReadOnlyList<SkillDefinition> Skills => _buyableSkills;

        public IReadOnlyList<OneTimeUpgradeDefinition> OneTimes => _catalog.OneTimes;

        [Inject]
        public UpgradesViewModel(
            IUpgradeService upgrades,
            ICollectionProgress progress,
            IWalletService wallet,
            IWorldInteractionLock worldLock,
            ISkillGate skillGate,
            UpgradeCatalog catalog)
        {
            _upgrades = upgrades;
            _progress = progress;
            _wallet = wallet;
            _worldLock = worldLock;
            _skillGate = skillGate;
            _catalog = catalog;

            foreach (PermanentUpgradeDefinition permanent in _catalog.Permanents)
            {
                if (permanent != null && !permanent.IsTaskUnlocked)
                    _buyablePermanents.Add(permanent);
            }

            foreach (SkillDefinition skill in _catalog.Skills)
            {
                if (skill != null && !skill.IsTaskUnlocked)
                    _buyableSkills.Add(skill);
            }

            // Tracked live so a purchase, a page reward or a cheat is reflected in the header at
            // once. The balance itself is read fresh on each open (see Open).
            _wallet.RealCurrencyChanged += OnCurrencyChanged;
        }

        public void Open()
        {
            if (_isOpen.Value)
                return;

            // Do not stack on top of the album or a card close-up - they already hold the room.
            if (_worldLock.IsLocked)
                return;

            _worldHandle = _worldLock.Acquire(this);
            _skillGate.Blocked = true;

            RefreshSkillPoints();
            _isOpen.Value = true;
        }

        public void Close()
        {
            if (!_isOpen.Value)
                return;

            _isOpen.Value = false;
            _skillGate.Blocked = false;
            ReleaseWorld();
        }

        public int GetLevel(LeveledUpgradeDefinition definition) => _upgrades.GetLevel(definition);

        /// <summary>
        /// The blurb for this row, with every number that a purchase would move written as
        /// "now→after". A description writes {0} where its own number goes, {1} for its cooldown,
        /// and both come back as that pair.
        ///
        /// Showing one number could not work. Quote the level on offer and the last two states of
        /// an upgrade read identically - at 4/5 the offer is level 5 and at 5/5 there is nothing
        /// past level 5 either, so "5 cards, 60s" means both "buy this" and "you have this". Quote
        /// the level in force instead and the bottom collapses the same way: an unbought upgrade
        /// is level 0, which reads "pull up to 0 cards". Showing the step makes every state
        /// distinct on its own numbers, with no prefix needed to say which of the two it is.
        ///
        /// The ends are the exception, and only because there is genuinely nothing to compare
        /// against: unbought there is no value in force, and maxed there is none on offer, so those
        /// two show their single number.
        ///
        /// The cooldown is added here rather than authored into each skill's text: it is the same
        /// phrase every time, and one of them saying it differently would be a bug nobody would
        /// notice. Both it and the arrows are kept terse - these rows are not wide.
        /// </summary>
        public string GetDescription(LeveledUpgradeDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            string description = definition.Description ?? string.Empty;

            // No levels authored - there is no number to speak of, so the text stands as written.
            if (definition.MaxLevel <= 0)
                return description;

            int owned = Mathf.Clamp(_upgrades.GetLevel(definition), 0, definition.MaxLevel);

            // The level in force and the level on offer. They are the same level at either end,
            // which is exactly what collapses the step back to a single number there.
            int from = Mathf.Max(1, owned);
            int to = Mathf.Clamp(owned + 1, 1, definition.MaxLevel);

            string value = Step(definition.GetValue(from), definition.GetValue(to));

            // Only skills wait to be used again. The cooldowns are the ones authored on the levels,
            // not what the reduction rewards leave of them - the row is describing what these
            // levels are, and folding Traveler in would make the same level read differently to two
            // players looking at the same shop.
            var skill = definition as SkillDefinition;
            if (skill == null)
                return Fill(definition, description, value, string.Empty);

            string cooldown = Step(skill.GetCooldown(from), skill.GetCooldown(to));
            description = Fill(definition, description, value, cooldown);

            // One line, not two: some of these rows are narrow enough that a second line would be
            // clipped, so the cooldown joins the sentence instead of sitting under it.
            return $"{description}{Separator(description)}Cooldown: {cooldown}s";
        }

        public int GetMaxLevel(LeveledUpgradeDefinition definition) =>
            definition != null ? definition.MaxLevel : 0;

        public int GetNextCost(LeveledUpgradeDefinition definition)
        {
            if (definition == null)
                return 0;

            int level = _upgrades.GetLevel(definition);
            return level >= definition.MaxLevel ? 0 : definition.GetCost(level + 1);
        }

        public bool IsMaxed(LeveledUpgradeDefinition definition) =>
            definition != null && _upgrades.GetLevel(definition) >= definition.MaxLevel;

        public bool CanAfford(LeveledUpgradeDefinition definition) =>
            definition != null && !IsMaxed(definition) && _skillPoints.Value >= GetNextCost(definition);

        public bool TryLevelUp(LeveledUpgradeDefinition definition) => _upgrades.TryLevelUp(definition);

        public bool IsUnlocked(OneTimeUpgradeDefinition definition) => _upgrades.IsUnlocked(definition);

        public bool TryClaim(OneTimeUpgradeDefinition definition) => _upgrades.TryClaim(definition);

        public bool DebugForceClaim(OneTimeUpgradeDefinition definition)
        {
            if (definition == null || _upgrades.IsUnlocked(definition))
                return false;

            _upgrades.DebugForceUnlock(definition);
            return true;
        }

        public UpgradeTaskProgress GetTaskProgress(OneTimeUpgradeDefinition definition)
        {
            if (definition == null || definition.Objective == null)
                return new UpgradeTaskProgress(string.Empty, string.Empty, 0f, false, false);

            CollectionObjective objective = definition.Objective;
            int required = objective.Required;
            int completed = objective.GetCompleted(_progress);
            float ratio = required > 0 ? Mathf.Clamp01((float)completed / required) : 0f;

            return new UpgradeTaskProgress(
                Describe(objective),
                Remaining(objective, required, completed),
                ratio,
                objective.IsSatisfied(_progress),
                _upgrades.IsUnlocked(definition));
        }

        public override void Dispose()
        {
            _wallet.RealCurrencyChanged -= OnCurrencyChanged;

            // A view torn down while open must not leave the room locked or the skills gated.
            _skillGate.Blocked = false;
            ReleaseWorld();

            base.Dispose();
        }

        private void OnCurrencyChanged(CurrencyType type, long value)
        {
            if (type == CurrencyType.SkillPoints)
                _skillPoints.Value = value;
        }

        /// <summary>
        /// Puts the level's numbers into the blurb. A description with no braces in it comes back
        /// untouched, which is how an upgrade whose value would mean nothing on screen - a fog
        /// radius, a walking speed - simply says what it does instead.
        ///
        /// A malformed description is reported and then shown as written rather than thrown: this
        /// runs while the shop is being built, and one mistyped brace should cost that row its
        /// numbers, not the whole screen.
        /// </summary>
        private static string Fill(
            LeveledUpgradeDefinition definition, string description, string value, string cooldown)
        {
            try
            {
                return string.Format(description, value, cooldown);
            }
            catch (FormatException)
            {
                Debug.LogError(
                    $"[{nameof(UpgradesViewModel)}] '{definition.Id}' has a description the level " +
                    $"numbers cannot be put into: \"{description}\". Only {{0}} (the value) and " +
                    "{1} (the cooldown) are available.", definition);

                return description;
            }
        }

        /// <summary>
        /// One number when a purchase would not move it - or when there is no purchase left to make
        /// - and "now→after" when it would. Collapsing the two-the-same case matters beyond tidiness:
        /// "60→60s" would advertise an upgrade that changes nothing.
        /// </summary>
        private static string Step(float from, float to) =>
            Mathf.Approximately(from, to)
                ? Number(to)
                : $"{Number(from)}{Arrow}{Number(to)}";

        /// <summary>
        /// What goes between the blurb and the cooldown. Descriptions are authored without a full
        /// stop, so one is supplied - unless the author ended the sentence themselves, in which case
        /// adding a second would be the only thing anyone noticed.
        /// </summary>
        private static string Separator(string description)
        {
            if (string.IsNullOrEmpty(description))
                return string.Empty;

            char last = description[description.Length - 1];
            return last == '.' || last == '!' || last == '?' ? " " : ". ";
        }

        // Trailing zeroes dropped, and invariant so a comma decimal separator cannot creep in from
        // the machine's locale: 1 stays "1" and 1.25 stays "1.25" wherever the game is played.
        private static string Number(float value) =>
            value.ToString("0.##", CultureInfo.InvariantCulture);

        private void RefreshSkillPoints() =>
            _skillPoints.Value = _wallet.GetRealCurrencyBalance(CurrencyType.SkillPoints);

        private void ReleaseWorld()
        {
            _worldHandle?.Dispose();
            _worldHandle = null;
        }

        private string Describe(CollectionObjective objective)
        {
            switch (objective.Kind)
            {
                case CollectionObjective.ObjectiveKind.CompleteSpecificSets:
                    // Only what is still missing is named, so a task half done reads as the work
                    // that is left rather than as a list the player has to check off themselves.
                    List<CardSetDefinition> named = CollectSets(objective.Sets, onlyUnfinished: true);

                    // Nothing left - the task is done. The line falls back to the whole list so it
                    // still reads as a sentence while the row waits to be claimed.
                    if (named.Count == 0)
                        named = CollectSets(objective.Sets, onlyUnfinished: false);

                    string names = JoinSetNames(named);
                    string unit = named.Count == 1 ? "set" : "sets";
                    return $"Fully complete {names} {unit} to unlock this ability";

                case CollectionObjective.ObjectiveKind.CompletePages:
                    return $"Fully complete {objective.Count} pages to unlock this ability";

                case CollectionObjective.ObjectiveKind.CompleteAnySets:
                    return $"Fully complete {objective.Count} sets to unlock this ability";

                case CollectionObjective.ObjectiveKind.StoreDuplicates:
                    return $"Put {objective.Count} duplicates away in the box to unlock this ability";

                default:
                    return string.Empty;
            }
        }

        private static string Remaining(CollectionObjective objective, int required, int completed)
        {
            int remaining = Mathf.Max(0, required - completed);
            string unit = objective.UnitName;

            if (remaining != 1)
                unit += "s";

            return $"{remaining} {unit} remaining";
        }

        /// <summary>
        /// The sets an objective names, optionally thinned to the ones still unfinished. Nulls are
        /// dropped either way, so the caller can count the result as the number it puts in the text.
        /// </summary>
        private List<CardSetDefinition> CollectSets(
            IReadOnlyList<CardSetDefinition> sets, bool onlyUnfinished)
        {
            var result = new List<CardSetDefinition>();
            if (sets == null)
                return result;

            foreach (CardSetDefinition set in sets)
            {
                if (set == null)
                    continue;

                if (onlyUnfinished && _progress.IsSetCompleted(set.SetId))
                    continue;

                result.Add(set);
            }

            return result;
        }

        /// <summary>
        /// The set names run together, each picked out in the album's warm gold and bolded so the
        /// names stand off the rest of the sentence rather than dissolving into it.
        /// </summary>
        private static string JoinSetNames(IReadOnlyList<CardSetDefinition> sets)
        {
            if (sets == null)
                return string.Empty;

            var names = new List<string>();
            foreach (CardSetDefinition set in sets)
            {
                if (set != null)
                    names.Add($"<b><color={SetNameColor}>{set.SetName}</color></b>");
            }

            return string.Join(", ", names);
        }
    }
}
