using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class CameraController : MonoBehaviour
{
    [SerializeField] private InputActionReference cameraLeftRight;
    [SerializeField] private InputActionReference cameraUpDown;
    [SerializeField] private InputActionReference cameraInOut;

    [SerializeField] private float orbitSpeed = 60f;
    [SerializeField] private float tiltSpeed = 40f;
    [SerializeField] private float zoomSpeed = 8f;

    private CinemachineOrbitalFollow orbitalFollow;

    private void Awake()
    {
        orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
    }

    private void OnEnable()
    {
        cameraLeftRight?.action?.Enable();
        cameraUpDown?.action?.Enable();
        cameraInOut?.action?.Enable();
    }

    private void OnDisable()
    {
        cameraLeftRight?.action?.Disable();
        cameraUpDown?.action?.Disable();
        cameraInOut?.action?.Disable();
    }

    private void Update()
    {
        if (orbitalFollow == null) return;

        float horizontal = cameraLeftRight?.action?.ReadValue<float>() ?? 0;
        float vertical = cameraUpDown?.action?.ReadValue<float>() ?? 0;
        float zoom = cameraInOut?.action?.ReadValue<float>() ?? 0;

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
