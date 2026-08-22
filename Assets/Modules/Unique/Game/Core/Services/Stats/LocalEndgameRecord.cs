using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vesolovsky.Core.Services.Save;
using Vesolovsky.Game.Services.Save;
using Zenject;

namespace Vesolovsky.Game.Services.Stats
{
    /// <summary>
    /// The ending, written down once.
    ///
    /// The moment the final card is filed the game is over, and the numbers on that screen are the
    /// numbers it ended on. Everything that produces them keeps running afterwards, though - the
    /// playtime clock does not stop, and the player is free to go back into the room - so reading
    /// them live would mean an ending that quietly rewrote itself every time it was looked at.
    /// This takes the copy, at the one moment it is the truth, and hands the same copy back
    /// forever after.
    /// </summary>
    public interface IEndgameRecord
    {
        /// <summary>
        /// Raised once, with the ending that was just written down, at the moment the game is
        /// finished. Never raised again - a second ending is refused, so neither is a second
        /// announcement - and never raised for an ending loaded back from a past session, which is
        /// news to nobody. Something that has to react to a save that was already finished should
        /// read <see cref="Summary"/> on load instead.
        /// </summary>
        event Action<EndgameSummary> Recorded;

        /// <summary>The frozen ending, or null while the game has not been finished.</summary>
        EndgameSummary Summary { get; }

        /// <summary>Whether the ending has already been written down.</summary>
        bool IsRecorded { get; }

        /// <summary>
        /// Freezes the tally as it stands and stamps the date. Does nothing once an ending is
        /// already on record - the first one is the real one, and nothing afterwards may move it.
        /// </summary>
        void Record();
    }

    /// <summary>
    /// Backed by the local save, and bound where the save is rather than with the scene that
    /// happens to draw the ending - the same reasoning as the album. A finished game outlives the
    /// gameplay scene, and the main menu reads this without any of the room being present.
    /// </summary>
    public class LocalEndgameRecord : IEndgameRecord
    {
        public event Action<EndgameSummary> Recorded;

        private readonly ISaveService<GameSave> _saveService;
        private readonly ISaveCoordinator _saveCoordinator;

        [Inject]
        public LocalEndgameRecord(ISaveService<GameSave> saveService, ISaveCoordinator saveCoordinator)
        {
            _saveService = saveService;
            _saveCoordinator = saveCoordinator;
        }

        public EndgameSummary Summary => _saveService.CurrentSave?.Endgame;

        public bool IsRecorded => Summary != null;

        public void Record()
        {
            GameSave save = _saveService.CurrentSave;
            if (save == null || save.Endgame != null)
                return;

            // The tracker works on this object in place, so it is the live tally rather than a
            // stale copy of it - which is why this has to be cloned rather than referenced.
            save.Endgame = new EndgameSummary
            {
                Stats = save.PlayerStats?.Clone() ?? new PlayerStatsData(),
                CompletedAt = DateTime.Now,
            };

            // The finale runs for the better part of half a minute and then loads the credits, so
            // this needs to be on its way to disk rather than waiting for a save that may never
            // come - and marking it dirty is not that. Nothing between here and the main menu
            // writes: the finale loads Credits straight from its fade, Credits loads the menu, and
            // the room's contributors are torn down on the way. That leaves the last stretch of
            // play - the final cards filed, the ending itself - riding on the auto-save timer,
            // which the player can lengthen or switch off entirely, and on a clean quit firing
            // OnApplicationQuit. A game killed instead of quit would come back to the menu still
            // a few cards short, with those cards lying on the floor of a room it had finished.
            //
            // So the write is forced here, at the one moment the game is over and the room is
            // still standing to be captured with it. Fire-and-forget because nothing waits on the
            // ending being on disk; the coordinator serializes a snapshot off the main thread and
            // logs its own failures, and the finale's ~15 seconds of typing is time enough.
            _saveCoordinator.MarkDirty();

            Debug.Log($"[{nameof(LocalEndgameRecord)}] Ending recorded on " +
                      $"{save.Endgame.CompletedAt:dd:MM:yyyy} with " +
                      $"{save.Endgame.Stats.CorrectlyPlacedCards}/{save.Endgame.Stats.TotalCards} " +
                      "cards placed.");

            // Announced before the write rather than after: the coordinator takes its snapshot
            // synchronously, so anything a listener puts into the save has to be in it by then.
            Recorded?.Invoke(save.Endgame);

            _saveCoordinator.SaveNow(force: true).Forget();
        }
    }
}
