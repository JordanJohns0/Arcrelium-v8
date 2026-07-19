using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FantasyBrawl.HexGrid
{
    /// <summary>
    /// Makes a hex tile clickable so it can be picked as a card's target (e.g. a movement
    /// destination). Requires a collider on the same object (the HexMeshGenerator's mesh
    /// works fine with a MeshCollider, or use a simple CapsuleCollider/BoxCollider sized to
    /// the tile if you'd rather avoid MeshCollider overhead).
    ///
    /// Uses a static event rather than a per-cell reference so UI code doesn't need to wire
    /// up every tile individually — subscribe once from your turn-flow controller.
    /// </summary>
    [RequireComponent(typeof(HexCell))]
    public class HexCellSelectable : MonoBehaviour
    {
        public static event System.Action<HexCell> AnyCellClicked;

        private HexCell cell;
        private static bool processedThisFrame;

        private void Awake()
        {
            cell = GetComponent<HexCell>();
        }

        private void Update()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (processedThisFrame) return;
            processedThisFrame = true;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                HexCell hitCell = hit.collider.GetComponent<HexCell>();
                if (hitCell != null) AnyCellClicked?.Invoke(hitCell);
            }
        }

        private void LateUpdate()
        {
            processedThisFrame = false;
        }
    }
}
