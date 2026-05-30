using UnityEngine;

public class LookAtCameraUpright : MonoBehaviour
{
    private void LateUpdate()
    {
        // Forces the object to ignore its parent rotation and stay flat against the screen
        transform.rotation = Quaternion.identity;
    }
}