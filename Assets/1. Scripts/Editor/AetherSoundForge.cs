using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AETHER 효과음 제작기.
///
/// 프로젝트에 없던 소리(바닥별 발걸음, 로봇 금속 발, 드론 회전날개, EMP, 심장박동, 환경음 …)를
/// 물리적인 성분(발뒤꿈치 저음 + 접지 잡음 + 재질 공명)을 겹쳐 합성하고,
/// 이미 있는 좋은 음원(UI 클릭, 불)은 골라서 같은 폴더로 복사한다.
/// 결과는 Resources/AETHERAudio 에 들어가고 AetherAudio 가 이름_번호 로 묶어 불러 쓴다.
///
/// 같은 씨앗(seed)으로 만들므로 몇 번을 다시 돌려도 같은 소리가 나온다.
/// </summary>
public static class AetherSoundForge
{
    private const int SR = 48000;
    private const string OutDir = "Assets/Resources/AETHERAudio";

    [MenuItem("AETHER/사운드/효과음 전부 다시 만들기")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(OutDir);
        var made = new List<string>();

        // ---------- 발걸음(플레이어) : 바닥 재질 3종 × 8 ----------
        for (int v = 0; v < 8; v++)
        {
            made.Add(Save($"step_concrete_{v + 1}", Footstep(Surface.Concrete, 100 + v, false), 0.80f));
            made.Add(Save($"step_gravel_{v + 1}", Footstep(Surface.Gravel, 200 + v, false), 0.80f));
            made.Add(Save($"step_metal_{v + 1}", Footstep(Surface.Metal, 300 + v, false), 0.80f));
        }

        for (int v = 0; v < 3; v++)
        {
            made.Add(Save($"land_concrete_{v + 1}", Footstep(Surface.Concrete, 400 + v, true), 0.85f));
            made.Add(Save($"land_gravel_{v + 1}", Footstep(Surface.Gravel, 410 + v, true), 0.85f));
            made.Add(Save($"land_metal_{v + 1}", Footstep(Surface.Metal, 420 + v, true), 0.85f));
            made.Add(Save($"jump_{v + 1}", Jump(430 + v), 0.6f));
            made.Add(Save($"crouch_{v + 1}", ClothRustle(440 + v), 0.5f));
        }

        // ---------- 로봇 ----------
        for (int v = 0; v < 8; v++)
            made.Add(Save($"robot_step_{v + 1}", RobotStep(500 + v), 0.85f));
        for (int v = 0; v < 3; v++)
            made.Add(Save($"robot_servo_{v + 1}", RobotServo(520 + v), 0.6f));
        for (int v = 0; v < 2; v++)
            made.Add(Save($"robot_alert_{v + 1}", RobotAlert(530 + v), 0.6f));
        for (int v = 0; v < 2; v++)
            made.Add(Save($"robot_death_{v + 1}", RobotDeath(540 + v), 0.9f));

        // ---------- 드론·터렛 ----------
        made.Add(Save("drone_hum", DroneHum(600), 0.55f));
        made.Add(Save("drone_alert", DroneAlert(601), 0.6f));
        made.Add(Save("drone_death", DroneDeath(602), 0.9f));
        made.Add(Save("turret_death", TurretDeath(603), 0.85f));

        // ---------- 플레이어 상태 ----------
        made.Add(Save("heartbeat", Heartbeat(700), 0.9f));
        made.Add(Save("player_death", PlayerDeath(701), 0.85f));
        made.Add(Save("respawn", Respawn(702), 0.6f));

        // ---------- 근접·능력 ----------
        for (int v = 0; v < 4; v++)
        {
            made.Add(Save($"punch_swing_{v + 1}", PunchSwing(800 + v), 0.6f));
            made.Add(Save($"punch_hit_{v + 1}", PunchHit(810 + v), 0.85f));
        }
        made.Add(Save("punch_charge", ChargeLoop(820, 220f), 0.45f));
        made.Add(Save("power_punch", PowerPunch(821), 0.95f));
        made.Add(Save("emp_throw", EmpThrow(830), 0.6f));
        made.Add(Save("emp_blast", EmpBlast(831), 0.95f));
        made.Add(Save("superjump_charge", ChargeLoop(840, 300f), 0.45f));
        made.Add(Save("superjump_release", SuperJumpRelease(841), 0.85f));
        made.Add(Save("shield_on", ShieldSweep(850, true), 0.6f));
        made.Add(Save("shield_off", ShieldSweep(851, false), 0.6f));
        made.Add(Save("shield_hum", ShieldHum(852), 0.35f));
        made.Add(Save("breaker_hit", BreakerHit(860), 0.95f));
        made.Add(Save("weapon_draw", WeaponDraw(870), 0.6f));
        made.Add(Save("firemode", SmallClick(871, 3400f), 0.5f));

        // ---------- 세계 ----------
        made.Add(Save("paper_open", Paper(900, 0.42f), 0.55f));
        made.Add(Save("paper_close", Paper(901, 0.30f), 0.5f));
        made.Add(Save("transition", Whoosh(902, 1.1f), 0.5f));
        made.Add(Save("target_hit", Bell(903), 0.7f));
        made.Add(Save("debris", Debris(904), 0.9f));
        made.Add(Save("light_buzz", LightBuzz(905), 0.3f));
        for (int v = 0; v < 3; v++)
        {
            made.Add(Save($"spark_{v + 1}", Sparks(910 + v, 0.28f), 0.6f));
            made.Add(Save($"drip_{v + 1}", Drip(920 + v), 0.45f));
            made.Add(Save($"creak_{v + 1}", Creak(930 + v), 0.5f));
            made.Add(Save($"distant_debris_{v + 1}", DistantDebris(940 + v), 0.5f));
        }

        // ---------- 2차 점검에서 찾은 빈 곳 ----------
        made.Add(Save("door_open", SwingDoor(960), 0.8f));                 // 04 벙커 양문
        made.Add(Save("door_slide_open", SlideDoor(961, true), 0.75f));    // 엘리베이터 문
        made.Add(Save("door_slide_close", SlideDoor(962, false), 0.75f));
        made.Add(Save("elevator_move", ElevatorHum(963), 0.5f));
        made.Add(Save("elevator_ding", Ding(964), 0.6f));
        made.Add(Save("puzzle_clear", Arpeggio(965), 0.6f));
        made.Add(Save("hold_loop", ChargeLoop(966, 330f), 0.3f));
        made.Add(Save("eat", Eat(967), 0.6f));
        made.Add(Save("drink", Drink(968), 0.6f));
        made.Add(Save("energy_pack", EnergyPack(969), 0.65f));
        for (int v = 0; v < 4; v++)
        {
            made.Add(Save($"robot_hit_{v + 1}", RobotHit(970 + v), 0.8f));
            made.Add(Save($"bullet_impact_concrete_{v + 1}", BulletImpact(980 + v, false), 0.75f));
            made.Add(Save($"bullet_impact_metal_{v + 1}", BulletImpact(990 + v, true), 0.75f));
        }

        // ---------- 환경음(스테레오, 이음매 없는 반복) ----------
        made.Add(SaveStereo("amb_city", AmbCity(1000), 0.5f));
        made.Add(SaveStereo("amb_bunker", AmbBunker(1001), 0.45f));
        made.Add(SaveStereo("amb_lab", AmbLab(1002), 0.45f));
        made.Add(SaveStereo("amb_parts", AmbParts(1003), 0.4f));

        // ---------- 기존 음원 골라 쓰기 ----------
        const string Ui = "Assets/10. DownloadAssets/SFX/SCI-FI_UI_SFX_PACK/";
        const string Pz = "Assets/10. DownloadAssets/PuzzleSound/Futuristic Puzzle Game Sound Effects Pack/Audio/WAV_Game_Futuristic_Puzzle_";
        // 원본마다 음량이 0.13~1.0 으로 제각각이고 일부는 찌그러져(클리핑) 있어,
        // 읽어서 같은 최대치로 맞추고 끝을 부드럽게 닫아 다시 저장한다.
        Copy(Ui + "Clicks/Click_Mid-High.wav", "ui_hover", made, 0.35f);
        Copy(Ui + "Clicks/Click_1.wav", "ui_click", made, 0.55f);
        Copy(Ui + "Click Combos/Click_Combo_2.wav", "ui_confirm", made, 0.6f);
        Copy(Ui + "Clicks/Click_Pitched_Down.wav", "ui_back", made, 0.5f);
        Copy(Ui + "Rings/Ring_Pitched_Up.wav", "craft_success", made, 0.7f);
        Copy(Ui + "Click Combos/Click_Combo_4.wav", "equip", made, 0.65f);
        Copy(Ui + "Glitches/Glitch_3.wav", "ui_error", made, 0.5f);
        Copy(Ui + "Clicks/Click_Scoop_Up.wav", "chat_open", made, 0.45f);
        Copy(Pz + "Item_Collect_Sound_1.wav", "receive", made, 0.6f);
        Copy(Pz + "Piece_Pick-Up_Sound_1.wav", "drag_pick", made, 0.5f);
        Copy(Pz + "Piece_Place_Sound_1.wav", "drag_snap", made, 0.6f);
        Copy(Pz + "Piece_Remove_Sound_1.wav", "drag_cancel", made, 0.45f);
        Copy("Assets/10. DownloadAssets/Vefects/Free Fire VFX/Audio/SFX_FireMedium_L.wav", "fire_loop", made, 0f);
        Copy("Assets/_AETHER_Project/09_Audio/5. Sound/ExplosionSound.mp3", "explosion", made, 0.9f);

        // 키로 소리를 부르는 AudioManager 가 빈 목록으로 만들어지는 경우(씬을 바로 실행)의 대체음. 키 이름 그대로.
        const string Snd = "Assets/_AETHER_Project/09_Audio/5. Sound/";
        Copy(Snd + "Pickup.mp3", "pickup", made, 0.5f);
        Copy(Snd + "Inventory_Open.mp3", "inventory_Open", made, 0.45f);
        Copy(Snd + "Inventory_Close.wav", "inventory_Close", made, 0.45f);
        Copy(Snd + "Select.mp3", "select", made, 0.45f);
        Copy(Snd + "Button_Click_Sound.wav", "btn_Click", made, 0.5f);
        Copy(Snd + "TileRotate.wav", "tile_rotate", made, 0.5f);

        // 원본 'Air FX' 는 최대치 0.01 로 거의 들리지 않아 직접 만든다.
        made.Add(Save("ui_open", UiSweep(950, true), 0.4f));
        made.Add(Save("ui_close", UiSweep(951, false), 0.4f));

        // 이번에 만들지 않은 옛 파일(변형 수를 줄였을 때의 name_N 등)은 지운다. 남아 있으면 Bank() 가 계속 섞어 쓴다.
        var keep = new HashSet<string>(made);
        foreach (string file in Directory.GetFiles(OutDir))
        {
            if (file.EndsWith(".meta")) continue;
            string n = Path.GetFileNameWithoutExtension(file);
            if (!keep.Contains(n))
            {
                AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
                Debug.Log($"[AetherSoundForge] 옛 파일 삭제: {n}");
            }
        }

        AssetDatabase.Refresh();
        ApplyImportSettings();
        Debug.Log($"[AetherSoundForge] {made.Count}개 효과음을 만들었습니다 → {OutDir}");
    }

