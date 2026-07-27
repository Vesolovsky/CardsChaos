using System;

namespace Vesolovsky.Game.Services.Hud
{
    /// <summary>
    /// A channel for the HUD to ask the big screens to open or close, the same way
    /// <c>IAlbumFocusRequest</c> lets a skill ask the album to turn to a page.
    ///
    /// The album and the upgrades screen each live in their own context, out of the HUD's reach, and
    /// each already knows how to toggle itself for its keyboard shortcut. This lets the HUD button
    /// pull that same lever without either side holding a reference to the other.
    /// </summary>
    public interface IGameplayPanels
    {
        event Action AlbumToggleRequested;
        event Action UpgradesToggleRequested;

        void ToggleAlbum();

        void ToggleUpgrades();
    }

    public class GameplayPanels : IGameplayPanels
    {
        public event Action AlbumToggleRequested;
        public event Action UpgradesToggleRequested;

        public void ToggleAlbum() => AlbumToggleRequested?.Invoke();

        public void ToggleUpgrades() => UpgradesToggleRequested?.Invoke();
    }
}
