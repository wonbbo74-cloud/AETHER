using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerRespawnController : MonoBehaviour
{
    [Header("리스폰 위치")]
    [SerializeField] private Transform 시작위치기준점;
    [SerializeField] private bool 시작시현재위치를리스폰지점으로사용 = true;

    [Header("리스폰 시 회전 초기화")]
    [SerializeField] private bool 회전도복구 = true;

    [Header("이동 스크립트 연결(선택)")]
    [SerializeField] private Move_Yunseo 윤서이동스크립트;

    private Rigidbody 리지드바디;
    private Vector3 현재리스폰위치;
    private Quaternion 현재리스폰회전값;

    private void Awake()
    {
        리지드바디 = GetComponent<Rigidbody>();

        if (윤서이동스크립트 == null)
            윤서이동스크립트 = GetComponent<Move_Yunseo>();

        if (시작위치기준점 != null)
        {
            현재리스폰위치 = 시작위치기준점.position;
            현재리스폰회전값 = 시작위치기준점.rotation;
        }
        else if (시작시현재위치를리스폰지점으로사용)
        {
            현재리스폰위치 = transform.position;
            현재리스폰회전값 = transform.rotation;
        }
    }

    public void 리스폰실행()
    {
        if (리지드바디 == null)
            return;

        // 물리 상태 초기화
        리지드바디.linearVelocity = Vector3.zero;
        리지드바디.angularVelocity = Vector3.zero;
        리지드바디.Sleep();

        // 이동 잠깐 막았다가 다시 허용
        if (윤서이동스크립트 != null)
        {
            윤서이동스크립트.SetMoveEnabled(false);
            윤서이동스크립트.SetLookEnabled(false);
        }

        // 위치 복귀
        transform.position = 현재리스폰위치;

        if (회전도복구)
            transform.rotation = 현재리스폰회전값;

        // 리지드바디 위치 동기화
        리지드바디.position = 현재리스폰위치;

        if (회전도복구)
            리지드바디.rotation = 현재리스폰회전값;

        Physics.SyncTransforms();

        리지드바디.WakeUp();

        if (윤서이동스크립트 != null)
        {
            윤서이동스크립트.SetMoveEnabled(true);
            윤서이동스크립트.SetLookEnabled(true);
        }
    }

    public void 리스폰지점변경(Transform 새기준점)
    {
        if (새기준점 == null)
            return;

        현재리스폰위치 = 새기준점.position;
        현재리스폰회전값 = 새기준점.rotation;
    }

    public void 리스폰지점변경(Vector3 새위치, Quaternion 새회전)
    {
        현재리스폰위치 = 새위치;
        현재리스폰회전값 = 새회전;
    }

    public Vector3 현재리스폰위치가져오기()
    {
        return 현재리스폰위치;
    }
}