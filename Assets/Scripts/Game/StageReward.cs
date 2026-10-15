using UnityEngine;

namespace Game
{
    /// <summary>
    /// A reward a stage vote can offer: what the player gets after winning the fight. Today a
    /// reward opens one of the end-of-level panes (<see cref="SpecialArea"/>); create more
    /// assets to offer more rewards once they exist.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStageReward", menuName = "Arcrelium/Stage Reward")]
    public class StageReward : ScriptableObject
    {
        public string RewardName = "New Reward";
        [TextArea] public string Description;
        public SpecialArea Area = SpecialArea.Shop;
    }
}
