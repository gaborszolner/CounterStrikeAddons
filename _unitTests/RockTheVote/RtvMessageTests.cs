using System.Globalization;
using System.Resources;
using RockTheVotePlugin = global::RockTheVote.RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class RtvMessageTests
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
        public void AlreadyTriggered_IncludesCurrentAndRequiredVotes(string language)
        {
            var resources = new ResourceManager("RockTheVote.Resources.Messages", typeof(RockTheVotePlugin).Assembly);
            string template = resources.GetString("RtvAlreadyTriggered", CultureInfo.GetCultureInfo(language))!;

            string message = string.Format(template, 1, 8);

            Assert.IsTrue(message.Contains("1"), language);
            Assert.IsTrue(message.Contains("8"), language);
        }

        [TestMethod]
        public void AlreadyTriggered_EnglishUsesRequestedFormat()
        {
            var resources = new ResourceManager("RockTheVote.Resources.Messages", typeof(RockTheVotePlugin).Assembly);
            string template = resources.GetString("RtvAlreadyTriggered", CultureInfo.InvariantCulture)!;

            Assert.EndsWith("(1 voted, 8 needed)", string.Format(template, 1, 8));
        }
    }
}
