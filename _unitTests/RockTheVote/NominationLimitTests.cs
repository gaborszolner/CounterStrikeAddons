using RockTheVotePlugin = global::RockTheVote.RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class NominationLimitTests
    {
        private static Dictionary<ulong, string> Nominations(params (ulong id, string map)[] items)
            => items.ToDictionary(i => i.id, i => i.map);

        [TestMethod]
        public void CanAddNomination_BelowLimit_Allowed()
        {
            var nominations = Nominations((1, "de_dust2"));

            Assert.IsTrue(RockTheVotePlugin.CanAddNomination(nominations, 2, "de_mirage", 2));
        }

        [TestMethod]
        public void CanAddNomination_LimitReached_NewMapRejected()
        {
            var nominations = Nominations((1, "de_dust2"), (2, "de_mirage"));

            Assert.IsFalse(RockTheVotePlugin.CanAddNomination(nominations, 3, "de_inferno", 2));
        }

        [TestMethod]
        public void CanAddNomination_LimitReached_AlreadyNominatedMapAllowed()
        {
            var nominations = Nominations((1, "de_dust2"), (2, "de_mirage"));

            Assert.IsTrue(RockTheVotePlugin.CanAddNomination(nominations, 3, "DE_MIRAGE", 2));
        }

        [TestMethod]
        public void CanAddNomination_LimitReached_PlayerCanReplaceOwnNomination()
        {
            var nominations = Nominations((1, "de_dust2"), (2, "de_mirage"));

            Assert.IsTrue(RockTheVotePlugin.CanAddNomination(nominations, 2, "de_inferno", 2));
        }

        [TestMethod]
        public void CanAddNomination_SameMapByMultiplePlayers_CountsOnce()
        {
            var nominations = Nominations((1, "de_dust2"), (2, "de_dust2"));

            Assert.IsTrue(RockTheVotePlugin.CanAddNomination(nominations, 3, "de_mirage", 2));
        }
    }
}
