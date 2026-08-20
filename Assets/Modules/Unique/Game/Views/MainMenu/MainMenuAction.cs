namespace Vesolovsky.Game.Views.MainMenu
{
    /// <summary>
    /// Which entry a menu card is. The cards are authored by hand in the prefab, so this - not
    /// their order or their name - is what the view binds behaviour to; moving a card along the
    /// fan or renaming its object changes nothing.
    /// </summary>
    public enum MainMenuAction
    {
        None = 0,

        /// <summary>Picks the current save back up. Only present once a game has actually been started.</summary>
        Continue,

        /// <summary>Wipes the save and starts over. Asks first, but only when there is something to lose.</summary>
        NewGame,

        Settings,

        /// <summary>The album as a display case - every filed card, nothing movable.</summary>
        Album,

        Credits,

        Discord,

        Quit,
    }
}
