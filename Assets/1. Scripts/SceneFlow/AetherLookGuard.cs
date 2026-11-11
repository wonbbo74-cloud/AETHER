using UnityEngine;

/// <summary>
/// 퍼즐·상점·인벤토리처럼 커서를 풀었다가 다시 잠그는 순간, 커서가 클릭하던 자리에서 화면 가운데로
/// 순간이동한 만큼이 마우스 움직임으로 한꺼번에 들어와 시선이 확 꺾였다(04 전력 퍼즐을 닫자 바닥을 봄).
/// 커서가 다시 잠긴 뒤 몇 프레임은 시선 입력을 버린다.
/// </summary>
public static class AetherLookGuard
{
    private const int IgnoreFrames = 4;

    private static CursorLockMode lastMode = CursorLockMode.Locked;
    private static int lastCheckedFrame = -1;
    private static int ignoreUntilFrame;

    public static bool ShouldIgnoreLook()
    {
        int frame = Time.frameCount;
        if (frame != lastCheckedFrame)
        {
            lastCheckedFrame = frame;
            CursorLockMode mode = Cursor.lockState;
            if (mode == CursorLockMode.Locked && lastMode != CursorLockMode.Locked)
                ignoreUntilFrame = frame + IgnoreFrames;
            lastMode = mode;
        }

        return frame <= ignoreUntilFrame;
    }
}
