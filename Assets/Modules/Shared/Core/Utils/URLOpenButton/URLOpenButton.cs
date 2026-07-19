using Steamworks;
using UnityEngine;
using Vesolovsky.Core.UISystem.UIComponents;
using Vesolovsky.Game;

namespace Vesolovsky.Core
{
    //TODO: add to the core
    [RequireComponent(typeof(VButton))]
    public class URLOpenButton : MonoBehaviour
    {
        [SerializeField] private string URL = "";

        private VButton _button;

        private void Awake()
        {
            _button = GetComponent<VButton>();
        }

        private void Start()
        {
            _button.Bind(OpenURL);
        }
        private void OpenURL()
        {
            if (SteamworksInstaller.IsSteamInitialized)
            {
                SteamFriends.ActivateGameOverlayToWebPage(URL);
            }
            else
            {
                Application.OpenURL(URL);
            }
        }
    }
}
