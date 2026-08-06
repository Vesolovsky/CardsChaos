using UnityEngine;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Editor-only visual aid for balancing Levitate's reach. Draws the radius from
    /// <see cref="LevitateSettings"/> as a wire ring on the ground around this object, so the
    /// otherwise-invisible number is easy to judge in the scene. The ring is flat and centred on
    /// the object's floor position because that is exactly how targeting measures distance -
    /// horizontal (XZ) from the camera, height ignored (see <see cref="LevitateTargeting"/>).
    ///
    /// Drop it on the main gameplay camera - the same transform targeting reads - and assign the
    /// LevitateSettings asset wired on UpgradesInstaller. It reads the radius every redraw, so the
    /// ring follows the value live as it is dragged. Gizmos never render in a build; this is a
    /// scene-view helper only.
    /// </summary>
    [AddComponentMenu("CardsChaos/Debug/Levitate Radius Gizmo")]
    public class LevitateRadiusGizmo : MonoBehaviour
    {
        [Tooltip("The same LevitateSettings asset wired on UpgradesInstaller.")]
        [SerializeField] private LevitateSettings settings;

        [Tooltip("World Y the ring sits at - set it to the table/floor height so it reads as the " +
                 "reach across the room.")]
        [SerializeField] private float groundHeight;

        [Tooltip("Draw the ring only while this object is selected, to keep the scene view tidy.")]
        [SerializeField] private bool onlyWhenSelected;

        [SerializeField] private Color color = new Color(0.35f, 0.8f, 1f, 0.9f);

        private const int Segments = 64;

        private void OnDrawGizmos()
        {
            if (!onlyWhenSelected)
                Draw();
        }

        private void OnDrawGizmosSelected()
        {
            if (onlyWhenSelected)
                Draw();
        }

        private void Draw()
        {
            if (settings == null || settings.Radius <= 0f)
                return;

            float radius = settings.Radius;
            Vector3 origin = transform.position;
            Vector3 center = new Vector3(origin.x, groundHeight, origin.z);

            Gizmos.color = color;

            // A flat ring on the ground plane, matched to the horizontal distance targeting uses.
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }

            // A dropline from the camera down to the ring's centre, so the ring reads as "around
            // here" rather than floating free.
            Gizmos.DrawLine(origin, center);
        }
    }
}
