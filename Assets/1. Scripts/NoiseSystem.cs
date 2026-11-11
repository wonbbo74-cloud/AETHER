using System;
using UnityEngine;

public struct NoiseEvent
{
    public Vector3 position;
    public float loudness;   // ���Ҹ� ũ�⡱
    public float radius;     // ������ �ݰ桱
    public float time;

    public NoiseEvent(Vector3 pos, float loudness, float radius)
    {
        position = pos;
        this.loudness = loudness;
        this.radius = radius;
        time = Time.time;
    }
}

public class NoiseSystem : MonoBehaviour
{
    public static NoiseSystem Instance { get; private set; }
    public event Action<NoiseEvent> OnNoise;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void EmitNoise(Vector3 pos, float loudness, float radius)
    {
        OnNoise?.Invoke(new NoiseEvent(pos, loudness, radius));
    }
}
