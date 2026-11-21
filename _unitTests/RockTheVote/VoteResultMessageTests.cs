using System.Globalization;
using System.Resources;
using RockTheVotePlugin = global::RockTheVote.RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class VoteResultMessageTests
    {
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
        public void VoteResultHeaders_AreTranslated(string language)
        {
            var resources = new ResourceManager("RockTheVote.Resources.Messages", typeof(RockTheVotePlugin).Assembly);
            var resourceSet = resources.GetResourceSet(CultureInfo.GetCultureInfo(language), createIfNotExists: true, tryParents: false);

            Assert.IsNotNull(resourceSet, language);
            Assert.IsFalse(string.IsNullOrWhiteSpace(resourceSet.GetString("RtvVoteResults")), language);
            Assert.IsFalse(string.IsNullOrWhiteSpace(resourceSet.GetString("EndOfMapVoteResults")), language);
        }
    }
}
