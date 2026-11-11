using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 퍼즐3(숫자 해독) 화면 = 번호키 도어락(사용자가 준 doorlock_ui_pack, Resources/UI/DoorlockPack).
/// 게임 규칙·판정은 사용자 매니저(CodebreakerGameManager_Final)가 그대로 맡고, 이 화면은 보여 주기·입력만 한다.
///
/// - 배경 = 팩의 빈 화면(도어락 본체 + 시도 기록 패널). 그림에 박힌 예시 값(0 / 6, 02:00, 커서)은 같은 색 조각으로 덮고 실제 값을 쓴다.
/// - LCD 4칸: 빈 칸은 그림의 7세그 잔상, 입력한 칸은 앰버 숫자, 다음 칸에 커서 막대.
/// - 키패드: 마우스로 누를 수 있고(✳ 모두 지움, # 확인), 키보드로 눌러도 해당 키가 빛난다.
/// - 시도 기록 6줄, 남은 시도 n / 6, 남은 시간. 매니저의 힌트(DDA)는 기록 아래 빈칸에.
/// - 규칙(팩 SPEC): 시도 6회 · 제한시간 02:00. 결과 = 팩의 UNLOCKED / LOCKED 창.
/// - 소리: 배경 환경음, 판정음(S·B·OUT), 마지막 10초 째깍.
/// </summary>
public class CodebreakerSceneOverlay : MonoBehaviour
{
    private const string FontPath = "UI/Fonts/NanumGothicCoding-Bold SDF";
    private const string PackPath = "UI/DoorlockPack/";
    private const int MaxAttempts = 6;
    private const float TimeLimit = 120f;

    private static readonly Color Amber = new Color32(0xF0, 0xA8, 0x30, 0xFF);
    private static readonly Color Blood = new Color32(0xA8, 0x33, 0x2C, 0xFF);
    private static readonly Color Pale = new Color32(0xDE, 0xE4, 0xE8, 0xFF);
    private static readonly Color Grey = new Color(0.55f, 0.57f, 0.58f, 1f);

    // 팩 기준 좌표(1920×1080, 왼쪽 위 0,0).
    private static readonly float[] LcdX = { 482f, 594f, 706f, 818f };
    private const float LcdY = 270f;
    private static readonly float[] KeyX = { 486f, 650f, 814f };
    private static readonly float[] KeyY = { 445f, 569f, 693f, 817f };
    private static readonly float[] CellX = { 1167f, 1231f, 1295f, 1359f };
    private const float RowY0 = 333f, RowPitch = 70f;

    private CodebreakerGameManager_Final manager;
    private TMP_FontAsset font;
    private RectTransform stage;

    private readonly TextMeshProUGUI[] lcdDigits = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI[] lcdGlows = new TextMeshProUGUI[4];
    private Image cursorBar;
    private TextMeshProUGUI attemptsText, timeText, hintText;
    private readonly List<TextMeshProUGUI[]> rowTexts = new List<TextMeshProUGUI[]>();   // NO, c0..c3, S, B
    private readonly List<Image[]> rowPatches = new List<Image[]>();                     // NO, S, B 덮개
    private readonly Dictionary<string, Image> keyFlash = new Dictionary<string, Image>();
    private readonly Dictionary<string, float> keyFlashUntil = new Dictionary<string, float>();
    private TMP_Text[] ddaTexts = new TMP_Text[0];
    private GameObject keyHint;

    private GameObject successScreen, failScreen;
    private TextMeshProUGUI[] successDigits;
    private TextMeshProUGUI successFooter, failTitle, failFooter;

    private int seenAttempts;
    private int lastTickSecond = -1;

