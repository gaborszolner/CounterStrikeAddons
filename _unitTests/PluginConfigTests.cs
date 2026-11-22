using System.Text.Json;
using SharedLibrary;

namespace _unitTests
{
    [TestClass]
    public class PluginConfigTests
    {
        private string _directory = null!;

        [TestInitialize]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"PluginConfigTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void CleanUp() => Directory.Delete(_directory, recursive: true);

        public static IEnumerable<object[]> PluginConfigs()
        {
            yield return [typeof(global::AdminMenu.AdminMenuConfig), new[] { "AutoTeamShuffleOnRoundStart", "AutoTeamShuffleMinDifferentPercentage", "WelcomeMessage", "DateRangeForStatisticsInMonth", "MinimumPlayerCountToStatistic", "AllowSameName", "MuteAfterDeathInSecounds", "Language" }];
            yield return [typeof(global::DamageReport.DamageReportConfig), new[] { "Language" }];
            yield return [typeof(global::GameStatistic.GameStatisticConfig), new[] { "DateRangeForStatisticsInMonth", "MinimumKillCountToShowInTop", "MinimumPlayerCountToStatistic", "EnableTopBottomCommand", "Language" }];
            yield return [typeof(global::MenuHotKey.MenuHotKeyConfig), new[] { "Language" }];
            yield return [typeof(global::QuickDefuse.QuickDefuseConfig), new[] { "Language" }];
            yield return [typeof(global::ReviveTeammate.ReviveTeammateConfig), new[] { "CanReviveTeammate", "ReviveHoldDurationSeconds", "ReviveDeathWindowSeconds", "ReviveHP", "Language" }];
            yield return [typeof(global::RockTheVote.RtvConfig), new[] { "EnableRtv", "EnableRtvInWarmup", "MinimumRtvPercentage", "ChangeMapImmediatelyAfterRtv", "EnableNominate", "EnableNominateInWarmup", "EnableEndOfMapVote", "EndOfMapVoteRoundsBeforeEnd", "VotingSecondLimit", "CloseVotingMenuAfterVote", "MapsToShow", "MapsInCoolDown", "MapSortOrder", "Language" }];
            yield return [typeof(global::SiteRestrict.SiteRestrictConfig), new[] { "MinCTsForSiteRestrict", "Language" }];
            yield return [typeof(global::SpamFilter.SpamFilterConfig), new[] { "Language" }];
        }

        [TestMethod]
        [DynamicData(nameof(PluginConfigs))]
        public void PluginConfig_ContainsOnlyItsOwnSettings(Type type, string[] expectedProperties)
        {
            var json = JsonSerializer.SerializeToElement(Activator.CreateInstance(type), type);
            CollectionAssert.AreEquivalent(expectedProperties, json.EnumerateObject().Select(property => property.Name).ToArray());
        }

        [TestMethod]
        [DynamicData(nameof(PluginConfigs))]
        public void DefaultFile_MatchesPluginConfig(Type type, string[] expectedProperties)
        {
            string fileName = type == typeof(global::RockTheVote.RtvConfig) ? "rtvConfig.json" : $"{type.Namespace}Config.json";
            string path = Path.Combine(AppContext.BaseDirectory, "ConfigDefaults", fileName);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var defaults = JsonSerializer.SerializeToElement(Activator.CreateInstance(type), type);

            Assert.IsTrue(JsonElement.DeepEquals(defaults, document.RootElement), $"{path} does not match the plugin defaults.");
        }

