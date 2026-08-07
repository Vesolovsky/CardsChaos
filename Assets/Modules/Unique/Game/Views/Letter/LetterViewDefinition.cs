using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.UISystem;

namespace Vesolovsky.Game.Views
{ 
    public class LetterViewDefinition : IViewDefinition
    {
        public ViewName Name { get; set; } = ViewName.Letter;
        public string Address { get; set; } = "LetterView";
        public string Id { get; set; } = "LetterView";
        public IViewInitData ViewInitData { get; set; } = ViewInitDataDefaults.Default;
        public IViewModelInitData ViewModelInitData { get; set; } = ViewModelInitDataDefaults.Default;
    }
}