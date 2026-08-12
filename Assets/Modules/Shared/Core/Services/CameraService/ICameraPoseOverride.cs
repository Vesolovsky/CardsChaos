namespace Vesolovsky.Core.Services
{
    /// <summary>
    /// Puts the camera at an exact heading and tilt at once.
    ///
    /// <see cref="ICameraHeading"/> is the gameplay-facing half - a save restores where the player
    /// was facing and nothing else. This is the tool-facing half: something that poses the camera
    /// outright (the trailer shot list, the dolly handing control back) has to tell the look
    /// controller where it left the camera, or the very next right-drag would swing the camera
    /// back to the tilt the scene was authored with.
    ///
    /// Roll is not part of it. The look controller pins roll at zero every frame, so a pose with
    /// roll in it survives only until the player looks around.
    /// </summary>
    public interface ICameraPoseOverride
    {
        void SetPose(float yawDegrees, float pitchDegrees);
    }
}
