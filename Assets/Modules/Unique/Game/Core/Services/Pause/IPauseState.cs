namespace Vesolovsky.Game.Services.Pause
{
    /// <summary>
    /// Whether the game is paused, and with it whether game time is running.
    ///
    /// Taking the room and stopping time are two different things: a card close-up or the album takes
    /// the room but leaves cooldowns ticking, whereas the pause menu stops the clock outright. This
    /// is the "clock stopped" half - anything that advances on game time (today only skill cooldowns)
    /// reads it and holds still while it is set.
    /// </summary>
    public interface IPauseState
    {
        bool IsPaused { get; set; }
    }

    public class PauseState : IPauseState
    {
        public bool IsPaused { get; set; }
    }
}
