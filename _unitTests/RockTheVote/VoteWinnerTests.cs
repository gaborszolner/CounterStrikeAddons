using RockTheVotePlugin = global::RockTheVote.RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class VoteWinnerTests
    {
        [TestMethod]
        public void GetWinningMaps_MostVotesWins()
        {
            var winners = RockTheVotePlugin.GetWinningMaps([("de_dust2", 1), ("de_mirage", 2), ("de_mirage", 3)]);

            CollectionAssert.AreEqual(new[] { "de_mirage" }, winners);
        }

        [TestMethod]
        public void GetWinningMaps_Tie_MapReachingCountFirstWins()
        {
            var winners = RockTheVotePlugin.GetWinningMaps([("de_dust2", 1), ("de_mirage", 2), ("de_mirage", 3), ("de_dust2", 4)]);

            CollectionAssert.AreEqual(new[] { "de_mirage" }, winners);
        }

        [TestMethod]
        public void GetWinningMaps_TieWithSameOrder_ReturnsAllTied()
        {
            var winners = RockTheVotePlugin.GetWinningMaps([("de_dust2", 0), ("de_mirage", 0)]);

            CollectionAssert.AreEquivalent(new[] { "de_dust2", "de_mirage" }, winners);
        }

        [TestMethod]
        public void GetWinningMaps_NoVotes_ReturnsEmpty()
        {
            var winners = RockTheVotePlugin.GetWinningMaps([]);

            Assert.AreEqual(0, winners.Count);
        }
    }
}
