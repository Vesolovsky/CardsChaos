using Cysharp.Threading.Tasks;
using PrimeTween;
using System.Collections.Generic;
using TransitionsPlus;
using UnityEngine;
using UnityEngine.Serialization;
using Vesolovsky.Core.Utils.Extensions;

namespace Vesolovsky.Game
{
    public interface ISceneTransition
    {
        public UniTask FadeIn();
        public UniTask FadeOut();
    }

    /// <summary>
    /// The wipe that covers a scene change. It lives on the project context, so one instance
    /// carries the player from whichever scene they are leaving into whichever one is coming.
    ///
    /// A change is two halves of the same effect run in opposite directions: <see cref="FadeIn"/>
    /// closes the wipe over the outgoing scene, the load happens behind it, and
    /// <see cref="FadeOut"/> opens it back up onto the incoming one. Each half picks its own shape,
    /// so the screen can be swallowed by one thing and revealed by another rather than the second
    /// half always being the first one rewound.
    ///
    /// Everything about it is decoration, and decoration is never allowed to be the reason a
    /// player cannot get into the game: a shape that has gone missing from the project, or an
    /// animator that was never wired, makes the change plain rather than making it fail.
    /// </summary>
    //TODO: Add to the core
    public class SceneTransition : MonoBehaviour, ISceneTransition
    {
        [SerializeField] private TransitionAnimator transitionAnimator;

        [Tooltip("Shapes the wipe can close with, over the scene being left. One is picked at " +
                 "random per transition. Entries left empty - or pointing at an asset that is no " +
                 "longer in the project - are skipped.")]
        [FormerlySerializedAs("transitionShapes")]
        [SerializeField] private List<Sprite> closingShapes;

        [Tooltip("Shapes the wipe can open with, onto the scene being entered. Picked the same " +
                 "way. Leave this empty to keep the old behaviour: the opening reuses whatever " +
                 "shape the closing half picked, so the change reads as one movement played " +
                 "forwards and then backwards.")]
        [SerializeField] private List<Sprite> openingShapes;

        [SerializeField] private float fadeDuration = 1.0f;

        // Reused rather than allocated per transition, and it is also what keeps a list with one
        // good shape among several broken ones from ever picking a broken one.
        private readonly List<Sprite> _usableShapes = new List<Sprite>();

        private bool _warnedAboutShapes;

        public async UniTask FadeIn()
        {
            Sprite shape = PickShape(closingShapes);

            if (shape != null)
                ApplyShape(shape);
            else
                WarnAboutShapes();

            if (transitionAnimator == null)
            {
                WarnAboutAnimator();
                return;
            }

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
            // No opening shape is a real setup rather than a mistake - it means "come back the way
            // you went in" - so unlike the closing half this says nothing when the list is empty.
            Sprite shape = PickShape(openingShapes);

            if (shape != null)
                ApplyShape(shape);

            if (transitionAnimator == null)
            {
                WarnAboutAnimator();
                return;
            }

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

        /// <summary>
        /// Gives the wipe its shape for the half about to play. The profile is the animator's own,
        /// so this holds until the next half sets it again.
        /// </summary>
        private void ApplyShape(Sprite shape)
        {
            if (transitionAnimator == null)
                return;

            TransitionProfile profile = transitionAnimator.profile;
            if (profile == null)
                return;

            profile.shapeTexture = shape.texture;
            transitionAnimator.SetProfile(profile);
        }

        /// <summary>
        /// One usable shape out of a list, or null when the list offers none. Null is answered by
        /// simply not changing the shape, which is a plainer transition rather than no transition.
        /// </summary>
        private Sprite PickShape(List<Sprite> shapes)
        {
            _usableShapes.Clear();

            if (shapes != null)
            {
                foreach (Sprite shape in shapes)
                {
                    // A reference to an asset that has been deleted from the project reads as null
                    // here and only throws once something dereferences it - which is exactly what
                    // reading .texture off it used to do, taking the whole scene change down with
                    // it. Filtering first is what makes a dead entry merely a missing shape.
                    if (shape != null)
                        _usableShapes.Add(shape);
                }
            }

            return _usableShapes.Count > 0 ? _usableShapes.GetRandomElement() : null;
        }

        private void WarnAboutShapes()
        {
            if (_warnedAboutShapes)
                return;

            _warnedAboutShapes = true;

            Debug.LogWarning(
                $"[{nameof(SceneTransition)}] No usable shape in '{nameof(closingShapes)}' - " +
                "every entry is empty or points at an asset that is no longer in the project. " +
                "Scene changes still work; they just wipe with whatever shape the transition " +
                "profile already carries. Assign a sprite on the ProjectContext prefab to fix it.",
                this);
        }

        private void WarnAboutAnimator()
        {
            Debug.LogWarning(
                $"[{nameof(SceneTransition)}] No {nameof(TransitionAnimator)} assigned, so scene " +
                "changes happen with no wipe over them.", this);
        }
    }
}
