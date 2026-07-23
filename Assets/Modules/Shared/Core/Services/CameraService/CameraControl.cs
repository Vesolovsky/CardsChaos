namespace Vesolovsky.Core.Services
{
    /// <summary>
    /// The single switch the camera rig reads. Gameplay flips it when something else takes the
    /// mouse - the card close-up, say - so panning and turning stand down together rather than
    /// each having to be suspended by hand.
    /// </summary>
    public interface ICameraControl
    {
        bool Enabled { get; set; }
    }

    public class CameraControl : ICameraControl
    {
        public bool Enabled { get; set; } = true;
    }
}
