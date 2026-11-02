using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class TriangleButtonFix : MonoBehaviour
{
    void Awake()
    {
        Image img = GetComponent<Image>();
        if (img != null)
        {
            // Only registers clicks if the pixel touched is at least 10% opaque
            img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }
}