    // =====================================================================
    // 바닥 재질별 발걸음
    // =====================================================================
    private enum Surface { Concrete, Gravel, Metal }

    private static float[] Footstep(Surface s, int seed, bool landing)
    {
        var r = new System.Random(seed);
        float len = landing ? 0.75f : 0.40f;
        float[] o = New(len);
        float weight = landing ? 1.6f : 1f;

        // 발뒤꿈치(heel) → 앞꿈치(toe) 두 번 닿는다. 착지는 거의 동시에 두 발.
        float toeAt = landing ? Rf(r, 0.018f, 0.03f) : Rf(r, 0.045f, 0.075f);
        Contact(o, r, s, 0f, 1f * weight, landing);
        Contact(o, r, s, toeAt, Rf(r, 0.45f, 0.6f) * weight, landing);

        // 신발 밑창이 바닥을 쓸고 지나가는 소리
        AddNoiseBurst(o, r, Rf(r, 0.015f, 0.03f), Rf(r, 0.06f, 0.10f), BP(Rf(r, 900f, 1500f), 0.7f), 0.10f * weight, Shape.Hann);

        if (s == Surface.Gravel)
        {
            // 자갈·잔해: 작은 알갱이가 흩어지는 소리
            int grains = (int)(Rf(r, 28, 46) * (landing ? 1.8f : 1f));
            for (int i = 0; i < grains; i++)
            {
                float at = (float)Math.Pow(r.NextDouble(), 1.6) * (landing ? 0.32f : 0.20f);
                AddNoiseBurst(o, r, at, Rf(r, 0.002f, 0.006f), BP(Rf(r, 2200f, 6800f), 2.2f),
                    Rf(r, 0.04f, 0.22f) * (float)Math.Exp(-at / 0.09f), Shape.Decay);
            }
        }

        if (s == Surface.Metal)
        {
            // 철판·격자: 발이 닿을 때마다 판이 울린다(비조화 공명)
            float[] modes = { 287f, 521f, 846f, 1243f, 1712f, 2396f, 3150f };
            AddModes(o, r, 0f, modes, Rf(r, 0.92f, 1.08f), 0.30f * weight, 0.06f, 0.30f);
            AddModes(o, r, toeAt, modes, Rf(r, 0.92f, 1.08f), 0.16f * weight, 0.05f, 0.22f);
            // 격자 덜그럭
            for (int i = 0; i < 6; i++)
                AddNoiseBurst(o, r, Rf(r, 0.0f, 0.09f), 0.004f, BP(Rf(r, 900f, 1900f), 3f), Rf(r, 0.04f, 0.10f), Shape.Decay);
        }

        Fade(o, 0.001f, 0.03f);
        return o;
    }

    private static void Contact(float[] o, System.Random r, Surface s, float at, float gain, bool landing)
    {
        // 저음 쿵: 몸무게가 실리는 소리. 주파수가 떨어지며 사라진다.
        float f0 = (s == Surface.Metal ? 95f : 110f) * Rf(r, 0.9f, 1.1f) * (landing ? 0.72f : 1f);
        float tau = (landing ? 0.045f : 0.022f) * Rf(r, 0.85f, 1.15f);
        AddSweep(o, at, f0, f0 * 0.5f, 0.04f, tau, 0.55f * gain);

        // 접지 순간의 짧은 '탁'
        float clickF = s == Surface.Metal ? Rf(r, 2600f, 3400f) : Rf(r, 1700f, 2600f);
        AddNoiseBurst(o, r, at, 0.012f, BP(clickF, 0.9f), 0.32f * gain, Shape.Decay);

        // 몸통(저역 잡음)
        float bodyLp = s == Surface.Gravel ? 900f : 650f;
        AddNoiseBurst(o, r, at, landing ? 0.12f : 0.05f, LP(bodyLp, 0.7f), 0.55f * gain, Shape.Decay, pink: true);
    }

