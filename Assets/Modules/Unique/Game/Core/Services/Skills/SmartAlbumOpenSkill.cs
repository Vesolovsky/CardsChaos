using CardsChaos.Cards;
using CardsChaos.Cards.Album;
using Vesolovsky.Core.Services;
using Vesolovsky.Game.Upgrades;
using Zenject;

namespace Vesolovsky.Game.Services.Skills
{
    /// <summary>
    /// Opens the album straight to the page for the card in hand.
    ///
    /// Only from outside the album - its whole point is getting there in one press. It works out
    /// the set and page from the selected card and asks the album to open there through the focus
    /// channel; higher levels only shorten the cooldown, which the skill service handles, so there
    /// is nothing level-dependent to do here.
    /// </summary>
    public class SmartAlbumOpenSkill : ISkillHandler
    {
        private readonly CardHand _hand;
        private readonly IAlbumFocusRequest _focus;
        private readonly IWorldInteractionLock _worldLock;

        [Inject]
        public SmartAlbumOpenSkill(
            CardHand hand, IAlbumFocusRequest focus, IWorldInteractionLock worldLock)
        {
            _hand = hand;
            _focus = focus;
            _worldLock = worldLock;
        }

        public SkillId Id => SkillId.SmartAlbumOpen;

        public bool CanActivate()
        {
            return !_worldLock.IsLocked && _hand.SelectedCard != null;
        }

        public bool Activate(SkillDefinition definition, int level)
        {
            Card selected = _hand.SelectedCard;
            if (selected == null || selected.Identity == null)
                return false;

            // Numbers are one-based and slots zero-based, so the card's number minus one is its
            // slot, which the layout turns into a page.
            int page = AlbumLayout.PageOfSlot(selected.Identity.Number - 1);
            _focus.Request(selected.Identity.SetId, page);
            return true;
        }
    }
}
