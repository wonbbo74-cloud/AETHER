using UnityEngine;
using UnityEngine.UI;

public class ModuleWorldMarker : MonoBehaviour
{
    [SerializeField] private ItemData itemData;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private float maxVisibleDistance = 18f;
    [SerializeField] private float fullVisibleDistance = 7f;
    [SerializeField] private float closeFadeDistance = 1.6f;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 2.4f;
    [SerializeField] private Vector2 markerSize = new Vector2(0.1f, 0.1f);
    [SerializeField] private Color glowColor = new Color(0.15f, 0.95f, 1f, 0.42f);
    [SerializeField] private Color iconColor = Color.white;

    private Transform markerRoot;
    private CanvasGroup canvasGroup;
    private Image iconImage;
    private Camera cachedCamera;
    private float seed;

    private bool duplicate;

    public void Configure(ItemData data)
    {
        if (data != null)
            itemData = data;

        // 겹친 표식은 나중에 Configure 가 불려도 다시 만들지 않는다(부트스트랩이 안쪽 모델에도 부른다).
        if (duplicate || IsNestedDuplicate())
        {
            RemoveOwnMarker();
            return;
        }

        EnsureMarker();
        RefreshIcon();
    }

    /// <summary>표식이 뜨는 높이·크기를 바꾼다(바닥의 작은 물건용).</summary>
    public void SetLayout(Vector3 offset, Vector2 size, float visibleDistance)
    {
        worldOffset = offset;
        markerSize = size;
        maxVisibleDistance = visibleDistance;
        // 7m 부터 흐려져 10m 앞 모듈의 표식이 어둠 속에서 희미했다. 보이는 거리의 60% 까지는 또렷하게.
        fullVisibleDistance = visibleDistance * 0.6f;
        if (markerRoot != null)
        {
            RectTransform rect = markerRoot as RectTransform;
            if (rect != null)
                rect.sizeDelta = size;
        }
    }

    private bool IsNestedDuplicate()
    {
        return transform.parent != null && transform.parent.GetComponentInParent<ModuleWorldMarker>() != null;
    }

    private void RemoveOwnMarker()
    {
        duplicate = true;
        enabled = false;
        Transform own = transform.Find("Module World Marker");
        if (own != null)
            Destroy(own.gameObject);
    }

    private void Awake()
    {
        // 바깥 물체와 안쪽 모델 양쪽에 붙어 표식이 두 개씩 겹쳐 떴다. 바깥 것만 남긴다.
        if (IsNestedDuplicate())
        {
            RemoveOwnMarker();
            return;
        }

        seed = Random.value * 10f;
        EnsureMarker();
        RefreshIcon();
    }

    private void LateUpdate()
    {
        EnsureMarker();

        if (cachedCamera == null)
            cachedCamera = Camera.main;

        if (cachedCamera == null || markerRoot == null || canvasGroup == null)
            return;

        Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * bobSpeed + seed) * bobAmplitude);
        markerRoot.position = transform.position + worldOffset + bob;
        markerRoot.forward = cachedCamera.transform.forward;

        // 05 모듈은 물체 크기가 10배(케이블은 축마다 다름)라 표식도 따라 커져 화면을 덮었다.
        // 물체에서 떼어 내 markerSize(m) 그대로 보이게 한다. 위치는 위에서 매 프레임 따라간다.
        if (markerRoot.parent != null)
            markerRoot.SetParent(null, true);
        markerRoot.localScale = Vector3.one;

        float distance = Vector3.Distance(cachedCamera.transform.position, transform.position);
        float farAlpha = Mathf.InverseLerp(maxVisibleDistance, fullVisibleDistance, distance);
        // 바로 앞(줍기 안내가 뜨는 거리)에서는 표식을 감춘다. 1.6m 까지 보이던 때는 05 모듈 바로 위에서
        // 표식 그림이 크게 떠 벽에 붙은 물건처럼 보였다(줍는 위치를 헷갈리게 함).
        float closeAlpha = Mathf.InverseLerp(1.8f, Mathf.Max(closeFadeDistance, 2.8f), distance);
        canvasGroup.alpha = Mathf.Clamp01(farAlpha * closeAlpha);
        canvasGroup.gameObject.SetActive(canvasGroup.alpha > 0.02f);
    }

    private void OnDisable()
    {
        // 떼어 낸 표식은 물체를 따라 꺼지지 않으므로 직접 끈다.
        if (markerRoot != null && markerRoot.parent == null)
            markerRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        // 주워서 물체가 사라지면 떼어 낸 표식도 같이 지운다.
        if (markerRoot != null && markerRoot.parent == null)
            Destroy(markerRoot.gameObject);
    }

    private void EnsureMarker()
    {
        if (duplicate || markerRoot != null)
            return;

        Transform existing = transform.Find("Module World Marker");
        if (existing != null)
        {
            markerRoot = existing;
            canvasGroup = markerRoot.GetComponent<CanvasGroup>();
            iconImage = markerRoot.GetComponentInChildren<Image>();
            if (canvasGroup == null)
                canvasGroup = markerRoot.gameObject.AddComponent<CanvasGroup>();
            return;
        }

        GameObject root = new GameObject("Module World Marker", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        root.transform.localPosition = worldOffset;
        root.layer = gameObject.layer;

        markerRoot = root.transform;
        canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = markerSize;
        rootRect.localScale = Vector3.one;

        GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(root.transform, false);
        RectTransform glowRect = glow.GetComponent<RectTransform>();
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = Vector2.zero;
        glowRect.offsetMax = Vector2.zero;
        glowRect.localEulerAngles = new Vector3(0f, 0f, 45f);
        Image glowImage = glow.GetComponent<Image>();
        glowImage.color = glowColor;
        glowImage.raycastTarget = false;

        GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(root.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.18f, 0.18f);
        iconRect.anchorMax = new Vector2(0.82f, 0.82f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconImage = icon.GetComponent<Image>();
        iconImage.color = iconColor;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    private void RefreshIcon()
    {
        if (iconImage == null)
            return;

        if (itemData == null)
            itemData = PartResourceResolver.FindItemData(gameObject.name);

        Sprite sprite = itemData != null ? itemData.itemSprite : null;
        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }
}
