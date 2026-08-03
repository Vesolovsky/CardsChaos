using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.Services.Save
{
    public interface ISaveService<out T> where T : IGameSave
    {
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
