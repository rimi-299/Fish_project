using UnityEngine;

public class HumanPresenceBridge : MonoBehaviour
{
    [Header("References")]
    public NetworkManager networkManager;
    public PersonManager personManager;

    [Header("No-data timeout (seconds)")]
    public float noDataTimeout = 3.0f;  // we keep this to clear spheres if tracking stops

    private float lastDataTime = -1f;

    void Awake()
    {
        if (networkManager == null)
            networkManager = FindObjectOfType<NetworkManager>();

        if (personManager == null)
            personManager = FindObjectOfType<PersonManager>();

        if (networkManager != null)
        {
            networkManager.OnHumanDataReceived += HandleHumanData;
            Debug.Log("HumanPresenceBridge subscribed to NetworkManager.");
        }
        else
        {
            Debug.LogError("HumanPresenceBridge: No NetworkManager found in scene!");
        }
    }

    void OnDestroy()
    {
        if (networkManager != null)
            networkManager.OnHumanDataReceived -= HandleHumanData;
    }

    private void HandleHumanData(NetworkManager.HumanData[] humans)
    {
        if (humans != null && humans.Length > 0)
        {
            lastDataTime = Time.time;

            if (personManager != null)
            {
                personManager.UpdateHumans(humans);
            }
        }
        // If humans is empty/null, we do nothing here.
        // Spheres will be cleared only after timeout in Update().
    }

    void Update()
    {
        // If we haven't received data for a while, clear all spheres
        if (lastDataTime > 0f && (Time.time - lastDataTime) > noDataTimeout)
        {
            if (personManager != null)
            {
                personManager.ClearAll();
            }
            lastDataTime = -1f;
        }
    }
}






// using UnityEngine;

// public class HumanPresenceBridge : MonoBehaviour
// {
//     [Header("References")]
//     public NetworkManager networkManager;
//     public PersonManager personManager;

//     [Header("No-data timeout (seconds)")]
//     public float noDataTimeout = 2.0f;

//     private float lastDataTime = -1f;
//     private bool currentPresent = false;

//     void Awake()
//     {
//         // Auto-find if not assigned in Inspector
//         if (networkManager == null)
//             networkManager = FindObjectOfType<NetworkManager>();

//         if (personManager == null)
//             personManager = FindObjectOfType<PersonManager>();

//         if (networkManager != null)
//         {
//             networkManager.OnHumanDataReceived += HandleHumanData;
//             Debug.Log("HumanPresenceBridge subscribed to NetworkManager.");
//         }
//         else
//         {
//             Debug.LogError("HumanPresenceBridge: No NetworkManager found in scene!");
//         }
//     }

//     void OnDestroy()
//     {
//         if (networkManager != null)
//             networkManager.OnHumanDataReceived -= HandleHumanData;
//     }

//     private void HandleHumanData(NetworkManager.HumanData[] humans)
//     {
//         lastDataTime = Time.time;

//         bool presentNow = humans != null && humans.Length > 0;

//         if (presentNow != currentPresent)
//         {
//             currentPresent = presentNow;
//             if (personManager != null)
//                 personManager.SetPersonPresent(currentPresent);
//         }
//     }

//     void Update()
//     {
//         // If we previously had a person, but no data for a while → assume no one present
//         if (currentPresent && lastDataTime > 0f && (Time.time - lastDataTime) > noDataTimeout)
//         {
//             currentPresent = false;
//             if (personManager != null)
//                 personManager.SetPersonPresent(false);
//         }
//     }
// }
