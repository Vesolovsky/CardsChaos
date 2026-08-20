using UnityEngine;

namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// The shared identity of anything the player can own from the upgrades screen - permanent
    /// upgrades, skills and one-time upgrades all carry a name, a blurb and an icon, and all are
    /// referred to by a stable id.
    ///
    /// The id is what the save writes, so it must not change once players have it. It is authored
    /// explicitly rather than taken from the asset name so a definition can be renamed in the
    /// project without stranding everyone's saved level.
    /// </summary>
    public abstract class UpgradeDefinition : ScriptableObject
    {
        [Tooltip("Stable key written to the save. Set it once and never change it - renaming this " +
                 "orphans the level every player has bought. Leave empty to fall back to the asset " +
                 "name, which is fine until the asset is renamed.")]
        [SerializeField] private string id;

        [SerializeField] private string displayName;

        [Tooltip("The blurb on the upgrades screen. For a leveled upgrade this may contain {0} - " +
                 "the level's value - and {1} - its cooldown. Each comes out as the step a purchase " +
                 "would make, '4→5', so write the sentence around a number that can be a pair: " +
                 "'Pull up to {0} matching cards' reads correctly either way. Unbought and maxed " +
                 "out there is only one level to quote, and it comes out as the bare number. Skills " +
                 "get 'Cooldown: N s' appended on their own, so there is no need to write one. " +
                 "Leave the braces out entirely for an upgrade whose value would mean nothing to a " +
                 "player - a fog radius, a walking speed - and just say what it does.")]
        [TextArea]
        [SerializeField] private string description;

        [SerializeField] private Sprite icon;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? Id : displayName;

        public string Description => description;

        public Sprite Icon => icon;
    }
}
