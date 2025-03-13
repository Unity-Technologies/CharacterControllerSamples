using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class FlyCam : MonoBehaviour
{
    public float MaxMoveSpeed = 10f;
    public float MoveSharpness = 10f;
    public float SprintSpeedBoost = 5f;

    public float RotationSpeed = 10f;
    public float RotationSharpness = 999999f;

    private float _pitchAngle = 0f;
    private Vector3 _planarForward = Vector3.forward;
    private Vector3 _currentMoveVelocity = default;
    private Vector2 _previousMousePos = default;

    void Start()
    {
        _planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        _pitchAngle = Vector3.SignedAngle(_planarForward, transform.forward, transform.right);
    }

    void Update()
    {
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            _previousMousePos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.rightButton.isPressed)
        {
            // Rotation Input
            Vector3 mouseDelta = (Mouse.current.position.ReadValue() - _previousMousePos);
            _previousMousePos = Mouse.current.position.ReadValue();

            // Yaw
            float yawAngleChange = mouseDelta.x * RotationSpeed * Time.deltaTime;
            Quaternion yawRotation = Quaternion.Euler(Vector3.up * yawAngleChange);
            _planarForward = yawRotation * _planarForward;

            // Pitch
            _pitchAngle += -mouseDelta.y * RotationSpeed * Time.deltaTime;
            _pitchAngle = Mathf.Clamp(_pitchAngle, -89f, 89f);
            Quaternion pitchRotation = Quaternion.Euler(Vector3.right * _pitchAngle);

            // Final rotation
            Quaternion targetRotation = Quaternion.LookRotation(_planarForward, Vector3.up) * pitchRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSharpness * Time.deltaTime);

            // Move Input
            Vector3 forwardInput = transform.forward * ((Keyboard.current.wKey.isPressed ? 1f : 0f) + (Keyboard.current.sKey.isPressed ? -1f : 0f));
            Vector3 rightInput = transform.right * ((Keyboard.current.dKey.isPressed ? 1f : 0f) + (Keyboard.current.aKey.isPressed ? -1f : 0f));
            Vector3 upInput = transform.up * ((Keyboard.current.eKey.isPressed ? 1f : 0f) + (Keyboard.current.qKey.isPressed ? -1f : 0f));
            Vector3 directionalInput = Vector3.ClampMagnitude(forwardInput + rightInput + upInput, 1f);

            // Move
            float finalMaxSpeed = MaxMoveSpeed;
            if (Keyboard.current.leftShiftKey.isPressed)
            {
                finalMaxSpeed *= SprintSpeedBoost;
            }

            _currentMoveVelocity = Vector3.Lerp(_currentMoveVelocity, directionalInput * finalMaxSpeed, Mathf.Clamp01(MoveSharpness * Time.deltaTime));
            transform.position += _currentMoveVelocity * Time.deltaTime;
        }
    }
}