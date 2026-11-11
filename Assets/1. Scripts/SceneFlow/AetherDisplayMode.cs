using UnityEngine;

/// <summary>
/// 게임은 늘 전체 화면(모니터 해상도)으로 시작한다(10/12 사용자: "전체화면이 안 돼").
/// 유니티는 마지막 창 상태를 레지스트리(Screenmanager Fullscreen mode)에 기억하는데, 시험 중 창 모드 1280×720 이
/// 남아 매번 작은 창으로 켜졌다. 시작할 때 한 번 모니터 해상도의 전체 화면 창으로 맞춘다.
/// 게임 중에는 Alt+Enter 로 창/전체 화면을 바꿀 수 있다(유니티 기본 기능).
/// </summary>
public static class AetherDisplayMode
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ForceFullScreen()
    {
        if (Application.isEditor)
            return;

        Resolution native = Screen.currentResolution;
        Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
    }
}