    private static float[] Jump(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.35f);
        AddSweepNoise(o, r, 0f, 0.22f, 600f, 1800f, 0.8f, 0.35f);
        AddNoiseBurst(o, r, 0f, 0.05f, BP(1200f, 0.8f), 0.18f, Shape.Hann);
        AddSweep(o, 0f, 120f, 70f, 0.03f, 0.018f, 0.25f);
        Fade(o, 0.002f, 0.05f);
        return o;
    }

    private static float[] ClothRustle(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.4f);
        int n = r.Next(4, 7);
        for (int i = 0; i < n; i++)
            AddNoiseBurst(o, r, Rf(r, 0f, 0.25f), Rf(r, 0.03f, 0.08f), BP(Rf(r, 1400f, 3600f), 0.9f), Rf(r, 0.1f, 0.25f), Shape.Hann);
        Fade(o, 0.002f, 0.05f);
        return o;
    }

    // =====================================================================
    // 로봇
    // =====================================================================
    private static readonly float[] RobotModes = { 176f, 389f, 655f, 1012f, 1490f, 2205f, 3020f };

    private static float[] RobotStep(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.7f);
        // 무거운 금속 발: 낮은 쿵 + 몸통
        AddSweep(o, 0f, 72f * Rf(r, 0.9f, 1.1f), 36f, 0.06f, 0.07f, 1.0f);
        AddNoiseBurst(o, r, 0f, 0.09f, LP(420f, 0.7f), 0.7f, Shape.Decay, pink: true);
        // 발바닥 금속판 두 번 '철컥'
        AddModes(o, r, 0f, RobotModes, Rf(r, 0.9f, 1.1f), 0.45f, 0.08f, 0.35f);
        AddModes(o, r, Rf(r, 0.018f, 0.035f), RobotModes, Rf(r, 0.9f, 1.1f), 0.22f, 0.05f, 0.2f);
        AddNoiseBurst(o, r, 0f, 0.008f, BP(Rf(r, 2800f, 3600f), 1f), 0.35f, Shape.Decay);
        // 유압 피식
        AddNoiseBurst(o, r, Rf(r, 0.035f, 0.06f), 0.13f, HP(3800f, 0.7f), 0.07f, Shape.AttackDecay);
        // 관절 모터 윙
        AddServo(o, r, Rf(r, 0.05f, 0.09f), 0.24f, Rf(r, 470f, 620f), 1.25f, 0.06f);
        Fade(o, 0.001f, 0.06f);
        return o;
    }

    private static float[] RobotServo(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.55f);
        AddServo(o, r, 0f, 0.48f, Rf(r, 300f, 380f), 1.7f, 0.35f);
        // 기어 맞물림 거칠음
        for (int i = 0; i < 40; i++)
            AddNoiseBurst(o, r, i * 0.011f, 0.003f, BP(Rf(r, 1500f, 2600f), 2f), 0.03f * Hann01(i / 40f), Shape.Decay);
        Fade(o, 0.01f, 0.06f);
        return o;
    }

    private static float[] RobotAlert(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.45f);
        float a = seed % 2 == 0 ? 1320f : 1175f;
        float b = seed % 2 == 0 ? 1760f : 1568f;
        AddTone(o, 0f, 0.085f, a, 7, 0.32f);
        AddTone(o, 0.11f, 0.12f, b, 7, 0.32f);
        AddServo(o, r, 0.02f, 0.3f, 520f, 1.2f, 0.06f);
        Filter(o, LP(6500f, 0.7f));
        Fade(o, 0.002f, 0.06f);
        return o;
    }

    private static float[] RobotDeath(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(2.0f);
        // 전원이 꺼지며 내려가는 모터음
        float[] pd = New(2.0f);
        AddSaw(pd, 0f, 1.3f, 260f * Rf(r, 0.9f, 1.1f), 32f, 10, 0.35f, true);
        Filter(pd, LP(1800f, 0.7f));
        Mix(o, pd, 1f);
        // 처음 0.35초 합선 지직
        for (int i = 0; i < 26; i++)
            AddNoiseBurst(o, r, Rf(r, 0f, 0.35f), Rf(r, 0.003f, 0.012f), HP(2500f, 0.7f), Rf(r, 0.08f, 0.3f), Shape.Decay);
        // 무너지며 부딪히는 금속
        AddModes(o, r, 0.36f, RobotModes, 0.85f, 0.7f, 0.1f, 0.5f);
        AddModes(o, r, 0.63f, RobotModes, 0.78f, 0.55f, 0.1f, 0.45f);
        AddModes(o, r, 0.80f, RobotModes, 1.05f, 0.35f, 0.08f, 0.35f);
        AddSweep(o, 0.63f, 62f, 30f, 0.12f, 0.12f, 1.0f);
        AddNoiseBurst(o, r, 0.63f, 0.18f, LP(380f, 0.7f), 0.7f, Shape.Decay, pink: true);
        for (int i = 0; i < 22; i++)
            AddNoiseBurst(o, r, 0.63f + Rf(r, 0f, 0.5f), 0.004f, BP(Rf(r, 1800f, 5200f), 2f), Rf(r, 0.03f, 0.12f), Shape.Decay);
        Fade(o, 0.001f, 0.15f);
        return o;
    }

    // =====================================================================
    // 드론·터렛
    // =====================================================================
    private static float[] DroneHum(int seed)
    {
        var r = new System.Random(seed);
        const float len = 2f; // 모든 주파수가 2초에 정수 주기 → 이음매 없음
        float[] o = New(len);
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float blade = 1f - 0.28f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 29f * t));
            float s = 0f;
            for (int h = 1; h <= 12; h++)
            {
                s += Mathf.Sin(2f * Mathf.PI * 118f * h * t + h) / h;
                s += 0.6f * Mathf.Sin(2f * Mathf.PI * 123f * h * t + h * 2f) / h;
            }
            o[i] = s * 0.12f * blade;
        }
        float[] air = NoiseLoop(r, len, BP(950f, 0.5f), 0.4f);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            o[i] += air[i] * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 29f * t));
        }
        // 끝에 필터를 걸면 반복 이음매가 튄다(검증 0.19). 고조파가 1.5kHz 까지라 필터가 필요 없다.
        return o;
    }

    private static float[] DroneAlert(int seed)
    {
        float[] o = New(0.5f);
        AddChirp(o, 0f, 0.14f, 900f, 1800f, 0.35f);
        AddChirp(o, 0.18f, 0.14f, 900f, 1800f, 0.35f);
        Filter(o, LP(7000f, 0.7f));
        Fade(o, 0.002f, 0.05f);
        return o;
    }

    private static float[] DroneDeath(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.6f);
        AddNoiseBurst(o, r, 0f, 0.06f, HP(1500f, 0.7f), 0.9f, Shape.Decay);
        AddSweep(o, 0f, 140f, 50f, 0.2f, 0.12f, 0.8f);
        for (int i = 0; i < 18; i++)
            AddNoiseBurst(o, r, Rf(r, 0f, 0.45f), Rf(r, 0.003f, 0.01f), HP(2800f, 0.7f), Rf(r, 0.1f, 0.3f), Shape.Decay);
        float[] fall = New(1.6f);
        AddSaw(fall, 0.05f, 0.9f, 700f, 120f, 6, 0.15f, true);
        Filter(fall, LP(2500f, 0.7f));
        Mix(o, fall, 1f);
        AddModes(o, r, 0.95f, new[] { 420f, 910f, 1530f, 2290f, 3310f }, 1f, 0.5f, 0.06f, 0.3f);
        AddNoiseBurst(o, r, 0.95f, 0.08f, LP(600f, 0.7f), 0.5f, Shape.Decay, pink: true);
        Fade(o, 0.001f, 0.15f);
        return o;
    }

    private static float[] TurretDeath(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.5f);
        for (int i = 0; i < 30; i++)
            AddNoiseBurst(o, r, Rf(r, 0f, 0.6f), Rf(r, 0.003f, 0.015f), HP(2200f, 0.7f), Rf(r, 0.1f, 0.35f) * (1f - i / 40f), Shape.Decay);
        float[] pd = New(1.5f);
        AddSaw(pd, 0.1f, 1.1f, 340f, 40f, 8, 0.3f, true);
        Filter(pd, LP(2000f, 0.7f));
        Mix(o, pd, 1f);
        AddModes(o, r, 0.05f, new[] { 230f, 540f, 980f, 1610f, 2480f }, 1f, 0.4f, 0.1f, 0.4f);
        Fade(o, 0.001f, 0.15f);
        return o;
    }

    // =====================================================================
    // 플레이어 상태
    // =====================================================================
    private static float[] Heartbeat(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.0f); // 반복 재생, 빠르기는 실행 중 pitch 로
        Beat(o, r, 0f, 55f, 1f);
        Beat(o, r, 0.27f, 64f, 0.62f);
        Filter(o, LP(240f, 0.7f));
        Fade(o, 0.001f, 0.02f);
        return o;
    }

    private static void Beat(float[] o, System.Random r, float at, float f, float g)
    {
        AddSweep(o, at, f, f * 0.75f, 0.05f, 0.055f, g);
        AddNoiseBurst(o, r, at, 0.06f, LP(150f, 0.7f), 0.5f * g, Shape.AttackDecay, pink: true);
    }

    private static float[] PlayerDeath(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(3.4f);
        AddSweep(o, 0f, 70f, 28f, 0.3f, 0.22f, 1f);
        AddNoiseBurst(o, r, 0f, 0.3f, LP(300f, 0.7f), 0.6f, Shape.Decay, pink: true);
        // 귀울림(이명): 높고 가는 사인, 서서히 들어왔다 사라진다
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float env = Mathf.Clamp01((t - 0.05f) / 0.25f) * Mathf.Exp(-(t - 0.05f) / 1.4f);
            if (t < 0.05f) env = 0f;
            o[i] += Mathf.Sin(2f * Mathf.PI * 3600f * t) * 0.055f * env;
        }
        Fade(o, 0.001f, 0.3f);
        return o;
    }

    private static float[] Respawn(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.4f);
        AddSweepNoise(o, r, 0f, 1.1f, 300f, 4000f, 0.7f, 0.35f);
        float[] shim = New(1.4f);
        AddSaw(shim, 0f, 1.2f, 220f, 880f, 5, 0.12f, false);
        Filter(shim, LP(3500f, 0.7f));
        Mix(o, shim, 1f);
        Fade(o, 0.15f, 0.25f);
        return o;
    }

    // =====================================================================
    // 근접·능력
    // =====================================================================
    private static float[] PunchSwing(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.28f);
        AddSweepNoise(o, r, 0f, Rf(r, 0.13f, 0.18f), Rf(r, 350f, 500f), Rf(r, 2200f, 3000f), 1.1f, 0.6f);
        Fade(o, 0.003f, 0.04f);
        return o;
    }

    private static float[] PunchHit(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.3f);
        AddSweep(o, 0f, Rf(r, 110f, 140f), 55f, 0.04f, 0.035f, 1f);
        AddNoiseBurst(o, r, 0f, 0.035f, LP(1600f, 0.7f), 0.8f, Shape.Decay, pink: true);
        AddNoiseBurst(o, r, 0f, 0.006f, BP(Rf(r, 2800f, 3600f), 1f), 0.5f, Shape.Decay);
        // 금속 몸통을 쳤을 때의 둔탁한 울림
        AddModes(o, r, 0f, RobotModes, Rf(r, 1.1f, 1.3f), 0.18f, 0.04f, 0.15f);
        Fade(o, 0.001f, 0.05f);
        return o;
    }

    // Shiri 잽 타격음 '퍽'(10/12 사용자 "좀 더 퍽퍽 소리"): 금속 울림 없이 낮게 떨어지는 몸통 쿵 + 둔탁한 살 때리는 소리.
    [MenuItem("AETHER/사운드/쉬리 타격음(퍽)만 만들기")]
    public static void MakePunchThud()
    {
        Directory.CreateDirectory(OutDir);
        for (int v = 0; v < 4; v++)
            Save($"punch_thud_{v + 1}", PunchThud(830 + v), 0.95f);
        AssetDatabase.Refresh();
    }

    private static float[] PunchThud(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.28f);
        AddSweep(o, 0f, Rf(r, 95f, 115f), 42f, 0.09f, 0.07f, 1f);                            // 몸통에 들어가는 쿵
        AddNoiseBurst(o, r, 0f, 0.055f, LP(Rf(r, 700f, 900f), 0.7f), 1f, Shape.Decay, pink: true);   // 퍽
        AddNoiseBurst(o, r, 0f, 0.012f, BP(Rf(r, 1100f, 1500f), 0.8f), 0.45f, Shape.Decay);   // 맞닿는 순간의 탁
        // 더 크게 들리도록(10/13 사용자 "훨씬 크게") 부드럽게 눌러 꽉 채운다: 최대치는 같아도 체감 음량이 커진다.
        float peak = 1e-6f;
        foreach (float s in o) peak = Mathf.Max(peak, Mathf.Abs(s));
        const float drive = 4f;
        for (int i = 0; i < o.Length; i++)
            o[i] = (float)System.Math.Tanh(o[i] / peak * drive) / (float)System.Math.Tanh(drive);
        Fade(o, 0.0005f, 0.06f);
        return o;
    }

    private static float[] PowerPunch(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.4f);
        AddSweep(o, 0f, 90f, 32f, 0.15f, 0.15f, 1f);
        AddNoiseBurst(o, r, 0f, 0.12f, LP(900f, 0.7f), 0.9f, Shape.Decay, pink: true);
        AddModes(o, r, 0f, new[] { 140f, 330f, 560f, 870f, 1290f, 1880f }, 1f, 0.7f, 0.15f, 0.8f);
        for (int i = 0; i < 30; i++)
            AddNoiseBurst(o, r, Rf(r, 0.02f, 0.6f), 0.004f, BP(Rf(r, 1500f, 5000f), 2f), Rf(r, 0.03f, 0.12f), Shape.Decay);
        Fade(o, 0.001f, 0.2f);
        return o;
    }

    private static float[] ChargeLoop(int seed, float f0)
    {
        var r = new System.Random(seed);
        const float len = 1f;
        float[] o = New(len);
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float s = 0f;
            for (int h = 1; h <= 6; h++)
                s += Mathf.Sin(2f * Mathf.PI * f0 * h * t + 0.3f * Mathf.Sin(2f * Mathf.PI * 6f * t)) / (h * h);
            o[i] = s * 0.3f * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 8f * t));
        }
        float[] air = NoiseLoop(r, len, HP(3000f, 0.7f), 0.06f);
        for (int i = 0; i < n; i++) o[i] += air[i];
        return o;
    }

    private static float[] EmpThrow(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.4f);
        AddSweepNoise(o, r, 0.02f, 0.2f, 500f, 2400f, 1f, 0.45f);
        AddModes(o, r, 0f, new[] { 2450f, 4100f, 5900f }, 1f, 0.25f, 0.01f, 0.03f);
        AddChirp(o, 0.05f, 0.25f, 1200f, 2600f, 0.06f);
        Fade(o, 0.001f, 0.05f);
        return o;
    }

    private static float[] EmpBlast(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(2.0f);
        AddNoiseBurst(o, r, 0f, 0.02f, HP(1800f, 0.7f), 1f, Shape.Decay);
        AddSweep(o, 0f, 58f, 28f, 0.4f, 0.32f, 1f);
        AddNoiseBurst(o, r, 0f, 0.35f, LP(260f, 0.7f), 0.5f, Shape.Decay, pink: true);
        // 전기 아크: 지직거림이 점점 잦아든다
        for (int i = 0; i < 70; i++)
        {
            float at = (float)Math.Pow(r.NextDouble(), 1.8) * 1.4f;
            AddNoiseBurst(o, r, at, Rf(r, 0.002f, 0.014f), HP(Rf(r, 1800f, 4000f), 0.7f), Rf(r, 0.1f, 0.4f) * (float)Math.Exp(-at / 0.7f), Shape.Decay);
        }
        AddChirp(o, 0.02f, 0.55f, 2400f, 180f, 0.18f);
        Fade(o, 0.001f, 0.2f);
        return o;
    }

    private static float[] SuperJumpRelease(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.8f);
        AddSweep(o, 0f, 95f, 40f, 0.08f, 0.08f, 1f);
        AddSweepNoise(o, r, 0f, 0.45f, 1800f, 400f, 0.7f, 0.6f);
        AddNoiseBurst(o, r, 0f, 0.04f, HP(2500f, 0.7f), 0.3f, Shape.Decay);
        Fade(o, 0.001f, 0.1f);
        return o;
    }

    private static float[] ShieldSweep(int seed, bool on)
    {
        var r = new System.Random(seed);
        float[] o = New(0.6f);
        float a = on ? 200f : 760f, b = on ? 760f : 180f;
        AddSaw(o, 0f, 0.5f, a, b, 6, 0.25f, !on);
        Filter(o, LP(4000f, 0.7f));
        AddSweepNoise(o, r, 0f, 0.4f, on ? 1500f : 5000f, on ? 6000f : 1200f, 0.8f, 0.2f);
        Fade(o, on ? 0.01f : 0.002f, 0.08f);
        return o;
    }

    private static float[] ShieldHum(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1f);
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            o[i] = (Mathf.Sin(2f * Mathf.PI * 160f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 321f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 482f * t))
                   * 0.25f * (0.9f + 0.1f * Mathf.Sin(2f * Mathf.PI * 5f * t));
        }
        float[] air = NoiseLoop(r, 1f, BP(4200f, 0.8f), 0.05f);
        for (int i = 0; i < n; i++) o[i] += air[i];
        return o;
    }

    private static float[] BreakerHit(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.5f);
        AddSweep(o, 0f, 75f, 28f, 0.2f, 0.18f, 1f);
        AddNoiseBurst(o, r, 0f, 0.16f, LP(700f, 0.7f), 1f, Shape.Decay, pink: true);
        AddModes(o, r, 0f, new[] { 110f, 260f, 470f, 760f, 1140f, 1650f, 2300f }, 1f, 0.8f, 0.15f, 0.9f);
        for (int i = 0; i < 45; i++)
            AddNoiseBurst(o, r, Rf(r, 0.03f, 0.9f), 0.004f, BP(Rf(r, 1200f, 5200f), 2f), Rf(r, 0.03f, 0.14f), Shape.Decay);
        Fade(o, 0.001f, 0.25f);
        return o;
    }

    private static float[] WeaponDraw(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.35f);
        AddNoiseBurst(o, r, 0f, 0.08f, BP(1800f, 0.8f), 0.15f, Shape.Hann);
        AddModes(o, r, 0.06f, new[] { 2900f, 4700f, 6300f }, 1f, 0.35f, 0.008f, 0.02f);
        AddModes(o, r, 0.17f, new[] { 2500f, 4100f, 5800f }, 1f, 0.45f, 0.01f, 0.025f);
        AddNoiseBurst(o, r, 0.17f, 0.01f, LP(1500f, 0.7f), 0.3f, Shape.Decay);
        Fade(o, 0.001f, 0.04f);
        return o;
    }

    private static float[] SmallClick(int seed, float f)
    {
        var r = new System.Random(seed);
        float[] o = New(0.12f);
        AddModes(o, r, 0f, new[] { f, f * 1.62f, f * 2.3f }, 1f, 0.5f, 0.006f, 0.015f);
        AddNoiseBurst(o, r, 0f, 0.004f, HP(2500f, 0.7f), 0.3f, Shape.Decay);
        Fade(o, 0.0005f, 0.02f);
        return o;
    }

    // =====================================================================
    // 세계
    // =====================================================================
    // ---------- 2차 점검 추가분 ----------
    private static float[] SwingDoor(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(2.2f);
        // 걸쇠가 풀리는 철컥 → 무거운 철문이 경첩에서 끼익 → 끝까지 열려 쿵
        AddModes(o, r, 0f, new[] { 620f, 1430f, 2610f, 3900f }, 1f, 0.5f, 0.02f, 0.08f);
        AddNoiseBurst(o, r, 0f, 0.01f, HP(2500f, 0.7f), 0.3f, Shape.Decay);
        float[] creak = Creak(seed + 1);
        for (int i = 0; i < creak.Length && i + (int)(0.15f * SR) < o.Length; i++) o[i + (int)(0.15f * SR)] += creak[i] * 0.9f;
        AddSweep(o, 1.75f, 85f, 40f, 0.1f, 0.09f, 0.8f);
        AddModes(o, r, 1.75f, new[] { 180f, 410f, 690f, 1050f }, 1f, 0.35f, 0.08f, 0.3f);
        Fade(o, 0.001f, 0.15f);
        return o;
    }

    private static float[] SlideDoor(int seed, bool open)
    {
        var r = new System.Random(seed);
        float[] o = New(1.6f);
        // 공압 피식 + 모터가 문을 미는 윙 + 레일 덜컹 + 끝에서 탁
        AddNoiseBurst(o, r, 0f, 0.25f, HP(3000f, 0.7f), 0.25f, Shape.AttackDecay);
        AddServo(o, r, 0.05f, 1.1f, open ? 210f : 260f, open ? 1.35f : 0.75f, 0.22f);
        AddNoiseBurst(o, r, 0.05f, 1.1f, LP(260f, 0.7f), 0.35f, Shape.Hann, pink: true);
        for (int i = 0; i < 10; i++)
            AddNoiseBurst(o, r, 0.1f + i * 0.1f, 0.006f, BP(Rf(r, 700f, 1400f), 2f), Rf(r, 0.04f, 0.09f), Shape.Decay);
        AddModes(o, r, 1.18f, new[] { 230f, 520f, 940f, 1500f }, 1f, open ? 0.35f : 0.55f, 0.04f, 0.2f);
        AddSweep(o, 1.18f, 90f, 45f, 0.06f, 0.05f, open ? 0.4f : 0.7f);
        Fade(o, 0.002f, 0.12f);
        return o;
    }

    private static float[] ElevatorHum(int seed)
    {
        var r = new System.Random(seed);
        const float len = 4f;
        float[] o = New(len);
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            o[i] = 0.18f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.09f * Mathf.Sin(2f * Mathf.PI * 110f * t + 1f)
                 + 0.04f * Mathf.Sin(2f * Mathf.PI * 165f * t + 2f) + 0.03f * Mathf.Sin(2f * Mathf.PI * 440f * t);
        }
        float[] rumble = NoiseLoop(r, len, LP(220f, 0.7f), 0.35f, brown: true);
        float[] cable = NoiseLoop(r, len, BP(1800f, 1.2f), 0.025f);
        for (int i = 0; i < n; i++) o[i] += rumble[i] + cable[i];
        return o;
    }

    private static float[] Ding(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.8f);
        foreach (var (at, f) in new[] { (0f, 1318f), (0.32f, 1047f) })
            AddModes(o, r, at, new[] { f, f * 2.01f, f * 3.0f, f * 4.2f }, 1f, 0.5f, 0.2f, 0.9f);
        Fade(o, 0.001f, 0.2f);
        return o;
    }

    private static float[] Arpeggio(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.4f);
        float[] notes = { 523f, 659f, 784f, 1047f };
        for (int k = 0; k < notes.Length; k++)
            AddModes(o, r, k * 0.09f, new[] { notes[k], notes[k] * 2f, notes[k] * 3f }, 1f, 0.45f, 0.15f, 0.55f);
        Fade(o, 0.001f, 0.2f);
        return o;
    }

    private static float[] Eat(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.1f);
        // 포장 바스락 + 씹는 소리 세 번
        for (int i = 0; i < 10; i++)
            AddNoiseBurst(o, r, Rf(r, 0f, 0.2f), Rf(r, 0.004f, 0.015f), BP(Rf(r, 2500f, 6000f), 1.2f), Rf(r, 0.08f, 0.2f), Shape.Decay);
        for (int c = 0; c < 3; c++)
        {
            float at = 0.3f + c * 0.25f;
            AddNoiseBurst(o, r, at, 0.05f, BP(Rf(r, 600f, 1100f), 0.8f), 0.6f, Shape.Hann);
            for (int i = 0; i < 6; i++)
                AddNoiseBurst(o, r, at + Rf(r, 0f, 0.05f), 0.004f, BP(Rf(r, 1500f, 3500f), 1.5f), Rf(r, 0.1f, 0.3f), Shape.Decay);
        }
        Filter(o, LP(5000f, 0.7f));
        Fade(o, 0.002f, 0.1f);
        return o;
    }

    private static float[] Drink(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.3f);
        // 뚜껑 딸깍 + 꿀꺽 세 번(목에서 내려가는 낮은 쿵 + 거품)
        AddModes(o, r, 0f, new[] { 1900f, 3300f }, 1f, 0.3f, 0.01f, 0.03f);
        for (int g = 0; g < 3; g++)
        {
            float at = 0.25f + g * 0.32f;
            AddSweep(o, at, 160f, 90f, 0.08f, 0.05f, 0.7f);
            AddChirp(o, at + 0.02f, 0.08f, 400f, 900f, 0.12f);
            AddNoiseBurst(o, r, at, 0.06f, LP(700f, 0.7f), 0.3f, Shape.Hann, pink: true);
        }
        Fade(o, 0.002f, 0.1f);
        return o;
    }

    private static float[] EnergyPack(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.0f);
        // 배터리 끼우는 철컥 + 충전되며 올라가는 전자음
        AddModes(o, r, 0f, new[] { 1500f, 2700f, 4300f }, 1f, 0.5f, 0.015f, 0.05f);
        AddNoiseBurst(o, r, 0f, 0.01f, LP(1500f, 0.7f), 0.4f, Shape.Decay);
        AddChirp(o, 0.12f, 0.6f, 300f, 1400f, 0.25f);
        AddChirp(o, 0.12f, 0.6f, 600f, 2800f, 0.08f);
        Fade(o, 0.001f, 0.1f);
        return o;
    }

    private static float[] RobotHit(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.5f);
        // 탄이 금속 장갑에 맞는 '깡' + 불꽃 지직
        AddModes(o, r, 0f, new[] { 930f, 2140f, 3480f, 5120f }, Rf(r, 0.9f, 1.15f), 0.6f, 0.03f, 0.12f);
        AddNoiseBurst(o, r, 0f, 0.004f, HP(3000f, 0.7f), 0.6f, Shape.Decay);
        for (int i = 0; i < 6; i++)
            AddNoiseBurst(o, r, Rf(r, 0.01f, 0.12f), 0.003f, HP(3500f, 0.7f), Rf(r, 0.1f, 0.25f), Shape.Decay);
        Fade(o, 0.0005f, 0.05f);
        return o;
    }

    private static float[] BulletImpact(int seed, bool metal)
    {
        var r = new System.Random(seed);
        float[] o = New(0.6f);
        if (metal)
        {
            AddModes(o, r, 0f, new[] { 1240f, 2870f, 4630f }, Rf(r, 0.85f, 1.2f), 0.5f, 0.02f, 0.09f);
            if (r.NextDouble() < 0.5) AddChirp(o, 0.02f, 0.3f, Rf(r, 2600f, 3400f), Rf(r, 1200f, 1700f), 0.15f); // 도탄 '피융'
        }
        else
        {
            // 콘크리트가 깨지는 '탁' + 부스러기
            AddNoiseBurst(o, r, 0f, 0.006f, BP(Rf(r, 1800f, 2800f), 0.8f), 0.8f, Shape.Decay);
            AddNoiseBurst(o, r, 0f, 0.03f, LP(900f, 0.7f), 0.5f, Shape.Decay, pink: true);
            for (int i = 0; i < 18; i++)
                AddNoiseBurst(o, r, Rf(r, 0.01f, 0.3f), 0.003f, BP(Rf(r, 2000f, 6000f), 2f), Rf(r, 0.03f, 0.12f), Shape.Decay);
        }
        Fade(o, 0.0005f, 0.06f);
        return o;
    }

    private static float[] UiSweep(int seed, bool open)
    {
        var r = new System.Random(seed);
        float[] o = New(0.32f);
        AddSweepNoise(o, r, 0f, 0.22f, open ? 900f : 3200f, open ? 3600f : 900f, 0.9f, 0.4f);
        AddChirp(o, 0.02f, 0.12f, open ? 880f : 1320f, open ? 1320f : 880f, 0.18f);
        Fade(o, 0.003f, 0.05f);
        return o;
    }

    private static float[] Paper(int seed, float len)
    {
        var r = new System.Random(seed);
        float[] o = New(len + 0.1f);
        int n = r.Next(18, 30);
        for (int i = 0; i < n; i++)
            AddNoiseBurst(o, r, Rf(r, 0f, len), Rf(r, 0.004f, 0.025f), BP(Rf(r, 2000f, 6500f), 1.2f), Rf(r, 0.08f, 0.3f), Shape.Decay);
        AddSweepNoise(o, r, 0f, len, 800f, 2500f, 0.6f, 0.12f);
        Fade(o, 0.002f, 0.05f);
        return o;
    }

    private static float[] Whoosh(int seed, float len)
    {
        var r = new System.Random(seed);
        float[] o = New(len);
        AddSweepNoise(o, r, 0f, len * 0.9f, 250f, 3200f, 0.7f, 0.7f);
        AddSweep(o, 0f, 55f, 70f, len * 0.8f, len * 0.4f, 0.25f, attack: len * 0.4f);
        Fade(o, 0.05f, 0.2f);
        return o;
    }

    private static float[] Bell(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.1f);
        AddModes(o, r, 0f, new[] { 1180f, 2950f, 4410f, 6120f }, 1f, 0.7f, 0.12f, 0.6f);
        AddNoiseBurst(o, r, 0f, 0.006f, HP(3000f, 0.7f), 0.4f, Shape.Decay);
        Fade(o, 0.0005f, 0.15f);
        return o;
    }

    private static float[] Debris(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(2.4f);
        for (int k = 0; k < 9; k++)
        {
            float at = (float)Math.Pow(r.NextDouble(), 1.3) * 1.8f;
            AddModes(o, r, at, RobotModes, Rf(r, 0.6f, 1.4f), Rf(r, 0.15f, 0.5f) * (float)Math.Exp(-at / 1.2f), 0.05f, 0.35f);
            AddSweep(o, at, Rf(r, 70f, 110f), 40f, 0.06f, 0.05f, Rf(r, 0.2f, 0.5f) * (float)Math.Exp(-at / 1.2f));
        }
        for (int i = 0; i < 90; i++)
        {
            float at = (float)Math.Pow(r.NextDouble(), 1.5) * 2.0f;
            AddNoiseBurst(o, r, at, Rf(r, 0.002f, 0.008f), BP(Rf(r, 1500f, 6000f), 2f), Rf(r, 0.03f, 0.15f) * (float)Math.Exp(-at / 0.9f), Shape.Decay);
        }
        Fade(o, 0.001f, 0.3f);
        return o;
    }

    private static float[] LightBuzz(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1f);
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float s = 0f;
            for (int h = 1; h <= 14; h++)
                s += Mathf.Sin(2f * Mathf.PI * 120f * h * t + h * 1.3f) / (h * 0.8f + 0.5f);
            o[i] = s * 0.05f;
        }
        Filter(o, BP(900f, 0.4f));
        float[] air = NoiseLoop(r, 1f, BP(3500f, 1f), 0.03f);
        for (int i = 0; i < n; i++) o[i] += air[i];
        return o;
    }

    private static float[] Sparks(int seed, float len)
    {
        var r = new System.Random(seed);
        float[] o = New(len + 0.05f);
        for (int i = 0; i < 22; i++)
            AddNoiseBurst(o, r, Rf(r, 0f, len), Rf(r, 0.002f, 0.009f), HP(Rf(r, 2000f, 4500f), 0.7f), Rf(r, 0.15f, 0.5f), Shape.Decay);
        Fade(o, 0.0005f, 0.03f);
        return o;
    }

    private static float[] Drip(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(0.35f);
        float f = Rf(r, 1200f, 1700f);
        AddChirp(o, 0f, 0.06f, f, f * 1.6f, 0.5f, decay: 0.025f);
        AddNoiseBurst(o, r, 0f, 0.01f, BP(5000f, 1f), 0.1f, Shape.Decay);
        Fade(o, 0.0005f, 0.05f);
        return o;
    }

    private static float[] Creak(int seed)
    {
        var r = new System.Random(seed);
        float[] o = New(1.8f);
        int n = o.Length;
        float f0 = Rf(r, 260f, 420f);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float env = Hann01(t / 1.6f);
            float f = f0 * (1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.6f * t)) + (float)(r.NextDouble() - 0.5) * 40f;
            ph += 2 * Math.PI * f / SR;
            float stick = (Mathf.Sin(2f * Mathf.PI * 23f * t) > 0.2f) ? 1f : 0.35f; // 끊기듯 긁힘
            o[i] = (float)(Math.Sin(ph) + 0.5 * Math.Sin(2 * ph) + 0.3 * Math.Sin(3 * ph)) * env * stick * 0.2f;
        }
        Filter(o, LP(1800f, 0.7f));
        Fade(o, 0.05f, 0.2f);
        return o;
    }

    private static float[] DistantDebris(int seed)
    {
        var r = new System.Random(seed);
        float[] o = Debris(seed + 5000);
        Filter(o, LP(900f, 0.7f));
        Filter(o, LP(900f, 0.7f));
        return o;
    }

    // ---------- 환경음 ----------
    private static float[][] AmbCity(int seed)
    {
        const float len = 24f;
        var r = new System.Random(seed);
        float[][] st = new float[2][];
        for (int c = 0; c < 2; c++)
        {
            var rc = new System.Random(seed * 7 + c);
            st[c] = Wind(rc, len, 0.6f);
            float[] rumble = NoiseLoop(rc, len, LP(110f, 0.7f), 0.35f, brown: true);
            for (int i = 0; i < st[c].Length; i++) st[c][i] += rumble[i];
        }
        return st;
    }

    private static float[][] AmbBunker(int seed)
    {
        const float len = 20f;
        float[][] st = new float[2][];
        for (int c = 0; c < 2; c++)
        {
            var rc = new System.Random(seed * 7 + c);
            float[] room = NoiseLoop(rc, len, LP(180f, 0.7f), 0.35f, brown: true);
            float[] air = NoiseLoop(rc, len, BP(520f, 0.6f), 0.05f);
            int n = room.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                room[i] += air[i]
                           + 0.030f * Mathf.Sin(2f * Mathf.PI * 60f * t)
                           + 0.014f * Mathf.Sin(2f * Mathf.PI * 120f * t + c)
                           + 0.007f * Mathf.Sin(2f * Mathf.PI * 180f * t);
            }
            st[c] = room;
        }
        return st;
    }

    private static float[][] AmbLab(int seed)
    {
        const float len = 20f;
        float[][] st = new float[2][];
        for (int c = 0; c < 2; c++)
        {
            var rc = new System.Random(seed * 7 + c);
            float[] hvac = NoiseLoop(rc, len, BP(380f, 0.5f), 0.22f, pink: true);
            float[] low = NoiseLoop(rc, len, LP(140f, 0.7f), 0.25f, brown: true);
            int n = hvac.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                hvac[i] += low[i] + 0.02f * Mathf.Sin(2f * Mathf.PI * 100f * t) + 0.01f * Mathf.Sin(2f * Mathf.PI * 200f * t + c);
            }
            st[c] = hvac;
        }
        return st;
    }

    private static float[][] AmbParts(int seed)
    {
        const float len = 20f;
        float[][] st = new float[2][];
        for (int c = 0; c < 2; c++)
        {
            var rc = new System.Random(seed * 7 + c);
            float[] fan = NoiseLoop(rc, len, BP(650f, 0.6f), 0.14f, pink: true);
            float[] low = NoiseLoop(rc, len, LP(160f, 0.7f), 0.2f, brown: true);
            int n = fan.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                // 장비 냉각팬: 일정한 회전음
                fan[i] = fan[i] * (0.9f + 0.1f * Mathf.Sin(2f * Mathf.PI * 47f * t)) + low[i]
                         + 0.012f * Mathf.Sin(2f * Mathf.PI * 235f * t) + 0.008f * Mathf.Sin(2f * Mathf.PI * 470f * t + c);
            }
            st[c] = fan;
        }
        return st;
    }

    private static float[] Wind(System.Random r, float len, float gain)
    {
        // 분홍 잡음을 중심 주파수가 천천히 움직이는 대역통과 필터에 통과시키고, 돌풍처럼 크기를 흔든다.
        int n = (int)(len * SR);
        int tail = (int)(2f * SR);
        float[] buf = new float[n + tail];
        var pink = new Pink();
        var bq = new Biquad();
        double p1 = r.NextDouble() * 6.28, p2 = r.NextDouble() * 6.28, p3 = r.NextDouble() * 6.28;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)SR;
            if ((i & 63) == 0)
            {
                float fc = 380f + 260f * (float)Math.Sin(2 * Math.PI * 0.07 * t + p1) + 140f * (float)Math.Sin(2 * Math.PI * 0.19 * t + p2);
                bq.SetBP(fc, 0.8f);
            }
            float gust = 0.55f + 0.3f * (float)Math.Sin(2 * Math.PI * 0.05 * t + p3) + 0.15f * (float)Math.Sin(2 * Math.PI * 0.23 * t + p1);
            buf[i] = bq.Process(pink.Next(r)) * gust * gain;
        }
        return LoopCrossfade(buf, n, tail);
    }

    // =====================================================================
    // 합성 부품
    // =====================================================================
    private enum Shape { Decay, AttackDecay, Hann }

    private static float[] New(float sec) => new float[Mathf.CeilToInt(sec * SR)];
    private static float Rf(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    private static float Hann01(float x) => x <= 0f || x >= 1f ? 0f : 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * x);

    private static void AddSweep(float[] o, float at, float f0, float f1, float sweepTime, float tau, float gain, float attack = 0.0015f)
    {
        int s0 = (int)(at * SR);
        int len = (int)(Mathf.Max(tau * 7f, attack + tau * 6f) * SR);
        double ph = 0;
        for (int i = 0; i < len && s0 + i < o.Length; i++)
        {
            float t = i / (float)SR;
            float k = Mathf.Exp(-t / Mathf.Max(0.001f, sweepTime * 0.4f));
            float f = f1 + (f0 - f1) * k;
            ph += 2 * Math.PI * f / SR;
            float env = (t < attack ? t / attack : 1f) * Mathf.Exp(-Mathf.Max(0f, t - attack) / tau);
            o[s0 + i] += (float)Math.Sin(ph) * env * gain;
        }
    }

    private static void AddNoiseBurst(float[] o, System.Random r, float at, float tau, Biquad filter, float gain, Shape shape, bool pink = false)
    {
        int s0 = (int)(at * SR);
        int len = shape == Shape.Hann ? (int)(tau * SR) : (int)(tau * 7f * SR);
        var p = new Pink();
        for (int i = 0; i < len && s0 + i < o.Length; i++)
        {
            if (s0 + i < 0) continue;
            float t = i / (float)SR;
            float env;
            switch (shape)
            {
                case Shape.Hann: env = Hann01(t / tau); break;
                case Shape.AttackDecay: env = Mathf.Min(1f, t / (tau * 0.25f)) * Mathf.Exp(-t / tau); break;
                default: env = Mathf.Exp(-t / tau); break;
            }
            float x = pink ? p.Next(r) : (float)(r.NextDouble() * 2 - 1);
            o[s0 + i] += filter.Process(x) * env * gain;
        }
    }

    private static void AddSweepNoise(float[] o, System.Random r, float at, float len, float f0, float f1, float q, float gain)
    {
        int s0 = (int)(at * SR);
        int n = (int)(len * SR);
        var bq = new Biquad();
        for (int i = 0; i < n && s0 + i < o.Length; i++)
        {
            float x = i / (float)n;
            if ((i & 31) == 0) bq.SetBP(f0 * Mathf.Pow(f1 / f0, x), q);
            o[s0 + i] += bq.Process((float)(r.NextDouble() * 2 - 1)) * Hann01(x) * gain;
        }
    }

    /// <summary>금속판의 비조화 공명(모달 합성). 높은 모드일수록 빨리 사라진다.</summary>
    private static void AddModes(float[] o, System.Random r, float at, float[] modes, float scale, float gain, float tauHigh, float tauLow)
    {
        int s0 = (int)(at * SR);
        for (int m = 0; m < modes.Length; m++)
        {
            float f = modes[m] * scale * Rf(r, 0.985f, 1.015f);
            float k = modes.Length > 1 ? m / (float)(modes.Length - 1) : 0f;
            float tau = Mathf.Lerp(tauLow, tauHigh, k) * Rf(r, 0.8f, 1.2f);
            float amp = gain * Mathf.Lerp(1f, 0.35f, k) * Rf(r, 0.6f, 1f);
            float ph = (float)r.NextDouble() * 6.28f;
            int len = (int)(tau * 7f * SR);
            for (int i = 0; i < len && s0 + i < o.Length; i++)
            {
                float t = i / (float)SR;
                float att = Mathf.Min(1f, t / 0.0008f);
                o[s0 + i] += Mathf.Sin(2f * Mathf.PI * f * t + ph) * Mathf.Exp(-t / tau) * amp * att;
            }
        }
    }

    private static void AddServo(float[] o, System.Random r, float at, float len, float f0, float rise, float gain)
    {
        int s0 = (int)(at * SR);
        int n = (int)(len * SR);
        double ph = 0;
        for (int i = 0; i < n && s0 + i < o.Length; i++)
        {
            float x = i / (float)n;
            float f = f0 * Mathf.Lerp(1f, rise, Mathf.Sin(x * Mathf.PI * 0.5f));
            ph += 2 * Math.PI * f / SR;
            float s = (float)(Math.Sin(ph) + 0.5 * Math.Sin(2 * ph) + 0.25 * Math.Sin(3 * ph) + 0.12 * Math.Sin(5 * ph));
            o[s0 + i] += s * Hann01(x) * gain;
        }
    }

    private static void AddTone(float[] o, float at, float len, float f, int harmonics, float gain)
    {
        int s0 = (int)(at * SR);
        int n = (int)(len * SR);
        for (int i = 0; i < n && s0 + i < o.Length; i++)
        {
            float t = i / (float)SR;
            float s = 0f;
            for (int h = 1; h <= harmonics; h += 2) s += Mathf.Sin(2f * Mathf.PI * f * h * t) / h;
            float env = Mathf.Min(1f, t / 0.003f) * Mathf.Min(1f, (len - t) / 0.01f);
            o[s0 + i] += s * env * gain;
        }
    }

    private static void AddChirp(float[] o, float at, float len, float f0, float f1, float gain, float decay = 0f)
    {
        int s0 = (int)(at * SR);
        int n = (int)(len * SR);
        double ph = 0;
        for (int i = 0; i < n && s0 + i < o.Length; i++)
        {
            float x = i / (float)n;
            float t = i / (float)SR;
            ph += 2 * Math.PI * (f0 * Mathf.Pow(f1 / f0, x)) / SR;
            float env = decay > 0f ? Mathf.Min(1f, t / 0.001f) * Mathf.Exp(-t / decay) : Hann01(x);
            o[s0 + i] += (float)Math.Sin(ph) * env * gain;
        }
    }

    private static void AddSaw(float[] o, float at, float len, float f0, float f1, int harmonics, float gain, bool fadeOut)
    {
        int s0 = (int)(at * SR);
        int n = (int)(len * SR);
        double ph = 0;
        for (int i = 0; i < n && s0 + i < o.Length; i++)
        {
            float x = i / (float)n;
            float f = f0 * Mathf.Pow(f1 / f0, x);
            ph += 2 * Math.PI * f / SR;
            float s = 0f;
            for (int h = 1; h <= harmonics; h++) s += (float)Math.Sin(ph * h) / h;
            float env = fadeOut ? Mathf.Min(1f, x / 0.02f) * (1f - x) : Hann01(x);
            o[s0 + i] += s * env * gain;
        }
    }

    private static float[] NoiseLoop(System.Random r, float len, Biquad f, float gain, bool pink = false, bool brown = false)
    {
        int n = (int)(len * SR);
        int tail = Mathf.Min((int)(1.5f * SR), n / 3);
        float[] buf = new float[n + tail];
        var p = new Pink();
        float b = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float x;
            if (brown) { b = (b + 0.02f * (float)(r.NextDouble() * 2 - 1)) * 0.998f; x = b * 3.5f; }
            else x = pink ? p.Next(r) : (float)(r.NextDouble() * 2 - 1);
            buf[i] = f.Process(x) * gain;
        }
        return LoopCrossfade(buf, n, tail);
    }

    /// <summary>앞부분 n 샘플만 남기고, 뒤에 남은 꼬리를 앞머리에 겹쳐 반복할 때 이음매가 안 들리게 한다.</summary>
    private static float[] LoopCrossfade(float[] buf, int n, int tail)
    {
        float[] o = new float[n];
        Array.Copy(buf, o, n);
        for (int i = 0; i < tail && i < n; i++)
        {
            float k = i / (float)tail;
            float a = Mathf.Sqrt(k), b = Mathf.Sqrt(1f - k);
            o[i] = buf[i] * a + buf[n + i] * b;
        }
        return o;
    }

    private static void Mix(float[] o, float[] add, float g)
    {
        for (int i = 0; i < o.Length && i < add.Length; i++) o[i] += add[i] * g;
    }

    private static void Filter(float[] o, Biquad f)
    {
        for (int i = 0; i < o.Length; i++) o[i] = f.Process(o[i]);
    }

    private static void Fade(float[] o, float inSec, float outSec)
    {
        int fi = (int)(inSec * SR), fo = (int)(outSec * SR);
        for (int i = 0; i < fi && i < o.Length; i++) o[i] *= i / (float)fi;
        for (int i = 0; i < fo && i < o.Length; i++) o[o.Length - 1 - i] *= i / (float)fo;
    }

    private static Biquad LP(float f, float q) { var b = new Biquad(); b.SetLP(f, q); return b; }
    private static Biquad HP(float f, float q) { var b = new Biquad(); b.SetHP(f, q); return b; }
    private static Biquad BP(float f, float q) { var b = new Biquad(); b.SetBP(f, q); return b; }

    private class Biquad
    {
        private float b0, b1, b2, a1, a2, z1, z2;

        public void SetLP(float f, float q) { Coeffs(f, q, 0); }
        public void SetHP(float f, float q) { Coeffs(f, q, 1); }
        public void SetBP(float f, float q) { Coeffs(f, q, 2); }

        private void Coeffs(float f, float q, int type)
        {
            f = Mathf.Clamp(f, 10f, SR * 0.45f);
            double w = 2 * Math.PI * f / SR, cs = Math.Cos(w), sn = Math.Sin(w), al = sn / (2 * q);
            double B0, B1, B2, A0 = 1 + al, A1 = -2 * cs, A2 = 1 - al;
            if (type == 0) { B0 = (1 - cs) / 2; B1 = 1 - cs; B2 = (1 - cs) / 2; }
            else if (type == 1) { B0 = (1 + cs) / 2; B1 = -(1 + cs); B2 = (1 + cs) / 2; }
            else { B0 = al; B1 = 0; B2 = -al; }
            b0 = (float)(B0 / A0); b1 = (float)(B1 / A0); b2 = (float)(B2 / A0); a1 = (float)(A1 / A0); a2 = (float)(A2 / A0);
        }

        public float Process(float x)
        {
            float y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            return y;
        }
    }

    private class Pink
    {
        private float b0, b1, b2, b3, b4, b5, b6;

        public float Next(System.Random r)
        {
            float w = (float)(r.NextDouble() * 2 - 1);
            b0 = 0.99886f * b0 + w * 0.0555179f; b1 = 0.99332f * b1 + w * 0.0750759f;
            b2 = 0.96900f * b2 + w * 0.1538520f; b3 = 0.86650f * b3 + w * 0.3104856f;
            b4 = 0.55000f * b4 + w * 0.5329522f; b5 = -0.7616f * b5 - w * 0.0168980f;
            float p = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362f;
            b6 = w * 0.115926f;
            return p * 0.11f;
        }
    }

    // =====================================================================
    // 저장·가져오기·검증
    // =====================================================================
    private static string Save(string name, float[] data, float peak)
    {
        RemoveDc(data);
        Normalize(data, peak);
        WriteWav($"{OutDir}/{name}.wav", new[] { data });
        return name;
    }

    private static string SaveStereo(string name, float[][] data, float peak)
    {
        foreach (var ch in data) RemoveDc(ch);
        float pk = 0f;
        foreach (var ch in data) foreach (var v in ch) pk = Mathf.Max(pk, Mathf.Abs(v));
        if (pk > 0f) foreach (var ch in data) for (int i = 0; i < ch.Length; i++) ch[i] *= peak / pk;
        WriteWav($"{OutDir}/{name}.wav", data);
        return name;
    }

    private static void RemoveDc(float[] d)
    {
        double m = 0; foreach (var v in d) m += v; m /= Math.Max(1, d.Length);
        for (int i = 0; i < d.Length; i++) d[i] -= (float)m;
    }

    private static void Normalize(float[] d, float peak)
    {
        float pk = 0f; foreach (var v in d) pk = Mathf.Max(pk, Mathf.Abs(v));
        if (pk <= 0f) return;
        float g = peak / pk;
        for (int i = 0; i < d.Length; i++) d[i] *= g;
    }

    private static void WriteWav(string path, float[][] ch)
    {
        int channels = ch.Length, n = ch[0].Length;
        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int dataBytes = n * channels * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataBytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)channels);
            w.Write(SR); w.Write(SR * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataBytes);
            for (int i = 0; i < n; i++)
                for (int c = 0; c < channels; c++)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(ch[c][i] * 32767f), -32768, 32767));
        }
    }

    /// <summary>
    /// 기존 음원을 가져온다. peak > 0 이면 압축을 풀어 읽은 뒤 최대치를 맞추고 앞뒤를 짧게 페이드해 WAV 로 다시 쓴다.
    /// (읽을 수 없는 압축 설정이면 그대로 복사)
    /// </summary>
    private static void Copy(string src, string name, List<string> made, float peak)
    {
        if (!File.Exists(src)) { Debug.LogWarning($"[AetherSoundForge] 원본 없음: {src}"); return; }

        if (peak > 0f)
        {
            var imp = AssetImporter.GetAtPath(src) as AudioImporter;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(src);
            if (imp != null && clip != null)
            {
                var old = imp.defaultSampleSettings;
                bool changed = old.loadType != AudioClipLoadType.DecompressOnLoad;
                if (changed)
                {
                    var s = old; s.loadType = AudioClipLoadType.DecompressOnLoad;
                    imp.defaultSampleSettings = s; imp.SaveAndReimport();
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(src);
                }

                float[] inter = new float[clip.samples * clip.channels];
                bool ok = clip.GetData(inter, 0);
                int chs = clip.channels, freq = clip.frequency;

                if (changed) { imp.defaultSampleSettings = old; imp.SaveAndReimport(); }

                if (ok)
                {
                    int n = inter.Length / chs;
                    float[][] ch = new float[chs][];
                    for (int c = 0; c < chs; c++) { ch[c] = new float[n]; for (int i = 0; i < n; i++) ch[c][i] = inter[i * chs + c]; }
                    if (freq != SR) for (int c = 0; c < chs; c++) ch[c] = Resample(ch[c], freq, SR);
                    foreach (var c in ch) { RemoveDc(c); Fade(c, 0.002f, 0.02f); }
                    float pk = 0f; foreach (var c in ch) foreach (var v in c) pk = Mathf.Max(pk, Mathf.Abs(v));
                    if (pk > 0f) foreach (var c in ch) for (int i = 0; i < c.Length; i++) c[i] *= peak / pk;
                    string old2 = $"{OutDir}/{name}{Path.GetExtension(src)}";
                    if (Path.GetExtension(src) != ".wav" && File.Exists(old2)) AssetDatabase.DeleteAsset(old2);
                    WriteWav($"{OutDir}/{name}.wav", ch);
                    made.Add(name);
                    return;
                }
            }
        }

        File.Copy(src, $"{OutDir}/{name}{Path.GetExtension(src)}", true);
        made.Add(name);
    }

    private static float[] Resample(float[] x, int from, int to)
    {
        int n = (int)((long)x.Length * to / from);
        float[] y = new float[n];
        for (int i = 0; i < n; i++)
        {
            double p = (double)i * from / to;
            int a = (int)p; float f = (float)(p - a);
            float s0 = x[Mathf.Min(a, x.Length - 1)], s1 = x[Mathf.Min(a + 1, x.Length - 1)];
            y[i] = s0 + (s1 - s0) * f;
        }
        return y;
    }

    /// <summary>이 제작기가 쓴 16비트 PCM WAV 를 다시 읽는다(압축 가져오기로 GetData 가 안 되는 반복음 검증용).</summary>
    private static float[] ReadWav(string path, out int channels)
    {
        channels = 1;
        byte[] b = File.ReadAllBytes(path);
        if (b.Length < 44 || b[0] != 'R') return null;
        channels = BitConverter.ToInt16(b, 22);
        int bits = BitConverter.ToInt16(b, 34);
        if (bits != 16) return null;
        int pos = 12;
        while (pos + 8 <= b.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
            int size = BitConverter.ToInt32(b, pos + 4);
            if (id == "data")
            {
                int n = Mathf.Min(size, b.Length - pos - 8) / 2;
                float[] d = new float[n];
                for (int i = 0; i < n; i++) d[i] = BitConverter.ToInt16(b, pos + 8 + i * 2) / 32768f;
                return d;
            }
            pos += 8 + size;
        }
        return null;
    }

    /// <summary>짧은 소리는 메모리에 풀어 두고(지연 없음), 긴 환경음은 스트리밍한다. 3D 위치감은 모노가 정확하다.</summary>
    private static void ApplyImportSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { OutDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            bool ambience = name.StartsWith("amb_") || name.StartsWith("bgm_");
            bool longLoop = ambience || name == "fire_loop";
            var s = imp.defaultSampleSettings;
            s.loadType = longLoop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            // 짧은 소리는 무압축 PCM: ADPCM 은 '딸깍' 같은 급한 소리에서 최대치가 1.0 까지 튀었다(검증 클리핑).
            s.compressionFormat = longLoop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.forceToMono = !ambience && !name.StartsWith("ui_") && name != "craft_success" && name != "equip" && name != "receive";
            imp.loadInBackground = longLoop;
            // 'Force To Mono' 에 딸린 정규화가 켜져 있으면 모노로 합치며 최대치를 1.0 까지 끌어올린다(검증 클리핑의 원인).
            var so = new SerializedObject(imp);
            var norm = so.FindProperty("m_Normalize");
            if (norm != null) { norm.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
            imp.SaveAndReimport();
        }
    }

    [MenuItem("AETHER/사운드/효과음 검증")]
    public static void Verify()
    {
        var sb = new System.Text.StringBuilder();
        int bad = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { OutDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (c == null) continue;
            float[] d = new float[c.samples * c.channels];
            if (!c.GetData(d, 0))
            {
                d = path.EndsWith(".wav") ? ReadWav(path, out _) : null;
                if (d == null) { sb.AppendLine($"{name}: 읽기 실패({c.loadType})"); continue; }
            }
            float pk = 0f; double rms = 0; int clip = 0;
            foreach (var v in d) { float a = Mathf.Abs(v); pk = Mathf.Max(pk, a); rms += v * v; if (a > 0.995f) clip++; }
            rms = Math.Sqrt(rms / d.Length);
            float head = Mathf.Abs(d[0]), tailV = Mathf.Abs(d[d.Length - 1]);
            bool loop = name.StartsWith("amb_") || name.StartsWith("bgm_") || name.EndsWith("_hum") || name.EndsWith("_loop") || name.EndsWith("_charge") || name == "heartbeat" || name == "light_buzz" || name == "elevator_move";
            float seam = loop ? Mathf.Abs(d[0] - d[d.Length - c.channels]) : 0f;
            var issues = new List<string>();
            if (clip > 0) issues.Add($"클리핑 {clip}");
            if (!loop && (head > 0.02f || tailV > 0.02f)) issues.Add($"끝 튐 {head:F2}/{tailV:F2}");
            if (loop && seam > 0.08f) issues.Add($"이음매 {seam:F2}");
            if (pk < 0.05f) issues.Add("너무 작음");
            if (issues.Count > 0) bad++;
            sb.AppendLine($"{name}: {c.length:F2}s pk {pk:F2} rms {rms:F3} {(issues.Count > 0 ? "⚠ " + string.Join(", ", issues) : "OK")}");
        }
        Debug.Log($"[AetherSoundForge] 검증 — 문제 {bad}개\n{sb}");
    }
}