        [TestMethod]
        public void Load_CreatesPluginFileWithPreviouslyShippedDefaults()
        {
            var config = ConfigLoader.Load<global::AdminMenu.AdminMenuConfig>(_directory, "AdminMenuConfig.json");

            Assert.IsTrue(config.AutoTeamShuffleOnRoundStart);
            Assert.IsFalse(config.AllowSameName);
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "AdminMenuConfig.json")));
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "config.json")));
        }

        [TestMethod]
        public void Load_MigratesOnlyRelevantLegacySettings()
        {
            string legacy = """{"Language":"hu","DateRangeForStatisticsInMonth":7,"MinimumPlayerCountToStatistic":9,"CanReviveTeammate":true}""";
            File.WriteAllText(Path.Combine(_directory, "config.json"), legacy);

            var config = ConfigLoader.Load<global::AdminMenu.AdminMenuConfig>(_directory, "AdminMenuConfig.json");
            var statistics = ConfigLoader.Load<global::GameStatistic.GameStatisticConfig>(_directory, "GameStatisticConfig.json");
            string migrated = File.ReadAllText(Path.Combine(_directory, "AdminMenuConfig.json"));

            Assert.AreEqual("hu", config.Language);
            Assert.AreEqual(7, config.DateRangeForStatisticsInMonth);
            Assert.AreEqual(9, config.MinimumPlayerCountToStatistic);
            Assert.AreEqual(7, statistics.DateRangeForStatisticsInMonth);
            Assert.AreEqual(9, statistics.MinimumPlayerCountToStatistic);
            Assert.IsFalse(migrated.Contains("CanReviveTeammate"));
            Assert.AreEqual(legacy, File.ReadAllText(Path.Combine(_directory, "config.json")));
        }

        [TestMethod]
        public void Load_UsesLegacyDefaultsForSettingsMissingFromOldFile()
        {
            File.WriteAllText(Path.Combine(_directory, "config.json"), "{}");

            var admin = ConfigLoader.Load<global::AdminMenu.AdminMenuConfig>(_directory, "AdminMenuConfig.json");
            var statistics = ConfigLoader.Load<global::GameStatistic.GameStatisticConfig>(_directory, "GameStatisticConfig.json");
            var revive = ConfigLoader.Load<global::ReviveTeammate.ReviveTeammateConfig>(_directory, "ReviveTeammateConfig.json");

            Assert.IsFalse(admin.AutoTeamShuffleOnRoundStart);
            Assert.IsTrue(admin.AllowSameName);
            Assert.IsTrue(statistics.EnableTopBottomCommand);
            Assert.IsTrue(revive.CanReviveTeammate);
        }

        [TestMethod]
        public void Load_PrefersExistingPluginFileOverLegacyFile()
        {
            string pluginFile = Path.Combine(_directory, "SpamFilterConfig.json");
            File.WriteAllText(pluginFile, """{"Language":"de"}""");
            File.WriteAllText(Path.Combine(_directory, "config.json"), """{"Language":"hu"}""");

            var config = ConfigLoader.Load<global::SpamFilter.SpamFilterConfig>(_directory, "SpamFilterConfig.json");

            Assert.AreEqual("de", config.Language);
            Assert.AreEqual("""{"Language":"de"}""", File.ReadAllText(pluginFile));
        }

        [TestMethod]
        public void Load_InvalidLegacyFileFailsWithoutCreatingPluginFile()
        {
            File.WriteAllText(Path.Combine(_directory, "config.json"), "{");

            Assert.Throws<JsonException>(() => ConfigLoader.Load<global::SpamFilter.SpamFilterConfig>(_directory, "SpamFilterConfig.json"));
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "SpamFilterConfig.json")));
        }

        [TestMethod]
        public void Load_InvalidPluginFileFailsInsteadOfUsingLegacyFile()
        {
            string pluginFile = Path.Combine(_directory, "SpamFilterConfig.json");
            File.WriteAllText(pluginFile, "{");
            File.WriteAllText(Path.Combine(_directory, "config.json"), """{"Language":"hu"}""");

            Assert.Throws<JsonException>(() => ConfigLoader.Load<global::SpamFilter.SpamFilterConfig>(_directory, "SpamFilterConfig.json"));
            Assert.AreEqual("{", File.ReadAllText(pluginFile));
        }

        [TestMethod]
        public void Load_PrefersPreviousPluginFilenameOverSharedConfig()
        {
            File.WriteAllText(Path.Combine(_directory, "SpamFilterconfig.json"), """{"Language":"de"}""");
            File.WriteAllText(Path.Combine(_directory, "config.json"), """{"Language":"hu"}""");

            var config = ConfigLoader.Load<global::SpamFilter.SpamFilterConfig>(_directory, "SpamFilterConfig.json");

            Assert.AreEqual("de", config.Language);
        }

        [TestMethod]
        public void Load_RockTheVoteDoesNotMigrateUnrelatedSharedConfig()
        {
            File.WriteAllText(Path.Combine(_directory, "config.json"), """{"Language":"hu"}""");

            var config = ConfigLoader.Load<global::RockTheVote.RtvConfig>(_directory, "rtvConfig.json", migrateSharedConfig: false);

            Assert.AreEqual("en", config.Language);
        }

        [TestMethod]
        public void Load_RockTheVotePreservesPreviousConfig()
        {
            File.WriteAllText(Path.Combine(_directory, "rtvconfig.json"), """{"Language":"de","MapsToShow":5}""");

            var config = ConfigLoader.Load<global::RockTheVote.RtvConfig>(_directory, "rtvConfig.json", migrateSharedConfig: false);

            Assert.AreEqual("de", config.Language);
            Assert.AreEqual(5, config.MapsToShow);
        }
    }
}
