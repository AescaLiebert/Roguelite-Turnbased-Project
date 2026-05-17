using UnityEngine;

public class UIFollowWorld : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2.0f, 0); // Offset in World Space (e.g. above head)
    [SerializeField] private bool hideIfBehindCamera = true;

    private Transform targetTransform;
    private RectTransform rectTransform;
    private Camera mainCamera;
    private Canvas parentCanvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        mainCamera = Camera.main;
        parentCanvas = GetComponentInParent<Canvas>();
    }

    public void SetTarget(Transform target)
    {
        targetTransform = target;
    }

    private void LateUpdate()
    {
        if (targetTransform == null)
        {
            // Optional: Destroy self if target is gone? 
            // For now, just hide
            // gameObject.SetActive(false);
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;

        // 1. Convert World Position to Screen Position
        Vector3 worldPos = targetTransform.position + offset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        // 2. Check if behind camera
        if (hideIfBehindCamera)
        {
            bool isBehind = screenPos.z < 0;
            if (isBehind != !gameObject.activeSelf)
            {
                 // Should be hidden
            }
            
            // Simple toggle active might be expensive if done every frame, 
            // but for a few units it's fine.
            // Better to move offscreen.
            if (screenPos.z < 0)
            {
                 screenPos = new Vector3(-1000, -1000, 0); // Move offscreen
            }
        }

        // 3. Apply to RectTransform
        // Note: functionality depends on Canvas render mode. 
        // For Screen Space - Overlay, WorldToScreenPoint returns pixel coords which match position.
        rectTransform.position = screenPos;
    }
}
