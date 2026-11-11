using UnityEngine;

public class DroneHearing : MonoBehaviour
{
    public float hearingMultiplier = 1f;

    public Vector3 LastHeardPosition { get; private set; }
    public float LastHeardTime { get; private set; }

    public bool HeardRecently(float seconds) => Time.time - LastHeardTime <= seconds;

    private void OnEnable()
    {
        if (NoiseSystem.Instance != null)
            NoiseSystem.Instance.OnNoise += OnNoise;
    }

    private void OnDisable()
    {
        if (NoiseSystem.Instance != null)
            NoiseSystem.Instance.OnNoise -= OnNoise;
    }

    private void OnNoise(NoiseEvent e)
    {
        float dist = Vector3.Distance(transform.position, e.position);
        float effectiveRadius = e.radius * hearingMultiplier;

        if (dist <= effectiveRadius)
        {
            LastHeardPosition = e.position;
            LastHeardTime = Time.time;
        }
    }
}