using UnityEditor;
using UnityEngine;

namespace Vesolovsky.Game.Trailer.TrailerEditor
{
    /// <summary>
    /// Authoring for the dolly rails: adds waypoints where the scene view is looking from, and
    /// scrubs the scene view along the finished track so the whole move can be judged before it is
    /// ever filmed.
    ///
    /// The intended loop is: fly the scene view to where the shot should begin, press Add Waypoint
    /// Here, fly on to the next point, press it again. Fine tuning is then dragging the waypoints
    /// about like any other object, with the path redrawing as they move.
    /// </summary>
    [CustomEditor(typeof(TrailerDollyTrack))]
    public class TrailerDollyTrackEditor : Editor
    {
        private float _preview;

        public override void OnInspectorGUI()
        {
            var track = (TrailerDollyTrack)target;

            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Authoring", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Waypoint Here (scene view pose)"))
                AddWaypoint(track);

            track.Rebuild();

            int count = track.WaypointCount;

            if (count < 2)
            {
                EditorGUILayout.HelpBox(
                    "A track needs at least two waypoints. Fly the scene view to where the shot " +
                    "should start and press Add Waypoint Here, then fly on and press it again.",
                    MessageType.Info);

                return;
            }

            EditorGUILayout.LabelField(
                $"{count} waypoint(s), {track.Length:0.00} m of rail.", EditorStyles.miniLabel);

            EditorGUILayout.Space();

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                _preview = EditorGUILayout.Slider("Preview", _preview, 0f, 1f);

                if (scope.changed)
                    LookAlong(track, _preview);
            }

            EditorGUILayout.HelpBox(
                "Preview drives the scene view along the rails - drag it to check the framing of " +
                "the whole move. Filming it is the Trailer Dolly component, in play mode.",
                MessageType.None);
        }

        private static void AddWaypoint(TrailerDollyTrack track)
        {
            SceneView view = SceneView.lastActiveSceneView;

            if (view == null || view.camera == null)
            {
                Debug.LogWarning("[Trailer] No scene view to take a waypoint from.");
                return;
            }

            Transform pose = view.camera.transform;

            var waypoint = new GameObject($"WP {track.transform.childCount + 1:00}");
            Undo.RegisterCreatedObjectUndo(waypoint, "Add Trailer Waypoint");

            waypoint.transform.SetParent(track.transform, worldPositionStays: true);
            waypoint.transform.SetPositionAndRotation(pose.position, pose.rotation);

            // Selected straight away: the next thing anyone does with a fresh waypoint is nudge it.
            Selection.activeGameObject = waypoint;
            EditorUtility.SetDirty(track);
        }

        private static void LookAlong(TrailerDollyTrack track, float distance)
        {
            SceneView view = SceneView.lastActiveSceneView;

            if (view == null)
                return;

            track.Rebuild();
            Pose pose = track.Sample(distance);

            // Same trick as the shot book's Look: the scene view orbits a pivot, so its camera
            // lands on the pose only if the pivot is put out in front of it.
            view.rotation = pose.rotation;
            view.pivot = pose.position + pose.rotation * Vector3.forward * view.cameraDistance;
            view.Repaint();
        }
    }
}
