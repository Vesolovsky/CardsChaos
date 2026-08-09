using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Vesolovsky.Game.Letters
{
    /// <summary>
    /// One person who leaves letters: the name that signs their notes and the handwriting they are
    /// written in.
    /// </summary>
    [Serializable]
    public class LetterAuthorInfo
    {
        public LetterAuthor Author;

        [Tooltip("The name that signs the letter, e.g. \"The Grand Collector\" or \"Mira Finch\"."), TextArea]
        public string DisplayName;

        [Tooltip("This person's handwriting - the font the letter is drawn in.")]
        public TMP_FontAsset Font;
    }

    /// <summary>
    /// The one place the letters' shared look is authored, rather than repeating it on every letter:
    /// the hover outline every letter uses, and the roster of people who leave letters (each with the
    /// name that signs their notes and the font they are written in). Bound once from
    /// <see cref="LettersInstaller"/>.
    /// </summary>
    [Serializable]
    public class LetterSettings
    {
        [Header("Hover outline")]
        [Tooltip("Colour of the hover outline. Shared by every letter.")]
        public Color HoverColor = Color.white;

        [Tooltip("Width of the hover outline, in world units (an absolute size, not relative to the " +
                 "letter). Shared by every letter. The cards use 0.002 because a card is tiny; a " +
                 "letter prop is far bigger, so it needs more - tune this up until the ring reads.")]
        public float HoverWidth = 0.01f;

        [Header("Authors")]
        [Tooltip("Everyone who leaves letters. Each letter names one of these as its author, which " +
                 "fills its signature and picks its handwriting.")]
        public List<LetterAuthorInfo> Authors = new List<LetterAuthorInfo>();

        [Header("Timed arrivals")]
        [Tooltip("Rules for letters that arrive on a milestone (a skill first used, a card-count " +
                 "reached, a set completed). Arrivals queue and show one at a time.")]
        public List<LetterTrigger> Triggers = new List<LetterTrigger>();

        [Header("Endgame")]
        [Tooltip("The set id of the one-card endgame set. Its card does not count toward the " +
                 "collection total (set CountsTowardCollection = false on the set asset too), and " +
                 "picking that card up brings the certificate letter.")]
        public string EpilogueSetId;

        [Tooltip("The certificate letter queued when the endgame card is picked up.")]
        public LetterId CertificateLetter;

        /// <summary>
        /// The record for this author, or null when the roster has no entry for them - in which case
        /// the letter simply shows with no signature and its default font.
        /// </summary>
        public LetterAuthorInfo GetAuthor(LetterAuthor author)
        {
            for (int i = 0; i < Authors.Count; i++)
            {
                if (Authors[i] != null && Authors[i].Author == author)
                    return Authors[i];
            }

            return null;
        }
    }
}
