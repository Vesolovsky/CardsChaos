namespace Vesolovsky.Core.UISystem.Animations
{
    /// <summary>
    /// Interface for controlling the state of a view's animation.
    /// This interface is implemented by animations that use an Animator. 
    /// <see cref="UnityEngine.StateMachineBehaviour"/> classes, such as 
    /// <see cref="ViewOpenedSMB"/> and <see cref="ViewClosedSMB"/>, are placed on the main animator of the view (ViewAC)
    /// and use this interface to inform the animating script, such as 
    /// <see cref="ViewAnimatorAnimation"/>, when the animation has completed.
    /// </summary>
    public interface IViewAnimatorStateController
    {
        /// <summary>
        /// Called when the view has fully entered the "opened" state.
        /// </summary>
        void EnterViewOpenedState();

        /// <summary>
        /// Called when the view has fully entered the "closed" state.
        /// </summary>
        void EnterViewClosedState();
    }
}