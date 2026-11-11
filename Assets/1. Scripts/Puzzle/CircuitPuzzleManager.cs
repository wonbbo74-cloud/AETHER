using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro; // ✅ TMP 추가

namespace CircuitPuzzle
{
    public enum SignalType { Blue = 0, Red = 1, Green = 2 }

    // ===================== JSON =====================
    [Serializable]
    public class CircuitPuzzleData
    {
        public List<GateData> gates;
        public List<SolutionEdge> correctSolution; // 사용 안 해도 됨(있어도 무시됨)
    }

    [Serializable]
    public class SolutionEdge
    {
        public int index;
        public string fromPortId;
        public string toPortId;
    }

    [Serializable]
    public class GateData
    {
        public string id;
        public string gateType; // "transmitter" / "normal" / "end"
        public Vector2Data position;
        public SizeData size;
        public List<PortData> ports;
    }

    [Serializable] public class PortData { public string id; public Vector2Data localPosition; public string type; }
    [Serializable] public class Vector2Data { public float x; public float y; public Vector2 ToVector2() => new Vector2(x, y); }
    [Serializable] public class SizeData { public float width; public float height; public Vector2 ToVector2() => new Vector2(width, height); }

    // ===================== Runtime =====================
    public class CP_Gate : MonoBehaviour
    {
        public string gateId;
        public string gateType;

        // ✅ 숨겨진 게이트 색(테스트해봐야 알 수 있음)
        public SignalType gateColor;

        // ✅ 정답색(올바른 색)으로 도달했는지(경로 검증용)
        public bool isActivatedByCorrectColor;

        public Image gateImage;
        public RectTransform gateRT;
    }

    public class CP_Port : MonoBehaviour
    {
        public string portId;
        public string portType; // "input" / "output"
        public CP_Gate parentGate;
    }

    public class PortUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public CircuitPuzzleManager manager;
        public CP_Port port;

