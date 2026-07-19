using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Collections.Generic;
using TransitionsPlus;
using UnityEngine;
using Vesolovsky.Core.Utils.Extensions;

namespace Vesolovsky.Game
{
    public interface ISceneTransition
    {
        public UniTask FadeIn();
        public UniTask FadeOut();
    }

    //TODO: Add to the core
    public class SceneTransition : MonoBehaviour, ISceneTransition
    {
        [SerializeField] private TransitionAnimator transitionAnimator;
        [SerializeField] private List<Sprite> transitionShapes;
        [SerializeField] private float fadeDuration = 1.0f;

        public async UniTask FadeIn()
        {
            var newProfile = transitionAnimator.profile;
            newProfile.shapeTexture = transitionShapes.GetRandomElement().texture;

            transitionAnimator.SetProfile(newProfile);

            await Tween.Custom(
                startValue: 0,
                endValue: 1,
                duration: fadeDuration,
                onValueChange: v =>
                {
                    transitionAnimator.SetProgress(v);
                }
            );
        }

        public async UniTask FadeOut() 
        {
            await Tween.Custom(
                startValue: 1,
                endValue: 0,
                duration: fadeDuration,
                onValueChange: v =>
                {
                    transitionAnimator.SetProgress(v);
                }
            );
        }
    }
}
