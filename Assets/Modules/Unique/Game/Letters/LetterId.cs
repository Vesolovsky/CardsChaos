namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// Stable identity of a specific letter. Whether a letter has been read is stored in the save
    /// keyed by this, and each placed letter names a different one.
    ///
    /// The values are fixed 6-digit numbers on purpose: Unity serializes the letter's id field by
    /// value, so a fixed number keeps every letter already placed in a scene pointing at the same
    /// entry even when the enum is reordered or entries are inserted. Add a new letter by adding an
    /// entry here with its own fresh 6-digit value.
    /// </summary>
    public enum LetterId
    {
        TheGranCollector_WelcomeLetter = 483920,
        Mira_WelcomeLetter = 617254,
    }
}
