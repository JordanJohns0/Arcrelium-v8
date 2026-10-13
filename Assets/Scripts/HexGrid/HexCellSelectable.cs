using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HexGrid
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
        /// <summary>Fires when the mouse pointer moves over a different hex tile.</summary>
        public static event System.Action<HexCell> AnyCellHovered;
        /// <summary>Fires when the mouse pointer leaves a hex tile / the board.</summary>
        public static event System.Action AnyCellHoverEnded;

        private HexCell cell;
        private static bool processedThisFrame;
        private static bool hoverProcessedThisFrame;
        private static HexCell lastHoveredCell;

        private void Awake()
        {
            cell = GetComponent<HexCell>();
        }

        private void OnDestroy()
        {
            if (lastHoveredCell != null)
            {
                lastHoveredCell = null;
                AnyCellHoverEnded?.Invoke();
            }
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            if (!hoverProcessedThisFrame)
            {
                hoverProcessedThisFrame = true;
                UpdateHover();
            }

            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (processedThisFrame) return;
            processedThisFrame = true;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            if (Camera.main == null) { Debug.LogWarning("[HexCellSelectable] No Camera.main!"); return; }
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                HexCell hitCell = hit.collider.GetComponentInParent<HexCell>();
                if (hitCell != null)
                {
                    Debug.Log($"[HexCellSelectable] Hit cell at {hitCell.Coordinates}");
                    AnyCellClicked?.Invoke(hitCell);
                }
            }
        }

        /// <summary>
        /// Tracks which cell the mouse is currently over and raises hover events when it
        /// changes (or when the pointer leaves the board). Runs once per frame via a static
        /// gate so only the first tile's Update performs the raycast.
        /// </summary>
        private void UpdateHover()
        {
            HexCell hovered = null;

            if (Mouse.current != null
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                && Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    hovered = hit.collider.GetComponentInParent<HexCell>();
                }
            }

            if (hovered == lastHoveredCell) return;

            if (lastHoveredCell != null)
            {
                AnyCellHoverEnded?.Invoke();
            }

            lastHoveredCell = hovered;

            if (hovered != null)
            {
                AnyCellHovered?.Invoke(hovered);
            }
        }

        private void LateUpdate()
        {
            processedThisFrame = false;
            hoverProcessedThisFrame = false;
        }
    }
}
