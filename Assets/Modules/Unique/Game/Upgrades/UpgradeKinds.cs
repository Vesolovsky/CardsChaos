namespace Vesolovsky.Game.Upgrades
{
    /// <summary>
    /// Which permanent upgrade a definition drives. The effect appliers switch on this to find
    /// "their" definition in the catalog, so the wiring survives a definition being renamed.
    /// </summary>
    public enum PermanentUpgradeKind
    {
        ExtraCardSlot,
        WiderVision,
    }

    /// <summary>
    /// Which skill a definition drives. Used both to route activation from the keyboard and to
    /// pair a definition with the handler that carries out its effect.
    /// </summary>
    public enum SkillId
    {
        CardMagnet,
        SmartAlbumOpen,
        HandSort,
    }

    /// <summary>Which one-time upgrade a definition drives.</summary>
    public enum OneTimeUpgradeKind
    {
        Sprint,
        AlphabeticalSets,
    }
}
