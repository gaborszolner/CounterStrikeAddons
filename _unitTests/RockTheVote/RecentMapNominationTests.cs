using System.Globalization;
using System.Resources;
using RockTheVotePlugin = global::RockTheVote.RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class RecentMapNominationTests
    {
        private static readonly List<string> AllMaps = ["de_dust2", "de_mirage", "de_inferno"];

        [TestMethod]
        public void FindRecentlyPlayedMaps_ExactRecentMap_ReturnsIt()
        {
            var result = RockTheVotePlugin.FindRecentlyPlayedMaps(AllMaps, ["de_mirage"], "DE_MIRAGE");

            CollectionAssert.AreEqual(new[] { "de_mirage" }, result);
        }

        [TestMethod]
        public void FindRecentlyPlayedMaps_PartialRecentMap_ReturnsIt()
        {
            var result = RockTheVotePlugin.FindRecentlyPlayedMaps(AllMaps, ["de_mirage"], "mira");

            CollectionAssert.AreEqual(new[] { "de_mirage" }, result);
        }

        [TestMethod]
        public void FindRecentlyPlayedMaps_UnknownMap_ReturnsEmpty()
        {
            var result = RockTheVotePlugin.FindRecentlyPlayedMaps(AllMaps, ["de_mirage"], "nuke");

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void FindRecentlyPlayedMaps_RecentMapMissingFromMapList_ReturnsEmpty()
        {
            var result = RockTheVotePlugin.FindRecentlyPlayedMaps(AllMaps, ["de_nuke"], "de_nuke");

            Assert.IsEmpty(result);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("de")]
        [DataRow("es")]
        [DataRow("fr")]
        [DataRow("hi")]
        [DataRow("hu")]
        [DataRow("pt")]
        [DataRow("ru")]
        [DataRow("tr")]
        [DataRow("uk")]
        [DataRow("zh")]
        public void MapRecentlyPlayed_IsLocalizedAndIncludesMap(string language)
        {
            var resources = new ResourceManager("RockTheVote.Resources.Messages", typeof(RockTheVotePlugin).Assembly);
            string? template = resources.GetString("MapRecentlyPlayed", CultureInfo.GetCultureInfo(language));

            Assert.IsNotNull(template, language);
            Assert.Contains("de_mirage", string.Format(template, "de_mirage"), language);
        }
    }
}
