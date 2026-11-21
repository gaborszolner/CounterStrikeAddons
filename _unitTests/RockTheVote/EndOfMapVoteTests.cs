using RockTheVotePlugin = global::RockTheVote.RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class EndOfMapVoteTests
    {
        [TestMethod]
        [DataRow(23, 1, true)]
        [DataRow(22, 1, false)]
        [DataRow(22, 2, true)]
        [DataRow(21, 2, false)]
        [DataRow(21, 3, true)]
        public void ShouldStartEndOfMapVote_RoundsRemaining(int roundsPlayed, int roundsBeforeEnd, bool expected)
        {
            bool result = RockTheVotePlugin.ShouldStartEndOfMapVote(24, roundsPlayed, canClinch: false, highestScore: 0, roundsBeforeEnd);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(12, 1, true)]
        [DataRow(11, 1, false)]
        [DataRow(11, 2, true)]
        [DataRow(10, 2, false)]
        public void ShouldStartEndOfMapVote_TeamCanClinch(int highestScore, int roundsBeforeEnd, bool expected)
        {
            bool result = RockTheVotePlugin.ShouldStartEndOfMapVote(24, 15, canClinch: true, highestScore, roundsBeforeEnd);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ShouldStartEndOfMapVote_ClinchDisabled_IgnoresScore()
        {
            bool result = RockTheVotePlugin.ShouldStartEndOfMapVote(24, 15, canClinch: false, highestScore: 12, 1);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldStartEndOfMapVote_NoMaxRounds_NeverStarts()
        {
            bool result = RockTheVotePlugin.ShouldStartEndOfMapVote(0, 100, canClinch: true, highestScore: 50, 1);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldStartEndOfMapVote_RoundsBeforeEndBelowOne_TreatedAsOne()
        {
            Assert.IsTrue(RockTheVotePlugin.ShouldStartEndOfMapVote(24, 23, canClinch: false, highestScore: 0, 0));
            Assert.IsFalse(RockTheVotePlugin.ShouldStartEndOfMapVote(24, 22, canClinch: false, highestScore: 0, 0));
        }
    }
}