    public static CodebreakerSceneOverlay Create(CodebreakerGameManager_Final manager)
    {
        GameObject root = new GameObject("AETHER_CodebreakerOverlay");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        CodebreakerSceneOverlay overlay = root.AddComponent<CodebreakerSceneOverlay>();
        overlay.manager = manager;
        overlay.Build((RectTransform)root.transform);

        Camera cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; }
        return overlay;
    }

    // ------------------------------------------------------------------ 만들기

    private Vector2 P(float x, float y) => new Vector2(x - 960f, 540f - y);

    private static Color Raw(float r, float g, float b) => new Color(r, g, b, 1f);

    private readonly Dictionary<string, Sprite> patchCache = new Dictionary<string, Sprite>();

    /// <summary>
    /// 그림에 박힌 예시 값을 덮는 조각. 같은 그림의 바로 옆 빈 곳(같은 높이)을 잘라 덮는다.
    /// 단색으로 덮으면 아주 어두운 색이 선형 색 공간에서 검게 뭉개져 띠처럼 보였다.
    /// srcWidth 를 주면 그 폭만큼 잘라 size 로 늘린다(위아래 그라데이션은 그대로).
    /// </summary>
    private Image Patch(RectTransform parent, string spriteName, float srcCx, float srcCy, Vector2 size, Vector2 dstPos, float srcWidth = -1f)
    {
        float w = srcWidth > 0f ? srcWidth : size.x;
        string key = spriteName + "|" + srcCx + "|" + srcCy + "|" + w + "|" + size.y;
        if (!patchCache.TryGetValue(key, out Sprite sprite))
        {
            Sprite source = Resources.Load<Sprite>(PackPath + spriteName);
            if (source != null)
            {
                Texture2D tex = source.texture;
                float sx = tex.width / 1920f, sy = tex.height / 1080f;
                Rect r = new Rect((srcCx - w * 0.5f) * sx, tex.height - (srcCy + size.y * 0.5f) * sy, w * sx, size.y * sy);
                sprite = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            patchCache[key] = sprite;
        }
        Image img = CreateImage("Patch", parent, sprite, dstPos, size);
        img.color = Color.white;
        return img;
    }

    private void Build(RectTransform root)
    {
        font = Resources.Load<TMP_FontAsset>(FontPath);

        // 원래 퍼즐 화면은 숨긴다(매니저와 기록은 계속 돈다).
        HideOriginalCanvas();
        manager.SetLimits(TimeLimit, MaxAttempts);

        Image back = CreateImage("Back", root, null, Vector2.zero, Vector2.zero);
        Stretch(back.rectTransform);
        back.color = Color.black;

        GameObject s = new GameObject("Stage", typeof(RectTransform));
        s.transform.SetParent(root, false);
        stage = (RectTransform)s.transform;
        stage.sizeDelta = new Vector2(1920f, 1080f);

        Image bg = CreateImage("DoorlockBackground", stage, Resources.Load<Sprite>(PackPath + "doorlock_bg"), Vector2.zero, new Vector2(1920f, 1080f));
        bg.color = Color.white;

        BuildLcd();
        BuildKeypad();
        BuildLog();

        // 맨 아래 조작 안내(도어락 본체 아래).
        TextMeshProUGUI hint = MakeText(stage, "KeyHint", 17f, new Color(0.62f, 0.64f, 0.65f, 0.9f), P(960f, 1030f), new Vector2(1500f, 30f));
        hint.text = "숫자 키·키패드 입력  ·  Enter / # 확인  ·  Backspace 한 칸 지움  ·  키패드 * 모두 지움  ·  ESC 나가기";
        keyHint = hint.gameObject;

        BuildResultScreens();
        StartAmbience();
    }

    private void HideOriginalCanvas()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        RectTransform slots = typeof(CodebreakerGameManager_Final).GetField("inputCellParent", flags)?.GetValue(manager) as RectTransform;
        Canvas c = slots != null ? slots.GetComponentInParent<Canvas>() : null;
        if (c != null)
        {
            c = c.rootCanvas;
            Transform hintRoot = c.transform.Find("Hint");
            if (hintRoot != null) ddaTexts = hintRoot.GetComponentsInChildren<TMP_Text>(true);
            c.enabled = false;
            GraphicRaycaster gr = c.GetComponent<GraphicRaycaster>();
            if (gr != null) gr.enabled = false;
        }
    }

    private void BuildLcd()
    {
        // 그림의 첫 칸 아래 커서 막대를 LCD 아래쪽 색으로 덮는다(커서는 직접 그린다).
        // 그림의 첫 칸 아래 커서 막대를 둘째 칸 아래(막대 없는 같은 줄)의 그림 조각으로 덮는다.
        Patch(stage, "doorlock_bg", 594f, 312f, new Vector2(64f, 14f), P(482f, 312f));

        for (int i = 0; i < 4; i++)
        {
            // 번짐: 같은 숫자를 조금 크게, 옅게 뒤에 깐다(TMP 글로우 재질은 화면에 네모로 나왔다).
            TextMeshProUGUI glow = MakeText(stage, "LcdGlow" + i, 92f, new Color(Amber.r, Amber.g, Amber.b, 0.22f), P(LcdX[i], LcdY - 2f), new Vector2(110f, 120f));
            glow.fontStyle = FontStyles.Bold;
            lcdGlows[i] = glow;
            TextMeshProUGUI d = MakeText(stage, "LcdDigit" + i, 84f, Amber, P(LcdX[i], LcdY - 2f), new Vector2(96f, 110f));
            d.fontStyle = FontStyles.Bold;
            lcdDigits[i] = d;
        }

        cursorBar = CreateImage("Cursor", stage, null, P(LcdX[0], 312f), new Vector2(52f, 5f));
        cursorBar.color = Amber;
    }

    private void BuildKeypad()
    {
        string[,] keys = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "*", "0", "#" } };
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                string k = keys[r, c];
                Image hit = CreateImage("Key_" + k, stage, null, P(KeyX[c], KeyY[r]), new Vector2(150f, 110f));
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                Button b = hit.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => PressKey(k, true));

                // 눌림: 앰버 빛 + 앰버 테두리(팩의 down 상태처럼).
                Image flash = CreateImage("Flash", hit.rectTransform, null, Vector2.zero, new Vector2(150f, 110f));
                flash.color = new Color(Amber.r, Amber.g, Amber.b, 0f);
                Outline o = flash.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(Amber.r, Amber.g, Amber.b, 0f);
                o.effectDistance = new Vector2(2f, -2f);
                keyFlash[k] = flash;
                keyFlashUntil[k] = 0f;
            }
        }
    }

    private void BuildLog()
    {
        // ATTEMPTS n / 6, TIME mm:ss — 그림의 예시 값을 덮고 쓴다.
        Patch(stage, "doorlock_bg", 1300f, 172f, new Vector2(70f, 22f), P(1517f, 172f));   // 예시 값 "0 / 6" 덮기

        Patch(stage, "doorlock_bg", 1300f, 197f, new Vector2(70f, 22f), P(1517f, 197f));   // 예시 값 "02:00" 덮기

        attemptsText = MakeText(stage, "Attempts", 16f, Amber, P(1544f, 172f), new Vector2(100f, 24f));
        attemptsText.alignment = TextAlignmentOptions.MidlineRight;
        attemptsText.rectTransform.pivot = new Vector2(1f, 0.5f);
        attemptsText.characterSpacing = 8f;
        timeText = MakeText(stage, "Time", 16f, Amber, P(1544f, 197f), new Vector2(100f, 24f));
        timeText.alignment = TextAlignmentOptions.MidlineRight;
        timeText.rectTransform.pivot = new Vector2(1f, 0.5f);
        timeText.characterSpacing = 8f;

        for (int i = 0; i < MaxAttempts; i++)
        {
            float y = RowY0 + RowPitch * i;
            Image noCover = Patch(stage, "doorlock_bg", 1100f, y, new Vector2(34f, 22f), P(1056f, y));
            Image sCover = Patch(stage, "doorlock_bg", 1471f, y + 3f, new Vector2(30f, 26f), P(1437f, y + 3f));
            Image bCover = Patch(stage, "doorlock_bg", 1471f, y + 3f, new Vector2(30f, 26f), P(1505f, y + 3f));
            rowPatches.Add(new[] { noCover, sCover, bCover });

            var texts = new TextMeshProUGUI[7];
            texts[0] = MakeText(stage, "No" + i, 16f, Pale, P(1056f, y), new Vector2(40f, 24f));
            for (int c = 0; c < 4; c++)
                texts[1 + c] = MakeText(stage, "Code" + i + "_" + c, 24f, Pale, P(CellX[c], y - 2f), new Vector2(50f, 32f));
            texts[5] = MakeText(stage, "S" + i, 24f, Amber, P(1437f, y + 2f), new Vector2(40f, 32f));
            texts[6] = MakeText(stage, "B" + i, 24f, Pale, P(1505f, y + 2f), new Vector2(40f, 32f));
            foreach (var t in texts) t.fontStyle = FontStyles.Bold;
            rowTexts.Add(texts);
        }

        // 매니저 힌트(DDA): 기록 아래 빈칸.
        hintText = MakeText(stage, "Hints", 17f, new Color(0.94f, 0.75f, 0.42f, 0.95f), P(1295f, 770f), new Vector2(498f, 96f));
        hintText.alignment = TextAlignmentOptions.MidlineLeft;
        hintText.textWrappingMode = TextWrappingModes.Normal;
        hintText.lineSpacing = 22f;
    }

    // ------------------------------------------------------------------ 입력

    private void PressKey(string k, bool fromClick)
    {
        Flash(k);
        if (!fromClick || manager == null || manager.IsEnded) return;
        switch (k)
        {
            case "*": manager.ClearAllInput(); break;
            case "#": manager.OnClickExecute(); break;
            case "0": manager.OnClickDigit0(); break;
            case "1": manager.OnClickDigit1(); break;
            case "2": manager.OnClickDigit2(); break;
            case "3": manager.OnClickDigit3(); break;
            case "4": manager.OnClickDigit4(); break;
            case "5": manager.OnClickDigit5(); break;
            case "6": manager.OnClickDigit6(); break;
            case "7": manager.OnClickDigit7(); break;
            case "8": manager.OnClickDigit8(); break;
            case "9": manager.OnClickDigit9(); break;
        }
    }

    private void Flash(string k)
    {
        if (keyFlashUntil.ContainsKey(k)) keyFlashUntil[k] = Time.unscaledTime + 0.16f;
    }

    private void ReadKeyboardForFlash()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || manager.IsEnded) return;
        for (int d = 0; d <= 9; d++)
        {
            Key top = (Key)((int)Key.Digit0 + d);
            Key pad = (Key)((int)Key.Numpad0 + d);
            if (kb[top].wasPressedThisFrame || kb[pad].wasPressedThisFrame) Flash(d.ToString());
        }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Flash("#");
        if (kb.backspaceKey.wasPressedThisFrame || kb.deleteKey.wasPressedThisFrame) Flash("*");
    }

    // ------------------------------------------------------------------ 매 프레임

    private void LateUpdate()
    {
        if (manager == null) return;
        ReadKeyboardForFlash();
        UpdateLcd();
        UpdateLog();
        UpdateKeys();
        UpdateTimer();
        UpdateHints();
        UpdateSounds();
    }

    private void UpdateLcd()
    {
        int len = manager.InputLength;
        for (int i = 0; i < 4; i++)
        {
            int d = manager.GetInputDigit(i);
            lcdDigits[i].text = d >= 0 ? d.ToString() : "";
            lcdGlows[i].text = lcdDigits[i].text;
        }
        bool showCursor = !manager.IsEnded && len < 4 && Mathf.Repeat(Time.unscaledTime * 1.6f, 1f) < 0.7f;
        cursorBar.enabled = showCursor;
        if (len < 4) cursorBar.rectTransform.anchoredPosition = P(LcdX[len], 312f);
    }

    private void UpdateLog()
    {
        var list = manager.Attempts;
        for (int i = 0; i < rowTexts.Count; i++)
        {
            bool filled = i < list.Count;
            TextMeshProUGUI[] t = rowTexts[i];
            foreach (Image p in rowPatches[i]) p.enabled = filled;
            if (!filled)
            {
                foreach (var x in t) x.text = "";
                continue;
            }
            var a = list[i];
            t[0].text = (i + 1).ToString("00");
            for (int c = 0; c < 4; c++) t[1 + c].text = c < a.code.Length ? a.code[c].ToString() : "";
            t[5].text = a.strike.ToString();
            t[5].color = a.strike > 0 ? Amber : Pale;
            t[6].text = a.ball.ToString();
        }
        int used = Mathf.Min(list.Count, MaxAttempts);
        attemptsText.text = $"{used} / {MaxAttempts}";
    }

    private void UpdateKeys()
    {
        foreach (var kv in keyFlash)
        {
            float k = Mathf.Clamp01((keyFlashUntil[kv.Key] - Time.unscaledTime) / 0.16f);
            kv.Value.color = new Color(Amber.r, Amber.g, Amber.b, 0.2f * k);
            Outline o = kv.Value.GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(Amber.r, Amber.g, Amber.b, 0.85f * k);
        }
    }

    private void UpdateTimer()
    {
        float t = Mathf.Max(0f, manager.RemainingTime);
        int s = Mathf.CeilToInt(t);
        timeText.text = $"{s / 60:00}:{s % 60:00}";
        bool low = t <= 15f && !manager.IsEnded;
        Color c = low ? new Color(0.9f, 0.3f, 0.25f) : Amber;
        if (low && Mathf.Repeat(Time.unscaledTime * 2f, 1f) > 0.5f) c.a = 0.45f;
        timeText.color = c;
    }

    private void UpdateHints()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (TMP_Text t in ddaTexts)
        {
            if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(t.text)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(t.text);
        }
        hintText.text = sb.ToString();
    }

    // ------------------------------------------------------------------ 소리

    private void StartAmbience()
    {
        AetherAudio.StartLoop("amb_lab", transform, 0.32f, 1f, 100f, false);
        AetherAudio.StartLoop("light_buzz", transform, 0.07f, 1f, 100f, false);
    }

    private void UpdateSounds()
    {
        int attempts = manager.AttemptCount;
        if (attempts < seenAttempts) seenAttempts = 0;
        if (attempts > seenAttempts)
        {
            seenAttempts = attempts;
            if (manager.LastStrike >= manager.DigitCount) { }                       // 정답: 매니저가 완료음을 낸다
            else if (manager.IsEnded) { }                                          // 시도 초과: 실패음은 결과 화면에서
            else if (manager.LastStrike > 0) AetherAudio.Play2D("target_hit", 0.85f, 1.05f);
            else if (manager.LastBall > 0) AetherAudio.Play2D("drag_snap", 0.9f);
            else AetherAudio.Play2D("ui_error", 0.8f, 0.8f);
        }

        if (!manager.IsEnded && manager.RemainingTime <= 10f && manager.RemainingTime > 0f)
        {
            int sec = Mathf.CeilToInt(manager.RemainingTime);
            if (sec != lastTickSecond)
            {
                lastTickSecond = sec;
                AetherAudio.Play2D("ui_hover", 1f, sec <= 3 ? 1.6f : 1.3f, 0f);
            }
        }
        else lastTickSecond = -1;
    }

    // ------------------------------------------------------------------ 결과 화면(팩의 UNLOCKED / LOCKED)

    private static readonly float[] ResultX = { 813f, 911f, 1009f, 1107f };

    private void BuildResultScreens()
    {
        successScreen = CreateResultScreen("ResultUnlocked", "overlay_unlocked");
        successDigits = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            Image cover = CreateImage("CellCover" + i, (RectTransform)successScreen.transform, null, P(ResultX[i], 591f), new Vector2(76f, 86f));
            cover.color = Raw(0.073f, 0.063f, 0.04f);
            successDigits[i] = MakeText(successScreen.transform, "Digit" + i, 50f, Amber, P(ResultX[i], 590f), new Vector2(76f, 86f));
            successDigits[i].fontStyle = FontStyles.Bold;
        }
        Patch((RectTransform)successScreen.transform, "overlay_unlocked", 620f, 684f, new Vector2(460f, 24f), P(960f, 684f), 160f);   // 예시 문구 덮기

        successFooter = MakeText(successScreen.transform, "Footer", 15f, Grey, P(960f, 684f), new Vector2(600f, 24f));
        ResultButton(successScreen.transform, "[F] 계속 진행", P(960f, 790f), new Vector2(260f, 52f), Amber, PuzzleSceneReturnBootstrap.RequestContinue);

        failScreen = CreateResultScreen("ResultLocked", "overlay_locked");
        Patch((RectTransform)failScreen.transform, "overlay_locked", 640f, 496f, new Vector2(320f, 30f), P(960f, 496f), 160f);

        failTitle = MakeText(failScreen.transform, "Subtitle", 20f, new Color(0.8f, 0.82f, 0.83f), P(960f, 496f), new Vector2(400f, 30f));
        Patch((RectTransform)failScreen.transform, "overlay_locked", 620f, 684f, new Vector2(460f, 24f), P(960f, 684f), 160f);

        failFooter = MakeText(failScreen.transform, "Footer", 15f, Grey, P(960f, 684f), new Vector2(600f, 24f));
        ResultButton(failScreen.transform, "[F] 다시 시도", P(820f, 790f), new Vector2(240f, 52f), Blood, PuzzleSceneReturnBootstrap.RequestCodeRetry);
        ResultButton(failScreen.transform, "[ESC] 나가기", P(1100f, 790f), new Vector2(240f, 52f), Grey, PuzzleSceneReturnBootstrap.RequestGiveUp);
    }

    private GameObject CreateResultScreen(string name, string sprite)
    {
        // 결과 상자 뒤에 불투명한 바탕을 깔아 뒤의 키패드가 비치지 않게 한다(그림만으로는 비쳐 보였다).
        Image screen = CreateImage(name, stage, null, Vector2.zero, new Vector2(1920f, 1080f));
        screen.color = new Color(0f, 0f, 0f, 0f);
        screen.raycastTarget = true;   // 뒤의 키패드가 눌리지 않게
        Image boxBack = CreateImage("BoxBack", screen.rectTransform, null, P(960f, 540f), new Vector2(900f, 368f));
        boxBack.color = Raw(0.02f, 0.023f, 0.027f);
        Image art = CreateImage("Art", screen.rectTransform, Resources.Load<Sprite>(PackPath + sprite), Vector2.zero, new Vector2(1920f, 1080f));
        art.color = Color.white;
        screen.gameObject.SetActive(false);
        return screen.gameObject;
    }

    private void ResultButton(Transform parent, string label, Vector2 pos, Vector2 size, Color accent, UnityEngine.Events.UnityAction onClick)
    {
        Image bg = CreateImage("Button", (RectTransform)parent, null, pos, size);
        bg.color = new Color(0.03f, 0.033f, 0.037f, 0.96f);
        bg.raycastTarget = true;
        Outline o = bg.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(accent.r, accent.g, accent.b, 0.8f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
        Button b = bg.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1f, 0.85f, 0.6f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
        TextMeshProUGUI t = MakeText(bg.transform, "Label", 20f, accent == Grey ? Pale : accent, Vector2.zero, size);
        t.text = label;
        t.fontStyle = FontStyles.Bold;
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    public void ShowSuccess()
    {
        var list = manager.Attempts;
        string code = list.Count > 0 ? list[list.Count - 1].code : "";
        for (int i = 0; i < 4; i++) successDigits[i].text = i < code.Length ? code[i].ToString() : "";

        // 에테르 = 기본 300 + 정확도(최대 150) + 남은 시간 1초당 2. 이미 받았으면 0.
        int accuracy = manager.AccuracyPercent;
        int aether = AetherLevel.CircuitPuzzleExp + Mathf.RoundToInt(accuracy * 1.5f) + Mathf.RoundToInt(manager.RemainingTime * 2f);
        int granted = AetherLevel.ReportPuzzle(AetherLabCorridor.GatePuzzleId, aether, "보안 코드 해독");
        string bonus = granted > 0 ? $"  ·  <color=#F0A830>+{granted} AE</color>" : "";
        successFooter.text = $"{manager.AttemptCount}회 시도  ·  {FormatTime(manager.RemainingTime)} 남음{bonus}";

        failScreen.SetActive(false);
        keyHint.SetActive(false);
        hintText.gameObject.SetActive(false);   // 결과창 버튼 뒤로 힌트 글자가 비쳐 보였다
        successScreen.transform.SetAsLastSibling();
        successScreen.SetActive(true);
    }

    public void ShowFail(bool timeout)
    {
        timeout = timeout || manager.TimedOut;
        failTitle.text = timeout ? "제한 시간 초과 · 접근 차단" : "시도 횟수 초과 · 접근 차단";

        int bestS = -1, bestB = 0;
        foreach (var a in manager.Attempts)
            if (a.strike > bestS || (a.strike == bestS && a.ball > bestB)) { bestS = a.strike; bestB = a.ball; }
        string best = bestS >= 0 ? $"  ·  최대 {bestS}S {bestB}B" : "";
        failFooter.text = timeout ? $"{manager.AttemptCount}회 시도  ·  시간 초과{best}" : $"{manager.AttemptCount}회 시도 실패{best}";

        successScreen.SetActive(false);
        keyHint.SetActive(false);
        hintText.gameObject.SetActive(false);
        failScreen.transform.SetAsLastSibling();
        failScreen.SetActive(true);
        AetherAudio.Play2D("robot_alert", 0.5f, 0.8f);
    }

    public void HideResult()
    {
        successScreen.SetActive(false);
        failScreen.SetActive(false);
        keyHint.SetActive(true);
        hintText.gameObject.SetActive(true);
        seenAttempts = 0;
    }

    /// <summary>다시 시작한 뒤(매니저가 시간을 원래 값으로 되돌린다) 도어락 규칙을 다시 건다.</summary>
    public void ApplyLimits() => manager.SetLimits(TimeLimit, MaxAttempts);

    // ------------------------------------------------------------------ 도우미

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, float size, Color color, Vector2 pos, Vector2 box)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = box;
        return t;
    }
}
