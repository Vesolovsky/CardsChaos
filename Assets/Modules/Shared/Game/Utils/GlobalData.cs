using Steamworks;
using UnityEngine;
using UnityEngine.Localization;

namespace Vesolovsky.Game.Utils
{
    /// <summary>
    /// Project-wide constants and shared localized strings.
    /// The Steam/Discord ids below are placeholders - fill them in per game.
    /// </summary>
    public static class GlobalData
    {
        private const string LOCALIZATION_STRINGS_TABLE = "StringsLocalizationTable";

        public static LayerMask UILayerMask => LayerMask.GetMask("UI");

        public const string MASTER_VOLUME_RTCP = "MasterVolume";
        public const string MUSIC_VOLUME_RTCP = "MusicVolume";
        public const string SFX_VOLUME_RTCP = "SFXVolume";
        public const string SELECTED_LOCALE_PLAYER_PREF = "selected-locale";
        public const string MAIN_MENU_SCENE_NAME = "MainMenu";

        public static AppId_t STEAM_APP_ID => (AppId_t)0;

        public static ulong DISCORD_APP_ID => 0;
        public static string STEAM_URL => $"https://store.steampowered.com/app/{STEAM_APP_ID}/";
        public static string DISCORD_URL => string.Empty;

        public static LocalizedString AreYouSureTitle => GetLocalizedStringById(LOCALIZATION_STRINGS_TABLE, "ID_Popup_AreYouSureTitle");
        public static LocalizedString Success => GetLocalizedStringById(LOCALIZATION_STRINGS_TABLE, "ID_Success");

        private static LocalizedString GetLocalizedStringById(string tableName, string key)
        {
            var ls = new LocalizedString
            {
                TableReference = tableName,
                TableEntryReference = key
            };

            return ls;
        }
    }
}
