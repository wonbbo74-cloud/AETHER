using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 에테르 레벨 — 한 판 동안 쌓이는 성장.
///
/// HUD 오른쪽 위 AETHER LEVEL 칸은 고정값(2, 450/1000)만 보여 주고 아무 데서도 바뀌지 않았다.
/// 이제는 게임에서 한 일(처치·퍼즐·수집·제작)로 에테르를 모으고, 레벨이 오르면 실제로 강해진다.
///
/// 설계 (04 → 05 → 07 한 판 기준):
///   - 모을 수 있는 에테르는 대략 3,200. 05 에서 거의 다 싸우면 05 끝에 Lv4, 07 제작에서 Lv5(최대).
///     싸움을 피하고 퍼즐·수집 위주로 가면 Lv4 정도 — 어느 쪽으로 가도 성장이 보이게 했다.
///   - 04 퍼즐만 깨도 Lv2 로 05 에 들어간다(첫 레벨업을 일찍 맛보게).
///   - 05 의 적이 몰린 구간에서 레벨이 오르며 체력이 가득 차서, 밀리던 싸움을 뒤집는 순간이 생긴다.
///   - 연속 처치(6초 안)는 에테르 공명으로 배율이 붙는다(최대 ×2). 싸움을 끊지 않고 이어 가는 재미.
///   - 같은 적·같은 물건·같은 퍼즐은 한 판에 한 번만 준다. 죽고 다시 하거나 퍼즐을 다녀와 05 가
///     다시 불러져도 같은 적으로 다시 쌓을 수 없다(앞에서 모은 것은 그대로 남는다).
///
/// 레벨 효과(누적) — 숫자는 이 파일 한 곳에서만 고친다:
///   Lv2  최대 체력 +20, 공격력 +10%
///   Lv3  자연 회복 +4/초, 회복 시작 5 → 4초
///   Lv4  최대 체력 +40, 받는 피해 -10%
///   Lv5  공격력 +25%, 받는 피해 -15%
///   그리고 레벨마다 퍼즐 제한 시간 +5초(Lv1 기준).
/// </summary>
public static class AetherLevel
{
    public const int MaxLevel = 5;

    /// <summary>각 레벨에 도달하는 누적 에테르. [0] = Lv1.</summary>
    private static readonly int[] LevelThresholds = { 0, 300, 900, 1800, 3000 };

    // ---- 얻는 양 ----
    public const int RobotKillExp = 60;
    public const int TurretKillExp = 90;
    public const int DroneKillExp = 35;
    public const int PickupExp = 12;      // 05 에 34 개 → 약 400
    public const int BlueprintPickupExp = 80;
    public const int CraftExp = 150;
    public const int BlockPuzzleExp = 250;
    public const int CircuitPuzzleExp = 300;

    /// <summary>이 시간(초) 안에 다음 적을 쓰러뜨리면 공명이 이어진다.</summary>
    public const float ChainWindow = 6f;
    private const float ChainStep = 0.25f;
    private const float ChainMax = 2f;

    private static int totalExp;
    private static int chainCount;
    private static float lastKillTime = -999f;
    private static readonly HashSet<string> awarded = new HashSet<string>();
    private static bool pendingRefill;

    /// <summary>
    /// 몸이 없는 곳(퍼즐 화면)에서 레벨이 올랐으면 한 번 true. 씬이 열리고 체력 복원이 끝난 뒤
    /// PlayerStatus 가 불러서 가득 채운다.
    /// </summary>
    public static bool ConsumePendingRefill()
    {
        if (!pendingRefill) return false;
        pendingRefill = false;
        return true;
    }

    /// <summary>에테르를 얻었을 때: (얻은 양, 공명 배율, 이유).</summary>
    public static event Action<int, float, string> ExpGained;

    /// <summary>레벨이 올랐을 때: 새 레벨.</summary>
    public static event Action<int> LevelUp;

    public static int TotalExp => totalExp;
    public static int Level => LevelFor(totalExp);
    public static bool IsMaxLevel => Level >= MaxLevel;

    /// <summary>지금 레벨 안에서 모은 양.</summary>
    public static int ExpIntoLevel => IsMaxLevel ? 0 : totalExp - LevelThresholds[Level - 1];

