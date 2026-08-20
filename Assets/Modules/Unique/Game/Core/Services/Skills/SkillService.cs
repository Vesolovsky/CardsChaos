using System;
using System.Collections.Generic;
using UnityEngine;
using Vesolovsky.Game.Services.Pause;
using Vesolovsky.Game.Services.Upgrades;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// The one place a skill is fired from.
    ///
    /// It reads the skill's level off the upgrade service - level zero means locked and never
    /// fires - runs the cooldown clock, and hands the actual effect to the matching handler. A
    /// handler that declines (nothing to pull, one card to sort) is treated as never having fired,
    /// so it costs no cooldown.
    ///
    /// A handler that is also an <see cref="ITimedSkill"/> stays switched on for a spell after the
    /// cast. That window runs first and the cooldown only starts once it closes, so the wait the
    /// player sees is one unbroken stretch: the skill working, then the skill recovering. Both
    /// halves stop for the pause menu, and both go into the save.
    /// </summary>
    public class SkillService : ISkillService, ITickable
    {
        public event Action<SkillId> Activated;

        private readonly UpgradeCatalog _catalog;
        private readonly IUpgradeService _upgrades;
        private readonly IPauseState _pauseState;
        private readonly ISkillCooldownModifiers _cooldownModifiers;
        private readonly Dictionary<SkillId, ISkillHandler> _handlers = new Dictionary<SkillId, ISkillHandler>();

        private readonly Dictionary<SkillId, float> _cooldownRemaining = new Dictionary<SkillId, float>();
        private readonly Dictionary<SkillId, float> _cooldownTotal = new Dictionary<SkillId, float>();

        // The switched-on stretch of a timed skill. Kept apart from the cooldown rather than folded
        // into it because the difference is visible: one is the skill doing its work, the other is
        // the player waiting, and only the first can be read off by something like the duplicate
        // service asking whether Muscle Memory is running.
        private readonly Dictionary<SkillId, float> _activeRemaining = new Dictionary<SkillId, float>();
        private readonly Dictionary<SkillId, float> _activeTotal = new Dictionary<SkillId, float>();

        // Reused by the tick so running a clock down does not allocate a key list every frame.
        private readonly List<SkillId> _tickScratch = new List<SkillId>();

        [Inject]
        public SkillService(
            UpgradeCatalog catalog, IUpgradeService upgrades, List<ISkillHandler> handlers,
            IPauseState pauseState, ISkillCooldownModifiers cooldownModifiers)
        {
            _catalog = catalog;
            _upgrades = upgrades;
            _pauseState = pauseState;
            _cooldownModifiers = cooldownModifiers;

            foreach (ISkillHandler handler in handlers)
            {
                if (handler == null)
                    continue;

                if (!_handlers.TryAdd(handler.Id, handler))
                    Debug.LogError($"[{nameof(SkillService)}] Two handlers for skill '{handler.Id}'.");
            }
        }

        public bool TryActivate(SkillId id)
        {
            SkillDefinition definition = _catalog.FindSkill(id);
            if (definition == null)
            {
                Debug.LogError($"[{nameof(SkillService)}] No definition for skill '{id}'.");
                return false;
            }

            int level = EffectiveLevel(definition);
            if (level <= 0)
                return false;

            if (GetCooldownRemaining(id) > 0f)
                return false;

            if (!_handlers.TryGetValue(id, out ISkillHandler handler))
            {
                Debug.LogError($"[{nameof(SkillService)}] No handler for skill '{id}'.");
                return false;
            }

            if (!handler.CanActivate() || !handler.Activate(definition, level))
                return false;

            // The reductions cut the recovery, not the work: a level that buys a longer spell must
            // not have that spell shortened by Traveler.
            float cooldown = definition.GetCooldown(level) * _cooldownModifiers.GetMultiplier(id);
            _cooldownRemaining[id] = cooldown;
            _cooldownTotal[id] = cooldown;

            float active = handler is ITimedSkill timed
                ? Mathf.Max(0f, timed.GetActiveDuration(definition, level))
                : 0f;

            _activeRemaining[id] = active;
            _activeTotal[id] = active;

            Activated?.Invoke(id);
            return true;
        }

        public bool IsUnlocked(SkillId id)
        {
            SkillDefinition definition = _catalog.FindSkill(id);
            return definition != null && EffectiveLevel(definition) > 0;
        }

        public bool IsReady(SkillId id) => IsUnlocked(id) && GetCooldownRemaining(id) <= 0f;

        public bool IsActive(SkillId id) => GetActiveRemaining(id) > 0f;

        public float GetActiveRemaining(SkillId id) =>
            _activeRemaining.TryGetValue(id, out float remaining) ? remaining : 0f;

        public float GetActiveNormalized(SkillId id)
        {
            float total = _activeTotal.TryGetValue(id, out float t) ? t : 0f;
            return total > 0f ? Mathf.Clamp01(GetActiveRemaining(id) / total) : 0f;
        }

        /// <summary>
        /// The level a skill is "at" for firing it. The upgrade service already folds the two ways
        /// a skill can be owned - bought, or its unlocking task claimed - into one number, so this
        /// is only a null guard over it.
        /// </summary>
        private int EffectiveLevel(SkillDefinition definition) =>
            definition != null ? _upgrades.GetLevel(definition) : 0;

        // The whole wait, not just the cooldown half: a timed skill is unusable while it is still
        // running, and the countdown on the HUD should read the time until it can be cast again
        // rather than restarting when the spell ends.
        public float GetCooldownRemaining(SkillId id) =>
            GetActiveRemaining(id) + RawCooldownRemaining(id);

        /// <summary>
        /// The wind-down of the phase the skill is in right now, not of the whole wait: while a
        /// timed skill is working this is its window running out, and once that closes it starts
        /// over as the cooldown running out.
        ///
        /// Two sweeps rather than one deliberately - a single bar spanning both would leave the
        /// player no way to see the moment the skill stops working, which is the one moment they
        /// need. The seconds are reported the same way: see <see cref="GetActiveRemaining"/>.
        /// </summary>
        public float GetCooldownNormalized(SkillId id)
        {
            if (IsActive(id))
                return GetActiveNormalized(id);

            float total = CooldownTotal(id);
            return total > 0f ? Mathf.Clamp01(RawCooldownRemaining(id) / total) : 0f;
        }

        public IReadOnlyList<SkillCooldownSnapshot> GetActiveCooldowns()
        {
            var snapshots = new List<SkillCooldownSnapshot>();

            // Keyed off the cooldown map: a skill is never given an active window without one, so
            // this covers every skill with anything left to run.
            foreach (KeyValuePair<SkillId, float> entry in _cooldownRemaining)
            {
                float active = GetActiveRemaining(entry.Key);
                if (entry.Value <= 0f && active <= 0f)
                    continue;

                float total = _cooldownTotal.TryGetValue(entry.Key, out float t) ? t : entry.Value;
                snapshots.Add(new SkillCooldownSnapshot(
                    entry.Key, entry.Value, total, active, ActiveTotal(entry.Key)));
            }

            return snapshots;
        }

        public void RestoreCooldown(
            SkillId id, float remaining, float total, float activeRemaining, float activeTotal)
        {
            if (remaining <= 0f && activeRemaining <= 0f)
                return;

            _cooldownRemaining[id] = Mathf.Max(0f, remaining);
            _cooldownTotal[id] = total > 0f ? total : remaining;

            if (activeRemaining <= 0f)
                return;

            _activeRemaining[id] = activeRemaining;
            _activeTotal[id] = activeTotal > 0f ? activeTotal : activeRemaining;
        }

        public void DebugResetCooldowns()
        {
            // Drop every running clock; a missing entry reads as ready and as not active, so
            // clearing is enough and the HUD's per-frame poll picks the change up on its own.
            _cooldownRemaining.Clear();
            _cooldownTotal.Clear();
            _activeRemaining.Clear();
            _activeTotal.Clear();
        }

        public void Tick()
        {
            // Cooldowns run on game time, and the pause menu stops the clock - a wait held over the
            // pause should come back with exactly as long left as it went in with. The active
            // window is stopped with it: a spell must not burn away behind a menu.
            if (_pauseState != null && _pauseState.IsPaused)
                return;

            if (_cooldownRemaining.Count == 0)
                return;

            float delta = Time.deltaTime;

            // Copied keys because the values are edited inside the loop; the scratch list is reused
            // because this runs every frame the room is up.
            _tickScratch.Clear();
            _tickScratch.AddRange(_cooldownRemaining.Keys);

            foreach (SkillId id in _tickScratch)
            {
                // The spell first, the recovery after it - the cooldown does not start ticking
                // until the skill has finished doing its work.
                float active = GetActiveRemaining(id);
                if (active > 0f)
                {
                    _activeRemaining[id] = Mathf.Max(0f, active - delta);
                    continue;
                }

                float remaining = _cooldownRemaining[id];
                if (remaining <= 0f)
                    continue;

                _cooldownRemaining[id] = Mathf.Max(0f, remaining - delta);
            }
        }

        private float RawCooldownRemaining(SkillId id) =>
            _cooldownRemaining.TryGetValue(id, out float remaining) ? remaining : 0f;

        private float CooldownTotal(SkillId id) =>
            _cooldownTotal.TryGetValue(id, out float total) ? total : 0f;

        private float ActiveTotal(SkillId id) =>
            _activeTotal.TryGetValue(id, out float total) ? total : 0f;
    }
}
