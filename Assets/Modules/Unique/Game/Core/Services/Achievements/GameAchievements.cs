using System;
using System.Collections.Generic;
using UnityEngine;
using Vesolovsky.Core.Services.Achievements;

namespace Vesolovsky.Game.Services.Achievements
{
    /// <summary>
    /// The game's achievement table: what each <see cref="AchievementId"/> is called on Steam, the
    /// milestone each counted one asks for, and the sets each collection one is made of.
    ///
    /// Deliberately code rather than an authored asset. The API names have to match the partner site
    /// character for character, and the set groupings are fixed content decisions, not tuning - a
    /// table read straight out of source is the version of this that can be diffed, reviewed and
    /// checked against the store page. <see cref="Validate"/> is what catches the one mistake this
    /// costs: a set id that no longer exists in the catalog.
    /// </summary>
    public static class GameAchievements
    {
        // --- Milestones ---

        /// <summary>Cards correctly filed for the first album milestone.</summary>
        public const int AlbumHundredTarget = 100;

        /// <summary>Cards correctly filed for the big album milestone.</summary>
        public const int AlbumThousandTarget = 1000;

        /// <summary>Duplicates boxed for the first duplicate milestone.</summary>
        public const int DuplicatesHundredTarget = 100;

        /// <summary>The endgame set, whose single card finishes the collection.</summary>
        public const string EndgameSetId = "TheCollector";

        // --- Steam API names ---

        // These are the strings on the Steamworks partner site. Change one here and it must be
        // changed there too, or the award is silently refused (the service logs it).
        private static readonly Dictionary<AchievementId, string> ApiNames =
            new Dictionary<AchievementId, string>
            {
                { AchievementId.AlbumHundred, "ALBUM_100" },
                { AchievementId.DuplicatesHundred, "DUPLICATES_100" },
                { AchievementId.SetsBirds, "SETS_BIRDS" },
                { AchievementId.SetsGlobetrotter, "SETS_GLOBETROTTER" },
                { AchievementId.SetsVehicles, "SETS_VEHICLES" },
                { AchievementId.SetsChildhood, "SETS_CHILDHOOD" },
                { AchievementId.TheCollector, "THE_COLLECTOR" },
                { AchievementId.SetsCuisine, "SETS_CUISINE" },
                { AchievementId.HouseByLevitate, "HOUSE_LEVITATE" },
                { AchievementId.HouseByThrow, "HOUSE_THROW" },
                { AchievementId.AllLetters, "LETTERS_ALL" },
                { AchievementId.AlbumThousand, "ALBUM_1000" },
                { AchievementId.AllDuplicates, "DUPLICATES_ALL" },
                { AchievementId.AllTasks, "TASKS_ALL" },
                { AchievementId.AllSkillsMaxed, "SKILLS_MAXED" },
            };

        // --- Set groupings ---

        // Set ids, which are the sets' folder names - the same key the album and the save use.
        private static readonly Dictionary<AchievementId, string[]> SetGroups =
            new Dictionary<AchievementId, string[]>
            {
                {
                    AchievementId.SetsBirds,
                    new[] { "BirdsOfTheSun", "MoonBirds" }
                },
                {
                    AchievementId.SetsGlobetrotter,
                    new[] { "Greece", "Iceland", "Italy", "Lapland", "Poland", "Spain", "Tokyo" }
                },
                {
                    AchievementId.SetsVehicles,
                    new[] { "18WheelsOfFutureSteel", "2077Beasts", "2077Phantoms", "RoadLegends" }
                },
                {
                    AchievementId.SetsChildhood,
                    new[] { "ClothesForBabyBoy", "ClothesForBabyGirl", "MyFirstPainting!" }
                },
                {
                    AchievementId.SetsCuisine,
                    new[]
                    {
                        "ChineseCuisine", "FrenchCuisine", "ItalianCuisine", "PolishCuisine",
                        "Vegetables",
                    }
                },
            };

        /// <summary>Every achievement made of a group of sets, and the sets it asks for.</summary>
        public static IReadOnlyDictionary<AchievementId, string[]> BySetGroup => SetGroups;

        /// <summary>The name Steam knows this achievement by.</summary>
        public static string ApiName(AchievementId id)
        {
            if (ApiNames.TryGetValue(id, out string name))
                return name;

            Debug.LogError($"[{nameof(GameAchievements)}] '{id}' has no Steam API name.");
            return string.Empty;
        }

        /// <summary>Awards an achievement by id. A no-op when it is already earned.</summary>
        public static void Unlock(this IAchievementService service, AchievementId id)
        {
            service?.Unlock(ApiName(id));
        }

        /// <summary>Whether the platform already has this one recorded as earned.</summary>
        public static bool IsUnlocked(this IAchievementService service, AchievementId id)
        {
            return service != null && service.IsUnlocked(ApiName(id));
        }

        /// <summary>Shows the "X / Y" toast for a counted achievement, without awarding it.</summary>
        public static void ReportProgress(
            this IAchievementService service, AchievementId id, int current, int required)
        {
            service?.ReportProgress(ApiName(id), current, required);
        }

        /// <summary>
        /// Checks the table against the shipped content: every achievement has a name, no two share
        /// one, and every set named in a group actually exists. Run once at startup, so a set
        /// renamed in the project surfaces here rather than as an achievement that quietly never
        /// awards.
        /// </summary>
        public static void Validate(Func<string, bool> setExists)
        {
            foreach (AchievementId id in (AchievementId[])Enum.GetValues(typeof(AchievementId)))
            {
                if (!ApiNames.ContainsKey(id))
                    Debug.LogError($"[{nameof(GameAchievements)}] '{id}' has no Steam API name.");
            }

            var seen = new HashSet<string>();
            foreach (KeyValuePair<AchievementId, string> entry in ApiNames)
            {
                if (string.IsNullOrEmpty(entry.Value))
                {
                    Debug.LogError($"[{nameof(GameAchievements)}] '{entry.Key}' has an empty API name.");
                    continue;
                }

                if (!seen.Add(entry.Value))
                {
                    Debug.LogError(
                        $"[{nameof(GameAchievements)}] Two achievements share the API name " +
                        $"'{entry.Value}'; '{entry.Key}' is the duplicate.");
                }
            }

            if (setExists == null)
                return;

            foreach (KeyValuePair<AchievementId, string[]> group in SetGroups)
            {
                foreach (string setId in group.Value)
                {
                    if (!setExists(setId))
                    {
                        Debug.LogError(
                            $"[{nameof(GameAchievements)}] '{group.Key}' asks for the set '{setId}', " +
                            "which is not in the card catalog - it can never be awarded.");
                    }
                }
            }

            if (!setExists(EndgameSetId))
            {
                Debug.LogError(
                    $"[{nameof(GameAchievements)}] The endgame set '{EndgameSetId}' is not in the " +
                    "card catalog; the collection achievement can never be awarded.");
            }
        }
    }
}
