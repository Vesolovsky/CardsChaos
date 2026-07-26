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

        [TextArea]
        [SerializeField] private string description;

        [SerializeField] private Sprite icon;

        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? Id : displayName;

        public string Description => description;

        public Sprite Icon => icon;
    }
}
