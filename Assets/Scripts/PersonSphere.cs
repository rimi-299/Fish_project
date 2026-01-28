using UnityEngine;
using TMPro;

public class PersonSphere : MonoBehaviour
{
    [Header("Person Info")]
    public int personID; // ID from YOLO

    [Header("ID Label")]
    public TMP_Text idText; // Text above the sphere

    [Header("Movement (used or ignored depending on setup)")]
    public Vector3 targetPosition;
    public float moveSpeed = 5f;

    void Awake()
    {
        // If you forgot to assign it in the prefab, try to grab any TMP_Text in children
        if (idText == null)
        {
            idText = GetComponentInChildren<TMP_Text>();

            if (idText == null)
            {
                Debug.LogWarning(
                    $"[PersonSphere] No TMP_Text found on {name}. ID label will NOT show."
                );
            }
        }
    }

    void Update()
    {
        // If you're using PersonManager's Lerp for movement, you can ignore this:
        // transform.position = Vector3.MoveTowards(
        //     transform.position,
        //     targetPosition,
        //     moveSpeed * Time.deltaTime
        // );

        // Billboard the label toward the camera
        if (idText != null && Camera.main != null)
        {
            idText.transform.rotation =
                Quaternion.LookRotation(
                    idText.transform.position - Camera.main.transform.position
                );
        }
    }

    public void SetTargetPosition(Vector3 pos)
    {
        targetPosition = pos;
    }

    public void SetPersonId(int id)
    {
        personID = id;

        if (idText != null)
        {
            idText.text = id.ToString();
            Debug.Log(
                $"[PersonSphere] SetPersonId({id}) on {name}, label text now '{idText.text}'"
            );
        }
        else
        {
            Debug.LogWarning(
                $"[PersonSphere] SetPersonId({id}) but idText is NULL on {name}"
            );
        }
    }
}
