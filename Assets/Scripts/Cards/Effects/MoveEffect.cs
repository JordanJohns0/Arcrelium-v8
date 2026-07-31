using System.Collections;
using System.Collections.Generic;
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
            if (distance > context.EffectiveRange) return false;

            if (context.Grid != null)
            {
                List<HexCell> path = context.Grid.FindPath(
                    context.Source.CurrentCell.Coordinates,
                    context.TargetCell.Coordinates,
                    context.EffectiveRange);
                return path != null;
            }

            return true;
        }

        public override void Execute(CardContext context)
        {
            HexCell target = context.TargetCell;
            if (target == null) return;

            List<HexCell> path = null;
            if (context.Grid != null)
            {
                path = context.Grid.FindPath(
                    context.Source.CurrentCell.Coordinates,
                    target.Coordinates,
                    context.EffectiveRange);
            }

            // Authoritative position updates immediately so a state broadcast right
            // after PlayCard already reflects the final destination. The coroutine
            // below is purely cosmetic.
            context.Source.SetCellOnly(target);

            if (path != null && path.Count > 0)
                context.Source.StartCoroutine(AnimatePath(context.Source, path));
            else
                context.Source.StartCoroutine(AnimateDirectMove(context.Source, target));
        }

        private IEnumerator AnimatePath(Character source, List<HexCell> path)
        {
            foreach (HexCell cell in path)
            {
                Vector3 destination = cell.transform.position;
                Transform t = source.transform;

                while (Vector3.Distance(t.position, destination) > arrivalThreshold)
                {
                    t.position = Vector3.MoveTowards(t.position, destination, moveSpeed * Time.deltaTime);
                    yield return null;
                }

                t.position = destination;
            }
        }

        private IEnumerator AnimateDirectMove(Character source, HexCell target)
        {
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
