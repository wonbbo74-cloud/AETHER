using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 전체가 한꺼번에 쏘지 못하게 교통정리를 한다.
///
/// 적 열여덟이 저마다 자기 시계만 보고 쏘니, 총성이 겹쳐서 빗소리처럼 들리고
/// 피해도 감당이 안 됐다. (로봇 13기는 씬에 발사 간격이 0 으로 저장되어 있어
/// 3연발을 간격 없이 쐈다.)
///
/// 하나의 통을 모두가 나눠 쓰게 했더니, 수가 많은 로봇이 먼저 다 가져가서
/// 드론 두 기는 영영 쏘지 못했다. 그래서 종류별로 몫을 따로 둔다.
///
/// 같은 종류 안에서도 먼저 물어본 쪽이 가져가게 두었더니, 사거리 안 로봇 12기 중
/// 6기가 15초 동안 한 발도 못 쐈다(총만 겨누고 서 있는 적). 지금은 차례를 가장 오래
/// 기다린 쪽에게 먼저 준다.
/// </summary>
public static class AetherFireDirector
{
    /// <summary>적 종류. 종류마다 쏠 수 있는 몫이 따로 있다.</summary>
    public enum Kind
    {
        Robot,
        Drone,
    }

    /// <summary>종류별로 1초에 쏠 수 있는 발수.</summary>
    private const float RobotShotsPerSecond = 0.85f;
    private const float DroneShotsPerSecond = 0.35f;

    /// <summary>한꺼번에 몰아 쏠 수 있는 최대치. 교전이 완전히 밋밋해지지 않게 약간 남겨 둔다.</summary>
    private const float Burst = 2f;

    /// <summary>어떤 두 발 사이의 최소 간격(초). 이보다 가까우면 소리가 뭉쳐 빗소리가 된다.</summary>
    private const float MinimumGap = 0.18f;

    /// <summary>이 시간 안에 물어본 적은 '지금 차례를 기다리는 중'으로 본다.</summary>
    private const float WaitingWindow = 0.3f;

    private static readonly float[] tokens = { Burst, Burst };
    private static readonly float[] refillTimes = new float[2];
    private static float lastShotTime = -99f;

    // 종류별로, 누가 언제 마지막으로 물어봤고 언제 마지막으로 쐈는지.
    private static readonly Dictionary<int, float>[] lastAsked = { new Dictionary<int, float>(), new Dictionary<int, float>() };
    private static readonly Dictionary<int, float>[] lastGranted = { new Dictionary<int, float>(), new Dictionary<int, float>() };

    /// <summary>점검용 집계. 실제로 몇 발이 나갔고 몇 발을 걸렀는지.</summary>
    public static int RobotShots { get; private set; }
    public static int DroneShots { get; private set; }
    public static int TurretShots { get; private set; }
    public static int DeniedShots { get; private set; }

    public static int AllowedShots => RobotShots + DroneShots;

    public static void ResetCounters()
    {
        RobotShots = 0;
        DroneShots = 0;
        TurretShots = 0;
        DeniedShots = 0;
    }

    /// <summary>
    /// 다음 한 발은 무조건 통과시킨다. 점검 도구에서 '지금 쏴 봐'를 할 때 쓴다.
    /// 상한에 걸려 총알이 안 나오면 연출을 확인할 수가 없다.
    /// </summary>
    public static void AllowNextShot()
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            tokens[i] = Burst;
            refillTimes[i] = Time.time;
        }

        lastShotTime = -99f;
    }

    /// <summary>
    /// 터렛처럼 '한 발 한 발이 들려야 하는' 쪽. 상한을 무시하고 통과시킨다.
    /// </summary>
    public static void TakeImportantShot()
    {
        TurretShots++;
        lastShotTime = Time.time;
    }

    /// <summary>지금 쏴도 되는지 묻는다.</summary>
    public static bool TryShoot(Kind kind)
    {
        return TryShoot(kind, null);
    }

    /// <summary>지금 쏴도 되는지 묻는다. 누가 묻는지 알려 주면 차례를 공평하게 돌린다.</summary>
    public static bool TryShoot(Kind kind, Object shooter)
    {
        float now = Time.time;
        int slot = (int)kind;
        int id = shooter != null ? shooter.GetInstanceID() : 0;

        if (shooter != null)
            lastAsked[slot][id] = now;

        if (now - lastShotTime < MinimumGap)
        {
            DeniedShots++;
            return false;
        }

        Refill(slot, now, kind == Kind.Robot ? RobotShotsPerSecond : DroneShotsPerSecond);

        if (tokens[slot] < 1f)
        {
            DeniedShots++;
            return false;
        }

        if (shooter != null && SomeoneWaitedLonger(slot, id, now))
        {
            DeniedShots++;
            return false;
        }

        tokens[slot] -= 1f;
        lastShotTime = now;

        if (shooter != null)
            lastGranted[slot][id] = now;

        if (kind == Kind.Robot)
            RobotShots++;
        else
            DroneShots++;

        return true;
    }

    /// <summary>지금 기다리는 다른 적 가운데 나보다 더 오래 못 쏜 쪽이 있는지.</summary>
    private static bool SomeoneWaitedLonger(int slot, int id, float now)
    {
        float mine = lastGranted[slot].TryGetValue(id, out float granted) ? granted : float.NegativeInfinity;

        foreach (KeyValuePair<int, float> asked in lastAsked[slot])
        {
            if (asked.Key == id || now - asked.Value > WaitingWindow)
                continue;

            float theirs = lastGranted[slot].TryGetValue(asked.Key, out float t) ? t : float.NegativeInfinity;
            if (theirs < mine)
                return true;
        }

        return false;
    }

    private static void Refill(int slot, float now, float rate)
    {
        // 씬을 다시 불러오면 시간이 되감길 수 있다. 그때는 가득 채우고 다시 센다.
        if (now < refillTimes[slot])
        {
            tokens[slot] = Burst;
            refillTimes[slot] = now;
            lastAsked[slot].Clear();
            lastGranted[slot].Clear();
            return;
        }

        tokens[slot] = Mathf.Min(Burst, tokens[slot] + (now - refillTimes[slot]) * rate);
        refillTimes[slot] = now;
    }
}
