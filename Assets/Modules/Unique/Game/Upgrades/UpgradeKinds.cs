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

        /// <summary>
        /// "Déjà vu": a card in hand whose twin is already filed in the album is drawn grey, the
        /// same way a misplaced card reads in the album. Read live by the duplicate service.
        ///
        /// Not bought: its definition is task-unlocked (see
        /// <see cref="OneTimeUpgradeKind.UnlockDuplicateSight"/>), so it is owned exactly while
        /// that task is claimed.
        /// </summary>
        DuplicateSight,

        /// <summary>
        /// How fast the player walks the room. Each level is a whole walking speed in world units
        /// per second, replacing the camera's authored one; level 0 leaves that authored speed
        /// alone. New kinds go on the end - the values are what the upgrade assets store.
        /// </summary>
        MoveSpeed,

        /// <summary>
        /// "Set sense": in the album, the set button of every set the player is holding a card from
        /// breathes gently. Read live by the album view.
        /// </summary>
        HandSetSense,
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

        /// <summary>
        /// "Muscle memory": for a spell after it is cast, every duplicate thrown with nothing aimed
        /// at flies itself into a duplicate box. A timed skill - see
        /// <see cref="Vesolovsky.Game.Services.Skills.ITimedSkill"/> - whose levels lengthen the
        /// spell and shorten the wait after it.
        /// </summary>
        MuscleMemory,
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
        /// "They sense more...": a card lying in the room whose slot in the album is already filled
        /// is drawn grey, so a spare can be picked out from across the floor. The room-side half of
        /// <see cref="PermanentUpgradeKind.DuplicateSight"/>, and read live by the floor shading.
        /// </summary>
        FloorDuplicateSight,

        /// <summary>
        /// Unlocks "Déjà vu" (<see cref="PermanentUpgradeKind.DuplicateSight"/>) rather than
        /// carrying an effect of its own: the permanent upgrade names this task as what unlocks it,
        /// so claiming this is what turns the grey wash in hand on.
        /// </summary>
        UnlockDuplicateSight,
    }
}
