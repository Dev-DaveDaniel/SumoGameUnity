using System.Collections;
using UnityEngine;

public class JuiceManager : MonoBehaviour
{
    public static JuiceManager Instance { get; private set; }

    [Header("Camera Reference")]
    [SerializeField] private Transform mainCameraTransform;
    private Vector3 originalCameraPos;
    private Coroutine shakeCoroutine;
    private Coroutine hitStopCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (mainCameraTransform == null && Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void Start()
    {
        if (mainCameraTransform != null)
        {
            originalCameraPos = mainCameraTransform.localPosition;
        }
    }

    /// <summary>
    /// Call this to trigger a combined hitstop freeze and screen shake.
    /// </summary>
    public void TriggerImpactJuice(float freezeDuration, float slowMoDuration, float shakeIntensity, float shakeDuration)
    {
        // 1. Trigger Hit Stop Freeze
        if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
        hitStopCoroutine = StartCoroutine(HitStopRoutine(freezeDuration, slowMoDuration));

        // 2. Trigger Screen Shake
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        if (mainCameraTransform != null)
        {
            shakeCoroutine = StartCoroutine(CameraShakeRoutine(shakeIntensity, shakeDuration));
        }
    }

    private IEnumerator HitStopRoutine(float freezeTime, float slowMoTime)
    {
        // Absolute hard freeze frame (using real-time since timeScale affects normal WaitForSeconds)
        Time.timeScale = 0.02f;
        yield return new WaitForSecondsRealtime(freezeTime);

        // Smooth recovery / slight slow-motion bleed out
        if (slowMoTime > 0)
        {
            Time.timeScale = 0.4f;
            float elapsed = 0f;
            while (elapsed < slowMoTime)
            {
                elapsed += Time.unscaledDeltaTime;
                // Linearly interpolate back to full operational speed
                Time.timeScale = Mathf.Lerp(0.4f, 1f, elapsed / slowMoTime);
                yield return null;
            }
        }

        Time.timeScale = 1f;
    }

    private IEnumerator CameraShakeRoutine(float intensity, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Use unscaledDeltaTime so the camera shakes dynamically even while game time is frozen!
            elapsed += Time.unscaledDeltaTime;

            // Generate a random vector offset
            float offsetX = Random.Range(-1f, 1f) * intensity;
            float offsetY = Random.Range(-1f, 1f) * intensity;

            mainCameraTransform.localPosition = new Vector3(originalCameraPos.x + offsetX, originalCameraPos.y + offsetY, originalCameraPos.z);
            yield return null;
        }

        // Return camera back safely to its anchor origin point
        mainCameraTransform.localPosition = originalCameraPos;
    }
}