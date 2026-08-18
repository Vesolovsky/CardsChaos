using Steamworks;
using UnityEngine;

namespace Vesolovsky.Core.Services.Steam
{
    /// <summary>
    /// Starts and stops the Steamworks session.
    ///
    /// Steamworks is native code that outlives a C# domain reload, so the running/not-running latch
    /// is static: entering play mode a second time in one editor process must not try to initialize
    /// twice, and a missed shutdown would leave the next play session unable to start at all.
    ///
    /// Failing to initialize is not an error the game stops for. Steam not running, the app not
    /// owned, the build launched straight out of a folder - all of those are ordinary situations in
    /// development, and the game plays through them with achievements and cloud simply switched off.
    /// </summary>
    public class SteamService : ISteamService
    {
        // Native-side state, so it cannot live in an instance field that a domain reload wipes while
        // the API it describes keeps running underneath.
        private static bool _apiRunning;

        private readonly AppId_t _appId;

        public SteamService(AppId_t appId)
        {
            _appId = appId;
        }

        /// <summary>
        /// Whether the session is up, without an instance to ask. For the handful of places that
        /// cannot take an injected <see cref="ISteamService"/> - components dropped into prefabs
        /// that no context is guaranteed to inject. Prefer the interface everywhere else.
        /// </summary>
        public static bool IsRunning => _apiRunning;

        public bool IsInitialized => _apiRunning;

        public string UserName => _apiRunning ? SteamFriends.GetPersonaName() : string.Empty;

        public ulong UserId => _apiRunning ? SteamUser.GetSteamID().m_SteamID : 0UL;

        /// <summary>
        /// Brings the API up. Returns false when the process should stop right now because Steam is
        /// relaunching the game through the client - the caller quits and lets that relaunch happen.
        /// A plain failure to initialize returns true: the game carries on without Steam.
        /// </summary>
        public bool Boot()
        {
            if (_apiRunning)
                return true;

            if (_appId == AppId_t.Invalid || _appId.m_AppId == 0)
            {
                Debug.LogError(
                    $"[{nameof(SteamService)}] No Steam app id is set. Fill in " +
                    "GlobalData.STEAM_APP_ID and steam_appid.txt before shipping.");

                return true;
            }

#if !UNITY_EDITOR
            // Only in a real build: run from outside Steam and this hands the launch back to the
            // client, which starts the game again properly. In the editor it would try to relaunch
            // Unity itself, and steam_appid.txt in the project root already stands in for it.
            if (SafeRestartAppIfNecessary())
            {
                Debug.Log($"[{nameof(SteamService)}] Relaunching through Steam.");
                return false;
            }
#endif

            ESteamAPIInitResult result;
            string error;

            try
            {
                result = SteamAPI.InitEx(out error);
            }
            catch (System.Exception e)
            {
                // The platform has no Steamworks binaries at all (an unsupported build target, a
                // stripped plugin folder). Nothing to recover, and nothing to stop the game for.
                Debug.LogWarning($"[{nameof(SteamService)}] Steamworks is unavailable here: {e.Message}");
                return true;
            }

            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                // Deliberately a warning, not an error: playing without the Steam client running is
                // the normal way to test the game, and a red console line every launch trains the
                // team to ignore the console.
                Debug.LogWarning(
                    $"[{nameof(SteamService)}] Steam did not start ({result}): {error}. " +
                    "Achievements and Steam Cloud are off for this session.");

                return true;
            }

            _apiRunning = true;
            Debug.Log($"[{nameof(SteamService)}] Steam ready for '{UserName}' (app {_appId}).");
            return true;
        }

        /// <summary>
        /// Pumps the Steam callback queue. Must be called every frame while the session is up -
        /// nothing Steam sends back (stats received, overlay opened) arrives without it.
        /// </summary>
        public void RunCallbacks()
        {
            if (!_apiRunning)
                return;

            SteamAPI.RunCallbacks();
        }

        /// <summary>
        /// Closes the session. Safe to call more than once, and safe to call when the session never
        /// came up. In the editor this is what lets the next play session initialize again.
        /// </summary>
        public void Shutdown()
        {
            if (!_apiRunning)
                return;

            _apiRunning = false;
            SteamAPI.Shutdown();
        }

#if !UNITY_EDITOR
        private bool SafeRestartAppIfNecessary()
        {
            try
            {
                return SteamAPI.RestartAppIfNecessary(_appId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[{nameof(SteamService)}] Could not ask Steam to relaunch: {e.Message}");
                return false;
            }
        }
#endif
    }
}
