using UnityEngine;
using Vesolovsky.Core.UISystem;
using Vesolovsky.Game.Views;

namespace Vesolovsky.Game.UISystem
{
    public static class ViewDefaultDefinitionFactory
    {
        public static IViewDefinition CreateDefaultViewDefinition(IView view)
        {
            switch (view)
            {
                case AnalyticsConsentView:
                    return new AnalyticsConsentViewDefinition();

                case ConfirmationPopupView:
                    return new ConfirmationPopupViewDefinition();

                case CardAlbumView:
                    return new CardAlbumViewDefinition();

                case UpgradesView:
                    return new UpgradesViewDefinition();

                case GameplayHudView:
                    return new GameplayHudViewDefinition();

                case PauseView:
                    return new PauseViewDefinition();

                case SettingsView:
                    return new SettingsViewDefinition();

                default:
                    Debug.Log($"Can't create default View Definition. View of type: '{view.GetType()}' not handled.");
                    return null;
            }
        }

        public static IViewDefinition CreateDefaultViewDefinition(ViewName viewName)
        {
            switch (viewName)
            {
                case ViewName.None:
                    return null;

                case ViewName.AnalyticsConsent:
                    return new AnalyticsConsentViewDefinition();

                case ViewName.ConfirmationPopup:
                    return new ConfirmationPopupViewDefinition();

                case ViewName.CardAlbum:
                    return new CardAlbumViewDefinition();

                case ViewName.Upgrades:
                    return new UpgradesViewDefinition();

                case ViewName.GameplayHud:
                    return new GameplayHudViewDefinition();

                case ViewName.Pause:
                    return new PauseViewDefinition();

                case ViewName.Settings:
                    return new SettingsViewDefinition();

                default:
                    Debug.Log($"Can't create default View Definition. ViewName: '{viewName}' not handled.");
                    return null;
            }
        }
    }
}
