using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 디자인 이미지를 배경으로 쓰는 전체화면 UI 표시. 이 캔버스는 크기 정규화 대상에서 제외한다.
/// (이미 화면비에 맞춰 통째로 들어가도록 Expand로 맞춰져 있다.)
/// </summary>
public class AetherDesignCanvasMarker : MonoBehaviour
{
}

/// <summary>
/// 화면 UI(HUD·상호작용 안내·퍼즐 창 등)의 크기를 해상도에 맞게 정규화한다.
///
/// 이 프로젝트의 캔버스 대부분은 <b>Constant Pixel Size</b>로 되어 있었다.
/// 그 설정은 해상도가 바뀌어도 UI가 같은 픽셀 크기로 남아서,
/// 1440p·4K에서는 UI가 눈에 띄게 작아지고 낮은 해상도에서는 화면을 덮는다.
/// PC 게임은 해상도가 제각각이므로 화면 크기에 비례해 커지고 작아져야 한다.
///
/// 씬 파일을 고치지 않고 실행 중에만 바꾸므로, 나중에 인스펙터에서 직접 정리하면
/// 이 스크립트는 그대로 두어도 같은 값이라 아무 영향이 없다.
///
/// 월드 공간 캔버스(적 체력바, 머리 위 표시 등)는 건드리지 않는다.
/// </summary>
public class AetherUIScaleNormalizer : MonoBehaviour
{
    /// <summary>PC 기준 해상도. 이 해상도에서 기존과 똑같이 보이고, 다른 해상도에서는 비례해 커진다.</summary>
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    /// <summary>가로·세로 중 무엇을 기준으로 늘릴지. 0.5 = 절충(가로세로 평균).</summary>
    private const float MatchWidthOrHeight = 0.5f;

    private const float WatchSeconds = 12f;
    private const float WatchInterval = 0.5f;

    private static bool subscribed;
    private static AetherUIScaleNormalizer instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        if (!subscribed)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            subscribed = true;
        }

        Ensure();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();

        if (instance != null)
            instance.Rewatch();
    }

    private static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherUIScaleNormalizer");
        instance = host.AddComponent<AetherUIScaleNormalizer>();
        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Rewatch();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Rewatch()
    {
        StopAllCoroutines();
        StartCoroutine(WatchForCanvases());
    }

    /// <summary>
    /// 캔버스는 씬 로드 직후뿐 아니라 나중에 런타임으로 만들어지기도 한다.
    /// 잠시 동안 주기적으로 다시 훑는다.
    /// </summary>
    private IEnumerator WatchForCanvases()
    {
        float elapsed = 0f;
        while (elapsed < WatchSeconds)
        {
            NormalizeAll();
            yield return new WaitForSecondsRealtime(WatchInterval);
            elapsed += WatchInterval;
        }

        NormalizeAll();
    }

    /// <summary>문제 추적용 스위치. true로 두면 캔버스 크기를 건드리지 않는다.</summary>
    public static bool TemporarilyDisabled = false;

    [ContextMenu("Normalize Now")]
    public void NormalizeAll()
    {
        if (TemporarilyDisabled)
            return;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
            Normalize(canvases[i]);
    }

    private static void Normalize(Canvas canvas)
    {
        if (canvas == null)
            return;

        // 월드에 떠 있는 표시(적 체력바 등)는 화면 해상도와 무관하다.
        if (canvas.renderMode == RenderMode.WorldSpace)
            return;

        // 최상위 캔버스만 처리한다. 중첩 캔버스는 부모 설정을 따른다.
        if (!canvas.isRootCanvas)
            return;

        // 디자인 이미지를 쓰는 전체화면 UI는 이미 맞춰져 있다.
        if (canvas.GetComponent<AetherDesignCanvasMarker>() != null)
            return;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            // 이미 비례 확대지만 기준 해상도가 엉뚱하면(예: 800x600) 바로잡는다.
            if (scaler.referenceResolution.x < 640f || scaler.referenceResolution.y < 360f)
                scaler.referenceResolution = ReferenceResolution;

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;
            return;
        }

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
            return;

        // 지금 1080p에서 보이는 크기를 그대로 유지하면서 해상도에 비례하게 만든다.
        //   Constant Pixel Size(배율 s) 는 1080p에서 "기준해상도 = 1920/s" 인 비례 확대와 같다.
        float factor = scaler.scaleFactor > 0.01f ? scaler.scaleFactor : 1f;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceResolution.x / factor, ReferenceResolution.y / factor);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = MatchWidthOrHeight;
    }
}
