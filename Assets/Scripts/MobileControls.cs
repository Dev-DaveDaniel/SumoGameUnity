using UnityEngine;

public class MobileControls : MonoBehaviour
{
    [HideInInspector] public bool rotateRightHeld;
    [HideInInspector] public bool moveForwardHeld;
    [HideInInspector] public bool dodgePressed;
    [HideInInspector] public bool pushPressed;   // TRUE for only 1 frame when tapped or until consumed

    // --- Called by UI Buttons (via Event Triggers: Pointer Down / Up) ---
    public void RotateRightDown() => rotateRightHeld = true;
    public void RotateRightUp() => rotateRightHeld = false;

    public void MoveForwardDown() => moveForwardHeld = true;
    public void MoveForwardUp() => moveForwardHeld = false;

    // --- Called by Action Buttons (via UI Button Component: OnClick) ---
    public void Dodge() => dodgePressed = true;
    public void PushButtonDown() => pushPressed = true;

    // --- Called by TopDownMovement script to consume inputs after execution ---
    public void ResetDodge() => dodgePressed = false;
    public void ResetPush() => pushPressed = false;
}