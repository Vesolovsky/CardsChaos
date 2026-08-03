namespace Vesolovsky.Core.Services
{
    /// <summary>
    /// The camera's heading (yaw) - the one part of its rotation the player controls. Exposed so a
    /// loaded save can put the player back facing the way they left off. Pitch and roll are fixed
    /// by the authored shot and are not the player's, nor the save's, to change.
    /// </summary>
    public interface ICameraHeading
    {
        float Heading { get; }
        void SetHeading(float yawDegrees);
    }
}
