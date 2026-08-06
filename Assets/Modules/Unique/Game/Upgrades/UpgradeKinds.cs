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

        /// <summary>
        /// Raises the selected card's set-mates off the floor to hover, turned to the camera, for a
        /// stretch before they fall. Unlocked by a task rather than bought - see
        /// <see cref="OneTimeUpgradeKind.UnlockLevitate"/>.
        /// </summary>
        Levitate,
    }

    /// <summary>Which one-time upgrade a definition drives.</summary>
    public enum OneTimeUpgradeKind
    {
        Sprint,

        /// <summary>"Helping Hands": Card Magnet pulls one more card. Read live by the magnet.</summary>
        CardMagnetBonus,

        /// <summary>"Playmaker": Hand Sort's cooldown is cut. Read live when Hand Sort fires.</summary>
        HandSortCooldownReduction,

        /// <summary>"Care Package": a one-off skill-point payout on claim. See SkillPointGrantApplier.</summary>
        SkillPointsGrant,

        /// <summary>"Traveler": every skill's cooldown is cut. Read live when any skill fires.</summary>
        AllSkillsCooldownReduction,

        /// <summary>"Is this magic?...": unlocks the <see cref="SkillId.Levitate"/> skill.</summary>
        UnlockLevitate,

        /// <summary>
        /// "They sense more...": the Levitate HUD button pulses while set-mates of the selected card
        /// are nearby. Read live by the HUD.
        /// </summary>
        LevitatePulse,
    }
}