    /// <summary>다음 레벨까지 이 레벨에서 모아야 하는 양.</summary>
    public static int ExpForNextLevel => IsMaxLevel ? 0 : LevelThresholds[Level] - LevelThresholds[Level - 1];

    public static float LevelProgress => IsMaxLevel ? 1f : Mathf.Clamp01((float)ExpIntoLevel / Mathf.Max(1, ExpForNextLevel));

    /// <summary>지금 공명 배율(연속 처치). 창이 지나면 1.</summary>
    public static float ChainMultiplier => Time.time - lastKillTime <= ChainWindow ? ChainFor(chainCount) : 1f;

    // ---- 레벨 효과 ----
    public static float MaxHPBonus => Level >= 4 ? 40f : Level >= 2 ? 20f : 0f;
    public static float DamageScale => Level >= 5 ? 1.25f : Level >= 2 ? 1.10f : 1f;
    public static float IncomingDamageScale => Level >= 5 ? 0.85f : Level >= 4 ? 0.90f : 1f;
    public static float RegenBonusPerSecond => Level >= 3 ? 4f : 0f;
    public static float RegenDelayCut => Level >= 3 ? 1f : 0f;
    public static float PuzzleBonusSeconds => (Level - 1) * 5f;

    /// <summary>끝 화면(작전 실패·To Be Continued)에 남기는 한 줄.</summary>
    public static string Summary()
    {
        return IsMaxLevel
            ? $"AETHER LV.{Level} (MAX)  ·  {TotalExp:N0} AE"
            : $"AETHER LV.{Level}  ·  {TotalExp:N0} AE";
    }

