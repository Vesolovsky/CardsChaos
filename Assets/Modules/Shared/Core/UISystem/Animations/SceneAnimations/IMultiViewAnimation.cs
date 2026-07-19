using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.UISystem.Animations
{
    /// <summary>
    /// Interface for managing animations that affect multiple views in a scene.
    /// Implementing this interface allows defining open/close animations for multiple views simultaneously.
    /// <para>
    /// Binded in scene context serves as a scene animation
    /// </para>
    /// </summary>
    public interface IMultiViewAnimation
    {
        /// <summary>
        /// Plays the open animation for all relevant views.
        /// </summary>
        /// <param name="views">The list of views to open.</param>
        /// <param name="immediately">If true, skips animation and directly opens the views.</param>
        UniTask Open(List<IView> views, bool immediately = false);

        /// <summary>
        /// Plays the close animation for all relevant views.
        /// </summary>
        /// <param name="views">The list of views to close.</param>
        /// <param name="immediately">If true, skips animation and directly closes the views.</param>
        UniTask Close(List<IView> views, bool immediately = false);
    }
}