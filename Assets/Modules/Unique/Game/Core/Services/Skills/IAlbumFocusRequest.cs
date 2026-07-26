using System;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// A one-way channel for asking the album to open on a particular set and page.
    ///
    /// The Smart Album Open skill knows which card is in hand and so which page it wants shown, but
    /// opening the album and turning to that page is the view's job. This sits between them: the
    /// skill raises a request, the album view listens and carries it out, and neither has to hold a
    /// reference to the other.
    /// </summary>
    public interface IAlbumFocusRequest
    {
        /// <summary>Raised with the set id and zero-based page index to open the album on.</summary>
        event Action<string, int> OpenRequested;

        void Request(string setId, int pageIndex);
    }

    public class AlbumFocusRequest : IAlbumFocusRequest
    {
        public event Action<string, int> OpenRequested;

        public void Request(string setId, int pageIndex) => OpenRequested?.Invoke(setId, pageIndex);
    }
}
