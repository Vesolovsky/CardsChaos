using System;

namespace Vesolovsky.Core.Services.Settings
{
    public interface IGameSettingsService
    {
        /// <summary>
        /// A defensive copy of the currently applied settings.
        /// </summary>
        GameSettingsData Current { get; }

        /// <summary>
        /// Raised after settings have been sanitized, applied to the runtime and persisted.
        /// The event argument is a snapshot and should be treated as read-only.
        /// </summary>
        event Action<GameSettingsData> Applied;

        /// <summary>
        /// Sanitizes, applies and persists a settings snapshot. The supplied object is never
        /// retained or mutated by the service.
        /// </summary>
        void Apply(GameSettingsData settings);
    }
}
