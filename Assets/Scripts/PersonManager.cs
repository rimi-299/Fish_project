// using UnityEngine;

// public class PersonManager : MonoBehaviour
// {
//     [Header("References")]
//     public NetworkManager networkManager;

//     [Header("Move THIS scene object (drag TrackedFishPrefab from Hierarchy)")]
//     public Transform trackedFish;

//     [Header("Camera → World mapping")]
//     public float imageWidth = 640f;   // must match Python width
//     public float fixedY = 0f;

//     [Tooltip("Keep this SAME as your fish's Z in the scene, or the fish will jump layers.")]
//     public float fixedZ = 300.46f; // <-- IMPORTANT: your fish is at ~300 in the screenshot

//     [Header("Smoothing")]
//     public float moveSpeed = 12f;

//     private Vector3 targetPos;
//     private bool hasTarget = false;

//     void Awake()
//     {
//         if (networkManager == null)
//             networkManager = FindObjectOfType<NetworkManager>();

//         if (trackedFish == null)
//             Debug.LogError("[PersonManager] trackedFish is NOT assigned. Drag TrackedFishPrefab (scene object) here.");
//     }

//     void OnEnable()
//     {
//         if (networkManager != null)
//         {
//             networkManager.OnHumanDataReceived += UpdateHumans;
//             Debug.Log("[PersonManager] Subscribed to NetworkManager.OnHumanDataReceived");
//         }
//         else
//         {
//             Debug.LogError("[PersonManager] NetworkManager not found. Cannot subscribe.");
//         }
//     }

//     void OnDisable()
//     {
//         if (networkManager != null)
//             networkManager.OnHumanDataReceived -= UpdateHumans;
//     }

//     /// <summary>
//     /// Called whenever NetworkManager receives new detections.
//     /// Moves the trackedFish based on the first detected person.
//     /// </summary>
//     public void UpdateHumans(NetworkManager.HumanData[] humans)
//     {
//         if (trackedFish == null) return;
//         if (humans == null || humans.Length == 0) return;

//         var h = humans[0]; // first person
//         float worldX = MapCameraXToWorld(h.x);

//         targetPos = new Vector3(worldX, fixedY, fixedZ);
//         hasTarget = true;

//         Debug.Log($"[PersonManager] humans={humans.Length} camX={h.x:F1} -> worldX={worldX:F2} target={targetPos}");
//     }

//     void Update()
//     {
//         if (!hasTarget || trackedFish == null) return;

//         trackedFish.position = Vector3.Lerp(
//             trackedFish.position,
//             targetPos,
//             Time.deltaTime * moveSpeed
//         );
//     }

//     public void ClearAll()
//     {
//         hasTarget = false;
//     }

//     private float MapCameraXToWorld(float camX)
//     {
//         Camera cam = Camera.main;
//         if (cam == null) return 0f;

//         float t = camX / (imageWidth - 1f);

//         // distance from camera to the fish plane
//         float zDist = trackedFish.position.z - cam.transform.position.z;

//         float worldLeft  = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, zDist)).x;
//         float worldRight = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, zDist)).x;

//         return Mathf.Lerp(worldLeft, worldRight, t);
//     }

// }

using UnityEngine;

public class PersonManager : MonoBehaviour
{
    [Header("References")]
    public NetworkManager networkManager;

    [Header("Move THIS scene object (root)")]
    public Transform trackedFish;

    [Header("Rotate THIS visual (child mesh). If null, will rotate trackedFish")]
    public Transform fishVisual;

    [Header("Camera X → World X mapping (custom swim area)")]
    [Tooltip("Must match your Python frame width (e.g., 640).")]
    public float imageWidth = 640f;

    [Tooltip("Left edge of swim area in WORLD X.")]
    public float worldLeftX = -220f;

    [Tooltip("Right edge of swim area in WORLD X.")]
    public float worldRightX = 1050f;

    [Header("Fixed plane")]
    public float fixedY = 0f;

    [Tooltip("Set 0 to auto-lock to trackedFish's current Z in Awake().")]
    public float fixedZ = 0f;

    [Header("Follow feel (natural)")]
    public float minSpeed = 1.5f;          // when close
    public float maxSpeed = 12f;           // when far
    public float speedMultiplier = 2.0f;   // higher = faster when far
    public float stopDistance = 0.03f;     // if within this, we don't move (prevents jitter)
    public float smoothTime = 0.12f;       // bigger = smoother but slower response

    [Header("Target update control (prevents 'moving in same spot')")]
    public float retargetDistance = 0.15f; // only accept a new target if it changed enough

