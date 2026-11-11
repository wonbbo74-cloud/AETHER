using UnityEngine;

public class FloorDetector : MonoBehaviour
{
    public enum FloorType
    {
        Floor1,
        Floor2,
        Floor3
    }

    [Header("⑷營 類")]
    public FloorType currentFloor;

    [Header("1類 彰嬪")]
    public float floor1MinY = 0f;
    public float floor1MaxY = 3.9f;

    [Header("2類 彰嬪")]
    public float floor2MinY = 4f;
    public float floor2MaxY = 8.9f;

    [Header("3類 彰嬪")]
    public float floor3MinY = 9f;
    public float floor3MaxY = 14f;

    void Update()
    {
        float y = transform.position.y;

        if (y >= floor1MinY && y <= floor1MaxY)
        {
            currentFloor = FloorType.Floor1;
        }
        else if (y >= floor2MinY && y <= floor2MaxY)
        {
            currentFloor = FloorType.Floor2;
        }
        else if (y >= floor3MinY && y <= floor3MaxY)
        {
            currentFloor = FloorType.Floor3;
        }
    }
}