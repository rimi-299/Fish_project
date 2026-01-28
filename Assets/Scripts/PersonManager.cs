using UnityEngine;

public class PersonManager : MonoBehaviour
{
    [Header("References")]
    public NetworkManager networkManager;

    [Header("Move THIS scene object (drag TrackedFishPrefab from Hierarchy)")]
    public Transform trackedFish;

    [Header("Camera → World mapping")]
    public float imageWidth = 640f;   // must match Python width
    public float fixedY = 0f;

    [Tooltip("Keep this SAME as your fish's Z in the scene, or the fish will jump layers.")]
    public float fixedZ = 300.46f; // <-- IMPORTANT: your fish is at ~300 in the screenshot

    [Header("Smoothing")]
    public float moveSpeed = 12f;

    private Vector3 targetPos;
    private bool hasTarget = false;

    void Awake()
    {
        if (networkManager == null)
            networkManager = FindObjectOfType<NetworkManager>();

        if (trackedFish == null)
            Debug.LogError("[PersonManager] trackedFish is NOT assigned. Drag TrackedFishPrefab (scene object) here.");
    }

    void OnEnable()
    {
        if (networkManager != null)
        {
            networkManager.OnHumanDataReceived += UpdateHumans;
            Debug.Log("[PersonManager] Subscribed to NetworkManager.OnHumanDataReceived");
        }
        else
        {
            Debug.LogError("[PersonManager] NetworkManager not found. Cannot subscribe.");
        }
    }

    void OnDisable()
    {
        if (networkManager != null)
            networkManager.OnHumanDataReceived -= UpdateHumans;
    }

    /// <summary>
    /// Called whenever NetworkManager receives new detections.
    /// Moves the trackedFish based on the first detected person.
    /// </summary>
    public void UpdateHumans(NetworkManager.HumanData[] humans)
    {
        if (trackedFish == null) return;
        if (humans == null || humans.Length == 0) return;

        var h = humans[0]; // first person
        float worldX = MapCameraXToWorld(h.x);

        targetPos = new Vector3(worldX, fixedY, fixedZ);
        hasTarget = true;

        Debug.Log($"[PersonManager] humans={humans.Length} camX={h.x:F1} -> worldX={worldX:F2} target={targetPos}");
    }

    void Update()
    {
        if (!hasTarget || trackedFish == null) return;

        trackedFish.position = Vector3.Lerp(
            trackedFish.position,
            targetPos,
            Time.deltaTime * moveSpeed
        );
    }

    public void ClearAll()
    {
        hasTarget = false;
    }

    private float MapCameraXToWorld(float camX)
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;

        float t = camX / (imageWidth - 1f);

        // distance from camera to the fish plane
        float zDist = trackedFish.position.z - cam.transform.position.z;

        float worldLeft  = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, zDist)).x;
        float worldRight = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, zDist)).x;

        return Mathf.Lerp(worldLeft, worldRight, t);
    }

}
