using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CircuitPuzzle
{
    // Design layer for the user's circuit puzzle (CircuitPuzzleManager_Final), from minigame_ui_pack_v2.
    // "원본 구조 유지, 디자인만 교체": the board, rules, columns, hidden correct gate, DDA hints and timer stay as they are.
    // This only swaps sprites, moves the gates onto the pack's layout, and draws the HUD and result screens.
    public class Puzzle2MinigameSkin : MonoBehaviour
    {
        const string PackPath = "UI/MinigamePack/";

        static readonly Color Amber = new Color32(0xF0, 0xA8, 0x30, 0xFF);
        static readonly Color AmberHighlight = new Color32(0xFF, 0xD2, 0x7A, 0xFF);
        static readonly Color AlertRed = new Color32(0xC0, 0x45, 0x3A, 0xFF);
        static readonly Color TextLight = new Color32(0xD8, 0xD6, 0xD0, 0xFF);
        static readonly Color ValueWhite = new Color32(0xEE, 0xEA, 0xE2, 0xFF);

        // One scale for the whole design (HUD panels, their text and lights, and the crates), so everything shrinks together.
        // At the pack's full size the 7-crate column overlaps itself and the SIGNAL BAND; 0.82 is the largest size where
        // that column fits with clear gaps. Each HUD panel stays pinned to its own screen edge or corner.
        const float HudScale = 0.82f;

        // Pack sizes at scale 1 (pack coordinates are the 1920x1080 sheet, top-left origin).
        static readonly Vector2 CrateSize = new Vector2(216f, 152f);   // crate sprite incl. glow margin
        const float CrateBodyHeight = 128f;
        const float CrateBodyWidth = 192f;
        const float MinRowGapRatio = 0.3f;     // gap between crates, at least 30% of a crate's height
        const float MaxRowGapRatio = 0.6f;
        const float ColumnLineRoom = 90f;      // free width between columns for the connection lines
        const float TopPanelsBottom = 140f;    // sheet y where the top panels (and their shadow) end
        const float SidePanelInnerEdge = 254f; // sheet x of the 송신 panel's right edge (수신 mirrored)

        static readonly Vector2 TransmitPanelCenter = new Vector2(139f, 520f);   // sheet coordinates
        static readonly Vector2 ReceivePanelCenter = new Vector2(1781f, 520f);
        static readonly Vector2 TerminalSize = new Vector2(230f, 300f);
        static readonly float[] TransmitDotX = { 87f, 139f, 191f };
        static readonly float[] ReceiveDotX = { 1729f, 1781f, 1833f };
        const float TerminalDotY = 478f;
        const float WrongFlashSeconds = 0.9f;

        RectTransform transmitGroup;
        RectTransform receiveGroup;

        class CrateView
        {
            public CP_Gate_Final gate;
            public Image icon;
            public Image[] dots;
            public Puzzle2CrateHover hover;
            public float wrongUntil;
        }

        CircuitPuzzleManager_Final manager;
        RectTransform canvasRect;
        TMP_FontAsset font;

        Sprite crateIdle, crateHover, crateCorrect, crateWrong, crateLocked;
        Sprite dotOff, dotRed;
        Sprite[] transmitCode;
        Sprite iconSprite;

        readonly List<CrateView> crates = new List<CrateView>();
        CP_Gate_Final firstGateSeen;
        int gateCountSeen;

        Image[] receiveDots;
        TextMeshProUGUI levelText;
        TextMeshProUGUI timeText;

        GameObject successScreen;
        GameObject failScreen;
        TextMeshProUGUI[] successValues;
        TextMeshProUGUI[] failValues;
        bool resultShown;

        int connectAttempts;
        int wrongPicks;
        int failCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToManagers();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToManagers();

        static void AttachToManagers()
        {
            foreach (CircuitPuzzleManager_Final found in FindObjectsByType<CircuitPuzzleManager_Final>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (found != null && found.GetComponent<Puzzle2MinigameSkin>() == null)
                    found.gameObject.AddComponent<Puzzle2MinigameSkin>();
            }
        }

        void Start()
        {
            manager = GetComponent<CircuitPuzzleManager_Final>();
            RectTransform board = manager != null ? (manager.boardParent != null ? manager.boardParent : manager.gatesParent) : null;
            Canvas canvas = board != null ? board.GetComponentInParent<Canvas>() : null;
            if (canvas == null || !LoadSprites())
            {
                enabled = false;
                return;
            }

            canvas = canvas.rootCanvas;
            canvasRect = canvas.GetComponent<RectTransform>();
            font = manager.feedbackText != null ? manager.feedbackText.font : TMP_Settings.defaultFontAsset;

            manager.useSkinLineColors = true;
            manager.skinLineColor = Amber;
            manager.skinDragLineColor = AmberHighlight;
            manager.GateWrongSelected += OnGateWrongSelected;
            manager.GateConnected += OnGateConnected;

            // Korean hints wrap at spaces instead of between syllables ("정 / 답") while this puzzle is open.
            previousHangulRule = TMP_Settings.useModernHangulLineBreakingRules;
            TMP_Settings.useModernHangulLineBreakingRules = true;
            changedHangulRule = true;

            FitScreen(canvas, board);
            BuildHud(board);
            BuildResultScreens();
        }

        // The canvas was Constant Pixel Size with a fixed 1920x1080 background, so on bigger or non-16:9 screens the
        // skybox showed around it (and the puzzle stayed small).
        void FitScreen(Canvas canvas, RectTransform board)
        {
            // Full-screen design screen: AetherUIScaleNormalizer would otherwise switch it back to match 0.5,
            // which cuts off the top and bottom of the board on wide screens.
            if (canvas.GetComponent<AetherDesignCanvasMarker>() == null)
                canvas.gameObject.AddComponent<AetherDesignCanvasMarker>();

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }

            if (canvas.worldCamera != null)
            {
                canvas.worldCamera.clearFlags = CameraClearFlags.SolidColor;
                canvas.worldCamera.backgroundColor = Color.black;
            }

            Image background = FindBackground(board);
            if (background == null) return;

            Sprite art = background.sprite;
            if (art.name == "robot-background")
            {
                // The old robot background has a 3 px transparent strip on its top and left edges.
                Rect r = art.rect;
                float trim = Mathf.Ceil(4f * r.width / 2159f);
                background.sprite = Sprite.Create(art.texture, new Rect(r.x + trim, r.y, r.width - trim, r.height - trim), new Vector2(0.5f, 0.5f), art.pixelsPerUnit);
            }
            background.preserveAspect = false;

            RectTransform rect = background.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            AspectRatioFitter fitter = background.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = background.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectRatio = 16f / 9f;   // same proportions as before; covers wider or taller screens
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        // The full-screen image under the board (the robot background).
        Image FindBackground(RectTransform board)
        {
            Transform boardTop = board;
            while (boardTop.parent != null && boardTop.parent != canvasRect) boardTop = boardTop.parent;
            foreach (Transform child in canvasRect)
            {
                if (child == boardTop) break;
                Image image = child.GetComponent<Image>();
                if (image != null && image.sprite != null && child.gameObject.activeSelf && ((RectTransform)child).rect.width >= 1900f)
                    return image;
            }

            return null;
        }

        bool previousHangulRule;
        bool changedHangulRule;

        void OnDestroy()
        {
            if (changedHangulRule) TMP_Settings.useModernHangulLineBreakingRules = previousHangulRule;
            if (manager == null) return;
            manager.GateWrongSelected -= OnGateWrongSelected;
            manager.GateConnected -= OnGateConnected;
        }

        bool LoadSprites()
        {
            crateIdle = LoadSprite("crate_idle_432x304");
            crateHover = LoadSprite("crate_hover_432x304");
            crateCorrect = LoadSprite("crate_correct_432x304");
            crateWrong = LoadSprite("crate_wrong_432x304");
            crateLocked = LoadSprite("crate_locked_432x304");
            dotOff = LoadSprite("dot_off_64");
            dotRed = LoadSprite("dot_red_64");
            // Transmit code, the same three lights as the result art.
            transmitCode = new[] { LoadSprite("dot_amber_64"), LoadSprite("dot_cyan_64"), LoadSprite("dot_green_64") };
            iconSprite = CropGateIcon(manager.normalGateSprite);
            return crateIdle != null && dotOff != null && transmitCode[0] != null;
        }

        static Sprite LoadSprite(string name) => Resources.Load<Sprite>(PackPath + name);

        // The pack's crates have an empty screen; the original gate's cyan glyph goes on it.
        // The glyph is crossed by grey cable bars in the original art, so only the cyan pixels are kept.
        static Sprite CropGateIcon(Sprite gateSprite)
        {
            if (gateSprite == null || gateSprite.texture == null) return null;
            Texture source = gateSprite.texture;
            Rect rect = gateSprite.rect;
            float k = rect.width / 280f;
            RectInt crop = new RectInt(Mathf.RoundToInt(rect.x + 88f * k), Mathf.RoundToInt(rect.y + 42f * k), Mathf.RoundToInt(94f * k), Mathf.RoundToInt(76f * k));

            // The texture is not readable; read it back through a render texture.
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, temporary);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;
            Texture2D icon = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false);
            icon.ReadPixels(new Rect(crop.x, crop.y, crop.width, crop.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            Color[] pixels = icon.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float cyan = Mathf.Min(c.g, c.b) - c.r;
                float alpha = Mathf.Clamp01((cyan - 0.08f) * 6f) * c.a;
                pixels[i] = new Color(Mathf.Min(1f, c.r * 1.3f), Mathf.Min(1f, c.g * 1.3f), Mathf.Min(1f, c.b * 1.3f), alpha);
            }

            icon.SetPixels(pixels);
            icon.Apply();
            icon.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(icon, new Rect(0, 0, crop.width, crop.height), new Vector2(0.5f, 0.5f), gateSprite.pixelsPerUnit);
        }

        void LateUpdate()
        {
            if (manager == null) return;

            IReadOnlyList<CP_Gate_Final> gates = manager.AllGates;
            if (gates.Count > 0 && (gates[0] != firstGateSeen || gates.Count != gateCountSeen))
            {
                firstGateSeen = gates[0];
                gateCountSeen = gates.Count;
                ApplyBoardLayout();
                connectAttempts = 0;
                wrongPicks = 0;
                HideResults();
            }
            RefreshTerminals();

            UpdateCrates();
            UpdateHud();

            // The skin's result screens replace the original CLEAR / FAILED panels.
            if (manager.clearPanel != null && manager.clearPanel.activeSelf) manager.clearPanel.SetActive(false);
            if (manager.failPanel != null && manager.failPanel.activeSelf) manager.failPanel.SetActive(false);

            if (manager.IsEnded && !resultShown)
                ShowResult(manager.IsCleared);

            if (resultShown)
                HandleResultKeys();
        }

        // ---------- board ----------

        void ApplyBoardLayout()
        {
            crates.Clear();
            IReadOnlyList<List<CP_Gate_Final>> columns = manager.GatesByColumn;

            // Board area: under the top panels, between the 송신 and 수신 panels, down to the bottom edge.
            // 첫 배치는 CanvasScaler 가 크기를 바꾸기 전에 일어나 canvasRect.rect 가 화면 픽셀 크기였다.
            // 1080p 근처에선 티가 안 났지만 2560x1600 같은 큰 화면에선 상자가 왼쪽 아래로 밀려 화면 밖까지 나갔다(10/12 exe).
            // 스케일러(1920x1080 Expand)가 만들 크기를 직접 계산해 쓴다.
            float expand = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            Vector2 areaSize = expand > 0f ? new Vector2(Screen.width, Screen.height) / expand : canvasRect.rect.size;
            Rect canvasArea = new Rect(-areaSize * 0.5f, areaSize);
            float boardTop = canvasArea.yMax - TopPanelsBottom * HudScale - 10f;
            float boardBottom = canvasArea.yMin + 12f;
            float boardLeft = canvasArea.xMin + SidePanelInnerEdge * HudScale;
            float boardRight = canvasArea.xMax - SidePanelInnerEdge * HudScale;

            int columnCount = columns.Count;
            int maxRows = 1;
            foreach (List<CP_Gate_Final> column in columns)
                if (column != null) maxRows = Mathf.Max(maxRows, column.Count);

            // Columns evenly between the two panels (the panels count as the outer stops).
            float columnPitch = (boardRight - boardLeft) / (columnCount + 1);

            // Crates use the HUD scale; only if the tallest column (or the column spacing) can't fit, they get smaller.
            float boardHeight = boardTop - boardBottom;
            float crateScale = HudScale;
            crateScale = Mathf.Min(crateScale, boardHeight / (CrateBodyHeight * (maxRows + MinRowGapRatio * (maxRows - 1))));
            crateScale = Mathf.Min(crateScale, (columnPitch - ColumnLineRoom) / CrateBodyWidth);
            Vector2 crateSize = CrateSize * crateScale;
            float body = CrateBodyHeight * crateScale;
            float rowPitch = maxRows > 1 ? Mathf.Min((boardHeight - body) / (maxRows - 1), body * (1f + MaxRowGapRatio)) : 0f;
            float boardCenterY = (boardTop + boardBottom) * 0.5f;

            foreach (CP_Gate_Final gate in manager.AllGates)
            {
                if (gate == null || gate.gateRT == null) continue;
                HideOkBox(gate);

                if (gate.gateType == "transmitter")
                {
                    SetupTerminal(gate, transmitGroup, TransmitPanelCenter, new Vector2(TerminalSize.x * 0.5f, TransmitPanelCenter.y - TerminalDotY));
                    continue;
                }

                if (gate.gateType == "end")
                {
                    SetupTerminal(gate, receiveGroup, ReceivePanelCenter, new Vector2(-TerminalSize.x * 0.5f, ReceivePanelCenter.y - TerminalDotY));
                    continue;
                }

                if (gate.columnIndex >= 0 && gate.columnIndex < columnCount && gate.gateIndexInColumn >= 0)
                {
                    int rows = columns[gate.columnIndex].Count;
                    float x = boardLeft + columnPitch * (gate.columnIndex + 1);
                    float y = boardCenterY + ((rows - 1) * 0.5f - gate.gateIndexInColumn) * rowPitch;
                    PlaceOnCanvas(gate.gateRT, new Vector2(x, y));
                    gate.gateRT.sizeDelta = crateSize;
                }

                crates.Add(SetupCrate(gate));
            }
        }

        CrateView SetupCrate(CP_Gate_Final gate)
        {
            Vector2 size = gate.gateRT.sizeDelta;
            float sx = size.x / CrateSize.x;
            float sy = size.y / CrateSize.y;

            if (gate.gateImage != null)
            {
                gate.gateImage.sprite = crateIdle;
                gate.gateImage.type = Image.Type.Simple;
                gate.gateImage.preserveAspect = false;
                gate.gateImage.color = Color.white;
            }

            // Ports sit on the crate's amber side notches; keep them clickable but invisible.
            foreach (CP_Port_Final port in gate.GetComponentsInChildren<CP_Port_Final>(true))
            {
                RectTransform portRect = port.GetComponent<RectTransform>();
                if (portRect != null)
                    portRect.anchoredPosition = new Vector2((port.portType == "input" ? -96f : 96f) * sx, 0f);
                Image portImage = port.GetComponent<Image>();
                if (portImage != null)
                    portImage.color = new Color(1f, 1f, 1f, 0f);
            }

            CrateView view = new CrateView { gate = gate, dots = new Image[3] };
            if (iconSprite != null)
            {
                view.icon = CreateImage("SkinIcon", gate.gateRT, iconSprite, new Vector2(0f, 14f * sy), new Vector2(162f * sx, 74f * sy));
                view.icon.preserveAspect = true;
            }

            for (int i = 0; i < 3; i++)
                view.dots[i] = CreateImage("SkinDot" + i, gate.gateRT, dotOff, new Vector2((i - 1) * 34f * sx, -42f * sy), Vector2.one * 16f * sx);

            view.hover = gate.GetComponent<Puzzle2CrateHover>();
            if (view.hover == null) view.hover = gate.gameObject.AddComponent<Puzzle2CrateHover>();
            return view;
        }

        void SetupTerminal(CP_Gate_Final gate, RectTransform panelGroup, Vector2 panelCenter, Vector2 portLocal)
        {
            // The HUD's 송신 / 수신 panels are the terminals now; the old gate art is hidden.
            if (gate.gateImage != null) gate.gateImage.enabled = false;
            terminals.RemoveAll(t => t.gate == null || t.gate == gate);
            terminals.Add((gate, panelGroup, panelCenter));
            PlaceTerminal(gate, panelGroup, panelCenter);
            gate.gateRT.sizeDelta = TerminalSize * HudScale;

            foreach (CP_Port_Final port in gate.GetComponentsInChildren<CP_Port_Final>(true))
            {
                RectTransform portRect = port.GetComponent<RectTransform>();
                if (portRect != null)
                {
                    portRect.anchoredPosition = portLocal * HudScale;
                    portRect.sizeDelta = Vector2.one * 44f * HudScale;
                }

                Image portImage = port.GetComponent<Image>();
                if (portImage != null)
                {
                    portImage.sprite = transmitCode[0];
                    portImage.type = Image.Type.Simple;
                    portImage.preserveAspect = true;
                    portImage.color = Color.white;
                }
            }
        }

        // 송신/수신 연결점은 화면 가장자리에 붙은 패널을 따라가야 한다. 처음 배치 때는 화면 크기 맞춤(CanvasScaler)이
        // 아직 안 돼 패널 자리가 달랐고, 그 뒤 패널만 옮겨 가 연결점이 화면 밖에 남았다(10/12 exe). 매 프레임 다시 맞춘다.
        readonly List<(CP_Gate_Final gate, RectTransform group, Vector2 center)> terminals = new List<(CP_Gate_Final, RectTransform, Vector2)>();

        void PlaceTerminal(CP_Gate_Final gate, RectTransform panelGroup, Vector2 panelCenter)
        {
            if (gate == null || gate.gateRT == null || panelGroup == null) return;
            gate.gateRT.position = panelGroup.TransformPoint(GroupLocal(panelGroup, panelCenter.x, panelCenter.y));
            Vector3 local = gate.gateRT.localPosition;
            gate.gateRT.localPosition = new Vector3(local.x, local.y, 0f);
        }

        void RefreshTerminals()
        {
            foreach (var t in terminals)
                PlaceTerminal(t.gate, t.group, t.center);
        }

        static void HideOkBox(CP_Gate_Final gate)
        {
            Transform okBox = gate.transform.Find("OK_Box");
            if (okBox != null) okBox.gameObject.SetActive(false);
        }

        void PlaceOnCanvas(RectTransform rect, Vector2 canvasPosition)
        {
            rect.position = canvasRect.TransformPoint(canvasPosition);
            Vector3 local = rect.localPosition;
            local.z = 0f;
            rect.localPosition = local;
        }

        void UpdateCrates()
        {
            float now = Time.unscaledTime;
            foreach (CrateView view in crates)
            {
                CP_Gate_Final gate = view.gate;
                if (gate == null || gate.gateImage == null) continue;

                Sprite crate = crateIdle;
                Sprite[] code = null;
                bool lit = true;
                if (gate.IsDDADisabled)
                {
                    crate = crateLocked;
                    lit = false;
                }
                else if (view.wrongUntil > now)
                {
                    crate = crateWrong;
                    code = new[] { dotRed, dotRed, dotRed };
                }
                else if (gate.isCorrectGate && gate.isActivatedByCorrectColor)
                {
                    crate = crateCorrect;
                    code = transmitCode;
                }
                else if (view.hover != null && view.hover.IsHovered && !manager.IsEnded)
                {
                    crate = crateHover;
                }

                if (gate.gateImage.sprite != crate) gate.gateImage.sprite = crate;
                if (view.icon != null) view.icon.color = lit ? Color.white : new Color(1f, 1f, 1f, 0.25f);
                for (int i = 0; i < view.dots.Length; i++)
                {
                    Sprite dot = code != null ? code[i] : dotOff;
                    if (view.dots[i] != null && view.dots[i].sprite != dot) view.dots[i].sprite = dot;
                }
            }
        }

        void OnGateWrongSelected(CP_Gate_Final gate)
        {
            connectAttempts++;
            wrongPicks++;
            bandWrongUntil = Time.unscaledTime + WrongFlashSeconds;
            foreach (CrateView view in crates)
                if (view.gate == gate) view.wrongUntil = Time.unscaledTime + WrongFlashSeconds;
        }

        void OnGateConnected(CP_Gate_Final gate) => connectAttempts++;

        // ---------- HUD ----------

        void BuildHud(RectTransform board)
        {
            // HUD frame goes above the background art and below the board.
            Transform boardTop = board;
            while (boardTop.parent != null && boardTop.parent != canvasRect) boardTop = boardTop.parent;

            Image hud = CreateImage("SkinHud", canvasRect, null, Vector2.zero, Vector2.zero);
            hud.enabled = false;
            Stretch(hud.rectTransform);
            if (boardTop.parent == canvasRect) hud.transform.SetSiblingIndex(boardTop.GetSiblingIndex());
            RectTransform hudRect = hud.rectTransform;

            // Only the panels this puzzle uses are cut out of the HUD sheet (no HP / SHIELD or AMMO).
            // Each panel lives in a group pinned to its screen edge or corner and scaled by HudScale, with its text and lights.
            Sprite sheet = LoadSprite("minigame_hud_overlay_1920x1080");
            RectTransform objectiveGroup = CreateGroup("HudObjective", hudRect, 0f, 0f);
            RectTransform statusGroup = CreateGroup("HudStatus", hudRect, 1920f, 0f);
            RectTransform bandGroup = CreateGroup("HudSignal", hudRect, 960f, 0f);
            transmitGroup = CreateGroup("HudTransmit", hudRect, 0f, 540f);
            receiveGroup = CreateGroup("HudReceive", hudRect, 1920f, 540f);

            CreateHudPiece(objectiveGroup, sheet, new RectInt(0, 0, 490, 170));       // OBJECTIVE + top-left bracket
            CreateHudPiece(statusGroup, sheet, new RectInt(1430, 0, 490, 170));    // AETHER LEVEL / TIME REMAINING + top-right bracket
            CreateHudPiece(transmitGroup, sheet, new RectInt(0, 345, 290, 370));   // 송신 TRANSMIT
            CreateHudPiece(receiveGroup, sheet, new RectInt(1630, 345, 290, 370)); // 수신 RECEIVE
            BuildSignalBand(bandGroup, sheet);

            CreateText("SkinObjective", objectiveGroup, "신호를 수신 단말까지 연결하라", GroupLocal(objectiveGroup, 246f, 85f), new Vector2(370f, 36f), 20f, TextLight, TextAlignmentOptions.Left);
            levelText = CreateText("SkinLevel", statusGroup, "", GroupLocal(statusGroup, 1573f, 90f), new Vector2(175f, 38f), 22f, TextLight, TextAlignmentOptions.Center);
            timeText = CreateText("SkinTime", statusGroup, "", GroupLocal(statusGroup, 1774f, 90f), new Vector2(175f, 38f), 24f, Amber, TextAlignmentOptions.Center);

            for (int i = 0; i < 3; i++)
                CreateImage("SkinTxDot" + i, transmitGroup, transmitCode[i], GroupLocal(transmitGroup, TransmitDotX[i], TerminalDotY), Vector2.one * 34f);

            receiveDots = new Image[3];
            for (int i = 0; i < 3; i++)
                receiveDots[i] = CreateImage("SkinRxDot" + i, receiveGroup, dotOff, GroupLocal(receiveGroup, ReceiveDotX[i], TerminalDotY), Vector2.one * 34f);

            // The manager still writes its own timer; ours shows it in the TIME REMAINING box.
            if (manager.timerText != null) manager.timerText.gameObject.SetActive(false);
            if (manager.legacyTimerText != null) manager.legacyTimerText.gameObject.SetActive(false);

            // The board fills the screen down to the bottom edge, so the hint goes in the empty lower half of the 송신 panel.
            TMP_Text feedback = manager.feedbackText;
            if (feedback != null)
            {
                RectTransform feedbackRect = feedback.rectTransform;
                feedbackRect.SetParent(transmitGroup, false);
                feedbackRect.anchorMin = feedbackRect.anchorMax = feedbackRect.pivot = new Vector2(0.5f, 0.5f);
                feedbackRect.localScale = Vector3.one;
                feedbackRect.anchoredPosition = GroupLocal(transmitGroup, 139f, 612f);
                feedbackRect.sizeDelta = new Vector2(196f, 104f);
                feedback.alignment = TextAlignmentOptions.TopLeft;
                feedback.enableAutoSizing = false;
                feedback.fontSize = 19f;
                feedback.lineSpacing = 4f;
                feedback.textWrappingMode = TextWrappingModes.Normal;
                feedback.overflowMode = TextOverflowModes.Overflow;
                feedback.color = TextLight;
            }
        }

        // A HUD group pinned to a point of the screen given in sheet coordinates (e.g. 0,0 = top-left corner).
        // Its children are laid out in sheet pixels and the group is scaled by HudScale around that point.
        static RectTransform CreateGroup(string name, RectTransform parent, float sheetX, float sheetY)
        {
            RectTransform group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(parent, false);
            Vector2 anchor = new Vector2(sheetX / 1920f, 1f - sheetY / 1080f);
            group.anchorMin = group.anchorMax = group.pivot = anchor;
            group.anchoredPosition = Vector2.zero;
            group.sizeDelta = Vector2.zero;
            group.localScale = Vector3.one * HudScale;
            return group;
        }

        // Sheet coordinates -> position inside a group.
        static Vector2 GroupLocal(RectTransform group, float sheetX, float sheetY)
        {
            float anchorX = group.anchorMin.x * 1920f;
            float anchorY = (1f - group.anchorMin.y) * 1080f;
            return new Vector2(sheetX - anchorX, anchorY - sheetY);
        }

        // A piece of the 1920x1080 HUD sheet, top-left pixel coordinates (x, y, width, height), shadows included.
        static void CreateHudPiece(RectTransform group, Sprite sheet, RectInt panel)
        {
            if (sheet == null) return;
            Rect sheetRect = sheet.rect;
            float k = sheetRect.width / 1920f;
            Rect crop = new Rect(sheetRect.x + panel.x * k, sheetRect.y + (1080 - panel.y - panel.height) * k, panel.width * k, panel.height * k);
            Sprite piece = Sprite.Create(sheet.texture, crop, new Vector2(0.5f, 0.5f), sheet.pixelsPerUnit);
            CreateImage("HudPanel", group, piece, GroupLocal(group, panel.x + panel.width * 0.5f, panel.y + panel.height * 0.5f), new Vector2(panel.width, panel.height));
        }

        // ---------- signal band (connection progress) ----------

        // Sheet coordinates, top-left origin.
        static readonly RectInt BandPanel = new RectInt(560, 0, 800, 170);
        static readonly RectInt BandMarker = new RectInt(946, 58, 28, 18);   // the amber triangle drawn at the centre
        static readonly RectInt BandDashes = new RectInt(1232, 43, 74, 15);  // "– – – –" top right
        const int BandMajorTickSpacing = 85;
        const float BandScaleStartX = 619f;
        const float BandScaleEndX = 1301f;
        const float BandMarkerY = 67f;
        const float BandLineY = 80f;

        RectTransform bandGroup;
        RectTransform bandMarker;
        Image bandMarkerImage;
        RectTransform bandFill;
        TextMeshProUGUI bandPercent;
        float bandShown;
        float bandWrongUntil;

        void BuildSignalBand(RectTransform parent, Sprite sheet)
        {
            if (sheet == null || Mathf.Abs(sheet.rect.width - 1920f) > 0.5f)
                return;
            bandGroup = parent;

            Texture2D band = ReadSheet(sheet, BandPanel);
            Texture2D marker = ReadSheet(sheet, BandMarker);

            // Keep only the amber triangle for the moving marker.
            Color[] markerPixels = marker.GetPixels();
            for (int i = 0; i < markerPixels.Length; i++)
            {
                Color c = markerPixels[i];
                float amber = Mathf.Clamp01((c.r - c.b - 0.25f) * 3f);
                markerPixels[i] = new Color(c.r, c.g, c.b, c.a * amber);
            }
            marker.SetPixels(markerPixels);
            marker.Apply();

            // Paint over the drawn marker with the same stretch of scale one major tick to the left (the pattern repeats),
            // and clear the placeholder dashes with the empty panel area just left of them.
            CopyWithin(band, BandMarker, -BandMajorTickSpacing);
            CopyWithin(band, BandDashes, -80);
            band.Apply();

            Vector2 bandCenter = SheetToCanvas(BandPanel.x + BandPanel.width * 0.5f, BandPanel.y + BandPanel.height * 0.5f);
            CreateImage("HudSignalBand", parent, Sprite.Create(band, new Rect(0, 0, band.width, band.height), new Vector2(0.5f, 0.5f)),
                bandCenter, new Vector2(BandPanel.width, BandPanel.height));

            Image fill = CreateImage("HudSignalFill", parent, null, SheetToCanvas(BandScaleStartX, BandLineY), new Vector2(0f, 2f));
            fill.color = new Color(Amber.r, Amber.g, Amber.b, 0.85f);
            bandFill = fill.rectTransform;
            bandFill.pivot = new Vector2(0f, 0.5f);

            bandMarkerImage = CreateImage("HudSignalMarker", parent, Sprite.Create(marker, new Rect(0, 0, marker.width, marker.height), new Vector2(0.5f, 0.5f)),
                SheetToCanvas(BandScaleStartX, BandMarkerY), new Vector2(BandMarker.width, BandMarker.height));
            bandMarker = bandMarkerImage.rectTransform;

            bandPercent = CreateText("HudSignalPercent", parent, "0%", SheetToCanvas(BandDashes.x + BandDashes.width - 40f, BandDashes.y + BandDashes.height * 0.5f),
                new Vector2(80f, 24f), 18f, Amber, TextAlignmentOptions.Right);
        }

        // Sheet coordinates -> position inside the signal band group.
        Vector2 SheetToCanvas(float x, float y) => GroupLocal(bandGroup, x, y);

        // Reads part of the (non-readable) HUD sheet; the result's row 0 is the bottom of the area.
        static Texture2D ReadSheet(Sprite sheet, RectInt area)
        {
            Texture source = sheet.texture;
            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, temporary);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;
            Texture2D result = new Texture2D(area.width, area.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(sheet.rect.x + area.x, sheet.rect.y + (1080 - area.y - area.height), area.width, area.height), 0, 0);
            result.Apply();
            result.wrapMode = TextureWrapMode.Clamp;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return result;
        }

        // Copies, inside the band texture, the sheet area shifted by offsetX onto the given sheet area.
        static void CopyWithin(Texture2D band, RectInt sheetArea, int offsetX)
        {
            int localX = sheetArea.x - BandPanel.x;
            int localY = BandPanel.y + BandPanel.height - (sheetArea.y + sheetArea.height);
            Color[] pixels = band.GetPixels(localX + offsetX, localY, sheetArea.width, sheetArea.height);
            band.SetPixels(localX, localY, sheetArea.width, sheetArea.height, pixels);
        }

        // Share of the hidden correct chain that is connected (receiver connected = 100%).
        float GetSignalProgress()
        {
            if (manager.IsCleared) return 1f;
            IReadOnlyList<List<CP_Gate_Final>> columns = manager.GatesByColumn;
            if (columns.Count == 0) return 0f;

            int reached = 0;
            for (int c = 0; c < columns.Count; c++)
            {
                bool columnReached = false;
                foreach (CP_Gate_Final gate in columns[c])
                    if (gate != null && gate.isCorrectGate && gate.isActivatedByCorrectColor) columnReached = true;
                if (!columnReached) break;
                reached++;
            }

            return reached / (columns.Count + 1f);
        }

        void UpdateSignalBand()
        {
            if (bandMarker == null) return;

            float target = GetSignalProgress();
            bandShown = Mathf.MoveTowards(bandShown, target, Time.unscaledDeltaTime * 1.5f);
            if (target < bandShown) bandShown = Mathf.Lerp(bandShown, target, Time.unscaledDeltaTime * 8f);

            float x = Mathf.Lerp(BandScaleStartX, BandScaleEndX, bandShown);
            bandMarker.anchoredPosition = SheetToCanvas(x, BandMarkerY);
            bandFill.sizeDelta = new Vector2(x - BandScaleStartX, 2f);

            bool wrong = bandWrongUntil > Time.unscaledTime;
            bandMarkerImage.color = wrong ? new Color(1f, 0.45f, 0.4f, 1f) : Color.white;
            bandPercent.text = Mathf.RoundToInt(target * 100f) + "%";
            bandPercent.color = wrong ? AlertRed : Amber;
        }

        void UpdateHud()
        {
            UpdateSignalBand();

            // AETHER LEVEL 칸: 플레이어의 에테르 레벨(레벨마다 이 퍼즐의 제한 시간이 5초씩 늘어난다).
            if (levelText != null) levelText.text = "LV." + AetherLevel.Level.ToString("00");

            if (timeText != null)
            {
                float remaining = manager.RemainingTime;
                timeText.text = FormatTime(remaining);
                bool danger = manager.TotalTimeLimit > 0f && remaining <= manager.TotalTimeLimit * manager.dangerRatio && !manager.IsEnded;
                Color color = danger ? AlertRed : Amber;
                if (danger) color.a = Mathf.Lerp(0.4f, 1f, (Mathf.Sin(Time.unscaledTime * manager.flashSpeed) + 1f) * 0.5f);
                timeText.color = color;
            }

            if (receiveDots != null)
            {
                for (int i = 0; i < receiveDots.Length; i++)
                {
                    Sprite dot = manager.IsCleared ? transmitCode[i] : dotOff;
                    if (receiveDots[i].sprite != dot) receiveDots[i].sprite = dot;
                }
            }
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        // ---------- result screens ----------

        void BuildResultScreens()
        {
            successScreen = CreateResultScreen("SkinResultSuccess", "result_SUCCESS_overlay_1920x1080");
            successValues = new[]
            {
                CreateValue(successScreen, -443f), CreateValue(successScreen, -148f),
                CreateValue(successScreen, 147f), CreateValue(successScreen, 442f),
            };
            CreateButton(successScreen, new Vector2(0f, -376f), Continue);

            failScreen = CreateResultScreen("SkinResultFail", "result_FAIL_overlay_1920x1080");
            failValues = new[] { CreateValue(failScreen, -300f), CreateValue(failScreen, 0f), CreateValue(failScreen, 300f) };
            CreateButton(failScreen, new Vector2(-163f, -376f), Retry);
            CreateButton(failScreen, new Vector2(163f, -376f), GiveUp);
        }

        GameObject CreateResultScreen(string name, string spriteName)
        {
            // A dark backing under the result art, so the board behind doesn't show through the numbers.
            Image screen = CreateImage(name, canvasRect, null, Vector2.zero, Vector2.zero);
            Stretch(screen.rectTransform);
            screen.color = new Color(0f, 0f, 0f, 0.6f);
            screen.raycastTarget = true;

            // The art keeps 16:9 on any screen; the dark backing fills the rest.
            Image art = CreateImage("Art", screen.rectTransform, LoadSprite(spriteName), Vector2.zero, new Vector2(1920f, 1080f));
            AspectRatioFitter fitArt = art.gameObject.AddComponent<AspectRatioFitter>();
            fitArt.aspectRatio = 16f / 9f;
            fitArt.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            screen.gameObject.SetActive(false);
            return screen.gameObject;
        }

        TextMeshProUGUI CreateValue(GameObject screen, float x) =>
            CreateText("Value", (RectTransform)screen.transform, "", new Vector2(x, -196f), new Vector2(270f, 50f), 34f, ValueWhite, TextAlignmentOptions.Center);

        void CreateButton(GameObject screen, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            Image glow = CreateImage("Button", (RectTransform)screen.transform, null, position, new Vector2(300f, 72f));
            glow.color = new Color(Amber.r, Amber.g, Amber.b, 0.14f);
            glow.raycastTarget = true;

            Button button = glow.gameObject.AddComponent<Button>();
            button.targetGraphic = glow;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
        }

        void ShowResult(bool cleared)
        {
            resultShown = true;
            float elapsed = Mathf.Max(0f, manager.TotalTimeLimit - manager.RemainingTime);
            if (cleared)
            {
                int attempts = Mathf.Max(1, connectAttempts);
                int accuracy = Mathf.RoundToInt(100f * (attempts - wrongPicks) / attempts);
                successValues[0].text = FormatTime(elapsed);
                successValues[1].text = attempts.ToString();
                successValues[2].text = accuracy + "%";
                // BONUS = 이번에 얻은 에테르. 기본 300 + 정확도(최대 150) + 남은 시간 1초당 2. 이미 받은 퍼즐이면 0.
                int aether = AetherLevel.CircuitPuzzleExp + Mathf.RoundToInt(accuracy * 1.5f) + Mathf.RoundToInt(manager.RemainingTime * 2f);
                int granted = AetherLevel.ReportPuzzle("05-robot-circuit", aether, "데이터 회로 복구");
                successValues[3].text = granted > 0 ? "+" + granted + " AE" : "-";
                successScreen.transform.SetAsLastSibling();
                successScreen.SetActive(true);
            }
            else
            {
                failCount++;
                failValues[0].text = "무제한";
                failValues[1].text = "LV." + failCount;
                failValues[2].text = FormatTime(elapsed);
                failScreen.transform.SetAsLastSibling();
                failScreen.SetActive(true);
            }
        }

        void HideResults()
        {
            resultShown = false;
            if (successScreen != null) successScreen.SetActive(false);
            if (failScreen != null) failScreen.SetActive(false);
        }

        void HandleResultKeys()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            bool confirm = keyboard.fKey.wasPressedThisFrame;
            bool cancel = keyboard.escapeKey.wasPressedThisFrame;
#else
            bool confirm = Input.GetKeyDown(KeyCode.F);
            bool cancel = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (successScreen.activeSelf && confirm) Continue();
            else if (failScreen.activeSelf && confirm) Retry();
            else if (failScreen.activeSelf && cancel) GiveUp();
        }

        void Continue()
        {
            if (PuzzleSceneReturnBootstrap.IsWaitingForReturn)
                PuzzleSceneReturnBootstrap.RequestContinue();
            else
                manager.RestartSameDifficulty();
        }

        void Retry()
        {
            HideResults();
            manager.RestartSameDifficulty();
        }

        void GiveUp()
        {
            if (PuzzleSceneReturnBootstrap.IsWaitingForReturn)
                PuzzleSceneReturnBootstrap.RequestGiveUp();
            else
                Retry();
        }

        // ---------- helpers ----------

        static Image CreateImage(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size)
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

        TextMeshProUGUI CreateText(string name, RectTransform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    public class Puzzle2CrateHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool IsHovered { get; private set; }
        public void OnPointerEnter(PointerEventData eventData) => IsHovered = true;
        public void OnPointerExit(PointerEventData eventData) => IsHovered = false;
        void OnDisable() => IsHovered = false;
    }
}
