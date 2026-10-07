using Cards;
using Cards.Effects;
using Characters;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Pure, unit-testable heuristics for the AI's "play with intent" logic. Decides how
    /// valuable a candidate card play is: finishing kills, focusing damaged enemies,
    /// keeping the source alive, moving toward a fight, and never wasting a draw at a
    /// full hand. Kept free of scene access so it can be tested without a board.
    /// </summary>
    public static class AICardScoring
    {
        public const float LethalBonus = 10f;
        public const float DamagePerHp = 1f;
        public const float SelfDamagePerHp = 1f;
        public const float WeakTargetBonus = 0.5f;
        public const float HealPerPoint = 1.5f;
        public const float NearDeathHealBonus = 5f;
        public const float BlockThreatened = 1.0f;
        public const float BlockIdle = 0.3f;
        public const float DrawPerCard = 0.8f;
        public const float NoHandSpacePenalty = -3f;
        public const float CostPenalty = 0.5f;
        public const float MovePerHex = 3f;
        public const float PassThreshold = 0f;
        public const int ThreatRange = 2;
        public const int NearDeathThreshold = 2;

        /// <summary>
        /// Sums the raw resources a card's effect produces for the source character:
        /// damage dealt to a target, self-heal, self-block, and cards drawn.
        /// </summary>
        public static void Accumulate(CardEffectSO effect, Character source,
            ref int damage, ref int heal, ref int block, ref int draw, ref int selfDamage)
        {
            if (effect == null) return;

            switch (effect)
            {
                case AttackEffect attack:
                    damage += attack.Damage.Evaluate(source);
                    // Follow-up effects on the enemy hit also count (they resolve on a hit).
                    if (attack.OnHitEffects != null)
                    {
                        foreach (CardEffectSO onHit in attack.OnHitEffects)
                        {
                            if (onHit != null)
                                Accumulate(onHit, source, ref damage, ref heal, ref block, ref draw, ref selfDamage);
                        }
                    }
                    break;
                case AreaDamageEffect area:
                    damage += area.Damage.Evaluate(source);
                    break;
                case HealEffect healEffect:
                    heal += healEffect.Amount.Evaluate(source);
                    break;
                case GainBlockEffect blockEffect:
                    block += blockEffect.Amount.Evaluate(source);
                    break;
                case DrawCardsEffect drawEffect:
                    draw += drawEffect.Amount.Evaluate(source);
                    break;
                case SelfDamageEffect selfDamageEffect:
                    selfDamage += selfDamageEffect.Damage.Evaluate(source);
                    break;
                // Board- or hand-state effects yield no direct damage/heal/block/draw here; their value is
                // positional and scored by AIController against the candidate cell/hand.
                case PlaceTileEffect:
                case TilePatternEffect:
                case ApplyModifiersToHandEffect:
                case PushOccupantsEffect:
                case SpawnRockEffect:
                    break;
            }
        }

        /// <summary>Total damage needed to kill the target accounting for block.</summary>
        public static int EffectiveHpToKill(Character target)
        {
            if (target == null || target.Stats == null) return 0;
            return target.Stats.Block + target.Stats.CurrentHealth;
        }

        /// <summary>
        /// Rates a candidate card play against the given target (may be null for
        /// self/cell plays). distanceToNearestEnemy is -1 when no enemies exist; it is
        /// used to decide whether defensive block is worthwhile.
        /// Uses the card's base cost.
        /// </summary>
        public static float Score(CardData card, Character source, Character target, int distanceToNearestEnemy)
        {
            return Score(card, source, target, distanceToNearestEnemy, card != null ? card.Cost : 0);
        }

        /// <summary>
        /// Same as the base-cost overload but with the candidate instance's live cost, so the
        /// AI accounts for in-hand cost modifications (cost lowered/raised while held).
        /// </summary>
        public static float Score(CardData card, Character source, Character target, int distanceToNearestEnemy, int cost)
        {
            if (card == null || card.Steps == null || card.Steps.Count == 0 || source == null || source.Stats == null)
                return float.MinValue;

            int damage = 0, heal = 0, block = 0, draw = 0, selfDamage = 0;
            foreach (CardEffectStep step in card.Steps)
            {
                if (step != null)
                    Accumulate(step.Effect, source, ref damage, ref heal, ref block, ref draw, ref selfDamage);
            }

            float score = 0f;
            score -= cost * CostPenalty;

            if (target != null && damage > 0)
            {
                int needed = EffectiveHpToKill(target);
                int effective = Mathf.Min(damage, needed);
                score += effective * DamagePerHp;

                if (damage >= needed)
                    score += LethalBonus;
                else if (target.Stats.CurrentHealth <= NearDeathThreshold)
                    score += WeakTargetBonus;
            }

            if (heal > 0 && source.Stats.CurrentHealth < source.Stats.MaxHealth)
            {
                int missing = source.Stats.MaxHealth - source.Stats.CurrentHealth;
                score += Mathf.Min(heal, missing) * HealPerPoint;
                if (source.Stats.CurrentHealth <= NearDeathThreshold)
                    score += NearDeathHealBonus;
            }

            if (block > 0)
            {
                bool threatened = distanceToNearestEnemy >= 0 && distanceToNearestEnemy <= ThreatRange;
                score += block * (threatened ? BlockThreatened : BlockIdle);
            }

            if (draw > 0)
            {
                int capacity = source.Hand != null ? source.Hand.MaxCapacity : 0;
                int handCount = source.Hand != null && source.Hand.Cards != null ? source.Hand.Cards.Count : 0;
                int space = Mathf.Max(0, capacity - handCount);
                int actual = Mathf.Min(draw, space);
                score += actual * DrawPerCard;
                if (actual <= 0) score += NoHandSpacePenalty;
            }

            if (selfDamage > 0)
            {
                int effective = EffectiveHpToKill(source);
                int actual = Mathf.Min(selfDamage, effective);
                score -= actual * SelfDamagePerHp;
                if (selfDamage >= effective)
                    score -= LethalBonus;
                else if (source.Stats.CurrentHealth <= NearDeathThreshold)
                    score -= NearDeathHealBonus;
            }

            return score;
        }

        /// <summary>Movement is worth MovePerHex for every hex the play closes to the fight.</summary>
        public static float ScoreMove(int distanceBefore, int distanceAfter)
        {
            return (distanceBefore - distanceAfter) * MovePerHex;
        }
    }
}
