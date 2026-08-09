using System;
using Vesolovsky.Game.Views.GameplayHud;

namespace Vesolovsky.Game.Services.Hud
{
    /// <summary>
    /// A channel for scene services to raise a HUD hint, the same way <see cref="IGameplayPanels"/>
    /// lets the HUD ask the big screens to open. The hint presenter lives on the HUD view, out of a
    /// plain service's reach; this lets, say, the letter-arrival service raise "New letter arrived"
    /// without either side holding a reference to the other.
    /// </summary>
    public interface IHudHints
    {
        event Action<HintId> Raised;

        void Raise(HintId id);
    }

    public class HudHints : IHudHints
    {
        public event Action<HintId> Raised;

        public void Raise(HintId id) => Raised?.Invoke(id);
    }
}