    /// <summary>레벨에 올라서 새로 생긴 효과를 한 줄로.</summary>
    public static string PerkText(int level)
    {
        switch (level)
        {
            case 2: return "최대 체력 +20 · 공격력 +10%";
            case 3: return "자연 회복 +4/초 · 회복 대기 -1초";
            case 4: return "최대 체력 +20 · 받는 피해 -10%";
            case 5: return "공격력 +25% · 받는 피해 -15%";
            default: return string.Empty;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // 도메인 리로드를 끈 에디터 플레이에서도 새로 시작한다.
        ResetRun();
    }

    /// <summary>새 판: 레벨 1, 에테르 0. AetherRunState.ResetRun 에서 부른다.</summary>
    public static void ResetRun()
    {
        totalExp = 0;
        chainCount = 0;
        lastKillTime = -999f;
        awarded.Clear();
        pendingRefill = false;
    }

    /// <summary>저장: 누적 에테르와 이미 받은 보상 목록.</summary>
    public static void Export(AetherSaveData data)
    {
        data.aetherExp = totalExp;
        data.aetherAwarded = new List<string>(awarded);
    }

    public static void Import(AetherSaveData data)
    {
        ResetRun();
        totalExp = Mathf.Max(0, data.aetherExp);
        foreach (string key in data.aetherAwarded)
            awarded.Add(key);
    }

    // ---- 얻는 곳 ----

    /// <summary>적을 쓰러뜨렸을 때. 같은 적은 한 판에 한 번만.</summary>
    public static void ReportKill(Component enemy, int baseExp, string reason)
    {
        if (enemy == null || !Grantable(KeyFor("kill", enemy)))
            return;

        // 공명: 바로 전 처치에서 6초 안이면 한 단계 오른다.
        chainCount = Time.time - lastKillTime <= ChainWindow ? chainCount + 1 : 0;
        lastKillTime = Time.time;
        float multiplier = ChainFor(chainCount);

        Add(Mathf.RoundToInt(baseExp * multiplier), multiplier, reason);
    }

    /// <summary>바닥 물건을 주웠을 때. 설계서(종이)는 더 많이.</summary>
    public static void ReportPickup(Component source, ItemData item)
    {
        if (source == null || !Grantable(KeyFor("pickup", source)))
            return;

        bool blueprint = IsBlueprint(item);
        Add(blueprint ? BlueprintPickupExp : PickupExp, 1f, blueprint ? "설계서 확보" : "모듈 회수");
    }

    /// <summary>파츠를 만들었을 때. 같은 파츠는 한 번만.</summary>
    public static void ReportCraft(ItemData part)
    {
        string id = part != null ? (string.IsNullOrEmpty(part.id) ? part.name : part.id) : "unknown";
        if (!Grantable("craft:" + id))
            return;

        Add(CraftExp, 1f, "파츠 제작");
    }

    /// <summary>퍼즐을 풀었을 때. 같은 퍼즐은 한 번만. 실제로 준 양을 돌려준다(이미 받았으면 0).</summary>
    public static int ReportPuzzle(string puzzleId, int amount, string reason)
    {
        if (amount <= 0 || !Grantable("puzzle:" + puzzleId))
            return 0;

        Add(amount, 1f, reason);
        return amount;
    }

    // ---- 내부 ----

    private static void Add(int amount, float multiplier, string reason)
    {
        if (amount <= 0)
            return;

        int before = Level;
        bool first = totalExp == 0;
        totalExp += amount;
        int after = Level;

        ExpGained?.Invoke(amount, multiplier, reason);
        AetherSave.MarkDirty();

        // 처음 에테르를 얻는 순간(보통 04 전력 박스) 한 번만 무엇인지 알려 준다.
        if (first && after == before)
            AetherObjectiveToast.Show("에테르 획득!\n처치·퍼즐·수집·제작으로 레벨업", 6f);

        for (int level = before + 1; level <= after; level++)
        {
            OnLevelUp(level, first);
            LevelUp?.Invoke(level);
        }
    }

    private static void OnLevelUp(int level, bool firstGain)
    {
        // 레벨이 오르면 지금 몸에 바로 반영하고 체력·실드를 가득 채운다.
        PlayerStatus status = FindActiveStatus();
        if (status != null)
            status.ApplyAetherLevel(true);
        else
            pendingRefill = true;   // 퍼즐 화면처럼 몸이 없는 곳에서 올랐으면 돌아가서 채운다.

        AetherAudio.Play2D("receive", 0.8f, 1f, 0f);
        // 첫 에테르로 바로 레벨이 오르면(04 전력 박스) 무엇인지도 같이 알려 준다.
        // 목표 칸은 작아서 짧게 쓴다(긴 글은 칸이 글자를 줄여 맞춘다).
        // 칸이 좁아 아무 데서나 줄이 꺾였다. 한 줄이 칸에 들어가도록 줄을 직접 나눈다.
        string howTo = firstGain ? "\n처치·퍼즐·수집·제작으로 레벨업" : string.Empty;
        AetherObjectiveToast.Show($"에테르 레벨 {level} 달성\n{PerkText(level)}{howTo}", firstGain ? 7f : 5f);
    }

    private static PlayerStatus FindActiveStatus()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player != null)
        {
            PlayerStatus own = player.GetComponentInParent<PlayerStatus>();
            if (own == null) own = player.GetComponentInChildren<PlayerStatus>();
            if (own != null) return own;
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
    }

    private static int LevelFor(int exp)
    {
        int level = 1;
        for (int i = 1; i < LevelThresholds.Length; i++)
            if (exp >= LevelThresholds[i]) level = i + 1;
        return Mathf.Min(level, MaxLevel);
    }

    private static float ChainFor(int chain) => Mathf.Min(ChainMax, 1f + ChainStep * chain);

    private static bool Grantable(string key) => awarded.Add(key);

    /// <summary>씬 이름 + 계층 경로(형제 순서 포함). 같은 씬을 다시 불러도 같은 값이 나온다.</summary>
    private static string KeyFor(string kind, Component c)
    {
        Transform t = c.transform;
        var path = new System.Text.StringBuilder();
        while (t != null)
        {
            path.Insert(0, "/" + t.GetSiblingIndex() + t.name);
            t = t.parent;
        }

        string scene = c.gameObject.scene.IsValid() ? c.gameObject.scene.name : SceneManager.GetActiveScene().name;
        return kind + ":" + scene + path;
    }

    private static bool IsBlueprint(ItemData item)
    {
        if (item == null) return false;
        string n = ((item.id ?? string.Empty) + " " + item.name + " " + (item.displayName ?? string.Empty)).ToLowerInvariant();
        return n.Contains("paper") || n.Contains("blueprint") || n.Contains("설계");
    }
}
