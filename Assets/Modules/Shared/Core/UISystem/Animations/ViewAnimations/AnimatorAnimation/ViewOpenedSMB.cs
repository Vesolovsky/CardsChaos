using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;

namespace Vesolovsky.Core.UISystem.Animations
{
    /// <summary>
    /// A StateMachineBehaviour that triggers actions when a view enters the "opened" state in the Animator.
    /// </summary>
    public class ViewOpenedSMB : StateMachineBehaviour
    {
        public override void OnStateEnter(UnityEngine.Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorControllerPlayable controller)
        {
            var viewAnimatorStateController = animator.GetComponent<IViewAnimatorStateController>();
            Assert.IsNotNull(viewAnimatorStateController, $"Missing IViewAnimatorStateController component on {animator.gameObject.name}");

            viewAnimatorStateController.EnterViewOpenedState();
        }
    }
}