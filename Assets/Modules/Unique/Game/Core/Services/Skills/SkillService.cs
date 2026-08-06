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

            float cooldown = definition.GetCooldown(level) * _cooldownModifiers.GetMultiplier(id);
            _cooldownRemaining[id] = cooldown;
            _cooldownTotal[id] = cooldown;

            Activated?.Invoke(id);
            return true;
        }

        public bool IsUnlocked(SkillId id)
        {
            SkillDefinition definition = _catalog.FindSkill(id);
            return definition != null && EffectiveLevel(definition) > 0;
        }

        public bool IsReady(SkillId id) => IsUnlocked(id) && GetCooldownRemaining(id) <= 0f;

        /// <summary>
        /// The level a skill is "at" for firing it: its bought level normally, or, for a
        /// task-unlocked skill, level 1 exactly while its task is claimed and 0 before. This is the
        /// one place the two ways a skill can be owned are folded into a single number.
        /// </summary>
        private int EffectiveLevel(SkillDefinition definition)
        {
            if (definition == null)
                return 0;

            if (definition.IsTaskUnlocked)
                return _upgrades.IsUnlocked(definition.UnlockedBy) ? 1 : 0;

            return _upgrades.GetLevel(definition);
        }

        public float GetCooldownRemaining(SkillId id) =>
            _cooldownRemaining.TryGetValue(id, out float remaining) ? remaining : 0f;

        public float GetCooldownNormalized(SkillId id)
        {
            float total = _cooldownTotal.TryGetValue(id, out float t) ? t : 0f;
            return total > 0f ? Mathf.Clamp01(GetCooldownRemaining(id) / total) : 0f;
        }

        public IReadOnlyList<SkillCooldownSnapshot> GetActiveCooldowns()
        {
            var snapshots = new List<SkillCooldownSnapshot>();

            foreach (KeyValuePair<SkillId, float> entry in _cooldownRemaining)
            {
                if (entry.Value <= 0f)
                    continue;

                float total = _cooldownTotal.TryGetValue(entry.Key, out float t) ? t : entry.Value;
                snapshots.Add(new SkillCooldownSnapshot(entry.Key, entry.Value, total));
            }

            return snapshots;
        }

        public void RestoreCooldown(SkillId id, float remaining, float total)
        {
            if (remaining <= 0f)
                return;

            _cooldownRemaining[id] = remaining;
            _cooldownTotal[id] = total > 0f ? total : remaining;
        }

        public void DebugResetCooldowns()
        {
            // Drop every running cooldown; a missing entry reads as ready, so clearing is enough and
            // the HUD's per-frame poll picks the change up on its own.
            _cooldownRemaining.Clear();
            _cooldownTotal.Clear();
        }

        public void Tick()
        {
            // Cooldowns run on game time, and the pause menu stops the clock - a wait held over the
            // pause should come back with exactly as long left as it went in with.
            if (_pauseState != null && _pauseState.IsPaused)
                return;

            if (_cooldownRemaining.Count == 0)
                return;

            float delta = Time.deltaTime;

            // Copied keys because the values are edited inside the loop; the set of skills on
            // cooldown is tiny, so the allocation is nothing to weigh against the clarity.
            foreach (SkillId id in new List<SkillId>(_cooldownRemaining.Keys))
            {
                float remaining = _cooldownRemaining[id];
                if (remaining <= 0f)
                    continue;

                _cooldownRemaining[id] = Mathf.Max(0f, remaining - delta);
            }
        }
    }
}
