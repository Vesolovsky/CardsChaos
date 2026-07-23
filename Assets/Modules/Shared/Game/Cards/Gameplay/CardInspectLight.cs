using PrimeTween;
using RoboRyanTron.SearchableEnum;
using UnityEngine;

namespace CardsChaos.Cards
{
    public interface ICardInspectLight
    {
        /// <summary>Brings the lamps up for a face of the given measured brightness.</summary>
        void Show(float faceLuminance);

        void Hide();
    }

    /// <summary>
    /// A two lamp rig that comes up only while a card is being looked at.
    ///
    /// The room is deliberately dark and the player can turn on the spot, so which way a card
    /// happened to be facing decided whether it was readable. Lighting the close-up separately
    /// takes that away: the card is lit the same way from wherever the player is standing.
    ///
    /// The pair is warm against cool, opposite sides of the colour wheel and opposite sides of
    /// the card. That is what gives a flat rectangle a front and a back - a single light, of any
    /// colour, only ever states how bright the card is. The warm lamp leads and the cool one
    /// answers at a fraction of it, because two lights of equal strength in complementary hues
    /// average back out to grey and the whole effect is spent for nothing.
    ///
    /// Brightness is per card rather than fixed. The same lamp that flatters dark artwork blows
    /// a pale face out to white, so intensity is dialled back as the measured face gets brighter,
    /// between the same luminance bounds CardSetBuilder uses for the material response.
    /// </summary>
    [AddComponentMenu("CardsChaos/Card Inspect Light")]
    public class CardInspectLight : MonoBehaviour, ICardInspectLight
    {
        [Header("Lamps")]
        [Tooltip("The lead lamp. Put it off to one side and above, so its highlight sweeps the " +
                 "artwork as the card tilts instead of sitting on it as a dead spot.")]
        [SerializeField] private Light warmLamp;

        [Tooltip("The answering lamp, on the opposite side. Its job is the edge of the card, " +
                 "which is what separates it from a black room.")]
        [SerializeField] private Light coolLamp;

        [Header("Brightness")]
        [Tooltip("Intensity for the darkest faces, which carry a lot of light before they read " +
                 "as lit at all.")]
        [SerializeField] private float darkFaceIntensity = 0.05f;

        [Tooltip("Intensity for the brightest faces, where the same lamp would wash the artwork " +
                 "out.")]
        [SerializeField] private float brightFaceIntensity = 0.012f;

        [Tooltip("Face brightness at which the lamps start dimming. Below it they stay at full. " +
                 "Matches CardSetBuilder's LuminanceKnee.")]
        [SerializeField, Range(0f, 1f)] private float luminanceKnee = 0.35f;

        [Tooltip("Face brightness past which the lamps stay at their dimmest. " +
                 "Matches CardSetBuilder's LuminanceCeiling.")]
        [SerializeField, Range(0f, 1f)] private float luminanceCeiling = 0.8f;

        [Tooltip("The cool lamp's share of the warm one. Kept under half so the pair reads as " +
                 "one light with a cold edge rather than as two lights arguing.")]
        [SerializeField, Range(0f, 1f)] private float coolRatio = 0.45f;

        [Header("Colour")]
        [Tooltip("Off leaves whatever colours the lamps were given in the scene alone.")]
        [SerializeField] private bool driveColour = true;

        [Tooltip("Roughly 3200 K - a filament bulb. Deliberately the less saturated of the two: " +
                 "this one lands on the artwork, and a strong tint here restates every colour " +
                 "the card was drawn in.")]
        [SerializeField, ColorUsage(false)] private Color warmColor = new Color(1f, 0.79f, 0.59f);

        [Tooltip("Roughly 8500 K - daylight through a window. Can afford more saturation than " +
                 "the warm lamp, because it only ever catches the edge.")]
        [SerializeField, ColorUsage(false)] private Color coolColor = new Color(0.65f, 0.76f, 1f);

        [Header("Fade")]
        [SerializeField] private float fadeIn = 0.3f;

        [Tooltip("Shorter than the fade in. Leaving should not linger.")]
        [SerializeField] private float fadeOut = 0.18f;

        [SerializeField, SearchableEnum] private Ease fadeEase = Ease.OutQuad;

        private float _intensity;
        private Tween _fade;

        private void Awake() => Apply(0f);

        private void OnDestroy()
        {
            if (_fade.isAlive)
                _fade.Stop();
        }

        public void Show(float faceLuminance)
        {
            // Always from wherever the fade currently sits, never from zero. Stepping from one
            // card to the next only changes the target, so the lamps shade across to the new
            // brightness instead of blinking off and on between cards.
            Fade(IntensityFor(faceLuminance), fadeIn);
        }

        public void Hide() => Fade(0f, fadeOut);

        private float IntensityFor(float faceLuminance)
        {
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(luminanceKnee, luminanceCeiling, faceLuminance));

            return Mathf.Lerp(darkFaceIntensity, brightFaceIntensity, t);
        }

        private void Fade(float target, float duration)
        {
            if (_fade.isAlive)
                _fade.Stop();

            if (duration <= 0f || Mathf.Approximately(_intensity, target))
            {
                Apply(target);
                return;
            }

            _fade = Tween.Custom(_intensity, target, duration, Apply, fadeEase);
        }

        private void Apply(float intensity)
        {
            _intensity = intensity;

            Drive(warmLamp, warmColor, intensity);
            Drive(coolLamp, coolColor, intensity * coolRatio);
        }

        private void Drive(Light lamp, Color color, float intensity)
        {
            if (lamp == null)
                return;

            // A lamp at zero still takes a slot in the per-object light list and still costs its
            // shadow pass, so it is switched off rather than merely turned down.
            bool on = intensity > 0.0001f;

            if (lamp.enabled != on)
                lamp.enabled = on;

            if (!on)
                return;

            lamp.intensity = intensity;

            if (driveColour)
                lamp.color = color;
        }
    }
}
