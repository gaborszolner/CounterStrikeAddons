using RockTheVotePlugin = global::RockTheVote.RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class NominationAutoVoteTests
    {
        [TestMethod]
        public void GetNominationVotes_NominatedMapInOptions_VotesForOptionName()
        {
            var nominations = new Dictionary<ulong, string> { [1] = "DE_DUST2" };

            var votes = RockTheVotePlugin.GetNominationVotes(nominations, ["de_dust2", "de_mirage"]);

            Assert.AreEqual(1, votes.Count);
            Assert.AreEqual("de_dust2", votes[1]);
        }

        [TestMethod]
        public void GetNominationVotes_NominatedMapNotInOptions_NoVote()
        {
            var nominations = new Dictionary<ulong, string> { [1] = "de_inferno", [2] = "de_mirage" };

            var votes = RockTheVotePlugin.GetNominationVotes(nominations, ["de_dust2", "de_mirage"]);

            Assert.IsFalse(votes.ContainsKey(1));
            Assert.AreEqual("de_mirage", votes[2]);
        }

        [TestMethod]
        public void HaveAllPlayersVoted_SinglePlayerAutoVoted_True()
        {
            Assert.IsTrue(RockTheVotePlugin.HaveAllPlayersVoted([1], [1]));
        }

        [TestMethod]
        public void HaveAllPlayersVoted_SomePlayerMissing_False()
        {
            Assert.IsFalse(RockTheVotePlugin.HaveAllPlayersVoted([1, 2], [1]));
        }

        [TestMethod]
        public void HaveAllPlayersVoted_NoPlayers_False()
        {
            Assert.IsFalse(RockTheVotePlugin.HaveAllPlayersVoted([], [1]));
        }
    }
}
