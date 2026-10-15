using System;
using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// One choice on the stage vote: the enemy group to fight (an index into the GameManager's
    /// enemy group list, or -1 for the default enemies) and the reward for winning it (an index
    /// into the reward list).
    /// </summary>
    public struct StageOption
    {
        public int EnemyGroupIndex;
        public int RewardIndex;

        public StageOption(int enemyGroupIndex, int rewardIndex)
        {
            EnemyGroupIndex = enemyGroupIndex;
            RewardIndex = rewardIndex;
        }
    }

    /// <summary>
    /// The vote before a stage. Every player picks one of two options once and cannot change it.
    /// When everyone has voted, a unanimous vote is taken as is; otherwise the outcome is random
    /// among the options. Plain logic with no Unity objects, so the rules can be tested directly.
    /// </summary>
    public sealed class StageVote
    {
        public const int OptionCount = 2;

        private readonly StageOption[] options;
        private readonly Dictionary<int, int> votes = new Dictionary<int, int>();

        public IReadOnlyList<StageOption> Options => options;
        public int ExpectedVoters { get; }
        public IReadOnlyDictionary<int, int> Votes => votes;
        public bool Resolved { get; private set; }
        /// <summary>The chosen option index once resolved; -1 before that.</summary>
        public int Chosen { get; private set; } = -1;
        /// <summary>True when every vote was for the same option (so the outcome was not random).</summary>
        public bool Unanimous { get; private set; }

        public StageVote(IReadOnlyList<StageOption> options, int expectedVoters)
        {
            if (options == null || options.Count != OptionCount)
                throw new ArgumentException($"A stage vote needs exactly {OptionCount} options.", nameof(options));

            this.options = new StageOption[OptionCount];
            for (int i = 0; i < OptionCount; i++) this.options[i] = options[i];
            ExpectedVoters = Math.Max(1, expectedVoters);
        }

        public bool HasVoted(int playerId) => votes.ContainsKey(playerId);

        /// <summary>
        /// Records a player's vote. Returns false (changing nothing) when the vote is already
        /// resolved, the player already voted, or the option or player is invalid.
        /// </summary>
        public bool CastVote(int playerId, int option)
        {
            if (Resolved || playerId < 0) return false;
            if (option < 0 || option >= OptionCount) return false;
            if (votes.ContainsKey(playerId)) return false;

            votes[playerId] = option;
            return true;
        }

        /// <summary>
        /// Resolves the vote once every expected player has voted. A unanimous vote picks that
        /// option; otherwise one is chosen at random. Returns true when this call resolved it.
        /// </summary>
        public bool TryResolve(System.Random rng)
        {
            if (Resolved || votes.Count < ExpectedVoters) return false;

            Unanimous = AllSame(votes.Values);
            if (Unanimous)
            {
                foreach (int option in votes.Values)
                {
                    Chosen = option;
                    break;
                }
            }
            else
            {
                Chosen = (rng ?? new System.Random()).Next(OptionCount);
            }

            Resolved = true;
            return true;
        }

        /// <summary>
        /// Builds the two options from the valid enemy group indices and reward indices. Each
        /// dimension uses two different entries when there are at least two to choose from. With
        /// no enemy groups configured both options use -1 (default enemies).
        /// </summary>
        public static List<StageOption> GenerateOptions(IReadOnlyList<int> enemyGroupIndices,
            IReadOnlyList<int> rewardIndices, System.Random rng)
        {
            int[] groups = PickDistinct(enemyGroupIndices, rng, fallback: -1);
            int[] rewards = PickDistinct(rewardIndices, rng, fallback: 0);

            var result = new List<StageOption>(OptionCount);
            for (int i = 0; i < OptionCount; i++)
                result.Add(new StageOption(groups[i], rewards[i]));
            return result;
        }

        /// <summary>Rebuilds a vote from its network snapshot (see Networking.StageVoteState).</summary>
        public static StageVote Restore(IReadOnlyList<StageOption> options, int expectedVoters,
            IReadOnlyList<int> votePlayerIds, IReadOnlyList<int> voteOptions, bool resolved, int chosen)
        {
            var vote = new StageVote(options, expectedVoters);

            int count = Math.Min(votePlayerIds?.Count ?? 0, voteOptions?.Count ?? 0);
            for (int i = 0; i < count; i++)
                vote.CastVote(votePlayerIds[i], voteOptions[i]);

            if (resolved)
            {
                vote.Resolved = true;
                vote.Chosen = chosen;
                vote.Unanimous = AllSame(vote.votes.Values);
            }
            return vote;
        }

        private static bool AllSame(IEnumerable<int> values)
        {
            bool first = true;
            int reference = 0;
            foreach (int value in values)
            {
                if (first)
                {
                    reference = value;
                    first = false;
                }
                else if (value != reference)
                {
                    return false;
                }
            }
            return !first;
        }

        private static int[] PickDistinct(IReadOnlyList<int> candidates, System.Random rng, int fallback)
        {
            if (candidates == null || candidates.Count == 0)
                return new[] { fallback, fallback };

            if (candidates.Count == 1)
                return new[] { candidates[0], candidates[0] };

            var pool = new List<int>(candidates);
            var random = rng ?? new System.Random();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return new[] { pool[0], pool[1] };
        }
    }
}
