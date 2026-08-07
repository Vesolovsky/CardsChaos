namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// Who left a letter. Their name is what signs it and their handwriting is the font it is shown
    /// in - both resolved from the roster in <see cref="LetterSettings"/>.
    ///
    /// Fixed 6-digit values for the same reason as <see cref="LetterId"/>: the letter's author field
    /// is serialized by value, so reordering this enum never repoints a placed letter at a different
    /// person.
    /// </summary>
    public enum LetterAuthor
    {
        TheGrandCollector = 401725,
        MiraFinch = 962438,
    }
}
