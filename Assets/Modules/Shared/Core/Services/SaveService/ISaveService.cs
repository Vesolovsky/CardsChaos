using System;
using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.Services.Save
{
    public interface ISaveService<out T> where T : IGameSave
    {
        /// <summary>
        /// Raised after <see cref="ClearSave"/> has wiped the in-memory save, on the main thread.
        ///
        /// This exists for anything that keeps its own copy of something read out of the save.
        /// <see cref="CurrentSave"/> is the same object either side of a clear - its contents are
        /// emptied in place - so nothing can tell by looking at it that the game it was describing
        /// is gone. A service that built an index from the save at startup and then outlives the
        /// scene (the album does, on the project context) would carry the finished game's contents
        /// straight into the new one. Subscribe and throw the copy away.
        /// </summary>
        event Action Cleared;

        public UniTask Save();

        /// <summary>
        /// Writes the save synchronously. For the application-quit path, where the player loop will
        /// not run long enough to finish an async write before the process exits.
        /// </summary>
        public void SaveBlocking();

        public void ClearSave();
        public T CurrentSave { get; }
    }
}
