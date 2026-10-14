using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using Characters;

[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class CameraController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float orbitSpeed = 60f;
    [SerializeField] private float tiltSpeed = 40f;
    [SerializeField] private float zoomSpeed = 8f;

    [Header("Target Group")]
    // The camera frames whatever members are in this group. If left unassigned it is
    // resolved from whatever the CinemachineCamera currently follows.
    [SerializeField] private CinemachineTargetGroup targetGroup;
    [SerializeField] private float targetWeight = 1f;
    [SerializeField] private float targetRadius = 1f;

    private CinemachineOrbitalFollow orbitalFollow;
    private CinemachineCamera cinemachineCamera;
    private InputAction horizontalAction;
    private InputAction verticalAction;
    private InputAction zoomAction;
    private readonly HashSet<Character> trackedCharacters = new HashSet<Character>();
    private bool inputLocked;

    private void Awake()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
        orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

        ResolveTargetGroup();

        if (inputActions == null)
        {
            Debug.LogError("[CameraController] inputActions is null! Assign InputSystem_Actions in the inspector.");
            return;
        }

        var cameraMap = inputActions.FindActionMap("Camera");
        if (cameraMap == null) return;

        horizontalAction = cameraMap.FindAction("Horizontal");
        verticalAction = cameraMap.FindAction("Vertical");
        zoomAction = cameraMap.FindAction("Zoom");
    }

    private void OnEnable()
    {
        horizontalAction?.Enable();
        verticalAction?.Enable();
        zoomAction?.Enable();

        Character.OnCharacterCreated += HandleCharacterCreated;
        Character.OnCharacterDestroyed += HandleCharacterDestroyed;

        // Pick up any characters that were already spawned before this component enabled.
        foreach (Character character in FindObjectsByType<Character>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            HandleCharacterCreated(character);
    }

    private void OnDisable()
    {
        horizontalAction?.Disable();
        verticalAction?.Disable();
        zoomAction?.Disable();

        Character.OnCharacterCreated -= HandleCharacterCreated;
        Character.OnCharacterDestroyed -= HandleCharacterDestroyed;
    }

    /// <summary>
    /// Locks or unlocks player camera control (orbit, tilt, zoom). Used when the match is
    /// decided so the board stays put behind the loadout screen.
    /// </summary>
    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
    }

    private void Update()
    {
        if (orbitalFollow == null || inputLocked) return;

        float horizontal = horizontalAction?.ReadValue<float>() ?? 0;
        float vertical = verticalAction?.ReadValue<float>() ?? 0;
        float zoom = zoomAction?.ReadValue<float>() ?? 0;

        if (horizontal != 0)
            orbitalFollow.HorizontalAxis.Value = ClampValue(
                orbitalFollow.HorizontalAxis.Value + horizontal * orbitSpeed * Time.deltaTime,
                orbitalFollow.HorizontalAxis);

        if (vertical != 0)
            orbitalFollow.VerticalAxis.Value = ClampValue(
                orbitalFollow.VerticalAxis.Value + vertical * tiltSpeed * Time.deltaTime,
                orbitalFollow.VerticalAxis);

        if (zoom != 0)
            orbitalFollow.RadialAxis.Value = ClampValue(
                orbitalFollow.RadialAxis.Value - zoom * zoomSpeed * Time.deltaTime,
                orbitalFollow.RadialAxis);
    }

    /// <summary>
    /// Finds the group to manage, preferring an explicit inspector reference and otherwise
    /// reusing whatever the CinemachineCamera already follows (so a scene wired up in the
    /// inspector needs no extra setup). The camera is then pointed at the group.
    /// </summary>
    private void ResolveTargetGroup()
    {
        if (targetGroup == null && cinemachineCamera != null && cinemachineCamera.Follow != null)
            targetGroup = cinemachineCamera.Follow.GetComponent<CinemachineTargetGroup>();

        if (targetGroup == null)
            targetGroup = FindFirstObjectByType<CinemachineTargetGroup>();

        if (targetGroup == null)
        {
            Debug.LogWarning("[CameraController] No CinemachineTargetGroup found; characters will not be framed by the camera.");
            return;
        }

        if (cinemachineCamera != null)
            cinemachineCamera.Follow = targetGroup.transform;
    }

    private void HandleCharacterCreated(Character character)
    {
        if (character == null || character.IsStructure || targetGroup == null) return;
        if (!trackedCharacters.Add(character)) return;

        targetGroup.AddMember(character.transform, targetWeight, targetRadius);
    }

    private void HandleCharacterDestroyed(Character character)
    {
        if (character == null || !trackedCharacters.Remove(character)) return;

        if (targetGroup != null)
            targetGroup.RemoveMember(character.transform);
    }

    private static float ClampValue(float value, InputAxis axis)
    {
        if (axis.Wrap)
        {
            float rangeSize = axis.Range.y - axis.Range.x;
            if (rangeSize > 0)
                return ((value - axis.Range.x) % rangeSize + rangeSize) % rangeSize + axis.Range.x;
            return value;
        }
        return Mathf.Clamp(value, axis.Range.x, axis.Range.y);
    }
}
