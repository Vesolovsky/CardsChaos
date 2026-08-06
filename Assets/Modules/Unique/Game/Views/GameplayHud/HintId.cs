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

        /// <summary>Shown the first time a card is picked up: how to throw it back down.</summary>
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
    }
}
