using UnityEngine;

public class SparkFlicker : MonoBehaviour
{
    public Light sparkLight;

    void Update()
    {
        sparkLight.intensity = Random.Range(0f, 6f);
    }
}
