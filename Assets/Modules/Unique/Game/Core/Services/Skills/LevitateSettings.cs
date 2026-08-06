using UnityEngine;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// The Levitate skill's tuning, kept in an asset so it can be balanced without touching code -
    /// the same split the card inspect settings use. The cooldown is not here; it stays on the
    /// skill's own definition beside every other skill's, since that is where the skill service
    /// reads it and where the HUD shows its key.
    /// </summary>
    [CreateAssetMenu(
        menuName = "CardsChaos/Skills/Levitate Settings",
        fileName = "LevitateSettings")]
    public class LevitateSettings : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("How far from the player a set-mate may be and still be raised, in world units. " +
                 "Also the range the 'They sense more...' pulse watches.")]
        [SerializeField] private float radius = 4f;

        [Tooltip("Most cards a single cast raises, nearest first. Zero means no limit.")]
        [SerializeField] private int maxCards = 8;

        [Header("Float")]
        [Tooltip("How far each card rises off the table, in world units.")]
        [SerializeField] private float riseHeight = 0.35f;

        [Tooltip("Seconds a card takes to rise into place and turn to face the camera.")]
        [SerializeField] private float riseDuration = 0.25f;

        [Tooltip("Seconds a card hovers before it falls, if it is not picked up first. This is the " +
                 "X in 'levitates for X seconds'.")]
        [SerializeField] private float hoverDuration = 4f;

        [Tooltip("Height of the gentle up-and-down bob while hovering, in world units. Zero holds " +
                 "the card perfectly still.")]
        [SerializeField] private float bobAmplitude = 0.05f;

        [Tooltip("Full bob cycles per second.")]
        [SerializeField] private float bobFrequency = 0.6f;

        [Header("Pulse")]
        [Tooltip("How often the 'They sense more...' pulse rechecks for nearby set-mates, in " +
                 "seconds. A scan is not free, so this is polled rather than run every frame.")]
        [SerializeField] private float targetPollInterval = 0.2f;

        public float Radius => radius;
        public int MaxCards => maxCards;
        public float RiseHeight => riseHeight;
        public float RiseDuration => riseDuration;
        public float HoverDuration => hoverDuration;
        public float BobAmplitude => bobAmplitude;
        public float BobFrequency => bobFrequency;
        public float TargetPollInterval => targetPollInterval;
    }
}
