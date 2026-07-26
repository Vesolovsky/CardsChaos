using System;
using System.Collections.Generic;
using UnityEngine;
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
        private readonly Dictionary<SkillId, ISkillHandler> _handlers = new Dictionary<SkillId, ISkillHandler>();

        private readonly Dictionary<SkillId, float> _cooldownRemaining = new Dictionary<SkillId, float>();
        private readonly Dictionary<SkillId, float> _cooldownTotal = new Dictionary<SkillId, float>();

        [Inject]
        public SkillService(
            UpgradeCatalog catalog, IUpgradeService upgrades, List<ISkillHandler> handlers)
        {
            _catalog = catalog;
            _upgrades = upgrades;

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

            int level = _upgrades.GetLevel(definition);
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

            float cooldown = definition.GetCooldown(level);
            _cooldownRemaining[id] = cooldown;
            _cooldownTotal[id] = cooldown;

            Activated?.Invoke(id);
            return true;
        }

        public bool IsReady(SkillId id)
        {
            SkillDefinition definition = _catalog.FindSkill(id);
            return definition != null && _upgrades.GetLevel(definition) > 0 && GetCooldownRemaining(id) <= 0f;
        }

        public float GetCooldownRemaining(SkillId id) =>
            _cooldownRemaining.TryGetValue(id, out float remaining) ? remaining : 0f;

        public float GetCooldownNormalized(SkillId id)
        {
            float total = _cooldownTotal.TryGetValue(id, out float t) ? t : 0f;
            return total > 0f ? Mathf.Clamp01(GetCooldownRemaining(id) / total) : 0f;
        }

        public void Tick()
        {
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
