using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileInputAutoLinker : MonoBehaviour
{
    [Header("Drag Your Prefab Buttons Here!")]
    [SerializeField] private GameObject rotateButton;
    [SerializeField] private GameObject moveButton;
    [SerializeField] private Button dodgeButton;
    [SerializeField] private Button shoveButton;

    private MobileControls controls;

    private void Awake()
    {
        controls = GetComponent<MobileControls>();
    }

    private void Start()
    {
        // Safety Check: Make sure the script found its own MobileControls component
        if (controls == null)
        {
            Debug.LogError($"<color=red><b>[LINKER ERROR]</b></color> MobileControls component missing on {gameObject.name}!");
            return;
        }

        // 1. Bind Hold Down / Release mechanics for Movement & Rotation
        if (rotateButton != null) AddPointerTriggers(rotateButton, controls.RotateRightDown, controls.RotateRightUp);
        else Debug.LogError("Rotate Button slot is empty on AutoLinker!", gameObject);

        if (moveButton != null) AddPointerTriggers(moveButton, controls.MoveForwardDown, controls.MoveForwardUp);
        else Debug.LogError("Move Button slot is empty on AutoLinker!", gameObject);

        // 2. Bind Instant Click mechanics for Actions
        if (dodgeButton != null)
        {
            dodgeButton.onClick.RemoveAllListeners();
            dodgeButton.onClick.AddListener(controls.Dodge);
        }
        else Debug.LogError("Dodge Button slot is empty on AutoLinker!", gameObject);

        if (shoveButton != null)
        {
            shoveButton.onClick.RemoveAllListeners();
            shoveButton.onClick.AddListener(controls.PushButtonDown);
        }
        else Debug.LogError("Shove Button slot is empty on AutoLinker!", gameObject);

        Debug.Log($"<color=green><b>[LINKER SUCCESS]</b></color> All controls successfully routed for {gameObject.name}!");
    }

    private void AddPointerTriggers(GameObject target, UnityEngine.Events.UnityAction downAction, UnityEngine.Events.UnityAction upAction)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.AddComponent<EventTrigger>();

        trigger.triggers.Clear();

        // Bind PointerDown (Pressing down)
        EventTrigger.Entry entryDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entryDown.callback.AddListener((data) => { downAction.Invoke(); });
        trigger.triggers.Add(entryDown);

        // Bind PointerUp (Lifting finger up)
        EventTrigger.Entry entryUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entryUp.callback.AddListener((data) => { upAction.Invoke(); });
        trigger.triggers.Add(entryUp);
    }
}