using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// Every upgrade in the game, gathered in one asset the way <c>CardCatalog</c> gathers the
    /// sets. The upgrade service and the effect appliers read the game's upgrades from here rather
    /// than holding their own references, so adding an upgrade is a matter of authoring a
    /// definition and dropping it in a list.
    /// </summary>
    [CreateAssetMenu(menuName = "CardsChaos/Upgrades/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public class UpgradeCatalog : ScriptableObject
    {
        [SerializeField] private List<PermanentUpgradeDefinition> permanents = new List<PermanentUpgradeDefinition>();
        [SerializeField] private List<SkillDefinition> skills = new List<SkillDefinition>();
        [SerializeField] private List<OneTimeUpgradeDefinition> oneTimes = new List<OneTimeUpgradeDefinition>();

        // NonSerialized for the same reason CardCatalog guards its cache: Unity keeps private
        // serializable fields across domain reloads, so a lookup built in edit mode would leak
        // into play mode.
        [System.NonSerialized] private Dictionary<string, UpgradeDefinition> _byId;

        public IReadOnlyList<PermanentUpgradeDefinition> Permanents => permanents;

        public IReadOnlyList<SkillDefinition> Skills => skills;

        public IReadOnlyList<OneTimeUpgradeDefinition> OneTimes => oneTimes;

        /// <summary>The definition with the given save id, or null when nothing matches.</summary>
        public UpgradeDefinition FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return ById.TryGetValue(id, out UpgradeDefinition def) ? def : null;
        }

        /// <summary>The permanent upgrade that drives a given effect, or null when none does.</summary>
        public PermanentUpgradeDefinition FindPermanent(PermanentUpgradeKind kind)
        {
            foreach (PermanentUpgradeDefinition def in permanents)
            {
                if (def != null && def.Kind == kind)
                    return def;
            }

            return null;
        }

        /// <summary>The definition for a given skill, or null when it is not in the catalog.</summary>
        public SkillDefinition FindSkill(SkillId id)
        {
            foreach (SkillDefinition def in skills)
            {
                if (def != null && def.SkillId == id)
                    return def;
            }

            return null;
        }

        /// <summary>The one-time upgrade that unlocks a given effect, or null when none does.</summary>
        public OneTimeUpgradeDefinition FindOneTime(OneTimeUpgradeKind kind)
        {
            foreach (OneTimeUpgradeDefinition def in oneTimes)
            {
                if (def != null && def.Kind == kind)
                    return def;
            }

            return null;
        }

        private void OnDisable() => _byId = null;

        private Dictionary<string, UpgradeDefinition> ById => _byId ??= BuildLookup();

        private Dictionary<string, UpgradeDefinition> BuildLookup()
        {
            var lookup = new Dictionary<string, UpgradeDefinition>();

            AddAll(lookup, permanents);
            AddAll(lookup, skills);
            AddAll(lookup, oneTimes);

            return lookup;
        }

        private void AddAll<T>(Dictionary<string, UpgradeDefinition> lookup, List<T> defs)
            where T : UpgradeDefinition
        {
            foreach (T def in defs)
            {
                if (def == null)
                    continue;

                if (!lookup.TryAdd(def.Id, def))
                {
                    Debug.LogError(
                        $"[{nameof(UpgradeCatalog)}] Two upgrades share the id '{def.Id}'; " +
                        $"'{def.name}' is the duplicate.", def);
                }
            }
        }
    }
}
