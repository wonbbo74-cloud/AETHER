using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CircuitPuzzle
{
    public class CircuitPuzzleManager_Final : MonoBehaviour
    {
        [Serializable] public class PuzzleEvent : UnityEvent<PuzzleDifficulty_Final> { }

        [Header("Final Flow")]
        [SerializeField] private bool startOnAwake;
        [SerializeField] private PuzzleDifficulty_Final defaultDifficulty = PuzzleDifficulty_Final.Normal;
        [SerializeField, Min(0f)] private float totalTimeLimit = 600f;
        public PuzzleEvent OnPuzzleSuccess;
        public PuzzleEvent OnPuzzleFailed;

        // Notifications for the design skin (Puzzle2MinigameSkin). Rules and behaviour are unchanged.
        public event Action<CP_Gate_Final> GateWrongSelected;
        public event Action<CP_Gate_Final> GateConnected;
        public IReadOnlyList<CP_Gate_Final> AllGates => allGates;
        public IReadOnlyList<List<CP_Gate_Final>> GatesByColumn => gatesByColumn;
        public float RemainingTime => remainingTime;
        public float TotalTimeLimit => totalTimeLimit + timeBonus;
        float timeBonus;   // 에테르 레벨 보너스(시작할 때 정해진다)
        public bool IsCleared => isCleared;
        public bool IsEnded => isEnded;
        public int ReferenceLevel => referenceLevel;
        public void RestartSameDifficulty() => StartPuzzle(activeDifficulty);
        // Set by the skin: connected lines amber, the line being dragged in the highlight colour.
        [NonSerialized] public bool useSkinLineColors;
        [NonSerialized] public Color skinLineColor = Color.white;
        [NonSerialized] public Color skinDragLineColor = Color.white;

        [Header("Parents")]
        public RectTransform boardParent;
        public RectTransform gatesParent;
        public RectTransform connectionsParent;

        [Header("Prefabs")]
        public GameObject gatePrefab;
        public GameObject portPrefab;
        public GameObject connectionLinePrefab;

        [Header("Puzzle Layout")]
        public Vector2 gateSize = new Vector2(200, 140);
        public Vector2 startGateSize = new Vector2(200, 140);
        public Vector2 endGateSize = new Vector2(200, 140);
        public bool useAutomaticColumnSpacing = true;
        public float columnSpacing = 240f;
        public float rowSpacing = 40f;
        public float paddingLeft = 80f;
        public float paddingRight = 80f;
        public float paddingTop = 40f;
        public float paddingBottom = 40f;
        public Vector2 inputPortOffset = Vector2.zero;
        public Vector2 outputPortOffset = Vector2.zero;
        public Vector2 portClickSize = new Vector2(48f, 48f);

        [Header("Sprites")]
        public Sprite okSprite;
        public Sprite normalGateSprite;
        public Sprite transmitterGateSprite;
        public Sprite receiverGateSprite;

        [Header("Line Settings")]
        public float lineWidth = 6f;
        public float minimumVisibleLineWidth = 4f;
        public int lineSortingOrder = 30000;
        public float elbowPadding = 40f;
        public float curveStrength = 120f;

        [Header("DDA")]
        [SerializeField] private Puzzle2DDAController_Final ddaController;

        [Header("Inactive Line")]
        [Range(0, 1)] public float inactiveLineAlpha = 0.18f;
        public float inactiveLineWidthMultiplier = 0.85f;

        [Header("OK UI")]
        public Vector2 okSize = new Vector2(70, 70);
        [Range(0, 1)] public float okBoxAlpha = 0.25f;

        [Header("Optional UI")]
        public TMP_Text timerText;
        public Text legacyTimerText;
        public GameObject clearPanel;
        public GameObject failPanel;
        public TMP_Text feedbackText;

        [Header("Timer Color")]
        public Color timerColorSafe = new Color(0.2f, 1f, 0.45f, 1f);
        public Color timerColorWarn = new Color(1f, 0.85f, 0.2f, 1f);
        public Color timerColorDanger = new Color(1f, 0.25f, 0.25f, 1f);
        [Range(0f, 1f)] public float warnRatio = 0.35f;
        [Range(0f, 1f)] public float dangerRatio = 0.15f;
        public bool flashWhenDanger = true;
        public float flashSpeed = 8f;

        [Header("SFX")]
        public AudioSource sfxSource;
        public AudioClip sfxConnect;
        public AudioClip sfxCorrect;
        public AudioClip sfxWrong;

        [Header("Debug")]
        public bool logPortEvents;

        readonly List<ColumnConfig_Final> columns = new List<ColumnConfig_Final>();
        readonly Dictionary<string, CP_Port_Final> portDict = new Dictionary<string, CP_Port_Final>();
        readonly Dictionary<string, CP_Gate_Final> gateDict = new Dictionary<string, CP_Gate_Final>();
        readonly Dictionary<string, Image> okImagesByGate = new Dictionary<string, Image>();
        readonly List<CP_Gate_Final> allGates = new List<CP_Gate_Final>();
        readonly List<List<CP_Gate_Final>> gatesByColumn = new List<List<CP_Gate_Final>>();
        readonly List<CP_ConnData_Final> connections = new List<CP_ConnData_Final>();
        readonly Dictionary<string, string> activeConnByOutput = new Dictionary<string, string>();

        Canvas canvas;
        Camera uiCam;
        CP_Port_Final draggingFrom;
        LineRenderer dragLine;
        GameObject dragLineRoot;
        bool fallbackPointerActive;
        bool isRunning;
        bool isEnded;
        bool isTimeOver;
        bool isCleared;
        float remainingTime;
        SignalType_Final correctColor;
        string transmitterGateId;
        PuzzleDifficulty_Final activeDifficulty;
        int referenceLevel;

        void Awake()
        {
            if (connectionsParent == null) connectionsParent = gatesParent != null ? gatesParent : boardParent;
            canvas = (gatesParent != null ? gatesParent : boardParent)?.GetComponentInParent<Canvas>();
            uiCam = canvas != null ? canvas.worldCamera : null;
            if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
            EnsureDDAController();

            if (startOnAwake)
                StartPuzzle(defaultDifficulty);
        }

        void Update()
        {
            if (!isRunning || isEnded)
                return;

            if (!isCleared)
                HandlePointerFallbackInput();

            if (totalTimeLimit > 0f)
            {
                remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
                UpdateTimerUI();

                if (remainingTime <= 0f)
                    FailPuzzle(true);
            }
        }

        public void StartPuzzle(PuzzleDifficulty_Final difficulty)
        {
            activeDifficulty = difficulty;
            referenceLevel = GetReferenceLevel(difficulty);
            ApplyDifficultyColumns(difficulty);

            isRunning = true;
            isEnded = false;
            isTimeOver = false;
            isCleared = false;
            timeBonus = totalTimeLimit > 0f ? AetherLevel.PuzzleBonusSeconds : 0f;   // 에테르 레벨마다 +5초
            remainingTime = totalTimeLimit + timeBonus;
            if (clearPanel != null) clearPanel.SetActive(false);
            if (failPanel != null) failPanel.SetActive(false);
            SetFeedback("");

            EnsureDDAController();
            ddaController?.ResetDDAForNewGame();
            GeneratePuzzleUI();
            ResetPuzzleState();
            ddaController?.OnPuzzleStarted(referenceLevel, allGates, gatesByColumn);
        }

        int GetReferenceLevel(PuzzleDifficulty_Final difficulty)
        {
            switch (difficulty)
            {
                case PuzzleDifficulty_Final.Easy: return 3;
                case PuzzleDifficulty_Final.Hard: return 7;
                default: return 5;
            }
        }

        void ApplyDifficultyColumns(PuzzleDifficulty_Final difficulty)
        {
            columns.Clear();
            int[] counts;
            switch (difficulty)
            {
                case PuzzleDifficulty_Final.Easy:
                    counts = new[] { 3, 4, 4, 3 };
                    break;
                case PuzzleDifficulty_Final.Hard:
                    counts = new[] { 6, 7, 8, 8, 7, 6 };
                    break;
                default:
                    counts = new[] { 5, 6, 7, 6, 5 };
                    break;
            }

            for (int i = 0; i < counts.Length; i++)
                columns.Add(new ColumnConfig_Final { gateCount = counts[i] });
        }

        void EnsureDDAController()
        {
            if (ddaController != null)
                return;

            ddaController = GetComponent<Puzzle2DDAController_Final>();
            if (ddaController == null)
                ddaController = gameObject.AddComponent<Puzzle2DDAController_Final>();
        }

        bool ValidateSetup()
        {
            if (columns.Count == 0 || gatePrefab == null || portPrefab == null || (gatesParent == null && boardParent == null))
                return false;

            if (connectionLinePrefab == null)
                Debug.LogError("[CircuitPuzzle Final] connectionLinePrefab is required.", this);

            return true;
        }

        void GeneratePuzzleUI()
        {
            if (!ValidateSetup()) return;

            RectTransform parent = gatesParent != null ? gatesParent : boardParent;
            RectTransform board = boardParent != null ? boardParent : parent;
            if (connectionsParent == null) connectionsParent = parent;

            ClearAllLinesImmediate();
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (connectionsParent != null && child == connectionsParent) continue;
                Destroy(child.gameObject);
            }

            portDict.Clear();
            gateDict.Clear();
            okImagesByGate.Clear();
            allGates.Clear();
            gatesByColumn.Clear();
            transmitterGateId = null;

            for (int i = 0; i < columns.Count; i++)
                gatesByColumn.Add(new List<CP_Gate_Final>());

            Vector2 boardSize = board.rect.size;
            float left = -boardSize.x * 0.5f + paddingLeft;
            float right = boardSize.x * 0.5f - paddingRight;
            int slotCount = columns.Count + 1;
            float step = useAutomaticColumnSpacing ? (right - left) / slotCount : columnSpacing;
            float startX = useAutomaticColumnSpacing ? left : -step * slotCount * 0.5f;
            float endX = useAutomaticColumnSpacing ? right : startX + step * slotCount;

            CP_Gate_Final startGate = CreateGate("startGate", "transmitter", -1, -1, new Vector2(startX, 0f), startGateSize);
            if (startGate == null) return;
            CreatePort(startGate, "start", "output", new Vector2(startGateSize.x * 0.5f, 0f) + outputPortOffset);
            transmitterGateId = startGate.gateId;

            for (int c = 0; c < columns.Count; c++)
            {
                int gateCount = columns[c].gateCount;
                float x = useAutomaticColumnSpacing ? left + step * (c + 1) : startX + step * (c + 1);
                float effectiveRowSpacing = GetEffectiveRowSpacing(boardSize.y, gateSize.y, gateCount);
                float totalHeight = (gateCount - 1) * (gateSize.y + effectiveRowSpacing);

                for (int g = 0; g < gateCount; g++)
                {
                    float y = totalHeight * 0.5f - g * (gateSize.y + effectiveRowSpacing);
                    string gateId = $"gate_{c}_{g}";
                    CP_Gate_Final gate = CreateGate(gateId, "normal", c, g, new Vector2(x, y), gateSize);
                    if (gate == null) return;
                    CreatePort(gate, $"{gateId}_in", "input", new Vector2(-gateSize.x * 0.5f, 0f) + inputPortOffset);
                    CreatePort(gate, $"{gateId}_out", "output", new Vector2(gateSize.x * 0.5f, 0f) + outputPortOffset);
                    gatesByColumn[c].Add(gate);
                }
            }

            CP_Gate_Final endGate = CreateGate("endGate", "end", columns.Count, -1, new Vector2(endX, 0f), endGateSize);
            if (endGate == null) return;
            CreatePort(endGate, "end", "input", new Vector2(-endGateSize.x * 0.5f, 0f) + inputPortOffset);
        }

        float GetEffectiveRowSpacing(float boardHeight, float oneGateHeight, int gateCount)
        {
            if (gateCount <= 1) return 0f;
            float availableHeight = Mathf.Max(0f, boardHeight - paddingTop - paddingBottom);
            float required = gateCount * oneGateHeight + (gateCount - 1) * rowSpacing;
            if (required <= availableHeight) return rowSpacing;
            return Mathf.Max(0f, (availableHeight - gateCount * oneGateHeight) / (gateCount - 1));
        }

        CP_Gate_Final CreateGate(string id, string type, int columnIndex, int gateIndex, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform parent = gatesParent != null ? gatesParent : boardParent;
            GameObject gateObject = Instantiate(gatePrefab, parent);
            gateObject.name = id;

            RectTransform rect = gateObject.GetComponent<RectTransform>();
            if (rect == null) return null;

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            CP_Gate_Final gate = gateObject.GetComponent<CP_Gate_Final>();
            if (gate == null) gate = gateObject.AddComponent<CP_Gate_Final>();

            gate.gateId = id;
            gate.gateType = type;
            gate.columnIndex = columnIndex;
            gate.gateIndexInColumn = gateIndex;
            gate.isCorrectGate = false;
            gate.isActivatedByCorrectColor = false;
            gate.SetDDADisabled(false);
            gate.gateRT = rect;
            gate.gateImage = gateObject.GetComponent<Image>();

            if (gate.gateImage != null)
            {
                gate.gateImage.color = Color.white;
                if (type == "transmitter")
                    gate.gateImage.sprite = transmitterGateSprite != null ? transmitterGateSprite : normalGateSprite;
                else if (type == "end")
                    gate.gateImage.sprite = receiverGateSprite != null ? receiverGateSprite : normalGateSprite;
                else if (normalGateSprite != null)
                    gate.gateImage.sprite = normalGateSprite;
            }

            gateDict[id] = gate;
            allGates.Add(gate);
            CreateOkUI(gateObject, id);
            return gate;
        }

        CP_Port_Final CreatePort(CP_Gate_Final gate, string id, string type, Vector2 localPosition)
        {
            GameObject portObject = Instantiate(portPrefab, gate.transform);
            portObject.name = id;

            RectTransform rect = portObject.GetComponent<RectTransform>();
            if (rect == null) return null;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = localPosition;
            if (rect.sizeDelta.sqrMagnitude < 1f)
                rect.sizeDelta = portClickSize;

            EnsurePortRaycastTarget(portObject);

            CP_Port_Final port = portObject.GetComponent<CP_Port_Final>();
            if (port == null) port = portObject.AddComponent<CP_Port_Final>();
            port.portId = id;
            port.portType = type;
            port.parentGate = gate;
            portDict[id] = port;

            PortUI_Final portUI = portObject.GetComponent<PortUI_Final>();
            if (portUI == null) portUI = portObject.AddComponent<PortUI_Final>();
            portUI.manager = this;
            portUI.port = port;
            return port;
        }

        void EnsurePortRaycastTarget(GameObject portObject)
        {
            Graphic graphic = portObject.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image image = portObject.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = true;
                return;
            }

            graphic.raycastTarget = true;
        }

        void CreateOkUI(GameObject gateObject, string gateId)
        {
            GameObject boxObject = new GameObject("OK_Box", typeof(RectTransform), typeof(Image));
            boxObject.transform.SetParent(gateObject.transform, false);
            Image boxImage = boxObject.GetComponent<Image>();
            boxImage.color = new Color(0, 0, 0, okBoxAlpha);
            boxImage.raycastTarget = false;

            RectTransform boxRect = boxObject.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = okSize;

            GameObject okObject = new GameObject("OK", typeof(RectTransform), typeof(Image));
            okObject.transform.SetParent(boxObject.transform, false);
            Image okImage = okObject.GetComponent<Image>();
            okImage.sprite = okSprite;
            okImage.enabled = false;
            okImage.raycastTarget = false;
            okImage.preserveAspect = true;

            RectTransform okRect = okObject.GetComponent<RectTransform>();
            okRect.anchorMin = okRect.anchorMax = new Vector2(0.5f, 0.5f);
            okRect.pivot = new Vector2(0.5f, 0.5f);
            okRect.sizeDelta = okSize;
            okImagesByGate[gateId] = okImage;
        }

        void ResetPuzzleState()
        {
            ClearAllLinesImmediate();
            isTimeOver = false;
            isCleared = false;
            correctColor = (SignalType_Final)UnityEngine.Random.Range(0, 3);
            GenerateCorrectPath();
            ApplyStartGateVisual();
            RecalculateActivationAndUpdateAll();
            UpdateTimerUI();
        }

        void GenerateCorrectPath()
        {
            foreach (CP_Gate_Final gate in allGates)
            {
                if (gate == null) continue;
                gate.isCorrectGate = false;
                gate.isActivatedByCorrectColor = false;
                gate.gateColor = gate.gateType == "transmitter" || gate.gateType == "end"
                    ? correctColor
                    : RandomColorExcept(correctColor);
            }

            for (int c = 0; c < gatesByColumn.Count; c++)
            {
                List<CP_Gate_Final> column = gatesByColumn[c];
                if (column == null || column.Count == 0) continue;

                CP_Gate_Final correctGate = column[UnityEngine.Random.Range(0, column.Count)];
                correctGate.isCorrectGate = true;
                correctGate.gateColor = correctColor;
            }
        }

        SignalType_Final RandomColorExcept(SignalType_Final except)
        {
            List<SignalType_Final> pool = new List<SignalType_Final> { SignalType_Final.Blue, SignalType_Final.Red, SignalType_Final.Green };
            pool.Remove(except);
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        void ApplyStartGateVisual()
        {
            foreach (CP_Gate_Final gate in allGates)
            {
                if (gate == null || gate.gateImage == null) continue;
                gate.gateImage.color = gate.gateType == "transmitter" ? SignalToColor(correctColor, 1f) : Color.white;
            }
        }

        void UpdateTimerUI()
        {
            string value = $"TIME : {Mathf.CeilToInt(remainingTime)}";
            float ratio = TotalTimeLimit <= 0f ? 1f : Mathf.Clamp01(remainingTime / TotalTimeLimit);
            Color color = ratio > warnRatio ? timerColorSafe :
                ratio > dangerRatio ? Color.Lerp(timerColorDanger, timerColorWarn, Mathf.InverseLerp(dangerRatio, warnRatio, ratio)) :
                timerColorDanger;

            if (ratio <= dangerRatio && flashWhenDanger)
                color.a = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f);

            if (timerText != null)
            {
                timerText.text = value;
                timerText.color = color;
            }

            if (legacyTimerText != null)
            {
                legacyTimerText.text = value;
                legacyTimerText.color = color;
            }
        }

        void FailPuzzle(bool timeout)
        {
            if (isEnded) return;

            isEnded = true;
            isRunning = false;
            isTimeOver = timeout;
            ClearAllLinesImmediate();
            AetherAudio.Play2D("ui_error", 0.7f, 0.8f, 0f);
            if (failPanel != null) failPanel.SetActive(true);

            OnPuzzleFailed?.Invoke(activeDifficulty);
        }

        void CompletePuzzle()
        {
            if (isEnded) return;

            isEnded = true;
            isRunning = false;
            isCleared = true;
            ClearAllLinesImmediate();
            ddaController?.OnPuzzleCleared();
            AetherAudio.Play2D("puzzle_clear", 0.75f, 1f, 0f);
            if (clearPanel != null) clearPanel.SetActive(true);
            OnPuzzleSuccess?.Invoke(activeDifficulty);
        }

        void ClearAllLinesImmediate()
        {
            if (dragLineRoot != null) Destroy(dragLineRoot);
            dragLine = null;
            dragLineRoot = null;
            draggingFrom = null;
            ClearAllConnections();
        }

        void ClearAllConnections()
        {
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                CP_ConnData_Final connection = connections[i];
                if (connection == null) continue;
                foreach (GameObject hit in connection.hitAreas)
                    if (hit != null) Destroy(hit);
                if (connection.root != null) Destroy(connection.root);
                connections.RemoveAt(i);
            }

            activeConnByOutput.Clear();
        }

        void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
                sfxSource.PlayOneShot(clip);
        }

        // 효과음 칸(sfxConnect·sfxCorrect·sfxWrong)이 비어 있어 퍼즐 내내 아무 소리도 나지 않았다.
        // 칸에 소리가 있으면 그것을, 없으면 게임 공용 효과음(AetherAudio)을 낸다.
        void PlaySfx(AudioClip clip, string fallback, float volume = 0.55f, float pitch = 1f)
        {
            if (clip != null && sfxSource != null)
                sfxSource.PlayOneShot(clip);
            else if (!string.IsNullOrEmpty(fallback))
                AetherAudio.Play2D(fallback, volume, pitch, 0.04f);
        }

        public void BeginDragFromPort(CP_Port_Final from, PointerEventData eventData)
        {
            if (!isRunning || isEnded || isTimeOver || isCleared || from == null || from.portType != "output")
                return;
            if (from.parentGate != null && from.parentGate.IsDDADisabled)
                return;
            if (connectionLinePrefab == null)
                return;

            draggingFrom = from;
            AetherAudio.Play2D("drag_pick", 0.45f, 1.1f, 0.04f);
            dragLineRoot = Instantiate(connectionLinePrefab, connectionsParent);
            dragLineRoot.name = "DraggingLine";
            dragLineRoot.transform.localScale = Vector3.one;
            dragLineRoot.transform.localPosition = Vector3.zero;
            dragLineRoot.transform.localRotation = Quaternion.identity;
            dragLine = dragLineRoot.GetComponent<LineRenderer>();
            if (dragLine == null)
            {
                Destroy(dragLineRoot);
                draggingFrom = null;
                return;
            }

            dragLine.useWorldSpace = false;
            ApplyLineStyle(dragLine, true, Color.white);
            Vector3 start = GetPortLocalCenterInConnectionsParent(from);
            dragLine.positionCount = 2;
            dragLine.SetPosition(0, start);
            dragLine.SetPosition(1, start);
            DragTo(eventData);
        }

        public void DragTo(PointerEventData eventData)
        {
            if (dragLine == null || draggingFrom == null) return;
            SetSmoothLocal(dragLine, GetPortLocalCenterInConnectionsParent(draggingFrom), ScreenToLocalInConnectionsParent(eventData.position));
        }

        public void EndDrag(PointerEventData eventData)
        {
            if (dragLine == null || draggingFrom == null)
            {
                draggingFrom = null;
                return;
            }

            CP_Port_Final target = RaycastPortUnderPointer(eventData);
            if (CanConnect(draggingFrom, target))
            {
                SetElbowLocal(dragLine, GetPortLocalCenterInConnectionsParent(draggingFrom), GetPortLocalCenterInConnectionsParent(target));
                bool correctTarget = IsCorrectConnectionTarget(draggingFrom, target);
                string baseFeedback = correctTarget ? "정상 신호가 연결되었습니다." : "신호가 크게 벗어났습니다. 정답 게이트와 거리가 있습니다.";
                PlaySfx(sfxConnect, "drag_snap", 0.55f);
                if (correctTarget) PlaySfx(sfxCorrect, "target_hit", 0.5f, 1.15f);
                else PlaySfx(sfxWrong, "ui_error", 0.55f);

                if (!correctTarget)
                {
                    CP_Gate_Final wrongGate = target.parentGate;
                    GateWrongSelected?.Invoke(wrongGate);
                    ClearCurrentTemporaryLine();
                    SetFeedback(ddaController != null ? ddaController.OnWrongGateSelected(wrongGate, baseFeedback) : baseFeedback);
                    draggingFrom = null;
                    fallbackPointerActive = false;
                    return;
                }

                SetFeedback(baseFeedback);
                RemoveConnectionsToInput(target.portId);

                CP_ConnData_Final connection = new CP_ConnData_Final
                {
                    id = Guid.NewGuid().ToString(),
                    fromPortId = draggingFrom.portId,
                    toPortId = target.portId,
                    line = dragLine,
                    root = dragLineRoot,
                    isActive = true,
                    lineColor = correctColor
                };

                BuildHitsForConnection(connection);
                connections.Add(connection);
                ApplyOutputExclusivity(connection);
                ApplyLineStyle(connection.line, true, SignalToColor(connection.lineColor, 1f));
                GateConnected?.Invoke(target.parentGate);
                RecalculateActivationAndUpdateAll();
            }
            else
            {
                ClearCurrentTemporaryLine();
                AetherAudio.Play2D("drag_cancel", 0.4f, 1f, 0.04f);
            }

            dragLine = null;
            dragLineRoot = null;
            draggingFrom = null;
            fallbackPointerActive = false;
        }

        void HandlePointerFallbackInput()
        {
            if (EventSystem.current == null) return;
            if (!TryGetPointerState(out Vector2 screenPosition, out bool pressed, out bool held, out bool released))
                return;

            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            if (pressed && dragLine == null)
            {
                CP_Port_Final port = FindPortAtScreenPosition(screenPosition, pointerData);
                if (port != null)
                {
                    BeginDragFromPort(port, pointerData);
                    fallbackPointerActive = dragLine != null;
                }
            }

            if (held && fallbackPointerActive && dragLine != null)
                DragTo(pointerData);

            if (released && fallbackPointerActive)
                EndDrag(pointerData);
        }

        bool TryGetPointerState(out Vector2 screenPosition, out bool pressed, out bool held, out bool released)
        {
            screenPosition = Vector2.zero;
            pressed = false;
            held = false;
            released = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                pressed = Mouse.current.leftButton.wasPressedThisFrame;
                held = Mouse.current.leftButton.isPressed;
                released = Mouse.current.leftButton.wasReleasedThisFrame;
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                pressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
                held = Touchscreen.current.primaryTouch.press.isPressed;
                released = Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            pressed = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            released = Input.GetMouseButtonUp(0);
            return true;
#endif
            return false;
        }

        CP_Port_Final FindPortAtScreenPosition(Vector2 screenPosition, PointerEventData pointerData)
        {
            CP_Port_Final raycastPort = RaycastPortUnderPointer(pointerData);
            if (raycastPort != null) return raycastPort;

            Camera cameraForCanvas = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCam : null;
            foreach (CP_Port_Final port in portDict.Values)
            {
                RectTransform rect = port != null ? port.GetComponent<RectTransform>() : null;
                if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, cameraForCanvas))
                    return port;
            }

            return null;
        }

        bool CanConnect(CP_Port_Final from, CP_Port_Final to)
        {
            if (from == null || to == null || from.portType != "output" || to.portType != "input") return false;
            CP_Gate_Final fromGate = from.parentGate;
            CP_Gate_Final toGate = to.parentGate;
            if (fromGate == null || toGate == null || fromGate.IsDDADisabled || toGate.IsDDADisabled) return false;
            if (fromGate.gateType == "transmitter")
                return toGate.gateType == "normal" && toGate.columnIndex == 0;
            if (toGate.gateType == "end")
                return fromGate.gateType == "normal" && fromGate.columnIndex == columns.Count - 1;
            return fromGate.gateType == "normal" && toGate.gateType == "normal" && toGate.columnIndex == fromGate.columnIndex + 1;
        }

        bool IsCorrectConnectionTarget(CP_Port_Final from, CP_Port_Final to)
        {
            CP_Gate_Final fromGate = from?.parentGate;
            CP_Gate_Final toGate = to?.parentGate;
            if (toGate == null) return false;
            if (toGate.gateType == "end")
                return fromGate != null && fromGate.columnIndex == columns.Count - 1 && fromGate.isCorrectGate;
            return toGate.isCorrectGate;
        }

        void SetFeedback(string message)
        {
            if (feedbackText != null)
                feedbackText.text = message;
        }

        void ClearCurrentTemporaryLine()
        {
            if (dragLineRoot != null) Destroy(dragLineRoot);
            dragLine = null;
            dragLineRoot = null;
        }

        CP_Port_Final RaycastPortUnderPointer(PointerEventData eventData)
        {
            if (EventSystem.current == null) return null;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (RaycastResult result in results)
            {
                PortUI_Final portUI = result.gameObject.GetComponentInParent<PortUI_Final>();
                if (portUI != null && portUI.port != null) return portUI.port;
            }
            return null;
        }

        public void RemoveConnectionById(string id)
        {
            if (isEnded) return;
            CP_ConnData_Final connection = connections.Find(x => x.id == id);
            if (connection == null) return;

            if (activeConnByOutput.TryGetValue(connection.fromPortId, out string activeId) && activeId == connection.id)
                activeConnByOutput.Remove(connection.fromPortId);

            foreach (GameObject hit in connection.hitAreas)
                if (hit != null) Destroy(hit);
            if (connection.root != null) Destroy(connection.root);
            connections.Remove(connection);
            RecalculateActivationAndUpdateAll();
        }

        void RemoveConnectionsToInput(string inputPortId)
        {
            List<CP_ConnData_Final> toRemove = connections.FindAll(connection => connection.toPortId == inputPortId);
            foreach (CP_ConnData_Final connection in toRemove)
                RemoveConnectionById(connection.id);
        }

        void ApplyOutputExclusivity(CP_ConnData_Final newConnection)
        {
            if (activeConnByOutput.TryGetValue(newConnection.fromPortId, out string previousId) && previousId != newConnection.id)
            {
                CP_ConnData_Final previous = connections.Find(connection => connection.id == previousId);
                if (previous != null) previous.isActive = false;
            }

            activeConnByOutput[newConnection.fromPortId] = newConnection.id;
            newConnection.isActive = true;
        }

        void RecalculateActivationAndUpdateAll()
        {
            foreach (CP_Gate_Final gate in allGates)
                if (gate != null) gate.isActivatedByCorrectColor = false;

            if (!string.IsNullOrEmpty(transmitterGateId) && gateDict.TryGetValue(transmitterGateId, out CP_Gate_Final transmitter))
                transmitter.isActivatedByCorrectColor = true;

            bool changed = true;
            int safety = 0;
            while (changed && safety++ < 200)
            {
                changed = false;
                foreach (CP_ConnData_Final connection in connections)
                {
                    if (connection == null || !connection.isActive || connection.lineColor != correctColor) continue;
                    if (!portDict.TryGetValue(connection.fromPortId, out CP_Port_Final fromPort)) continue;
                    if (!portDict.TryGetValue(connection.toPortId, out CP_Port_Final toPort)) continue;
                    if (fromPort.parentGate == null || toPort.parentGate == null) continue;
                    if (!fromPort.parentGate.isActivatedByCorrectColor) continue;

                    if (!toPort.parentGate.isActivatedByCorrectColor)
                    {
                        toPort.parentGate.isActivatedByCorrectColor = true;
                        changed = true;
                    }
                }
            }

            foreach (CP_ConnData_Final connection in connections)
            {
                if (connection.line != null)
                    ApplyLineStyle(connection.line, connection.isActive, SignalToColor(connection.lineColor, 1f));
            }

            UpdateOKAndClear();
        }

        void UpdateOKAndClear()
        {
            foreach (Image image in okImagesByGate.Values)
                if (image != null) image.enabled = false;

            bool cleared = false;
            foreach (CP_Gate_Final gate in allGates)
            {
                if (gate == null) continue;
                if (gate.gateType == "normal" && gate.isCorrectGate && gate.isActivatedByCorrectColor)
                {
                    if (okImagesByGate.TryGetValue(gate.gateId, out Image okImage) && okImage != null)
                        okImage.enabled = true;
                }

                if (gate.gateType == "end" && gate.isActivatedByCorrectColor)
                    cleared = true;
            }

            if (cleared && !isCleared)
                CompletePuzzle();
        }

        void ApplyLineStyle(LineRenderer lineRenderer, bool active, Color color)
        {
            if (lineRenderer == null) return;
            if (lineRenderer.sharedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null) lineRenderer.material = new Material(shader);
            }

            if (useSkinLineColors && active)
                color = color == Color.white ? skinDragLineColor : skinLineColor;
            Color finalColor = active ? color : new Color(0.7f, 0.7f, 0.7f, inactiveLineAlpha);
            // The line is world-space width inside a Screen Space Camera canvas (canvas scale ~0.009).
            // The minimum width (4) was in canvas pixels but used as world units, so the line became ~450 px wide (a red blob covering the screen).
            // Convert the minimum to world units by the canvas scale, so the scene's lineWidth (0.05 ≈ 5–6 px) shows as intended.
            float canvasScale = connectionsParent != null ? Mathf.Abs(connectionsParent.lossyScale.x) : 1f;
            float minimumWorldWidth = minimumVisibleLineWidth * Mathf.Max(0.0001f, canvasScale);
            float width = Mathf.Max(minimumWorldWidth, lineWidth) * (active ? 1f : inactiveLineWidthMultiplier);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = finalColor;
            lineRenderer.endColor = finalColor;

            Renderer rendererComponent = lineRenderer.GetComponent<Renderer>();
            if (rendererComponent != null)
            {
                rendererComponent.sortingLayerName = "UI";
                rendererComponent.sortingOrder = lineSortingOrder;
            }
        }

        Color SignalToColor(SignalType_Final signal, float alpha)
        {
            switch (signal)
            {
                case SignalType_Final.Blue: return new Color(0.2f, 0.55f, 1f, alpha);
                case SignalType_Final.Red: return new Color(1f, 0.25f, 0.25f, alpha);
                case SignalType_Final.Green: return new Color(0.2f, 1f, 0.45f, alpha);
                default: return new Color(1f, 1f, 1f, alpha);
            }
        }

        Vector3 ScreenToLocalInConnectionsParent(Vector2 screenPosition)
        {
            Camera cameraForCanvas = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCam : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(connectionsParent, screenPosition, cameraForCanvas, out Vector2 local);
            return new Vector3(local.x, local.y, 0f);
        }

        Vector3 GetPortLocalCenterInConnectionsParent(CP_Port_Final port)
        {
            RectTransform rect = port != null ? port.GetComponent<RectTransform>() : null;
            if (rect == null) return Vector3.zero;

            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            Camera cameraForCanvas = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? uiCam : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cameraForCanvas, worldCenter);
            return ScreenToLocalInConnectionsParent(screen);
        }

        void SetSmoothLocal(LineRenderer lineRenderer, Vector3 start, Vector3 end)
        {
            const int SegmentCount = 20;
            lineRenderer.positionCount = SegmentCount;
            Vector3 c1 = start + Vector3.right * curveStrength;
            Vector3 c2 = end + Vector3.left * curveStrength;
            for (int i = 0; i < SegmentCount; i++)
            {
                float t = i / (SegmentCount - 1f);
                float u = 1f - t;
                Vector3 point = (u * u * u) * start + (3 * u * u * t) * c1 + (3 * u * t * t) * c2 + (t * t * t) * end;
                point.z = 0f;
                lineRenderer.SetPosition(i, point);
            }
        }

        void SetElbowLocal(LineRenderer lineRenderer, Vector3 start, Vector3 end)
        {
            float padding = Mathf.Max(0.01f, elbowPadding);
            float middleX = (start.x + end.x) * 0.5f;
            float exitX = Mathf.Min(start.x + padding, middleX);
            float enterX = Mathf.Max(end.x - padding, middleX);
            lineRenderer.positionCount = 5;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, new Vector3(exitX, start.y, 0f));
            lineRenderer.SetPosition(2, new Vector3(enterX, start.y, 0f));
            lineRenderer.SetPosition(3, new Vector3(enterX, end.y, 0f));
            lineRenderer.SetPosition(4, end);
        }

        void BuildHitsForConnection(CP_ConnData_Final connection)
        {
            int count = connection.line.positionCount;
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 a3 = connection.line.GetPosition(i);
                Vector3 b3 = connection.line.GetPosition(i + 1);
                Vector2 a = new Vector2(a3.x, a3.y);
                Vector2 b = new Vector2(b3.x, b3.y);
                Vector2 direction = b - a;
                float length = direction.magnitude;
                if (length < 0.01f) continue;

                Vector2 middle = (a + b) * 0.5f;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                GameObject hitObject = new GameObject("Hit", typeof(RectTransform), typeof(Image));
                hitObject.transform.SetParent(connectionsParent != null ? connectionsParent : gatesParent, false);
                Image image = hitObject.GetComponent<Image>();
                image.color = new Color(0, 0, 0, 0);
                image.raycastTarget = true;

                RectTransform rect = hitObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = middle;
                rect.sizeDelta = new Vector2(length, 50f);
                rect.localRotation = Quaternion.Euler(0, 0, angle);

                ConnectionLineHit_Final hit = hitObject.AddComponent<ConnectionLineHit_Final>();
                hit.manager = this;
                hit.connectionId = connection.id;
                connection.hitAreas.Add(hitObject);
            }
        }
    }
}
