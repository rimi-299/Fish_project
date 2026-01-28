using System;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using Debug = UnityEngine.Debug;



public class NetworkManager : MonoBehaviour
{
    [Header("WebSocket Settings")]
    public string WebSocketURL = "ws://localhost:8765";

    public PersonManager personManager;

    public event Action<HumanData[]> OnHumanDataReceived;

    private WebSocket ws;
    private readonly Queue<Action> actionsQueue = new Queue<Action>();

    void Start()
    {
        ws = new WebSocket(WebSocketURL);
        ws.OnMessage += (sender, e) => EnqueueMessage(e.Data);
        ws.OnOpen    += (sender, e) => Debug.Log($"Connected to WebSocket server at {WebSocketURL}");
        ws.OnClose   += (sender, e) => Debug.Log("WebSocket closed.");
        ws.OnError   += (sender, e) => Debug.LogError($"WebSocket error: {e.Message}");
        ws.Connect();
    }

    void Update()
    {
        while (actionsQueue.Count > 0)
            actionsQueue.Dequeue().Invoke();
    }

    private void EnqueueMessage(string jsonData)
    {
        actionsQueue.Enqueue(() =>
        {
            try
            {
                var humans = JsonHelper.FromJson<HumanData>(jsonData);
                // OnHumanDataReceived?.Invoke(humans);
                OnHumanDataReceived?.Invoke(humans);
                if (personManager != null)
                    personManager.UpdateHumans(humans);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse HumanData JSON: {ex.Message}\nData: {jsonData}");
            }
        });
    }

    void OnDestroy()
    {
        if (ws != null)
        {
            ws.Close();
            ws = null;
        }
    }

    [Serializable]
    public class KeypointMotion
    {
        public int   kp_id;
        public float x;
        public float y;
        public float intensity;
    }

    [Serializable]
    public class HumanData
    {
        public int    person_id;
        public float  x, y, w, h, depth, confidence;
        public int    frame;
        public bool   facing_screen;
        public bool   wave_detected;
        public long   server_ts_ms;
        public KeypointMotion[] keypoint_motions;
    }

    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string wrapped = "{\"Items\":" + json + "}";
            var wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return wrapper.Items;
        }

        [Serializable]
        private class Wrapper<T>
        {
           [SerializeField] public T[] Items;

        }
    }
}
