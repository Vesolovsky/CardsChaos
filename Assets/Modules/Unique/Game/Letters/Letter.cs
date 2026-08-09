using UnityEngine;

namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// One readable letter lying in the room. As far as the cursor is concerned it behaves like a
    /// card - it lights up with the same hover outline and opens on a click - but instead of going
    /// into the hand it shows a short piece of environmental story and is then gone for good.
    ///
    /// A letter authors only what is unique to it: which letter it is (<see cref="Id"/>), who left it
    /// (<see cref="Author"/> - fills the signature and picks the handwriting) and its
    /// <see cref="Body"/>. The shared look (outline colour and width) lives once in
    /// <see cref="LetterSettings"/>, not here.
    ///
    /// Keep a letter's object NOT marked "Batching Static": static batching swaps its mesh for the
    /// scene's combined mesh at play time, which the hover outline cannot trace. Letters are dynamic
    /// props anyway (some are animated), so this is no loss.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("CardsChaos/Letter")]
    public class Letter : MonoBehaviour
    {
        [Tooltip("Which letter this is. Whether it has been read is stored in the save under this " +
                 "id, so each placed letter must name a different one. Add new ids to the LetterId enum.")]
        [SerializeField] private LetterId id;

        [Tooltip("Who left this letter. Their name signs it and their handwriting is the font it is " +
                 "shown in, both taken from the author roster in the LettersInstaller's settings.")]
        [SerializeField] private LetterAuthor author;

        [TextArea(3, 12)]
        [Tooltip("The environmental story printed on the letter.")]
        [SerializeField] private string body;

        [Tooltip("The endgame certificate. When on, opening this letter shows the certificate state " +
                 "of the letter view (the Certificate object) instead of the note text, and Author/" +
                 "Body above are ignored - the certificate's message is fixed in the view.")]
        [SerializeField] private bool certificate;

        // One renderer for a plain prop, or one per LOD level. An LOD Group leaves only the current
        // level's renderer enabled, so the outline has to trace whichever that is.
        private MeshRenderer[] _renderers;
        private MeshFilter[] _filters;

        public LetterId Id => id;

        public LetterAuthor Author => author;

        public string Body => body;

        /// <summary>Whether this is the endgame certificate letter (opens the view in certificate mode).</summary>
        public bool IsCertificate => certificate;

        /// <summary>The renderer currently drawing the letter - the enabled LOD, or the only one.</summary>
        public MeshRenderer OutlineRenderer
        {
            get
            {
                int index = ActiveIndex();
                return index >= 0 ? _renderers[index] : null;
            }
        }

        /// <summary>The mesh of <see cref="OutlineRenderer"/>, so the two always describe one object.</summary>
        public Mesh OutlineMesh
        {
            get
            {
                int index = ActiveIndex();
                return index >= 0 && _filters[index] != null ? _filters[index].sharedMesh : null;
            }
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            _filters = new MeshFilter[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].TryGetComponent(out _filters[i]);
            }
        }

        /// <summary>
        /// Takes the letter out of the room. Once read a letter is gone for good - the object is
        /// simply switched off, so it stops drawing and stops answering the cursor. Called both when
        /// the player reads it and, on load, for a letter a past session already read.
        /// </summary>
        public void Collect()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// The renderer to outline: the first one that is actually drawing right now (an LOD Group
        /// enables only the current level), falling back to the first renderer if the group has yet
        /// to pick a level this frame.
        /// </summary>
        private int ActiveIndex()
        {
            if (_renderers == null)
                return -1;

            int fallback = -1;
            for (int i = 0; i < _renderers.Length; i++)
            {
                MeshRenderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                if (fallback < 0)
                    fallback = i;

                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    return i;
            }

            return fallback;
        }
    }
}
