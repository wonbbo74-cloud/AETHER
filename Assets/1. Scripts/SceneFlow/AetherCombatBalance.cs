using UnityEngine;

/// <summary>
/// 전투 난이도 조절 값을 한곳에 모아 둔다.
///
/// 적 하나하나의 피해량은 씬에 저장되어 있어서, 스무 명을 일일이 고치면 다음에 또 어긋난다.
/// 쏘는 순간 여기서 한 번 곱해 주면 한 숫자만 바꿔도 전체가 따라온다.
/// </summary>
public static class AetherCombatBalance
{
    /// <summary>
    /// 플레이어 최대 체력. 로봇(100)의 두 배로 둔다.
    /// 씬과 프리팹에는 100 으로 저장되어 있고, PlayerStatus 가 시작할 때 이 값으로 올린다.
    /// </summary>
    public const float PlayerMaxHP = 200f;

    /// <summary>
    /// 갈증이 초당 줄어드는 양. 프리팹에 저장된 값(0.2) 대신 이 값을 쓴다.
    /// 0.2 일 때는 05 중간쯤 갈증이 0 이 되어 체력 자연회복이 멈췄고, 물 3병으로는 모자랐다(10/11 Meka 플레이).
    /// 0.1 이면 가득 찬 갈증이 약 17분 간다. 물 한 병(+40, Water.asset)은 약 7분.
    /// </summary>
    public const float ThirstDrainPerSecond = 0.1f;

    /// <summary>
    /// 적 총알 피해 배율.
    ///
    /// 폐허된 도시 스폰 지점에 가만히 서 있으면 7초에 78(초당 11)이 깎여
    /// 9초 만에 죽었다. 엄폐하고 반격할 틈을 주려고 줄였다.
    /// </summary>
    public const float EnemyBulletDamageScale = 0.6f;

    /// <summary>
    /// 적이 손에 든 총(로봇)의 피해 배율.
    ///
    /// 터렛은 거치형 중화기라 한 방이 아파도 되지만, 들고 쏘는 소총까지 같은 무게면
    /// 사방에서 맞는 쪽이 감당이 안 된다. 들고 쏘는 쪽을 더 가볍게 둔다.
    /// </summary>
    public const float RobotBulletDamageScale = 0.35f;

    /// <summary>
    /// 플레이어가 적에게서 받는 피해 전체에 한 번 더 곱하는 배율(총알·드론 자폭).
    ///
    /// 2026-10-08 사용자 "다른 적들의 공격이 너무 세다". 적마다 다른 경로(총알·자폭·근접)로
    /// 들어오는 피해를 한곳(PlayerStatus)에서 함께 줄인다. 독가스·화염 같은 지형 피해는 그대로.
    /// </summary>
    /// 2026-10-12 사용자 "HP 가 너무 안 닳는다" → 0.6 에서 1.0 으로(체감 약 1.7배), 같은 날 한 번 더 1.3배 → 1.3.
    public const float IncomingEnemyDamageScale = 5.2f;   // 10/12: 1.3 의 2배(2.6), 10/13: 또 2배

    public static float ScaleIncomingEnemyDamage(float damage) => damage * IncomingEnemyDamageScale * AetherLevel.IncomingDamageScale * AetherEncounterDirector.DamageScale;

    /// <summary>
    /// 터렛이 플레이어를 노리기 시작하는 거리(m).
    ///
    /// 씬에 34m 로 저장되어 있어서, 잔해에 묻혀 보이지도 않는 터렛이 총알만 보내 왔다.
    /// 쏘는 쪽이 눈에 보이는 거리까지 당긴다.
    /// 10/11 사용자 요청으로 22m → 40m(더 멀리 있는 플레이어도 쏜다). 가려진 터렛은 HasClearShot 이 막는다.
    /// 씬 저장값(34m)보다 커야 하므로 EnemyTurretController 는 이 값을 그대로 쓴다.
    /// </summary>
    public const float TurretDetectionRange = 40f;

    /// <summary>
    /// 터렛의 발사 간격(초).
    ///
    /// 0.65초였다. 터렛 넷이 쉬지 않고 쏘니 총성이 겹쳐 빗소리가 됐다.
    /// 한 발씩 띄엄띄엄 쏘면서 '뻥' 소리가 들리게 늘린다.
    /// </summary>
    public const float TurretFireInterval = 2.1f;

    /// <summary>
    /// 로봇의 최소 발사 간격(초).
    ///
    /// 씬의 로봇 13기가 발사 간격 0 으로 저장되어 있어 3연발을 간격 없이 쐈다.
    /// 연발 안에서도 이만큼은 띄운다.
    /// </summary>
    public const float MinRobotFireInterval = 0.4f;

