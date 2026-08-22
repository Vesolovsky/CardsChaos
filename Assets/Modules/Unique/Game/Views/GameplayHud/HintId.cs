namespace Vesolovsky.Game.Views.GameplayHud
{
    /// <summary>
    /// Names a hint the HUD can raise. Each id maps to one authored entry on the <see cref="HudHint"/>
    /// presenter - the code triggers a hint by its id and the presenter owns what it says and looks
    /// like, so adding a hint is authoring an entry and raising it by its id.
    /// </summary>
    public enum HintId
    {
        /// <summary>Shown once at the start: how to turn the camera.</summary>
        RotateCamera,

        /// <summary>Shown the first time the hand is filled to its last slot: how to throw one back down.</summary>
        ThrowCard,

        /// <summary>Shown the first time the hand holds more than one card: the wheel cycles them.</summary>
        CycleCards,

        /// <summary>Shown when the Card Magnet skill is first unlocked.</summary>
        CardMagnetReady,

        /// <summary>Shown when the Smart Album Open skill is first unlocked.</summary>
        SmartAlbumOpenReady,

        /// <summary>Shown when the Hand Sort skill is first unlocked.</summary>
        HandSortReady,

        /// <summary>Shown when the Levitate skill is first unlocked (its "Is this magic?..." task claimed).</summary>
        LevitateReady,

        /// <summary>Shown when a letter slides into the room. Always-on (not silenced by "Show hints").</summary>
        NewLetterArrived,

        /// <summary>Shown when the endgame card slides out, once the collection is complete. Always-on.</summary>
        FinalCardArrived,

        /// <summary>The duplicate box already contains this exact set-and-number.</summary>
        DuplicateAlreadyStored,

        /// <summary>This card exists only once, so it belongs in the album, not in the box.</summary>
        NotADuplicate,

        /// <summary>
        /// Levitate was cast with no set-mate of the selected card near enough to raise. New ids go
        /// on the end: the values are what the HUD prefab stores against its authored hints, so
        /// inserting one in the middle would slide every entry after it onto the wrong hint.
        /// </summary>
        LevitateNothingNearby,

        /// <summary>Shown when the Muscle memory skill has recovered and can be cast again.</summary>
        MuscleMemoryReady,

        /// <summary>
        /// Shown the first time a card is picked up: where that card is meant to end up. This is
        /// the first thing a new player needs, so it takes the moment the throw hint used to have.
        /// </summary>
        OpenAlbum,
    }
}
