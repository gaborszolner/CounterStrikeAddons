using System.Globalization;
using System.Resources;
using SharedLibrary;

namespace _unitTests
{
    [TestClass]
    [DoNotParallelize]
    public class LocalizationTests
    {
        private static readonly string[] Languages = ["de", "es", "fr", "hi", "hu", "pt", "ru", "tr", "uk", "zh"];

        public static IEnumerable<object[]> Messages()
        {
            var adminMenu = typeof(global::AdminMenu.AdminMenu).Assembly;
            var spamFilter = typeof(global::SpamFilter.SpamFilter).Assembly;
            var rockTheVote = typeof(global::RockTheVote.RockTheVote).Assembly;

            yield return ["AdminMenu.Resources.Messages", adminMenu, "PlayerNoSteam", new object[] { "PlayerX" }];
            yield return ["SpamFilter.Resources.Messages", spamFilter, "NameChangeWarning", new object[] { "PlayerX", "STEAM_0:1:23", 4, 30 }];
            yield return ["RockTheVote.Resources.Messages", rockTheVote, "RtvSuccessNextMapSet", new object[] { "de_dust2" }];
        }

        [TestMethod]
        [DynamicData(nameof(Messages))]
        public void Message_IsTranslatedToEveryLanguageWithAllArguments(string baseName, System.Reflection.Assembly assembly, string key, object[] args)
        {
            var resources = new ResourceManager(baseName, assembly);
            string english = string.Format(resources.GetString(key, CultureInfo.InvariantCulture)!, args);

            foreach (var language in Languages)
            {
                string? template = resources.GetString(key, CultureInfo.GetCultureInfo(language));
                Assert.IsNotNull(template, $"{key} missing ({language})");

                string message = string.Format(template, args);
                Assert.AreNotEqual(english, message, $"{key} is not translated ({language})");
                foreach (var arg in args)
                    Assert.Contains(arg.ToString()!, message, $"{key} ({language})");
            }
        }

        [TestMethod]
        public void SharedMessages_UseTheSelectedAssemblyAndFormatArguments()
        {
            string originalCulture = Localizer.CurrentCulture.Name;
            try
            {
                Localizer.Initialize(null);

                Assert.AreEqual("You are not admin.", Msg<global::AdminMenu.AdminMenu>.Get("NotAdminError"));
                Assert.AreEqual("Player Alice (123) has changed their name 4 times within 30s.",
                    Msg<global::SpamFilter.SpamFilter>.Get("NameChangeWarning", "Alice", "123", 4, 30));
                Assert.AreEqual("Teams are equal", Msg<Config>.Get("TeamsEqual"));
                Assert.AreEqual("NotAdminError", Msg<global::SpamFilter.SpamFilter>.Get("NotAdminError"));
            }
            finally
            {
                Localizer.Initialize(originalCulture);
            }
        }

        [TestMethod]
        public void SharedMessages_UseCurrentLanguageAndFallBackForMissingKeys()
        {
            string originalCulture = Localizer.CurrentCulture.Name;
            try
            {
                Localizer.Initialize("hu");
                Assert.AreEqual("Nem vagy admin.", Msg<global::AdminMenu.AdminMenu>.Get("NotAdminError"));
                Assert.AreEqual("MissingKey", Msg<global::AdminMenu.AdminMenu>.Get("MissingKey"));

                Localizer.Initialize(null);
                Assert.AreEqual("You are not admin.", Msg<global::AdminMenu.AdminMenu>.Get("NotAdminError"));
            }
            finally
            {
                Localizer.Initialize(originalCulture);
            }
        }
    }
}
