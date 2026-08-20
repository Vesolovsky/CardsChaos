using System.Collections.Generic;
using UnityEngine;

namespace CardsChaos.Cards
{
    /// <summary>
    /// The drained-of-colour twin of a card's own material, made once per card face and shared by
    /// every copy of it.
    ///
    /// The obvious way to grey a card lying in the room is a MaterialPropertyBlock, the way the
    /// held card does it - but a renderer carrying a property block drops out of the SRP Batcher,
    /// and the room's cards are batched by nothing else: no static batching, a material each, one
    /// shader between them. Greying a few hundred floor cards that way would turn a few batches
    /// into a few hundred draws. A second material does not cost that: the batcher does not care
    /// how many materials it is fed, only that they share a shader and carry no per-renderer state.
    ///
    /// So the price is memory instead, and a small one - one extra Material per card face that is
    /// actually greyed, made the first time it is needed and kept for the session. Only cards
    /// authored twice can ever be greyed, so the ceiling is the number of duplicated faces.
    /// </summary>
    public static class CardGreyMaterials
    {
        private static readonly int GrayscaleId = Shader.PropertyToID("_Grayscale");

        private static readonly Dictionary<Material, Material> ByOriginal =
            new Dictionary<Material, Material>();

        /// <summary>
        /// The grey twin of <paramref name="original"/>, made on first ask. Null in, null out, so a
        /// card without a material simply stays as it is.
        /// </summary>
        public static Material Grey(Material original)
        {
            if (original == null)
                return null;

            if (ByOriginal.TryGetValue(original, out Material grey) && grey != null)
                return grey;

            grey = new Material(original)
            {
                name = $"{original.name} (Grey)",

                // Never written to disk and never picked up by a scene: this is a runtime twin of an
                // asset, and Unity should not try to save or unload it behind our backs.
                hideFlags = HideFlags.HideAndDontSave,
            };

            grey.SetFloat(GrayscaleId, 1f);

            ByOriginal[original] = grey;
            return grey;
        }

        // Cleared for the same reason CardCatalog guards its lookup: with domain reload switched
        // off, a static dictionary survives leaving play mode, and every Material in it does not -
        // entering play again would hand out a table of destroyed objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => ByOriginal.Clear();
    }
}
