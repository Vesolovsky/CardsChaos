namespace Vesolovsky.Core.Services.Wallet
{
    public enum CurrencyType
    {
        None = 0,

        /// <summary>
        /// The progression currency earned by completing album pages and spent on upgrades and
        /// skills. This is the game's only currency; it was renamed from the core's default
        /// "Coins" so the wallet, its reactive display and the save all speak the same word.
        /// </summary>
        SkillPoints = 179108,
    }
}