    [Header("Turning (3D)")]
    public float turnSpeed = 6f;
    public float turnDeadzone = 0.001f;    // ignore tiny movement
    public Vector3 modelForwardAxis = Vector3.forward; // change to Vector3.right if fish faces +X

    [Header("Optional: tiny swim bob")]
    public bool enableBobbing = true;
    public float bobAmplitude = 0.05f;
    public float bobFrequency = 1.5f;

    private Vector3 targetPos;
    private bool hasTarget = false;

    private Vector3 smoothVel;     // SmoothDamp velocity
    private Vector3 lastPos;
    private bool hasLastPos = false;

    void Awake()
    {
        if (networkManager == null)
            networkManager = FindObjectOfType<NetworkManager>();

        if (trackedFish == null)
            Debug.LogError("[PersonManager] trackedFish is NOT assigned. Drag TrackedFishPrefab here.");

        if (fishVisual == null)
            fishVisual = trackedFish; // fallback: rotate root if no child assigned

        // Auto-lock Z plane if fixedZ is 0
        if (trackedFish != null && Mathf.Approximately(fixedZ, 0f))
            fixedZ = trackedFish.position.z;

        Debug.Log($"[PersonManager] Awake: trackedFish={(trackedFish ? trackedFish.name : "NULL")} fixedZ={fixedZ}");
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
    /// Uses the first detected person and updates the target position ONLY when it changes enough,
    /// so the fish visibly swims from A -> B (instead of jittering in place).
    /// </summary>
    public void UpdateHumans(NetworkManager.HumanData[] humans)
    {
        if (trackedFish == null) return;
        if (humans == null || humans.Length == 0) return;

        var h = humans[0];

        float worldX = MapCameraXToWorldX(h.x);
        Vector3 newTarget = new Vector3(worldX, fixedY, fixedZ);

        // Only retarget if the target has changed meaningfully
        if (!hasTarget || Vector3.Distance(targetPos, newTarget) > retargetDistance)
        {
            targetPos = newTarget;
            hasTarget = true;

            // Optional debug:
            // Debug.Log($"[PersonManager] Retarget -> {targetPos} (camX={h.x:F1})");
        }
    }

    void Update()
    {
        Debug.Log($"targetPos: {targetPos}, hasTarget: {hasTarget}");

        if (!hasTarget || trackedFish == null) return;

        float distToTarget = Vector3.Distance(trackedFish.position, targetPos);

        // Stop jitter close to the target
        if (distToTarget <= stopDistance)
            return;

        // Distance-based speed (natural follow)
        float targetSpeed = Mathf.Clamp(distToTarget * speedMultiplier, minSpeed, maxSpeed);

        // Step toward target with speed, then SmoothDamp for nice motion
        Vector3 desired = Vector3.MoveTowards(trackedFish.position, targetPos, targetSpeed * 2);//Time.deltaTime);
        trackedFish.position = Vector3.SmoothDamp(trackedFish.position, desired, ref smoothVel, smoothTime);

        // Optional: subtle bobbing around fixedY
        if (enableBobbing)
        {
            Vector3 p = trackedFish.position;
            p.y = fixedY + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            trackedFish.position = p;
        }

        // ---- 3D turning based on REAL movement direction ----
        Transform visual = fishVisual != null ? fishVisual : trackedFish;

        if (!hasLastPos)
        {
            lastPos = trackedFish.position;
            hasLastPos = true;
            return;
        }

        Vector3 delta = trackedFish.position - lastPos;

        if (delta.sqrMagnitude > turnDeadzone * turnDeadzone)
        {
            Vector3 dir = delta.normalized;

            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);

            // Fix model axis (if fish faces +X, set modelForwardAxis=Vector3.right)
            Quaternion axisFix = Quaternion.FromToRotation(modelForwardAxis, Vector3.forward);

            Quaternion targetRot = look * axisFix;

            visual.rotation = Quaternion.Slerp(visual.rotation, targetRot, Time.deltaTime * turnSpeed);
        }

        lastPos = trackedFish.position;
    }

    public void ClearAll()
    {
        hasTarget = false;
        hasLastPos = false;
        smoothVel = Vector3.zero;
    }

    /// <summary>
    /// Maps camera pixel X (0..imageWidth-1) to world X in a custom swim area (worldLeftX..worldRightX).
    /// This gives you big, visible A->B movement.
    /// </summary>
    private float MapCameraXToWorldX(float camX)
    {
        float t = camX / (imageWidth - 1f);     // 0..1
        t = Mathf.Clamp01(t);

        return Mathf.Lerp(worldLeftX, worldRightX, t);
    }
}
