using UnityEngine;

public class SumoRingZone : MonoBehaviour
{
    [Header("Ring Configurations")]
    public bool isCurrentOutboundLimit = true;

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isCurrentOutboundLimit || SumoGameManager.Instance == null) return;

        // Trace back to the parent to make sure we have the root wrestler object
        TopDownMovement wrestler = other.GetComponentInParent<TopDownMovement>();

        if (wrestler != null)
        {
            // Send the exact falling wrestler gameobject to the manager
            SumoGameManager.Instance.WrestlerFellOut(wrestler.gameObject);
        }
    }
}