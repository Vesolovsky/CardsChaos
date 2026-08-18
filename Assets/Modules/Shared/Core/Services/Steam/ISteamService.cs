namespace Vesolovsky.Core.Services.Steam
{
    /// <summary>
    /// The Steamworks session, as the rest of the game sees it.
    ///
    /// Everything that talks to Steam goes through something that depends on this, so a build with
    /// no Steam client running - a dev launching the player directly, a press build, the editor
    /// without Steam open - has exactly one thing to check (<see cref="IsInitialized"/>) rather
    /// than a null check at every call site.
    /// </summary>
    public interface ISteamService
    {
        /// <summary>
        /// Whether the Steamworks API came up. False means Steam is not running, the user does not
        /// own the app, or the platform has no Steam at all - in every case the game must still
        /// play, just without achievements or cloud.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>The player's Steam persona name, or an empty string when Steam is not there.</summary>
        string UserName { get; }

        /// <summary>The player's 64-bit Steam id, or zero when Steam is not there.</summary>
        ulong UserId { get; }
    }
}
