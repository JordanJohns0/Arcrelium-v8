using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game;

namespace Tests.Game
{
    [TestFixture]
    public class StageVoteTests
    {
        private static List<StageOption> TwoOptions()
        {
            return new List<StageOption>
            {
                new StageOption(0, 0),
                new StageOption(1, 1)
            };
        }

        [Test]
        public void CastVote_OncePerPlayer_CannotBeChanged()
        {
            var vote = new StageVote(TwoOptions(), expectedVoters: 2);

            Assert.IsTrue(vote.CastVote(0, 1));
            Assert.IsFalse(vote.CastVote(0, 0), "A player who has voted cannot change their vote.");
            Assert.AreEqual(1, vote.Votes[0]);
        }

        [Test]
        public void CastVote_InvalidOptionOrPlayer_IsRefused()
        {
            var vote = new StageVote(TwoOptions(), expectedVoters: 2);

            Assert.IsFalse(vote.CastVote(0, 2));
            Assert.IsFalse(vote.CastVote(0, -1));
            Assert.IsFalse(vote.CastVote(-1, 0));
            Assert.IsFalse(vote.HasVoted(0));
        }

        [Test]
        public void TryResolve_WaitsForEveryone()
        {
            var vote = new StageVote(TwoOptions(), expectedVoters: 2);
            vote.CastVote(0, 0);

            Assert.IsFalse(vote.TryResolve(new System.Random(1)));
            Assert.IsFalse(vote.Resolved);
        }

        [Test]
        public void TryResolve_Unanimous_ChoosesThatOption()
        {
            var vote = new StageVote(TwoOptions(), expectedVoters: 3);
            vote.CastVote(0, 1);
            vote.CastVote(1, 1);
            vote.CastVote(2, 1);

            Assert.IsTrue(vote.TryResolve(new System.Random(1)));
            Assert.IsTrue(vote.Unanimous);
            Assert.AreEqual(1, vote.Chosen);
        }

        [Test]
        public void TryResolve_NotUnanimous_ChoosesOneOfTheOptionsRandomly()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                var vote = new StageVote(TwoOptions(), expectedVoters: 2);
                vote.CastVote(0, 0);
                vote.CastVote(1, 1);

                Assert.IsTrue(vote.TryResolve(new System.Random(seed)));
                Assert.IsFalse(vote.Unanimous, "Split votes are not unanimous.");
                seen.Add(vote.Chosen);
            }

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, seen, "A split vote must be able to land on either option.");
        }

        [Test]
        public void CastVote_AfterResolution_IsRefused()
        {
            var vote = new StageVote(TwoOptions(), expectedVoters: 1);
            vote.CastVote(0, 0);
            vote.TryResolve(new System.Random(1));

            Assert.IsFalse(vote.CastVote(0, 1));
        }

        [Test]
        public void GenerateOptions_TwoGroupsAndRewards_AreAllDistinct()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var options = StageVote.GenerateOptions(new[] { 0, 1, 2 }, new[] { 0, 1 }, new System.Random(seed));

                Assert.AreNotEqual(options[0].EnemyGroupIndex, options[1].EnemyGroupIndex, "The two enemy groups differ.");
                Assert.AreNotEqual(options[0].RewardIndex, options[1].RewardIndex, "The two rewards differ.");
            }
        }

        [Test]
        public void GenerateOptions_NoEnemyGroups_UsesDefaultEnemies()
        {
            var options = StageVote.GenerateOptions(new int[0], new[] { 0, 1 }, new System.Random(1));

            Assert.AreEqual(-1, options[0].EnemyGroupIndex);
            Assert.AreEqual(-1, options[1].EnemyGroupIndex);
        }

        [Test]
        public void Restore_RebuildsAResolvedVoteFromItsSnapshot()
        {
            var restored = StageVote.Restore(TwoOptions(), 2, new[] { 0, 1 }, new[] { 1, 1 }, resolved: true, chosen: 1);

            Assert.IsTrue(restored.Resolved);
            Assert.AreEqual(1, restored.Chosen);
            Assert.IsTrue(restored.Unanimous);
            Assert.IsTrue(restored.HasVoted(0));
            Assert.IsTrue(restored.HasVoted(1));
        }
    }
}