    /// <summary>
    /// 발사 애니메이션 이벤트를 기다리는 한계 시간(초).
    ///
    /// 'Firing Rifle' 클립(35프레임, 30fps = 1.17초)의 사정은 이렇다.
    ///   0.25초 — 전환(blend)이 끝나 총을 다 들어 올린다
    ///   0.92초 — 상태가 빠져나간다 (ExitTime 78.57%)
    ///   1.00초 — 클립에 찍힌 발사 이벤트. 상태가 이미 끝난 뒤라 **영영 오지 않는다**
    ///
    /// 그래서 총알은 늘 이 타이머가 쏜다. 씬 값 0.25초는 총을 들어 올리는 도중이라
    /// '총 내린 채 발사'로 보였고, 1.5초로 미뤘더니 사격 모션이 끝난 뒤라
    /// '총만 들고 있는데 총알이 뜬금없이' 나왔다.
    /// 사격 모션 한가운데(0.25~0.92초 사이)에 쏘게 둔다.
    /// </summary>
    public const float MinFireEventFallback = 0.55f;

    /// <summary>
    /// 사격 동작('Firing Rifle')이 끝날 때까지 걸리는 시간(초).
    ///
    /// 이 동안에는 조준 자세 플래그(IsAimingIdle)를 내려 두어야 한다.
    /// 애니메이터의 'Any State → Aiming Idle' 전환은 IsAimingIdle 만 보고
    /// **아무 때나 끼어들기 때문에**(HasExitTime 0), 켜 둔 채로 두면
    /// 사격 동작이 시작하자마자 조준 자세로 끌려가 잘려 버린다.
    /// 클립 1.17초 중 0.92초(ExitTime 78.57%)에 상태가 끝나므로 그보다 조금 길게 잡는다.
    /// </summary>
    public const float FiringAnimationSeconds = 0.95f;

    /// <summary>
    /// 사격 동작(Firing Rifle) 안에서 총알이 나가는 지점(정규화 0~1).
    ///
    /// 타이머로만 쏘면 동작과 상관없이 시간이 되면 나간다. 연발 간격(0.4초)이
    /// 동작 길이(0.95초)보다 짧아서, 반복 재생되는 동작의 0.00·0.76 지점처럼
    /// 총을 들기 전이나 내리는 중에 총알이 나갔다. 이제는 애니메이터가 실제로
    /// 이 지점을 지날 때만 쏜다. 총을 다 든 0.21 과 상태가 끝나는 0.786 사이에 둔다.
    /// </summary>
    public const float RobotFirePhase = 0.42f;

    /// <summary>이 지점을 넘었으면 그 동작으로는 쏘지 않는다(총을 내리기 시작한다).</summary>
    public const float RobotFirePhaseLimit = 0.78f;

    /// <summary>연발 다음 탄은 사격 동작을 이 시점(초)부터 다시 튼다. 총은 이미 들려 있다.</summary>
    public const float RobotFireRestartSeconds = 0.15f;

    /// <summary>이 시간 안에 사격 구간에 들어가지 못하면 그 탄은 쏘지 않는다.</summary>
    public const float RobotFirePoseTimeout = 1.2f;

    /// <summary>쏠 차례에 벽에 막힌 채 이만큼(초) 지나면 로봇이 자리를 옮긴다.</summary>
    public const float RobotBlockedRepositionSeconds = 1.0f;

    /// <summary>자리를 옮길 때 플레이어 쪽으로 최대 이만큼(초) 달린다. 그 사이 트이면 바로 쏜다.</summary>
    public const float RobotRepositionSeconds = 2.0f;

    /// <summary>자리를 옮길 때 적어도 이만큼(초)은 실제로 움직인 뒤에 다시 쏠지 본다.</summary>
    public const float RobotRepositionMinSeconds = 0.8f;

    /// <summary>적 총알 한 발의 피해를 배율까지 먹여서 돌려준다. 최소 1 은 남긴다.</summary>
    public static int ScaleEnemyDamage(int rawDamage)
    {
        return Scale(rawDamage, EnemyBulletDamageScale);
    }

    /// <summary>손에 든 총(로봇)의 피해.</summary>
    public static int ScaleRobotDamage(int rawDamage)
    {
        return Scale(rawDamage, RobotBulletDamageScale);
    }

    private static int Scale(int rawDamage, float factor)
    {
        if (rawDamage <= 0)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(rawDamage * factor));
    }
}
