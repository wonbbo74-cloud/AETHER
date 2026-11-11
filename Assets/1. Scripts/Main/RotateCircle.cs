using UnityEngine;

public class RotateCircle : MonoBehaviour
{
    public float rotationSpeed = 180f;  // 초당 회전 각도

    private RectTransform rect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    void Update()
    {
        rect.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }
}
