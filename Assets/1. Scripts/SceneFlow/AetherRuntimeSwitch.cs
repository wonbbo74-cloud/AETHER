/// <summary>
/// 문제를 가려낼 때 쓰는 전역 스위치. 평소에는 전부 false로 둔다.
///
/// DisableAll을 true로 두면 이번 작업에서 추가한 런타임 시스템이 전부 멈춘다.
/// 아래 개별 스위치로 하나씩만 꺼서 원인을 좁힐 수도 있다.
/// (예: 05의 'Invalid AABB' 오류가 제 코드 때문인지 씬 자체 문제인지 가려낼 때 썼다.
///      결과는 '씬 자체 문제' — 전부 꺼도 오류가 그대로 났다.)
/// </summary>
public static class AetherRuntimeSwitch
{
    public static bool DisableAll = false;

    /// <summary>픽업 안내·퀵슬롯·인벤토리 슬롯 생성·크로스헤어 크기 조정</summary>
    public static bool DisableUIBootstrap = false;

    /// <summary>인벤토리 / 파츠 장착 / 파츠 제작 / 일시정지 전체화면 UI</summary>
    public static bool DisableScreens = false;

    /// <summary>구역별 모듈·설계도 자동 배치</summary>
    public static bool DisableModules = false;
}
