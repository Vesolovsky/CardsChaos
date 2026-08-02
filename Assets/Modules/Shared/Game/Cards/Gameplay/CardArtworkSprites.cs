using System.Collections.Generic;
using UnityEngine;

namespace CardsChaos.Cards
{
    /// <summary>
    /// Supplies UI-only Sprite wrappers for card textures. Sprite.Create keeps the original
    /// Texture2D as its backing texture; it does not copy or decompress the artwork pixels.
    /// </summary>
    public static class CardArtworkSprites
    {
        private const float PixelsPerUnit = 100f;

        private static readonly Dictionary<Texture2D, Sprite> Sprites =
            new Dictionary<Texture2D, Sprite>();

        public static Sprite Get(Texture2D texture)
        {
            if (texture == null)
                return null;

            if (Sprites.TryGetValue(texture, out Sprite sprite) && sprite != null)
                return sprite;

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                Vector4.zero,
                false);

            sprite.name = $"{texture.name}_CardArtworkSprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[texture] = sprite;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            // Enter Play Mode can keep the managed domain alive. Explicitly dispose wrappers
            // from the previous run before clearing their managed references.
            foreach (Sprite sprite in Sprites.Values)
            {
                if (sprite == null)
                    continue;

                if (Application.isPlaying)
                    Object.Destroy(sprite);
                else
                    Object.DestroyImmediate(sprite);
            }

            Sprites.Clear();
        }
    }
}
