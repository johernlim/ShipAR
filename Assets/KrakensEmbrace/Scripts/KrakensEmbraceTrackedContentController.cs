using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Keeps spawned content visible only while its poster is tracked and gives
/// the ship a small, frame-rate-independent presentation motion.
/// </summary>
public sealed class KrakensEmbraceTrackedContentController : MonoBehaviour
{
    [SerializeField] GameObject contentRoot;
    [SerializeField] Transform shipPivot;
    [SerializeField] float yawDegreesPerSecond = 4f;
    [SerializeField] float bobHeight = 0.0025f;
    [SerializeField] float bobSpeed = 1.2f;

    ARTrackedImage trackedImage;
    Vector3 shipStartPosition;

    public void Configure(GameObject trackedContent, Transform animatedShipPivot)
    {
        contentRoot = trackedContent;
        shipPivot = animatedShipPivot;
    }

    void Awake()
    {
        trackedImage = GetComponent<ARTrackedImage>();
        if (shipPivot != null)
            shipStartPosition = shipPivot.localPosition;
    }

    void Update()
    {
        if (trackedImage == null)
            trackedImage = GetComponent<ARTrackedImage>();

        var visible = trackedImage != null && trackedImage.trackingState == TrackingState.Tracking;
        if (contentRoot != null && contentRoot.activeSelf != visible)
            contentRoot.SetActive(visible);

        if (!visible || shipPivot == null)
            return;

        shipPivot.Rotate(0f, yawDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        var position = shipStartPosition;
        position.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        shipPivot.localPosition = position;
    }
}
