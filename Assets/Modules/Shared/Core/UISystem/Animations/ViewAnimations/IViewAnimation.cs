using Cysharp.Threading.Tasks;
using System.Threading;

namespace Vesolovsky.Core.UISystem.Animations
{
    /// <summary>
    /// Defines the interface for animations in the UI system.
    /// Implementing this interface allows defining show and hide animations for a single view.
    /// </summary>
    public interface IViewAnimation
    {
        /// <summary>
        /// Plays the open animation for the view.
        /// </summary>
        /// <param name="immediately">If set to <c>true</c>, the animation should be skipped.</param>
        UniTask Open(CancellationToken ct,bool immediately = false);
        
        /// <summary>
        /// Plays the close animation for the view.
        /// </summary>
        /// <param name="immediately">If set to <c>true</c>, the animation should be skipped.</param>
        UniTask Close(CancellationToken ct, bool immediately = false);
    }
}