using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vesolovsky.Core.Services
{
    /// <summary>
    /// The single switch that says whether the player is still driving the room, or whether
    /// something else has taken it over - the card close-up, the album.
    ///
    /// Held rather than flipped. Every takeover keeps a handle and the room only comes back once
    /// the last one is let go, so two overlapping takeovers cannot hand control back while the
    /// other is still using it. A plain boolean gets that wrong the first time two of them
    /// overlap, and the symptom - a camera that pans behind an open panel - is a long way from
    /// the cause.
    /// </summary>
    public interface IWorldInteractionLock
    {
        /// <summary>True while anything at all is holding the room.</summary>
        bool IsLocked { get; }

        /// <summary>
        /// Takes the room. Dispose the handle to give it back; disposing twice is harmless, so
        /// callers are free to clean up on both an explicit close and their own teardown.
        /// </summary>
        /// <param name="owner">Only ever used to name the holder in diagnostics.</param>
        IDisposable Acquire(object owner);
    }

    public sealed class WorldInteractionLock : IWorldInteractionLock
    {
        private readonly HashSet<Handle> _holders = new HashSet<Handle>();

        public bool IsLocked => _holders.Count > 0;

        public IDisposable Acquire(object owner)
        {
            var handle = new Handle(this, owner);
            _holders.Add(handle);

            return handle;
        }

        private void Release(Handle handle)
        {
            if (_holders.Remove(handle))
                return;

            // Getting here means a handle outlived the lock it came from, which in practice means
            // two locks exist where the container was meant to provide one.
            Debug.LogError(
                $"[WorldInteractionLock] '{handle.Owner}' released a handle this lock never " +
                "issued. Check that the lock is bound AsSingle.");
        }

        private sealed class Handle : IDisposable
        {
            private readonly WorldInteractionLock _lock;
            private bool _released;

            public object Owner { get; }

            public Handle(WorldInteractionLock owningLock, object owner)
            {
                _lock = owningLock;
                Owner = owner;
            }

            public void Dispose()
            {
                if (_released)
                    return;

                _released = true;
                _lock.Release(this);
            }
        }
    }
}
