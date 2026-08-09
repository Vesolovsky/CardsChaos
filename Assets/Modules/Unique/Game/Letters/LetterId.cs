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
        TheGranCollector_UnderBigPileLetter = 958118,
        TheGranCollector_TheRightPage = 483419,
        TheGranCollector_GotCarriedAway = 947577,
        TheGranCollector_LoveLetterHesitate = 526538,
        TheGranCollector_LoveLetter = 951623,
        TheGranCollector_WasThatMagic = 206349,
        TheGranCollector_IrritatedProgress = 589296,
        TheGranCollector_Admire = 852974,
        Mira_WelcomeLetter = 617254,
        Mira_IgnoreGrandCollector = 774628,
        Mira_FinishTheCollection = 720174,
        Mira_UniqueWands = 732591,
        CertificateLetter = 206692,
    }
}
