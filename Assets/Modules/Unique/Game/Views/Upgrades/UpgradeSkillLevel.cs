using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Vesolovsky.Game.Views.Upgrades
{
    /// <summary>
    /// One notch of a skill item's level track: a diamond that fills in when its level is owned.
    ///
    /// The border is always drawn; only the fill moves, fading up from empty to full the moment
    /// the player buys the level it stands for. Set without animation while the item is first
    /// built, and with animation for the one level a purchase just lit.
    /// </summary>
    [AddComponentMenu("CardsChaos/Upgrades/Skill Level")]
    public class UpgradeSkillLevel : MonoBehaviour
    {
        [Tooltip("The fill that fades in when this level is owned.")]
        [SerializeField] private Image fill;

        [SerializeField] private float fadeDuration = 0.25f;

        private Tween _tween;

        public void SetFilled(bool filled, bool animate)
        {
            if (fill == null)
                return;

            float target = filled ? 1f : 0f;

            if (_tween.isAlive)
                _tween.Stop();

            if (!animate)
            {
                SetAlpha(target);
                return;
            }

            if (Mathf.Approximately(fill.color.a, target))
                return;

            _tween = Tween.Alpha(fill, target, fadeDuration);
        }

        private void SetAlpha(float alpha)
        {
            Color color = fill.color;
            color.a = alpha;
            fill.color = color;
        }

        private void OnDestroy()
        {
            if (_tween.isAlive)
                _tween.Stop();
        }
    }
}