        public void OnPointerDown(PointerEventData ed)
        {
            if (manager == null)
            {
                Debug.LogError("[CircuitPuzzle] PortUI: manager가 비어 있습니다.");
                return;
            }
            if (port == null)
            {
                Debug.LogError("[CircuitPuzzle] PortUI: port가 비어 있습니다.");
                return;
            }
            Debug.Log($"[CircuitPuzzle] 포인터 다운: portId={port.portId}, type={port.portType} (output만 드래그 시작)");
            manager.BeginDragFromPort(port, ed);
        }
        public void OnDrag(PointerEventData ed) => manager?.DragTo(ed);
        public void OnPointerUp(PointerEventData ed) => manager?.EndDrag(ed);
    }

    public class ConnectionLineHit : MonoBehaviour, IPointerClickHandler
    {
        public CircuitPuzzleManager manager;
        public string connectionId;

        float lastClickTime = 0f;
        const float DoubleClickWindow = 0.35f;

        public void OnPointerClick(PointerEventData ed)
        {
            if (Time.time - lastClickTime < DoubleClickWindow)
                manager?.RemoveConnectionById(connectionId);

            lastClickTime = Time.time;
        }
    }

    // ===================== Manager =====================
    public class CircuitPuzzleManager : MonoBehaviour
    {

public float ResultShowTime => resultShowTime;
        public event Action OnPuzzleCleared;

        [Header("필수 할당")]
        public RectTransform gatesParent;
        public RectTransform connectionsParent;
        public GameObject gatePrefab;
        public GameObject portPrefab;
        public GameObject connectionLinePrefab;
        public Sprite okSprite;
        public TextAsset puzzleDataJson;

        [Header("게이트 스프라이트(중요)")]
        public Sprite normalGateSprite;
        public Sprite transmitterGateSprite; // START용
        public Sprite receiverGateSprite;    // END용

        [Header("라인 설정")]
        public float lineWidth = 6f;
        public int lineSortingOrder = 30000;
        public float elbowPadding = 40f;
        public float curveStrength = 120f;

        [Header("비활성 라인")]
        [Range(0, 1)] public float inactiveLineAlpha = 0.18f;
        public float inactiveLineWidthMultiplier = 0.85f;

        [Header("OK UI")]
        public Vector2 okSize = new Vector2(70, 70);
        [Range(0, 1)] public float okBoxAlpha = 0.25f;

        [Header("좌표 반전")]
        public bool invertY = true;

        [Header("컬럼(세로줄) 묶음 기준 (x 거리)")]
        public float columnClusterThreshold = 260f;

        // ===================== TIMER (추가/수정) =====================
        [Header("⏱ Timer")]
        public float timeLimit = 60f;   // 제한 시간(초)
        public TMP_Text timerText;      // ✅ UI Text -> TMP_Text 로 변경

        [Header("⏱ Timer Color")]
        public Color timerColorSafe = new Color(0.2f, 1f, 0.45f, 1f);     // 초록
        public Color timerColorWarn = new Color(1f, 0.85f, 0.2f, 1f);     // 노랑
        public Color timerColorDanger = new Color(1f, 0.25f, 0.25f, 1f);  // 빨강
        [Range(0f, 1f)] public float warnRatio = 0.35f;   // 35% 이하부터 경고
        [Range(0f, 1f)] public float dangerRatio = 0.15f; // 15% 이하부터 위험
        public bool flashWhenDanger = true;
        public float flashSpeed = 8f;

        [Header("⏱ TimeOver Reset")]
        public float resetDelay = 0.5f; // (현재는 사용 안 해도 남겨둠)

        float remainingTime;
        bool isTimeOver = false;
        bool isClearedByTimer = false;
        // ============================================================

        // ===================== RESULT UI (추가) =====================
        [Header("✅ Result UI")]
        public GameObject clearPanel; // 첫번째 이미지(클리어)
        public GameObject failPanel;  // 두번째 이미지(실패)
        public float resultShowTime = 1.2f; // 결과 이미지 보여주는 시간(초)
        // ============================================================

        // ===================== SFX (추가) =====================
        [Header("🔊 SFX")]
        public AudioSource sfxSource;     // AudioSource(매니저 오브젝트에 붙이기)
        public AudioClip sfxConnect;      // 선 연결 기본 소리
        public AudioClip sfxCorrect;      // 정답 연결 소리
        public AudioClip sfxWrong;        // 오답 연결 소리
        // ======================================================

        CircuitPuzzleData puzzleData;

        readonly Dictionary<string, CP_Port> portDict = new Dictionary<string, CP_Port>();
        readonly Dictionary<string, CP_Gate> gateDict = new Dictionary<string, CP_Gate>();
        readonly Dictionary<string, Image> okImagesByGate = new Dictionary<string, Image>();

        [Serializable]
        public class CP_ConnData
        {
            public string id;
            public string fromPortId; // output
            public string toPortId;   // input
            public LineRenderer line;
            public GameObject root;
            public List<GameObject> hitAreas = new List<GameObject>();

            public bool isActive = true;  // output 독점으로 비활성 가능
            public SignalType lineColor;  // 실제 표시될 선 색(정답/오답 규칙 반영)
        }

        readonly List<CP_ConnData> connections = new List<CP_ConnData>();

        // ✅ outputPortId -> activeConnectionId (출력은 마지막 연결만 활성)
        readonly Dictionary<string, string> activeConnByOutput = new Dictionary<string, string>();

        CP_Port draggingFrom;
        LineRenderer dragLine;
        GameObject dragLineRoot;

        Canvas canvas;
        Camera uiCam;

        // ✅ 이번 판 정답색(START가 보여주는 색)
        SignalType correctColor;

        string transmitterGateId = null;
        string endGateId = null;

        // ✅ "정답 게이트" 목록(컬럼마다 1개씩) - normal만 포함
        readonly HashSet<string> correctGateIds = new HashSet<string>();

        void Start()
        {
            LoadPuzzleData();
            SetupUI();

            canvas = gatesParent != null ? gatesParent.GetComponentInParent<Canvas>() : null;
            uiCam = (canvas != null) ? canvas.worldCamera : null;

            if (connectionsParent == null) connectionsParent = gatesParent;

            // ===================== SFX INIT (추가) =====================
            if (sfxSource == null) sfxSource = GetComponent<AudioSource>();
            // ===========================================================

            // ✅ 이번 판 정답색 랜덤
            correctColor = (SignalType)UnityEngine.Random.Range(0, 3);

            // ✅ "컬럼(세로줄)마다 정답 게이트 1개" 방식으로 색 배정
            AssignGateColors_UniqueCorrectPerColumn();

            // ✅ START는 정답색을 "보여줘야" 함
            ApplyStartGateVisual();

            // ✅ 최초 갱신
            RecalculateActivationAndUpdateAll();

            // ===================== TIMER INIT (추가만) =====================
            remainingTime = timeLimit;
            UpdateTimerUI();
            // ==============================================================

            // ===================== RESULT UI INIT (추가) =====================
            if (clearPanel) clearPanel.SetActive(false);
            if (failPanel) failPanel.SetActive(false);
            // ================================================================
        }

        // ===================== TIMER UPDATE (추가만) =====================
        void Update()
        {
            if (isTimeOver || isClearedByTimer) return;

            remainingTime -= Time.deltaTime;
            if (remainingTime < 0) remainingTime = 0;

            UpdateTimerUI();

            if (remainingTime <= 0f)
                OnTimeOver();
        }

        // ✅ 수정: 남은 시간에 따라 색이 점점 변하도록
        void UpdateTimerUI()
        {
            if (timerText == null) return;

            int sec = Mathf.CeilToInt(remainingTime);
            timerText.text = $"TIME : {sec}";

            float ratio = (timeLimit <= 0f) ? 0f : Mathf.Clamp01(remainingTime / timeLimit);

            Color c;

            if (ratio > warnRatio)
            {
                c = timerColorSafe;
            }
            else if (ratio > dangerRatio)
            {
                float t = Mathf.InverseLerp(warnRatio, dangerRatio, ratio);
                c = Color.Lerp(timerColorDanger, timerColorWarn, t);
            }
            else
            {
                c = timerColorDanger;

                if (flashWhenDanger)
                {
                    float pulse = (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f;
                    c.a = Mathf.Lerp(0.35f, 1f, pulse);
                }
            }

            timerText.color = c;
        }

        // ✅ 수정: 타임오버 시 실패 이미지 띄우고 "선 전부 제거" 후 리셋
        void OnTimeOver()
        {
            if (isTimeOver) return;

            isTimeOver = true;
            Debug.Log("⏰ TIME OVER");

            // ✅ 결과 패널 뜰 때 선이 겹치지 않게 즉시 제거
            ClearAllLinesImmediate();

            if (failPanel) failPanel.SetActive(true);

            float delay = Mathf.Max(0f, resultShowTime);
            if (delay <= 0f) ResetPuzzleToStart();
            else Invoke(nameof(ResetPuzzleToStart), delay);
        }

        // ✅ 추가: 결과 패널 표시 시 "모든 선" 즉시 제거(드래그중 라인 포함)
        void ClearAllLinesImmediate()
        {
            if (dragLineRoot != null) Destroy(dragLineRoot);
            dragLine = null;
            dragLineRoot = null;
            draggingFrom = null;

            ClearAllConnections();
        }

        // ✅ 추가: 퍼즐을 "처음 상태"로 리셋
        void ResetPuzzleToStart()
        {
            if (clearPanel) clearPanel.SetActive(false);
            if (failPanel) failPanel.SetActive(false);

            // 드래그 중인 라인이 있으면 제거
            if (dragLineRoot != null) Destroy(dragLineRoot);
            dragLine = null;
            dragLineRoot = null;
            draggingFrom = null;

            // 모든 연결 제거
            ClearAllConnections();

            // 상태 플래그 리셋
            isTimeOver = false;
            isClearedByTimer = false;

            // 처음처럼 정답색/정답게이트 다시 랜덤 배치
            correctColor = (SignalType)UnityEngine.Random.Range(0, 3);
            correctGateIds.Clear();
            AssignGateColors_UniqueCorrectPerColumn();
            ApplyStartGateVisual();

            // 퍼즐 상태 갱신
            RecalculateActivationAndUpdateAll();

            // 타이머 리셋
            remainingTime = timeLimit;
            UpdateTimerUI();

            Debug.Log("[Puzzle] 🔄 RESET to START");
        }

        // ✅ 추가: 모든 연결선/히트박스 삭제
        void ClearAllConnections()
        {
            for (int i = connections.Count - 1; i >= 0; i--)
            {
                var c = connections[i];
                if (c == null) continue;

                if (c.hitAreas != null)
                {
                    foreach (var h in c.hitAreas)
                        if (h) Destroy(h);
                }

                if (c.root) Destroy(c.root);

                connections.RemoveAt(i);
            }

            activeConnByOutput.Clear();
        }
        // ===============================================================

        // ===================== SFX HELPER (추가) =====================
        void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            if (sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }
        // ============================================================

        void LoadPuzzleData()
        {
            if (!puzzleDataJson)
            {
                Debug.LogError("[Puzzle] puzzleDataJson(TextAsset)이 비어있습니다.");
                return;
            }

            puzzleData = JsonUtility.FromJson<CircuitPuzzleData>(puzzleDataJson.text);
            if (puzzleData == null || puzzleData.gates == null)
                Debug.LogError("[Puzzle] JSON 파싱 실패 또는 gates 없음");
        }

        void SetupUI()
        {
            if (!gatesParent || puzzleData == null || puzzleData.gates == null) return;
            if (!gatePrefab || !portPrefab)
            {
                Debug.LogError("[Puzzle] gatePrefab / portPrefab이 비어있습니다.");
                return;
            }

            if (EventSystem.current == null)
                Debug.LogError("[Puzzle] EventSystem이 씬에 없습니다. Hierarchy > UI > Event System 추가하세요.");

            for (int i = gatesParent.childCount - 1; i >= 0; i--) Destroy(gatesParent.GetChild(i).gameObject);

            portDict.Clear();
            gateDict.Clear();
            okImagesByGate.Clear();
            connections.Clear();
            activeConnByOutput.Clear();
            transmitterGateId = null;
            endGateId = null;
            correctGateIds.Clear();

            foreach (var gd in puzzleData.gates)
            {
                var gateGO = Instantiate(gatePrefab, gatesParent);
                gateGO.name = string.IsNullOrEmpty(gd.id) ? "Gate" : gd.id;

                var grt = gateGO.GetComponent<RectTransform>();
                if (grt == null)
                {
                    Debug.LogError("[Puzzle] gatePrefab은 RectTransform(UI)이어야 합니다.");
                    return;
                }

                grt.anchorMin = grt.anchorMax = new Vector2(0, 1);
                grt.pivot = new Vector2(0, 1);

                Vector2 gp = new Vector2(gd.position.x, gd.position.y);
                if (invertY) gp.y = -gp.y;
                grt.anchoredPosition = gp;

                Vector2 size = gd.size != null ? gd.size.ToVector2() : new Vector2(200, 140);
                grt.sizeDelta = size;
                grt.localScale = Vector3.one;

                var gate = gateGO.AddComponent<CP_Gate>();
                gate.gateId = gd.id;
                gate.gateType = string.IsNullOrEmpty(gd.gateType) ? "normal" : gd.gateType.Trim().ToLower();
                gate.gateImage = gateGO.GetComponent<Image>();
                gate.gateRT = grt;
                gate.isActivatedByCorrectColor = false;

                // ✅ START/END 스프라이트 분리 적용
                if (gate.gateImage != null)
                {
                    if (gate.gateType == "transmitter")
                    {
                        gate.gateImage.sprite = transmitterGateSprite != null ? transmitterGateSprite : normalGateSprite;
                    }
                    else if (gate.gateType == "end")
                    {
                        gate.gateImage.sprite = receiverGateSprite != null ? receiverGateSprite : normalGateSprite;
                    }
                    else
                    {
                        gate.gateImage.sprite = normalGateSprite != null ? normalGateSprite : gate.gateImage.sprite;
                    }
                }

                if (gate.gateType == "transmitter") transmitterGateId = gate.gateId;
                if (gate.gateType == "end") endGateId = gate.gateId;

                gateDict[gate.gateId] = gate;

                CreateOkUI(gateGO, gate.gateId);

                // ports
                if (gd.ports == null) continue;

                foreach (var pd in gd.ports)
                {
                    var portGO = Instantiate(portPrefab, gateGO.transform);
                    portGO.name = string.IsNullOrEmpty(pd.id) ? "Port" : pd.id;

                    var prt = portGO.GetComponent<RectTransform>();
                    if (prt == null)
                    {
                        Debug.LogError("[Puzzle] portPrefab은 RectTransform(UI)이어야 합니다.");
                        return;
                    }

                    prt.anchorMin = prt.anchorMax = new Vector2(0, 1);
                    prt.pivot = new Vector2(0.5f, 0.5f);

                    Vector2 lp = new Vector2(pd.localPosition.x, pd.localPosition.y);
                    if (invertY) lp.y = -lp.y;
                    prt.anchoredPosition = lp;
                    prt.localScale = Vector3.one;

                    var port = portGO.AddComponent<CP_Port>();
                    port.portId = pd.id;
                    port.portType = (pd.type ?? "").Trim().ToLower();
                    port.parentGate = gate;

                    portDict[port.portId] = port;

                    var pui = portGO.AddComponent<PortUI>();
                    pui.manager = this;
                    pui.port = port;
                }
            }

            if (string.IsNullOrEmpty(transmitterGateId))
                Debug.LogWarning("[Puzzle] gateType=transmitter 게이트를 찾지 못했습니다.");
            if (string.IsNullOrEmpty(endGateId))
                Debug.LogWarning("[Puzzle] gateType=end 게이트를 찾지 못했습니다.");
        }

        void CreateOkUI(GameObject gateGO, string gateId)
        {
            var boxGO = new GameObject("OK_Box", typeof(RectTransform), typeof(Image));
            boxGO.transform.SetParent(gateGO.transform, false);

            var boxImg = boxGO.GetComponent<Image>();
            boxImg.color = new Color(0, 0, 0, okBoxAlpha);
            boxImg.raycastTarget = false;

            var boxRT = boxGO.GetComponent<RectTransform>();
            boxRT.anchorMin = boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.pivot = new Vector2(0.5f, 0.5f);
            boxRT.anchoredPosition = Vector2.zero;
            boxRT.sizeDelta = okSize;

            var okGO = new GameObject("OK", typeof(RectTransform), typeof(Image));
            okGO.transform.SetParent(boxGO.transform, false);

            var okImg = okGO.GetComponent<Image>();
            okImg.sprite = okSprite;
            okImg.enabled = false;
            okImg.raycastTarget = false;
            okImg.preserveAspect = true;

            var okRT = okGO.GetComponent<RectTransform>();
            okRT.anchorMin = okRT.anchorMax = new Vector2(0.5f, 0.5f);
            okRT.pivot = new Vector2(0.5f, 0.5f);
            okRT.anchoredPosition = Vector2.zero;
            okRT.sizeDelta = okSize;

            okImagesByGate[gateId] = okImg;
        }

        // =========================================================
        // "컬럼(세로줄)마다 정답 게이트는 단 1개"
        // =========================================================
        void AssignGateColors_UniqueCorrectPerColumn()
        {
            // 1) transmitter / end 처리
            foreach (var gate in gateDict.Values)
            {
                if (gate == null) continue;

                if (gate.gateType == "transmitter")
                {
                    gate.gateColor = correctColor;
                }
                else if (gate.gateType == "end")
                {
                    gate.gateColor = correctColor;
                }
            }

            // 2) normal 게이트만 컬럼으로 묶기
            List<CP_Gate> normals = new List<CP_Gate>();
            foreach (var gate in gateDict.Values)
            {
                if (gate == null) continue;
                if (gate.gateType == "normal") normals.Add(gate);
            }

            // x 기준 정렬
            normals.Sort((a, b) => a.gateRT.anchoredPosition.x.CompareTo(b.gateRT.anchoredPosition.x));

            // 3) 컬럼 클러스터링
            List<List<CP_Gate>> columns = new List<List<CP_Gate>>();
            foreach (var g in normals)
            {
                float x = g.gateRT.anchoredPosition.x;

                if (columns.Count == 0)
                {
                    columns.Add(new List<CP_Gate> { g });
                    continue;
                }

                var lastCol = columns[columns.Count - 1];
                float lastX = lastCol[0].gateRT.anchoredPosition.x;

                if (Mathf.Abs(x - lastX) <= columnClusterThreshold)
                    lastCol.Add(g);
                else
                    columns.Add(new List<CP_Gate> { g });
            }

            // 4) 각 컬럼에서 정답 1개 선정, 나머지는 correctColor 제외 랜덤
            foreach (var col in columns)
            {
                if (col.Count == 0) continue;

                int pick = UnityEngine.Random.Range(0, col.Count);
                for (int i = 0; i < col.Count; i++)
                {
                    var gate = col[i];
                    if (i == pick)
                    {
                        gate.gateColor = correctColor;
                        correctGateIds.Add(gate.gateId);
                    }
                    else
                    {
                        gate.gateColor = RandomColorExcept(correctColor);
                    }
                }
            }
        }

        SignalType RandomColorExcept(SignalType except)
        {
            List<SignalType> pool = new List<SignalType> { SignalType.Blue, SignalType.Red, SignalType.Green };
            pool.Remove(except);
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        void ApplyStartGateVisual()
        {
            if (string.IsNullOrEmpty(transmitterGateId)) return;
            if (!gateDict.TryGetValue(transmitterGateId, out var tx)) return;

            if (tx.gateImage != null)
                tx.gateImage.color = SignalToColor(correctColor, 1f);
        }

        // =========================================================
        // Drag / Connect
        // =========================================================
        public void BeginDragFromPort(CP_Port from, PointerEventData ed)
        {
            if (from == null) return;
            if (from.portType != "output")
            {
                Debug.Log("[CircuitPuzzle] 이 포트는 output이 아니라 드래그로 선을 시작할 수 없습니다. (시작은 반드시 output에서)");
                return;
            }

            if (isTimeOver || isClearedByTimer) return;

            if (!connectionLinePrefab)
            {
                Debug.LogError("[Puzzle] connectionLinePrefab이 비어있습니다(LineRenderer 프리팹).");
                return;
            }

            if (canvas == null)
            {
                canvas = gatesParent != null ? gatesParent.GetComponentInParent<Canvas>() : null;
                uiCam = (canvas != null) ? canvas.worldCamera : null;
            }

            draggingFrom = from;

            dragLineRoot = Instantiate(connectionLinePrefab, connectionsParent);
            dragLineRoot.name = "DraggingLine";
            dragLineRoot.transform.localScale = Vector3.one;
            dragLineRoot.transform.localPosition = Vector3.zero;
            dragLineRoot.transform.localRotation = Quaternion.identity;

            dragLine = dragLineRoot.GetComponent<LineRenderer>();
            if (!dragLine)
            {
                Debug.LogError("[Puzzle] connectionLinePrefab에 LineRenderer가 없습니다.");
                Destroy(dragLineRoot);
                draggingFrom = null;
                return;
            }

            dragLine.useWorldSpace = false;

            ApplyLineStyle(dragLine, true, new Color(1f, 1f, 1f, 1f));

            Vector3 s = GetPortLocalCenterInConnectionsParent(from);
            dragLine.positionCount = 2;
            dragLine.SetPosition(0, s);
            dragLine.SetPosition(1, s);

            Debug.Log("[CircuitPuzzle] 드래그 라인 생성됨 (ConnectionsParent 아래 DraggingLine).");
            DragTo(ed);
        }

        public void DragTo(PointerEventData ed)
        {
            if (dragLine == null || draggingFrom == null) return;

            Vector3 s = GetPortLocalCenterInConnectionsParent(draggingFrom);
            Vector3 e = ScreenToLocalInConnectionsParent(ed.position);
            SetSmoothLocal(dragLine, s, e);
        }

        public void EndDrag(PointerEventData ed)
        {
            if (dragLine == null || draggingFrom == null)
            {
                draggingFrom = null;
                return;
            }

            CP_Port target = RaycastPortUnderPointer(ed);

            if (target != null && target != draggingFrom && target.portType == "input")
            {
                RemoveConnectionsToInput(target.portId);

                Vector3 s = GetPortLocalCenterInConnectionsParent(draggingFrom);
                Vector3 e = GetPortLocalCenterInConnectionsParent(target);
                SetElbowLocal(dragLine, s, e);

                CP_Gate targetGate = target.parentGate;

                bool isCorrectGate =
                    (targetGate != null) &&
                    (targetGate.gateType == "end" || correctGateIds.Contains(targetGate.gateId));

                // ===================== SFX PLAY (추가) =====================
                PlaySfx(sfxConnect);
                PlaySfx(isCorrectGate ? sfxCorrect : sfxWrong);
                // ==========================================================

                SignalType shownLineColor = isCorrectGate
                    ? correctColor
                    : RandomColorExcept(correctColor);

                var conn = new CP_ConnData
                {
                    id = Guid.NewGuid().ToString(),
                    fromPortId = draggingFrom.portId,
                    toPortId = target.portId,
                    line = dragLine,
                    root = dragLineRoot,
                    isActive = true,
                    lineColor = shownLineColor
                };

                BuildHitsForConnection(conn);
                connections.Add(conn);

                ApplyOutputExclusivity(conn);

                ApplyLineStyle(conn.line, true, SignalToColor(conn.lineColor, 1f));

                RecalculateActivationAndUpdateAll();
            }
            else
            {
                Destroy(dragLineRoot);
            }

            dragLine = null;
            dragLineRoot = null;
            draggingFrom = null;
        }

        CP_Port RaycastPortUnderPointer(PointerEventData ed)
        {
            if (EventSystem.current == null) return null;

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ed, results);

            foreach (var r in results)
            {
                var pui = r.gameObject.GetComponentInParent<PortUI>();
                if (pui != null && pui.port != null) return pui.port;
            }
            return null;
        }

        public void RemoveConnectionById(string id)
        {
            var c = connections.Find(x => x.id == id);
            if (c == null) return;

            if (activeConnByOutput.TryGetValue(c.fromPortId, out var activeId) && activeId == c.id)
                activeConnByOutput.Remove(c.fromPortId);

            foreach (var h in c.hitAreas) if (h) Destroy(h);
            if (c.root) Destroy(c.root);

            connections.Remove(c);
            RecalculateActivationAndUpdateAll();
        }

        void RemoveConnectionsToInput(string inputPortId)
        {
            var toRemove = connections.FindAll(c => c.toPortId == inputPortId);
            foreach (var c in toRemove)
                RemoveConnectionById(c.id);
        }

        void ApplyOutputExclusivity(CP_ConnData newConn)
        {
            string outId = newConn.fromPortId;

            if (activeConnByOutput.TryGetValue(outId, out var prevConnId))
            {
                if (!string.IsNullOrEmpty(prevConnId) && prevConnId != newConn.id)
                {
                    var prev = connections.Find(c => c.id == prevConnId);
                    if (prev != null)
                        prev.isActive = false;
                }
            }

            activeConnByOutput[outId] = newConn.id;
            newConn.isActive = true;
        }

        void RecalculateActivationAndUpdateAll()
        {
            foreach (var g in gateDict.Values)
                if (g != null) g.isActivatedByCorrectColor = false;

            CP_Gate txGate = null;
            if (!string.IsNullOrEmpty(transmitterGateId))
                gateDict.TryGetValue(transmitterGateId, out txGate);

            if (txGate != null)
                txGate.isActivatedByCorrectColor = true;

            Dictionary<string, CP_ConnData> activeConnByTo = new Dictionary<string, CP_ConnData>();
            foreach (var c in connections)
            {
                if (!c.isActive) continue;
                activeConnByTo[c.toPortId] = c;
            }

            bool changed = true;
            int safety = 0;

            while (changed && safety++ < 200)
            {
                changed = false;

                foreach (var gate in gateDict.Values)
                {
                    if (gate == null) continue;
                    if (gate.gateType == "transmitter") continue;

                    var inputs = GetInputPortsOfGate(gate);
                    if (inputs.Count == 0) continue;

                    bool anyCorrectArrived = false;

                    foreach (var inp in inputs)
                    {
                        if (!activeConnByTo.TryGetValue(inp.portId, out var conn))
                            continue;

                        if (conn.lineColor != correctColor)
                            continue;

                        if (!portDict.TryGetValue(conn.fromPortId, out var fromPort)) continue;
                        if (fromPort.parentGate == null) continue;
                        if (!fromPort.parentGate.isActivatedByCorrectColor) continue;

                        anyCorrectArrived = true;
                        break;
                    }

                    if (anyCorrectArrived && !gate.isActivatedByCorrectColor)
                    {
                        gate.isActivatedByCorrectColor = true;
                        changed = true;
                    }
                }
            }

            foreach (var c in connections)
            {
                if (c.line == null) continue;

                if (c.isActive)
                    ApplyLineStyle(c.line, true, SignalToColor(c.lineColor, 1f));
                else
                    ApplyLineStyle(c.line, false, SignalToColor(c.lineColor, 1f));
            }

            UpdateOKAndClear();
        }

void UpdateOKAndClear()
        {
            foreach (var img in okImagesByGate.Values)
                if (img) img.enabled = false;

            bool cleared = false;

            foreach (var gate in gateDict.Values)
            {
                if (gate == null) continue;
                if (gate.gateType == "transmitter") continue;

                if (gate.gateType == "normal" &&
                    gate.isActivatedByCorrectColor &&
                    correctGateIds.Contains(gate.gateId))
                {
                    if (okImagesByGate.TryGetValue(gate.gateId, out var ok) && ok) ok.enabled = true;
                }

                if (gate.gateType == "end" && gate.isActivatedByCorrectColor)
                    cleared = true;
            }

            if (cleared)
            {
                isClearedByTimer = true;
                ClearAllLinesImmediate();

                if (clearPanel) clearPanel.SetActive(true);

                OnPuzzleCleared?.Invoke();

                Debug.Log("[Puzzle] CLEAR");
            }
        }

        List<CP_Port> GetInputPortsOfGate(CP_Gate gate)
        {
            List<CP_Port> res = new List<CP_Port>();
            foreach (var p in portDict.Values)
            {
                if (p == null) continue;
                if (p.parentGate != gate) continue;
                if (p.portType == "input") res.Add(p);
            }
            return res;
        }

        void ApplyLineStyle(LineRenderer lr, bool active, Color col)
        {
            if (lr == null) return;

            if (lr.sharedMaterial == null)
            {
                var sh = Shader.Find("Sprites/Default");
                if (sh != null) lr.material = new Material(sh);
            }

            float a = active ? col.a : inactiveLineAlpha;
            Color finalCol = active
                ? new Color(col.r, col.g, col.b, a)
                : new Color(0.7f, 0.7f, 0.7f, a);

            float w = lineWidth * (active ? 1f : inactiveLineWidthMultiplier);
            lr.startWidth = w;
            lr.endWidth = w;

            lr.startColor = finalCol;
            lr.endColor = finalCol;

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(finalCol, 0f), new GradientColorKey(finalCol, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(finalCol.a, 0f), new GradientAlphaKey(finalCol.a, 1f) }
            );
            lr.colorGradient = g;

            var rend = lr.GetComponent<Renderer>();
            if (rend)
            {
                rend.sortingLayerName = "UI";
                rend.sortingOrder = lineSortingOrder;
            }
        }

        Color SignalToColor(SignalType t, float alpha)
        {
            switch (t)
            {
                case SignalType.Blue: return new Color(0.2f, 0.55f, 1f, alpha);
                case SignalType.Red: return new Color(1f, 0.25f, 0.25f, alpha);
                case SignalType.Green: return new Color(0.2f, 1f, 0.45f, alpha);
                default: return new Color(1f, 1f, 1f, alpha);
            }
        }

        Vector3 ScreenToLocalInConnectionsParent(Vector2 screenPos)
        {
            if (connectionsParent == null) connectionsParent = gatesParent;
            Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? uiCam : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                connectionsParent,
                screenPos,
                cam,
                out var local
            );

            return new Vector3(local.x, local.y, 0f);
        }

        Vector3 GetPortLocalCenterInConnectionsParent(CP_Port port)
        {
            if (port == null) return Vector3.zero;

            var rt = port.GetComponent<RectTransform>();
            if (rt == null) return port.transform.localPosition;

            Vector3 worldCenter = rt.TransformPoint(rt.rect.center);

            Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? uiCam : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);

            return ScreenToLocalInConnectionsParent(screen);
        }

        void SetSmoothLocal(LineRenderer lr, Vector3 s, Vector3 e)
        {
            const int seg = 20;
            lr.positionCount = seg;

            Vector3 c1 = s + Vector3.right * curveStrength;
            Vector3 c2 = e + Vector3.left * curveStrength;

            for (int i = 0; i < seg; i++)
            {
                float t = i / (seg - 1f);
                float u = 1f - t;

                Vector3 p =
                    (u * u * u) * s +
                    (3 * u * u * t) * c1 +
                    (3 * u * t * t) * c2 +
                    (t * t * t) * e;

                p.z = 0f;
                lr.SetPosition(i, p);
            }
        }

        void SetElbowLocal(LineRenderer lr, Vector3 s, Vector3 e)
        {
            float pad = Mathf.Max(0.01f, elbowPadding);

            float mx = (s.x + e.x) * 0.5f;
            float exitX = Mathf.Min(s.x + pad, mx);
            float enterX = Mathf.Max(e.x - pad, mx);

            if (enterX < exitX) { exitX = mx; enterX = mx; }

            lr.positionCount = 5;
            lr.SetPosition(0, s);
            lr.SetPosition(1, new Vector3(exitX, s.y, 0f));
            lr.SetPosition(2, new Vector3(enterX, s.y, 0f));
            lr.SetPosition(3, new Vector3(enterX, e.y, 0f));
            lr.SetPosition(4, e);
        }

        void BuildHitsForConnection(CP_ConnData conn)
        {
            foreach (var h in conn.hitAreas) if (h) Destroy(h);
            conn.hitAreas.Clear();

            int count = conn.line.positionCount;
            if (count < 2) return;

            for (int i = 0; i < count - 1; i++)
            {
                Vector3 aL3 = conn.line.GetPosition(i);
                Vector3 bL3 = conn.line.GetPosition(i + 1);

                Vector2 aL = new Vector2(aL3.x, aL3.y);
                Vector2 bL = new Vector2(bL3.x, bL3.y);

                Vector2 mid = (aL + bL) * 0.5f;
                Vector2 dir = (bL - aL);
                float len = dir.magnitude;
                if (len < 0.01f) continue;

                dir /= len;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                GameObject h = new GameObject("Hit", typeof(RectTransform), typeof(Image));
                h.transform.SetParent(connectionsParent != null ? connectionsParent : gatesParent, false);

                var img = h.GetComponent<Image>();
                img.color = new Color(0, 0, 0, 0);
                img.raycastTarget = true;

                var rt = h.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = mid;
                rt.sizeDelta = new Vector2(len, 50f);
                rt.localRotation = Quaternion.Euler(0, 0, angle);

                var hit = h.AddComponent<ConnectionLineHit>();
                hit.manager = this;
                hit.connectionId = conn.id;

                conn.hitAreas.Add(h);
            }
        }
    }
}
