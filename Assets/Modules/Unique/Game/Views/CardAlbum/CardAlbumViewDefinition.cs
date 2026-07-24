using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class CardAlbumViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.CardAlbum;
        public string Address { get; set; } = "CardAlbumView";
        public string Id { get; set; } = "CardAlbumView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}