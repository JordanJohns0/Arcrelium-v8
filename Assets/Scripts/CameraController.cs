using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class CameraController : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private float orbitSpeed = 60f;
    [SerializeField] private float tiltSpeed = 40f;
    [SerializeField] private float zoomSpeed = 8f;

    private CinemachineOrbitalFollow orbitalFollow;
    private InputAction horizontalAction;
    private InputAction verticalAction;
    private InputAction zoomAction;

    private void Awake()
    {
        orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

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
    }

    private void OnDisable()
    {
        horizontalAction?.Disable();
        verticalAction?.Disable();
        zoomAction?.Disable();
    }

    private void Update()
    {
        if (orbitalFollow == null) return;

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
