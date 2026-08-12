using UnityEditor;
using UnityEngine;
using Vesolovsky.Core.Services;

namespace Vesolovsky.Game.Trailer.TrailerEditor
{
    /// <summary>
    /// Capture and jump buttons for a <see cref="TrailerShotBook"/>.
    ///
    /// Out of play mode a shot is captured from wherever the scene view is looking from and
    /// previewed by putting the scene view back there - the game camera is never moved, because its
    /// pose in the scene is where a new game starts and nudging that by accident is a bad trade for
    /// a preview. In play mode both act on the real camera instead: walk the shot, press Capture,
    /// and it is a shot you can jump back to on the next take.
    /// </summary>
    [CustomEditor(typeof(TrailerShotBook))]
    public class TrailerShotBookEditor : Editor
    {
        private const float ActionWidth = 62f;

        public override void OnInspectorGUI()
        {
            var book = (TrailerShotBook)target;

            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shots"), true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (GUILayout.Button(Application.isPlaying
                    ? "Capture New Shot (game camera)"
                    : "Capture New Shot (scene view)"))
            {
                CaptureNew(book);
            }

            if (book.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No shots yet. Frame one in the scene view (or walk to it in play mode) and " +
                    "press Capture New Shot.", MessageType.Info);

                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shots", EditorStyles.boldLabel);

            for (int i = 0; i < book.Count; i++)
            {
                TrailerShot shot = book.Get(i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{i + 1}. {shot.Name}");

                    if (GUILayout.Button("Capture", GUILayout.Width(ActionWidth)))
                        Capture(book, shot);

                    if (GUILayout.Button("Look", GUILayout.Width(ActionWidth)))
                        LookThrough(shot);

                    using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    {
                        if (GUILayout.Button("Go", GUILayout.Width(ActionWidth)))
                            Go(shot);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Capture overwrites that shot's pose. Look points the scene view at it. Go moves " +
                "the running game's camera - Alt+1..0 do the same in play mode once a Trailer " +
                "Camera Shots component in the scene points at this book.", MessageType.None);
        }

        private static void CaptureNew(TrailerShotBook book)
        {
            if (!TryReadPose(out Vector3 position, out Quaternion rotation, out float fov))
                return;

            Undo.RecordObject(book, "Capture Trailer Shot");
            book.Add(position, rotation, fov);
            EditorUtility.SetDirty(book);
        }

        private static void Capture(TrailerShotBook book, TrailerShot shot)
        {
            if (!TryReadPose(out Vector3 position, out Quaternion rotation, out float fov))
                return;

            Undo.RecordObject(book, "Capture Trailer Shot");
            book.Write(shot, position, rotation, fov);
            EditorUtility.SetDirty(book);
        }

        /// <summary>
        /// The pose to remember: the live camera while the game runs, otherwise the scene view's.
        /// The field of view always comes from the game camera - the scene view has its own, which
        /// would frame the shot quite differently from the way it will be filmed.
        /// </summary>
        private static bool TryReadPose(out Vector3 position, out Quaternion rotation, out float fov)
        {
            Camera gameCamera = FindGameCamera();
            fov = gameCamera != null ? gameCamera.fieldOfView : 0f;

            if (Application.isPlaying && gameCamera != null)
            {
                position = gameCamera.transform.position;
                rotation = gameCamera.transform.rotation;
                return true;
            }

            SceneView view = SceneView.lastActiveSceneView;

            if (view == null || view.camera == null)
            {
                Debug.LogWarning("[Trailer] No scene view to capture from - open one, or enter " +
                                 "play mode to capture from the game camera.");

                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            position = view.camera.transform.position;
            rotation = view.camera.transform.rotation;
            return true;
        }

        private static void LookThrough(TrailerShot shot)
        {
            SceneView view = SceneView.lastActiveSceneView;

            if (view == null)
                return;

            var rotation = Quaternion.Euler(shot.Rotation);

            // The scene view orbits a pivot, so standing its camera on the shot means putting the
            // pivot out in front of the shot by however far back the view is currently orbiting.
            view.rotation = rotation;
            view.pivot = shot.Position + rotation * Vector3.forward * view.cameraDistance;
            view.Repaint();
        }

        private static void Go(TrailerShot shot)
        {
            var shots = FindFirstObjectByType<TrailerCameraShots>();

            if (shots != null)
            {
                // Routed through the component so the look controller is told about the new tilt,
                // the same as a jump made with the number keys.
                shots.Apply(shot);
                return;
            }

            Debug.LogWarning("[Trailer] No Trailer Camera Shots component in the scene, so the " +
                             "camera cannot be moved from here. Add one to the scene (beside the " +
                             "Cheats object) and point it at this book.");
        }

        private static Camera FindGameCamera()
        {
            var rig = FindFirstObjectByType<MainCamera>();

            if (rig != null && rig.Camera != null)
                return rig.Camera;

            return Camera.main;
        }
    }
}
