using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum Direction
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
}

public enum TileKind
{
    Start,
    End,
    Normal
}

public class Tile : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("타일 설정")]
    public TileKind kind = TileKind.Normal;

    [Tooltip("0, 90, 180, 270 중 하나")]
    public int rotation = 0;

    [Tooltip("rotation = 0 기준 Up, Right, Down, Left 순서")]
    public bool[] basePorts = new bool[4];

    [Header("Transform / Point / Checkmark")]
    [Tooltip("타일 전체(회전 대상). 없으면 자기 RectTransform 사용")]
    public RectTransform tileRect;

    [Tooltip("타일 안에 있는 point 이미지")]
    public Image pointImage;
    public Sprite pointNormalSprite;
    public Sprite pointConnectedSprite;

    [Tooltip("마우스 오버 시 색이 바뀔 체크마크 이미지")]
    public Image checkmarkImage;
    public Color checkmarkNormalColor = Color.white;
    public Color checkmarkHoverColor = Color.cyan;

    [Header("라인 이미지 (방향별)")]
    [Tooltip("Up, Right, Down, Left 순서로 할당")]
    public Image[] lineImages = new Image[4];

    [Tooltip("연결 시 라인 색 (#F7FE70)")]
    public Color lineConnectedColor = new Color(0xF7 / 255f, 0xFE / 255f, 0x70 / 255f);

    [Header("회전 설정")]
    [Tooltip("한 번 회전(90도)에 걸리는 시간(초)")]
    public float rotateDuration = 0.15f;

    private Color lineDefaultColor = Color.white;

    // 방향별로 이 타일에서 어떤 라인 세그먼트가 "실제 연결 경로"에 사용되었는지
    private bool[] connectedSegments = new bool[4];

    // 현재 회전 중인지 여부 (회전 중에는 추가 입력 무시)
    private bool isRotating = false;

    void Awake()
    {
        if (tileRect == null)
            tileRect = GetComponent<RectTransform>();

        // 기본 라인 색: 첫 번째 유효 lineImage 기준
        foreach (var img in lineImages)
        {
            if (img != null)
            {
                lineDefaultColor = img.color;
                break;
            }
        }

        if (checkmarkImage != null)
            checkmarkImage.color = checkmarkNormalColor;

        ApplyRotation();         // 초기 회전값 적용
        ResetConnectionVisual(); // 연결 비주얼 초기화
    }

    /* ===== 퍼즐 로직 ===== */

    // 현재 회전 상태에서 특정 방향에 포트가 있는지
    public bool HasPort(Direction dir)
    {
        int rotIndex = rotation / 90;        // 0~3
        int dirIndex = (int)dir;             // 0~3
        int baseIndex = (dirIndex - rotIndex + 4) % 4;

        return basePorts != null &&
               basePorts.Length == 4 &&
               basePorts[baseIndex];
    }

    // 시계 방향 90도 회전 (Normal만, lerp 회전)
    public void RotateClockwise()
    {
        if (kind != TileKind.Normal)
            return;

        if (isRotating)
            return; // 회전 중이면 무시

        int newRotation = rotation + 90;
        if (newRotation >= 360)
            newRotation = 0;

        // 논리적 회전 값 먼저 갱신 (포트 계산에 사용)
        int oldRotation = rotation;
        rotation = newRotation;

        // 비주얼 회전 코루틴 시작
        StartCoroutine(RotateCoroutine(oldRotation, newRotation));
    }

    // 실제 Transform(타일 전체) 회전을 즉시 적용 (초기 세팅용)
    public void ApplyRotation()
    {
        float z = -rotation;
        if (tileRect != null)
            tileRect.rotation = Quaternion.Euler(0f, 0f, z);
        else
            transform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    // 부드러운 회전을 위한 코루틴
    private System.Collections.IEnumerator RotateCoroutine(int fromRotation, int toRotation)
    {
        isRotating = true;

        // 시작/끝 회전값(Quaternion)
        Quaternion startRot;
        if (tileRect != null)
            startRot = tileRect.rotation;
        else
            startRot = transform.rotation;

        Quaternion endRot = Quaternion.Euler(0f, 0f, -toRotation);

        float elapsed = 0f;

        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotateDuration);
            Quaternion curRot = Quaternion.Slerp(startRot, endRot, t);

            if (tileRect != null)
                tileRect.rotation = curRot;
            else
                transform.rotation = curRot;

            yield return null;
        }

        // 최종 각도 스냅
        if (tileRect != null)
            tileRect.rotation = endRot;
        else
            transform.rotation = endRot;

        isRotating = false;
    }

    /* ===== 연결 시 비주얼 ===== */

    // TileManager가 경로 재계산 전에 호출하는 초기화 함수
    public void ResetConnectionVisual()
    {
        for (int i = 0; i < connectedSegments.Length; i++)
            connectedSegments[i] = false;

        ApplyConnectionVisual();
    }

    // 특정 방향(dir)의 라인 세그먼트를 연결/해제로 표시
    public void SetSegmentConnected(Direction dir, bool connected)
{
    // 격자 방향 인덱스
    int dirIndex = (int)dir;

    // 현재 회전(0,90,180,270) → 0~3
    int rotIndex = rotation / 90;

    // 회전 보정된 실제 라인 인덱스(HasPort와 동일 로직)
    int baseIndex = (dirIndex - rotIndex + 4) % 4;

    connectedSegments[baseIndex] = connected;

    ApplyConnectionVisual();
}

    // 내부 상태(connectedSegments)에 따라 실제 이미지 색/포인트 스프라이트 적용
    private void ApplyConnectionVisual()
    {
        // 라인: 연결된 방향만 연결 색, 나머지는 기본 색
        for (int i = 0; i < lineImages.Length; i++)
        {
            var img = lineImages[i];
            if (img == null) continue;

            bool segConnected = (i < connectedSegments.Length) && connectedSegments[i];
            img.color = segConnected ? lineConnectedColor : lineDefaultColor;
        }

        // point: 하나라도 연결 세그먼트가 있으면 connected sprite
        bool anyConnected = false;
        for (int i = 0; i < connectedSegments.Length; i++)
        {
            if (connectedSegments[i])
            {
                anyConnected = true;
                break;
            }
        }

        if (pointImage != null)
        {
            if (anyConnected && pointConnectedSprite != null)
                pointImage.sprite = pointConnectedSprite;
            else if (!anyConnected && pointNormalSprite != null)
                pointImage.sprite = pointNormalSprite;
        }
    }

    /* ===== 마우스 이벤트(IPointer~) ===== */

    // 좌클릭 시 회전
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        // Used to call GlobalManager.Audio.PlaySFX first, so in scenes where it was empty the click threw an error before the tile could rotate.
        AetherAudio.Play2D("tile_rotate", 0.5f, 1f, 0.05f);

        RotateClockwise();

        // TileManager에 경로 갱신 요청 
        var mgr = FindObjectOfType<TileManager>();
        if (mgr != null)
            mgr.OnTileRotated(this);

        // GameManager에 이동 횟수 증가 요청
        var gm = FindObjectOfType<PuzzleManager>();
        if (gm != null)
            gm.OnTileRotated();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (checkmarkImage != null)
            checkmarkImage.color = checkmarkHoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (checkmarkImage != null)
            checkmarkImage.color = checkmarkNormalColor;
    }

    /* ===== 디버그용 ===== */

    [ContextMenu("LogPorts")]
    public void LogPorts()
    {
        Debug.Log($"{name} Rot:{rotation} " +
                  $"U={HasPort(Direction.Up)} " +
                  $"R={HasPort(Direction.Right)} " +
                  $"D={HasPort(Direction.Down)} " +
                  $"L={HasPort(Direction.Left)}");
    }
}
