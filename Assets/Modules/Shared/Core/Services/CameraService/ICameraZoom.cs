namespace Vesolovsky.Core.Services
{
    /// <summary>
    /// How far the view is currently zoomed in, for anything that has to answer to it - the same
    /// small seam <see cref="ICameraHeading"/> is, so the look controller can slow itself down
    /// without knowing what a zoom is or who owns the field of view.
    /// </summary>
    public interface ICameraZoom
    {
        /// <summary>
        /// What a mouse movement should be worth right now, as a fraction of its normal turn: 1
        /// while the view is wide, dropping toward the zoom's share of the authored field of view
        /// as it narrows. Without it the same flick of the hand would swing a zoomed view twice as
        /// far across the room, which is exactly the twitchiness a zoom is meant to remove.
        /// </summary>
        float LookScale { get; }
    }
}
