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

                default:
                    Debug.Log($"Can't create default View Definition. ViewName: '{viewName}' not handled.");
                    return null;
            }
        }
    }
}
