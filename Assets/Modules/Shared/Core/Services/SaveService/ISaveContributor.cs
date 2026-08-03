namespace Vesolovsky.Core.Services.Save
{
    /// <summary>
    /// Something that owns live runtime state which belongs in the save - the room, the skills,
    /// anything the plain data services do not already keep in the save object themselves.
    ///
    /// The coordinator asks every registered contributor to write its current state into the save
    /// on the main thread, once, right before each write. Concentrating the capture here keeps the
    /// snapshot taken at a single moment and off the per-frame hot path.
    /// </summary>
    public interface ISaveContributor
    {
        void CaptureForSave();
    }
}
