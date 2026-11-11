using UnityEngine;

public class NavigationTarget : MonoBehaviour
{
    public enum FloorType
    {
        Floor1,
        Floor2,
        Floor3
    }

    [Header("이 목표가 있는 층")]
    public FloorType targetFloor;
}