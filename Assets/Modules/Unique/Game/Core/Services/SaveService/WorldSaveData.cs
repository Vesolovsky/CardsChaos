using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Game.Services.Save
{
    /// <summary>
    /// The saved room: where the player is standing, which cards are in hand and how, and where
    /// every card on the floor lies. Null on a save written before world state existed (or on a
    /// fresh game), which the apply step reads as "leave the authored scene as it is".
    /// </summary>
    public sealed class WorldState
    {
        public SaveVector3 PlayerPosition { get; set; }
        public SaveQuaternion PlayerRotation { get; set; }

        /// <summary>The hand layout at save time, stored by name (CardHandLayout: Pile / Fan).</summary>
        public string HandLayout { get; set; }

        /// <summary>
        /// Cards in hand, in hand order - index 0 is the top of the pile / leftmost of the fan.
        /// Repeated set-and-number rows are separate physical copies; the hand lays them out anew.
        /// </summary>
        public List<SavedCard> HeldCards { get; set; }

        /// <summary>
        /// Every physical card resting in the room. Repeated identities are preserved as separate
        /// rows; cards in a stack additionally name their container and local pose.
        /// </summary>
        public List<SavedGroundCard> GroundCards { get; set; }
    }

    /// <summary>
    /// One physical card named by set and number. The list row, not CardRef, is its instance:
    /// multiple equal rows deliberately represent duplicates.
    /// </summary>
    public class SavedCard
    {
        public string SetId { get; set; }
        public int Number { get; set; }
    }

    /// <summary>A card on the floor: its identity plus where it came to rest.</summary>
    public sealed class SavedGroundCard : SavedCard
    {
        public SaveVector3 Position { get; set; }
        public SaveQuaternion Rotation { get; set; }

        /// <summary>Empty for an ordinary room card; otherwise the stable CardStackContainer id.</summary>
        public string ContainerId { get; set; }

        /// <summary>Grid slot within the named container. Ignored when ContainerId is empty.</summary>
        public int ContainerSlot { get; set; }

        /// <summary>Exact local pose preserves stack ordering and authored loose test layouts.</summary>
        public SaveVector3 ContainerLocalPosition { get; set; }
        public SaveQuaternion ContainerLocalRotation { get; set; }
    }

    /// <summary>
    /// One skill's cooldown at save time. Skills that are ready are simply absent. The id is stored
    /// by name so reordering the SkillId enum never repoints a saved cooldown at the wrong skill.
    ///
    /// A timed skill saved mid-spell carries the rest of that spell too, so quitting with Muscle
    /// Memory running comes back with it still running rather than already recovering. Absent in a
    /// save written before timed skills existed, which reads as zero - plain cooldown.
    /// </summary>
    public sealed class SkillCooldownState
    {
        public string SkillId { get; set; }
        public float Remaining { get; set; }
        public float Total { get; set; }
        public float ActiveRemaining { get; set; }
        public float ActiveTotal { get; set; }
    }

    /// <summary>
    /// A Vector3 as three plain floats. Serializing UnityEngine.Vector3 directly through
    /// Newtonsoft drags in normalized/magnitude and their recursion; this keeps the JSON to x/y/z.
    /// </summary>
    [Serializable]
    public struct SaveVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public SaveVector3(Vector3 value)
        {
            X = value.x;
            Y = value.y;
            Z = value.z;
        }

        public Vector3 ToVector3() => new Vector3(X, Y, Z);
    }

    /// <summary>A Quaternion as four plain floats, for the same reason as <see cref="SaveVector3"/>.</summary>
    [Serializable]
    public struct SaveQuaternion
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public SaveQuaternion(Quaternion value)
        {
            X = value.x;
            Y = value.y;
            Z = value.z;
            W = value.w;
        }

        public Quaternion ToQuaternion() => new Quaternion(X, Y, Z, W);
    }
}
