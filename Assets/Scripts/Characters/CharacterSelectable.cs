using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FantasyBrawl.Characters
{
    /// <summary>
    /// Makes a character clickable so it can be picked as a card's target (e.g. an attack's
    /// victim). Requires a collider on the character (or a child object) — a CapsuleCollider
    /// sized to the character model is usually the simplest option.
    /// Uses a static event for the same reason as HexCellSelectable — one subscription point
    /// instead of wiring every character instance individually.
    /// </summary>
    [RequireComponent(typeof(Character))]
    public class CharacterSelectable : MonoBehaviour
    {
        public static event System.Action<Character> AnyCharacterClicked;

        private Character character;
        private static bool processedThisFrame;

        private void Awake()
        {
            character = GetComponent<Character>();
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
                Character hitChar = hit.collider.GetComponent<Character>();
                if (hitChar != null) AnyCharacterClicked?.Invoke(hitChar);
            }
        }

        private void LateUpdate()
        {
            processedThisFrame = false;
        }
    }
}
