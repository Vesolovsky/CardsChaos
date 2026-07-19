using UnityEngine;
using UnityEngine.Assertions;

namespace Vesolovsky.Core.UISystem.Animations
{
    /// <summary>
    /// A StateMachineBehaviour that triggers actions when a view enters the "closed" state in the Animator.
    /// </summary>
    public class ViewClosedSMB : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            var viewAnimatorStateController = animator.GetComponent<IViewAnimatorStateController>();
            Assert.IsNotNull(viewAnimatorStateController, $"Missing IViewAnimatorStateController component on {animator.gameObject.name}");

            viewAnimatorStateController.EnterViewClosedState();
        }
    }
}