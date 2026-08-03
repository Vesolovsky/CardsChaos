using System;
using Cysharp.Threading.Tasks;

namespace Vesolovsky.Core.Services.Save
{
    /// <summary>
    /// Single entry point for persisting the game save.
    /// <para>
    /// Nothing else in the project should call <see cref="ISaveService{T}.Save"/> directly.
    /// Code that mutates <c>CurrentSave</c> calls <see cref="MarkDirty"/> instead; the actual
    /// write happens when the player asks for it (<see cref="SaveNow"/>) or on the optional
    /// auto-save timer.
    /// </para>
    /// </summary>
    public interface ISaveCoordinator
    {
        /// <summary>
        /// True when <see cref="MarkDirty"/> has been called since the last successful write.
        /// </summary>
        bool HasUnsavedChanges { get; }

        /// <summary>
        /// Raised after every successful write, on the main thread.
        /// </summary>
        event Action Saved;

        /// <summary>
        /// Enables/disables the auto-save timer. Enabled by default with a five-minute interval.
        /// </summary>
        bool IsAutoSaveEnabled { get; set; }

        /// <summary>
        /// Seconds between auto-save attempts. Applies from the next interval.
        /// Values below one second are clamped.
        /// </summary>
        float AutoSaveIntervalSeconds { get; set; }

        /// <summary>
        /// Flags the in-memory save as changed. Cheap - safe to call on every mutation.
        /// Does not touch the disk.
        /// </summary>
        void MarkDirty();

        /// <summary>
        /// Writes the save to disk.
        /// </summary>
        /// <param name="force">
        /// When false (default) the write is skipped if nothing was marked dirty.
        /// Pass true to write unconditionally - needed after <see cref="ISaveService{T}.ClearSave"/>,
        /// which mutates the save without going through <see cref="MarkDirty"/>.
        /// </param>
        UniTask SaveNow(bool force = false);

        /// <summary>
        /// Captures contributors and writes synchronously. For the application-quit path, where an
        /// async write may not finish before the process exits. Always writes, dirty or not.
        /// </summary>
        void SaveBlocking();

        /// <summary>
        /// Registers a source of live runtime state (the room, the skills) to be captured into the
        /// save just before every write. Scene-scoped contributors register on load and must call
        /// <see cref="RemoveContributor"/> when their scene tears down.
        /// </summary>
        void AddContributor(ISaveContributor contributor);

        void RemoveContributor(ISaveContributor contributor);
    }
}
