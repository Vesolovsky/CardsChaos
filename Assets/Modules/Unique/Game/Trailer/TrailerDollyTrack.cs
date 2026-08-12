using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Game.Trailer
{
    /// <summary>
    /// The rails a trailer camera rides, laid out as child objects of this one.
    ///
    /// Every child is a waypoint, taken in hierarchy order: duplicate one, drag it where the camera
    /// should pass, and the track follows. The path is drawn in the scene view as you move them, so
    /// authoring is dragging things about rather than typing numbers.
    ///
    /// The ride is sampled by distance travelled rather than by which waypoint is next, so the
    /// camera holds an even speed instead of racing through the closely spaced parts of the track
    /// and crawling through the open ones.
    ///
    /// <see cref="TrailerDolly"/> is what actually rides it.
    /// </summary>
    [AddComponentMenu("CardsChaos/Trailer/Trailer Dolly Track")]
    public class TrailerDollyTrack : MonoBehaviour
    {
        public enum LookMode
        {
            /// <summary>Face wherever the waypoints face, turning smoothly from one to the next.</summary>
            Waypoints,

            /// <summary>Keep one thing in frame the whole way along.</summary>
            Target,

            /// <summary>Look along the rails, the way a car faces the road.</summary>
            Forward,
        }

        // Dense enough that walking the samples in a straight line is indistinguishable from the
        // curve itself, cheap enough to rebuild every time a gizmo is drawn.
        private const int SamplesPerSegment = 24;

        private const float Epsilon = 0.000001f;

        [Tooltip("Where the camera faces as it travels.")]
        [SerializeField] private LookMode look = LookMode.Waypoints;

        [Tooltip("Kept in frame the whole way along in Target mode.")]
        [SerializeField] private Transform lookTarget;

        [Tooltip("Rounds the path through the waypoints instead of running it corner to corner. " +
                 "A dolly shot wants this on; turn it off only to hit an exact straight line.")]
        [SerializeField] private bool curved = true;

        [Tooltip("Joins the last waypoint back to the first, for a ride that circles the room.")]
        [SerializeField] private bool closed;

        [Header("Gizmos")]
        [SerializeField] private Color pathColor = new Color(0.35f, 0.85f, 1f);

        [Tooltip("Radius of the ball drawn at each waypoint. The room is small - a card is 0.063 " +
                 "wide - so this is a fraction of a metre.")]
        [SerializeField] private float waypointSize = 0.02f;

        private readonly List<Transform> _waypoints = new List<Transform>();

        // The path walked out into a polyline: where each sample sits, how far along the track it
        // is, and the curve parameter it was taken at (so a rotation can be worked out for it).
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<float> _travelled = new List<float>();
        private readonly List<float> _parameters = new List<float>();

        public int WaypointCount => _waypoints.Count;

        /// <summary>Total length of the rails in metres, once <see cref="Rebuild"/> has run.</summary>
        public float Length => _travelled.Count > 0 ? _travelled[_travelled.Count - 1] : 0f;

        /// <summary>
        /// Re-reads the waypoints and walks the path out again. Called when a ride starts and
        /// whenever the gizmos are drawn in edit mode, so a waypoint dragged in the scene shows up
        /// at once.
        /// </summary>
        public void Rebuild()
        {
            _waypoints.Clear();

            foreach (Transform child in transform)
            {
                // An inactive waypoint is the obvious way to try a version of the track without
                // losing the other one, so honour it.
                if (child.gameObject.activeSelf)
                    _waypoints.Add(child);
            }

            BuildSamples();
        }

        /// <summary>
        /// Where the camera stands, and which way it faces, a fraction <paramref name="distance"/>
        /// of the way along the rails. 0 is the first waypoint, 1 the last.
        /// </summary>
        public Pose Sample(float distance)
        {
            if (_points.Count == 0)
                return new Pose(transform.position, transform.rotation);

            if (_points.Count == 1)
                return new Pose(_points[0], RotationAt(0f, Vector3.zero, _points[0]));

            float target = Mathf.Clamp01(distance) * Length;
            int index = FindSample(target);

            float from = _travelled[index];
            float span = _travelled[index + 1] - from;
            float t = span > Epsilon ? (target - from) / span : 0f;

            Vector3 position = Vector3.Lerp(_points[index], _points[index + 1], t);
            float parameter = Mathf.Lerp(_parameters[index], _parameters[index + 1], t);
            Vector3 tangent = _points[index + 1] - _points[index];

            return new Pose(position, RotationAt(parameter, tangent, position));
        }

        /// <summary>How many stretches of rail there are - one fewer than the waypoints, unless the
        /// track is closed, when the join back to the start is one more.</summary>
        private int SegmentCount
        {
            get
            {
                int count = _waypoints.Count;

                if (count < 2)
                    return 0;

                return closed ? count : count - 1;
            }
        }

        private void BuildSamples()
        {
            _points.Clear();
            _travelled.Clear();
            _parameters.Clear();

            int segments = SegmentCount;

            if (_waypoints.Count == 1)
            {
                _points.Add(_waypoints[0].position);
                _travelled.Add(0f);
                _parameters.Add(0f);
                return;
            }

            if (segments == 0)
                return;

            int steps = segments * SamplesPerSegment;
            float length = 0f;
            Vector3 previous = PointAt(0f);

            _points.Add(previous);
            _travelled.Add(0f);
            _parameters.Add(0f);

            for (int i = 1; i <= steps; i++)
            {
                float parameter = segments * i / (float)steps;
                Vector3 point = PointAt(parameter);

                length += Vector3.Distance(previous, point);
                previous = point;

                _points.Add(point);
                _travelled.Add(length);
                _parameters.Add(parameter);
            }
        }

        /// <summary>The last sample that starts no further along than <paramref name="target"/>.</summary>
        private int FindSample(float target)
        {
            int low = 0;
            int high = _travelled.Count - 1;

            while (low < high - 1)
            {
                int middle = (low + high) / 2;

                if (_travelled[middle] <= target)
                    low = middle;
                else
                    high = middle;
            }

            return Mathf.Clamp(low, 0, _points.Count - 2);
        }

        /// <param name="parameter">
        /// Distance along the track counted in whole segments - 1.5 is halfway along the second
        /// stretch of rail - which is what the spline and the waypoint rotations are both written in.
        /// </param>
        private Vector3 PointAt(float parameter)
        {
            if (_waypoints.Count == 0)
                return transform.position;

            if (_waypoints.Count == 1)
                return _waypoints[0].position;

            int segment = Mathf.Clamp(Mathf.FloorToInt(parameter), 0, SegmentCount - 1);
            float t = Mathf.Clamp01(parameter - segment);

            Vector3 start = WaypointPosition(segment);
            Vector3 end = WaypointPosition(segment + 1);

            if (!curved)
                return Vector3.Lerp(start, end, t);

            // Catmull-Rom: the curve passes through every waypoint (unlike a Bezier's controls) and
            // takes its shape from the neighbours on either side, so dragging one waypoint only
            // disturbs the rails around it.
            Vector3 before = WaypointPosition(segment - 1);
            Vector3 after = WaypointPosition(segment + 2);

            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (2f * start
                           + (-before + end) * t
                           + (2f * before - 5f * start + 4f * end - after) * t2
                           + (-before + 3f * start - 3f * end + after) * t3);
        }

        private Quaternion RotationAt(float parameter, Vector3 tangent, Vector3 position)
        {
            switch (look)
            {
                case LookMode.Target when lookTarget != null:
                {
                    Vector3 direction = lookTarget.position - position;

                    if (direction.sqrMagnitude > Epsilon)
                        return Quaternion.LookRotation(direction, Vector3.up);

                    break;
                }

                case LookMode.Forward:
                {
                    if (tangent.sqrMagnitude > Epsilon)
                        return Quaternion.LookRotation(tangent, Vector3.up);

                    break;
                }
            }

            if (_waypoints.Count == 0)
                return transform.rotation;

            if (_waypoints.Count == 1)
                return _waypoints[0].rotation;

            int segment = Mathf.Clamp(Mathf.FloorToInt(parameter), 0, SegmentCount - 1);
            float t = Mathf.Clamp01(parameter - segment);

            // Smoothed rather than slerped straight through: turning at a constant rate and then
            // changing rate the instant a waypoint is passed reads as a flinch on screen, and a
            // trailer is nothing but the reading of it.
            return Quaternion.Slerp(
                WaypointRotation(segment), WaypointRotation(segment + 1), Mathf.SmoothStep(0f, 1f, t));
        }

        private Vector3 WaypointPosition(int index) => _waypoints[WrapIndex(index)].position;

        private Quaternion WaypointRotation(int index) => _waypoints[WrapIndex(index)].rotation;

        /// <summary>
        /// A closed track wraps round the ends; an open one holds at them, which doubles up the end
        /// waypoint as its own neighbour and is what keeps the curve from flying off past the last
        /// one.
        /// </summary>
        private int WrapIndex(int index)
        {
            int count = _waypoints.Count;

            if (!closed)
                return Mathf.Clamp(index, 0, count - 1);

            return ((index % count) + count) % count;
        }

        private void OnDrawGizmos()
        {
            // Only rebuilt out of play mode: while a ride is running the waypoints are not moving,
            // and the dolly has already built the same samples it is reading.
            if (!Application.isPlaying)
                Rebuild();

            Gizmos.color = pathColor;

            for (int i = 0; i < _points.Count - 1; i++)
                Gizmos.DrawLine(_points[i], _points[i + 1]);

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Transform waypoint = _waypoints[i];
                Gizmos.DrawSphere(waypoint.position, waypointSize);

                // A stub out of each waypoint's nose, so which way the camera will be facing is
                // visible without selecting them one at a time.
                if (look == LookMode.Waypoints)
                {
                    Gizmos.DrawLine(waypoint.position,
                        waypoint.position + waypoint.forward * (waypointSize * 5f));
                }
            }

            if (look == LookMode.Target && lookTarget != null && _points.Count > 0)
            {
                Gizmos.color = new Color(pathColor.r, pathColor.g, pathColor.b, 0.25f);

                Gizmos.DrawLine(_points[0], lookTarget.position);
                Gizmos.DrawLine(_points[_points.Count - 1], lookTarget.position);
            }
        }
    }
}
