using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Keeps a streamed card texture at its authored mip while a large close-up is using it.
    /// Requests are reference counted because a set back can be shared by several views, and one
    /// view closing must not clear a request still owned by another one.
    /// </summary>
    public static class CardMipStreaming
    {
        private sealed class RequestState
        {
            public int Count;
        }

        private sealed class FullResolutionLease : IDisposable
        {
            private Texture2D _texture;

            public FullResolutionLease(Texture2D texture) => _texture = texture;

            public void Dispose()
            {
                if (_texture == null)
                    return;

                Release(_texture);
                _texture = null;
            }
        }

        private static readonly Dictionary<Texture2D, RequestState> Requests =
            new Dictionary<Texture2D, RequestState>();

        /// <summary>
        /// Pins the texture at mip zero until the returned lease is disposed. Returns
        /// null for missing or non-streamed textures, so callers can dispose with ?.Dispose().
        /// </summary>
        public static IDisposable RequestFullResolution(Texture2D texture)
        {
            if (texture == null || !texture.streamingMipmaps)
                return null;

            if (!Requests.TryGetValue(texture, out RequestState state))
            {
                state = new RequestState();
                Requests.Add(texture, state);
            }

            state.Count++;
            texture.requestedMipmapLevel = 0;

            return new FullResolutionLease(texture);
        }

        /// <summary>Compatibility overload for UI code that already holds a Sprite.</summary>
        public static IDisposable RequestFullResolution(Sprite sprite) =>
            RequestFullResolution(sprite != null ? sprite.texture : null);

        /// <summary>True once a requested close-up can be revealed without a low-mip pop.</summary>
        public static bool IsFullResolutionLoaded(Texture2D texture)
        {
            return texture == null
                   || !texture.streamingMipmaps
                   || texture.IsRequestedMipmapLevelLoaded();
        }

        /// <summary>Compatibility overload for UI code that already holds a Sprite.</summary>
        public static bool IsFullResolutionLoaded(Sprite sprite) =>
            IsFullResolutionLoaded(sprite != null ? sprite.texture : null);

        private static void Release(Texture2D texture)
        {
            if (!Requests.TryGetValue(texture, out RequestState state))
                return;

            state.Count--;
            if (state.Count > 0)
                return;

            texture.ClearRequestedMipmapLevel();
            Requests.Remove(texture);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRequests()
        {
            // Covers Enter Play Mode configurations that keep the managed domain alive.
            foreach (Texture2D texture in Requests.Keys)
            {
                if (texture != null)
                    texture.ClearRequestedMipmapLevel();
            }

            Requests.Clear();
        }
    }
}
