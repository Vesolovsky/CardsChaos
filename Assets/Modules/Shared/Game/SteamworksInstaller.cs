using Steamworks;
using UnityEngine;
using Vesolovsky.Game.Utils;
using Zenject;

namespace Vesolovsky.Game
{
    public class SteamworksInstaller : MonoInstaller
    {
        public static bool IsSteamInitialized = false;

        public override void InstallBindings()
        {
            if(SteamAPI.RestartAppIfNecessary(GlobalData.STEAM_APP_ID))
            {
                Debug.LogError("Steamworks not initialized properly. Game was launched outside of Steam!");
                return;
            }

            IsSteamInitialized = SteamAPI.Init();

            if(IsSteamInitialized)
            {
                Debug.Log($"Steamworks initialized successfully. User name: {SteamFriends.GetPersonaName()}");
            }
            else
            {
                Debug.LogError("Steamworks not initialized properly.");
            }
        }
    }
}
