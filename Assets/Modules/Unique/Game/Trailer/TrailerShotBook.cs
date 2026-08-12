using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Game.Trailer
{
    /// <summary>One remembered camera pose: where it stood, which way it faced, how wide it saw.</summary>
    [System.Serializable]
    public class TrailerShot
    {
        [Tooltip("Only ever shown in the inspector - name shots after what they are for.")]
        public string Name = "Shot";

        public Vector3 Position;

        [Tooltip("Euler angles. Roll (Z) is applied when the shot is taken, but the look controller " +
                 "pins roll at zero, so it is lost the moment the camera is looked around with.")]
        public Vector3 Rotation;

        [Tooltip("0 keeps whatever field of view the camera is already using.")]
        public float FieldOfView;
    }

    /// <summary>
    /// The list of camera positions a trailer take can jump between.
    ///
    /// It lives on an asset rather than on the scene object on purpose: a shot captured while the
    /// game is running would be thrown away with every other play-mode change if it were held on a
    /// component, and lining a shot up by walking to it in play is the whole point. Asset edits made
    /// in play mode survive leaving it.
    ///
    /// Author it from the asset's own inspector - each row captures from the Scene view (or, in play
    /// mode, from the live camera) and jumps the camera to itself. <see cref="TrailerCameraShots"/>
    /// is what makes the shots reachable from a key while a take is running.
    /// </summary>
    [CreateAssetMenu(menuName = "CardsChaos/Trailer/Shot Book", fileName = "TrailerShotBook")]
    public class TrailerShotBook : ScriptableObject
    {
        [SerializeField] private List<TrailerShot> shots = new List<TrailerShot>();

        public IReadOnlyList<TrailerShot> Shots => shots;

        public int Count => shots.Count;

        public TrailerShot Get(int index)
        {
            return index >= 0 && index < shots.Count ? shots[index] : null;
        }

        /// <summary>Appends a shot holding the given pose and hands it back.</summary>
        public TrailerShot Add(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            var shot = new TrailerShot { Name = $"Shot {shots.Count + 1}" };
            shots.Add(shot);

            Write(shot, position, rotation, fieldOfView);
            return shot;
        }

        /// <summary>Overwrites a shot's pose, leaving its name alone.</summary>
        public void Write(TrailerShot shot, Vector3 position, Quaternion rotation, float fieldOfView)
        {
            if (shot == null)
                return;

            shot.Position = position;
            shot.Rotation = rotation.eulerAngles;
            shot.FieldOfView = fieldOfView;

            MarkDirty();
        }

        public void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
