using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Characters
{
    /// <summary>
    /// Makes a character clickable so it can be picked as a card's target (e.g. an attack's
    /// victim). Requires a collider on the character (or a child object) — a CapsuleCollider
    /// sized to the character model is usually the simplest option.
    /// Uses a static event for the same reason as HexCellSelectable — one subscription point
    /// instead of wiring every character instance individually.
    /// Also fires hover events for outline/health-HUD highlighting.
    ///
    /// Uses manual raycasting via the new Input System — legacy OnMouseEnter/OnMouseExit
    /// callbacks do not fire when Active Input Handling is set to Input System only.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class CharacterSelectable : MonoBehaviour
    {
        public static event System.Action<Character> AnyCharacterClicked;
        public static event System.Action<Character> AnyCharacterHovered;
        public static event System.Action AnyCharacterHoverEnded;

        private Character character;
        private static bool processedThisFrame;

        // Shared per-frame raycast so N characters cost one raycast per frame, not N.
        private static int lastRaycastFrame = -1;
        private static Character lastHitCharacter;
        private static Character currentlyHovered;

        private void Awake()
        {
            character = GetComponent<Character>();
        }

        private void Update()
        {
            if (Mouse.current == null) return;
            if (character == null || !character.gameObject.activeInHierarchy) return;

            UpdateSharedRaycast();

            // Hover tracking — exactly one character hovered globally at a time.
            if (lastHitCharacter == character && currentlyHovered != character)
            {
                currentlyHovered = character;
                AnyCharacterHovered?.Invoke(character);
            }
            else if (lastHitCharacter != character && currentlyHovered == character)
            {
                currentlyHovered = null;
                AnyCharacterHoverEnded?.Invoke();
            }

            // Click handling.
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (processedThisFrame) return;
            if (lastHitCharacter != character) return;

            processedThisFrame = true;
            AnyCharacterClicked?.Invoke(character);
        }

        private void LateUpdate()
        {
            processedThisFrame = false;
        }

        private void OnDestroy()
        {
            if (currentlyHovered == character)
            {
                currentlyHovered = null;
                AnyCharacterHoverEnded?.Invoke();
            }
        }

        private static void UpdateSharedRaycast()
        {
            if (Time.frameCount == lastRaycastFrame) return;
            lastRaycastFrame = Time.frameCount;
            lastHitCharacter = null;

            Camera cam = Camera.main;
            if (cam == null) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
                lastHitCharacter = hit.collider.GetComponentInParent<Character>();
        }
    }
}
