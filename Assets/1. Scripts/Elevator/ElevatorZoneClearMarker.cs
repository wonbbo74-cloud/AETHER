using UnityEngine;

public class ElevatorZoneClearMarker : MonoBehaviour
{
    [SerializeField, Min(1)] private int zoneNumberToMark = 1;
    [SerializeField] private bool markOnStart;
    [SerializeField] private bool markOnTriggerEnter;
    [SerializeField] private string playerTag = "Player";

    private bool marked;

    private void Start()
    {
        if (markOnStart)
            MarkCleared();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!markOnTriggerEnter || marked)
            return;

        if (!other.CompareTag(playerTag))
            return;

        MarkCleared();
    }

    public void MarkCleared()
    {
        if (marked)
            return;

        marked = true;
        ElevatorZoneProgress.MarkZoneCleared(zoneNumberToMark);
    }
}
