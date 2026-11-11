using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("�� Transform")]
    public Transform leftDoor;    // ���� �� ������Ʈ
    public Transform rightDoor;   // ������ �� ������Ʈ

    [Header("ȸ�� ����")]
    public float openDuration = 1f;
    public float leftTargetAngle = -90f;   // ���� �� �ٱ����� ���� ��
    public float rightTargetAngle = 90f;   // ������ �� �ٱ����� ���� ��

    private bool isOpen = false;

    public void OpenDoors()
    {
        if (isOpen) return;
        isOpen = true;
        AetherAudio.PlayAt("door_open", transform.position + Vector3.up, 0.85f, 3f, 30f, 0.03f, 1f, 60); // latch + hinge creak + stop thud

        StartCoroutine(OpenRoutine());
    }

    /// <summary>이어하기: 소리·움직임 없이 열린 자리로 바로 둔다.
    /// (장면이 막 열린 순간엔 여는 움직임이 끊겨 문짝이 닫힌 채 남았다.)</summary>
    public void OpenImmediately()
    {
        // 두 번 불려도 같은 자리가 되도록 닫힌 자리 기준으로 둔다(이미 열렸다고 표시돼도 다시 맞춘다).
        isOpen = true;
        StopAllCoroutines();
        leftDoor.localRotation = Quaternion.Euler(leftClosedEuler + new Vector3(0f, leftTargetAngle, 0f));
        rightDoor.localRotation = Quaternion.Euler(rightClosedEuler + new Vector3(0f, rightTargetAngle, 0f));
    }

    private Vector3 leftClosedEuler;
    private Vector3 rightClosedEuler;

    private void Awake()
    {
        if (leftDoor != null) leftClosedEuler = leftDoor.localEulerAngles;
        if (rightDoor != null) rightClosedEuler = rightDoor.localEulerAngles;
    }

    private IEnumerator OpenRoutine()
    {
        Quaternion leftStartRot = leftDoor.localRotation;
        Quaternion rightStartRot = rightDoor.localRotation;

        Quaternion leftEndRot = Quaternion.Euler(
            leftStartRot.eulerAngles.x,
            leftStartRot.eulerAngles.y + leftTargetAngle,
            leftStartRot.eulerAngles.z);

        Quaternion rightEndRot = Quaternion.Euler(
            rightStartRot.eulerAngles.x,
            rightStartRot.eulerAngles.y + rightTargetAngle,
            rightStartRot.eulerAngles.z);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openDuration;

            leftDoor.localRotation = Quaternion.Slerp(leftStartRot, leftEndRot, t);
            rightDoor.localRotation = Quaternion.Slerp(rightStartRot, rightEndRot, t);

            yield return null;
        }

        leftDoor.localRotation = leftEndRot;
        rightDoor.localRotation = rightEndRot;
    }
}
