using System.Collections;
using UnityEngine;
using Characters;
using HexGrid;

namespace Cards.Effects
{
    [CreateAssetMenu(fileName = "MoveEffect", menuName = "/Effects/Move")]
    public class MoveEffect : CardEffectSO
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float arrivalThreshold = 0.05f;

        public override bool CanExecute(CardContext context)
        {
            if (context.TargetCell == null || !context.TargetCell.IsWalkable) return false;

            int distance = context.Source.CurrentCell.Coordinates.DistanceTo(context.TargetCell.Coordinates);
            return distance <= context.Range;
        }

        public override void Execute(CardContext context)
        {
            HexCell target = context.TargetCell;
            context.Source.StartCoroutine(AnimateMovement(context.Source, target));
        }

        private IEnumerator AnimateMovement(Character source, HexCell target)
        {
            source.SetCellOnly(target);

            Vector3 destination = target.transform.position;
            Transform t = source.transform;

            while (Vector3.Distance(t.position, destination) > arrivalThreshold)
            {
                t.position = Vector3.MoveTowards(t.position, destination, moveSpeed * Time.deltaTime);
                yield return null;
            }

            t.position = destination;
        }
    }
}
