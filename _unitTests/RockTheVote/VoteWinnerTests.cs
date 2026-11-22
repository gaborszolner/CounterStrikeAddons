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

        [TestMethod]
        public void IsVoteDecided_LeadLargerThanRemainingVoters_True()
        {
            // 3 vs 1 with 1 player left: the other map can reach at most 2
            Assert.IsTrue(RockTheVotePlugin.IsVoteDecided([("a", 1), ("a", 2), ("a", 3), ("b", 4)], remainingVoters: 1));
        }

        [TestMethod]
        public void IsVoteDecided_OtherMapCanOnlyTie_TrueBecauseLeaderReachedCountFirst()
        {
            Assert.IsTrue(RockTheVotePlugin.IsVoteDecided([("a", 1), ("a", 2), ("b", 3)], remainingVoters: 1));
        }

        [TestMethod]
        public void IsVoteDecided_OtherMapCanStillOvertake_False()
        {
            Assert.IsFalse(RockTheVotePlugin.IsVoteDecided([("a", 1), ("a", 2), ("b", 3)], remainingVoters: 2));
        }

        [TestMethod]
        public void IsVoteDecided_UnvotedMapCanStillWin_False()
        {
            Assert.IsFalse(RockTheVotePlugin.IsVoteDecided([("a", 1), ("a", 2)], remainingVoters: 3));
        }

        [TestMethod]
        public void IsVoteDecided_TieResolvedRandomly_False()
        {
            Assert.IsFalse(RockTheVotePlugin.IsVoteDecided([("a", 0), ("b", 0)], remainingVoters: 0));
        }

        [TestMethod]
        public void IsVoteDecided_NoVotes_False()
        {
            Assert.IsFalse(RockTheVotePlugin.IsVoteDecided([], remainingVoters: 0));
        }
    }
}
