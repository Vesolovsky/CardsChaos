namespace Vesolovsky.Core.Services.Save
{
    public interface IGameSave
    {
        /// <summary>
        /// A deep, independent copy. Taken on the main thread so the save can then be serialized on
        /// a background thread without racing gameplay code that keeps mutating the live save.
        /// Collections must be fresh instances; their elements may be shared only if they are never
        /// mutated in place after creation.
        /// </summary>
        IGameSave Clone();
    }
}
