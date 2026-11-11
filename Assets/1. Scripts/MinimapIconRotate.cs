using UnityEngine;

public class MinimapIconRotate : MonoBehaviour
{
    [SerializeField] private Transform player;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
            else
                Debug.LogWarning("[MinimapIconRotate] Player 태그를 가진 오브젝트를 찾지 못했습니다.");
        }
    }

    void LateUpdate()
    {
        if (!player) return;

        transform.rotation = Quaternion.Euler(90f, 0f, -player.eulerAngles.y);
    }
}