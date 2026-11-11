using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public Transform startPoint;

    public Transform player;

    private void Start()
    {
        if (startPoint == null)
        {
            Debug.LogError("startPoint가 설정되지 않았습니다.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("player가 설정되지 않았습니다.");
            return;
        }

        MovePlayerToStartPoint();
    }

    private void MovePlayerToStartPoint()
    {
        player.position = startPoint.position;
        player.rotation = startPoint.rotation;
    }
}



