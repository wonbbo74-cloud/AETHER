using UnityEngine;

/// <summary>
/// 엘리베이터 안으로 들어서면 문을 닫고 올려 보내던 예전 방식의 트리거.
///
/// 지금은 <see cref="ElevatorInteractionController"/> 가 그 일을 맡는다.
/// 06 씬에 남아 있는 이 트리거는 <c>elevator</c> 가 비어 있어서,
/// 플레이어가 엘리베이터에 들어설 때마다 NullReferenceException 을 뱉고 있었다.
/// 연결된 엘리베이터가 없으면 그냥 아무 일도 하지 않는다.
/// </summary>
public class ElevatorEnterTrigger : MonoBehaviour
{
    public ElevatorController elevator;

    private void OnTriggerEnter(Collider other)
    {
        if (elevator == null) return;
        if (!other.CompareTag("Player")) return;

        elevator.CloseDoorAndMove();
    }
}
