using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class TriangleButtonCollider : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("0 = registers everything. 1 = only registers 100% opaque pixels.")]
    public float alphaThreshold = 0.5f;

    private void Awake()
    {
        // Tell the Image to ignore clicks on transparent pixels
        GetComponent<Image>().alphaHitTestMinimumThreshold = alphaThreshold;
    }
}