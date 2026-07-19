namespace Vesolovsky.Core.UISystem.UIComponents
{
    using UnityEngine;

    //Add to the core
    [AddComponentMenu("Layout/Horizontal Animated Size Fitter")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HorizontalAnimatedSizeFitter : AnimatedSizeFitterBase
    {
        protected override Axis Axis => Axis.Horizontal;
    }
}