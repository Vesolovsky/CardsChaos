namespace Vesolovsky.Core.UISystem.UIComponents
{
    using UnityEngine;

    //Add to the core
    [AddComponentMenu("Layout/Vertical Animated Size Fitter")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class VerticalAnimatedSizeFitter : AnimatedSizeFitterBase
    {
        protected override Axis Axis => Axis.Vertical;
    }
}