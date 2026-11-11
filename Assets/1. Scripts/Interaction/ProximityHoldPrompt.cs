using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProximityHoldPrompt : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private MonoBehaviour interactableBehaviour;
    [SerializeField] private Transform player;
    [SerializeField] private string playerTag = "Player";

    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private float initialHideDuration = 0.75f;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private string promptText = "";
    [SerializeField] private bool useBoundsDistance = true;
    [SerializeField] private float minValidBoundsThickness = 0.05f;
    [SerializeField] private bool requirePlayerFacingTarget = false;
    [SerializeField, Range(1f, 90f)] private float maxFacingAngle = 35f;

    [Header("UI")]
    [SerializeField] private InteractionHoldUI interactionUI;
    [Header("안내 UI 크기 (1920x1080 기준)")]
    [SerializeField, Min(16f)] private float circleDiameter = 58f;
    [SerializeField, Min(8f)] private float keyFontSize = 24f;
    [SerializeField, Min(8f)] private float labelFontSize = 19f;
    [SerializeField] private float circleOffsetY = -132f;
    [SerializeField] private float labelOffsetY = -180f;

    [SerializeField] private Canvas promptCanvas;
    [SerializeField] private CanvasGroup promptGroup;
    [SerializeField] private Image radialFill;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Color circleColor = new Color(0.70f, 0.94f, 1f, 0.92f);
    [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.96f);

    private IHoldInteractable interactable;
    private Collider[] targetColliders;
    private Renderer[] targetRenderers;
    private float holdTimer;
    private float promptAllowedTime;
    private bool holding;

    private void Awake()
    {
        interactable = GetComponent<IHoldInteractable>();

        if (interactable == null && interactableBehaviour != null)
            interactable = interactableBehaviour as IHoldInteractable;

        if (interactionUI == null)
            interactionUI = FindFirstObjectByType<InteractionHoldUI>(FindObjectsInactive.Include);

        if (interactionUI == null)
            EnsurePromptUI();

        CacheDistanceSources();
        promptAllowedTime = Time.time + initialHideDuration;
        HidePrompt();
    }

    private void OnEnable()
    {
        CancelHold();
        promptAllowedTime = Time.time + initialHideDuration;
        HidePrompt();
    }

    private void Update()
    {
        if (Time.time < promptAllowedTime)
        {
            CancelHold();
            HidePrompt();
            return;
        }

        if (interactable == null)
        {
            HidePrompt();
            return;
        }

        if (interactable is IPickupInteractable && !(interactable is IHoldInteractable))
        {
            CancelHold();
            HidePrompt();
            return;
        }

        // 장면이 열린 직후엔 아직 꺼지지 않은 다른 캐릭터(Shiri 를 골라도 Meka)를 잡아 두고 계속 그 위치로 거리를 쟀다
        // (06 엘리베이터가 Shiri 로는 안 열림). 잡아 둔 플레이어가 꺼져 있으면 다시 찾는다.
        if (player == null || !player.gameObject.activeInHierarchy)
            FindPlayer();

        if (player == null)
        {
            HidePrompt();
            return;
        }

        if (GetDistanceToTarget(player.position) > interactDistance)
        {
            CancelHold();
            HidePrompt();
            return;
        }

        if (requirePlayerFacingTarget && !IsPlayerFacingTarget())
        {
            CancelHold();
            HidePrompt();
            return;
        }

        ShowPrompt();
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            holding = true;
            holdTimer = 0f;
            interactable.BeginHold(player.gameObject);
        }

        if (holding && Input.GetKey(interactKey))
        {
            holdTimer += Time.deltaTime;
            float duration = Mathf.Max(0.01f, interactable.GetHoldDuration());
            float progress = Mathf.Clamp01(holdTimer / duration);

            interactable.UpdateHold(player.gameObject, progress);
            SetProgress(progress);

            if (progress >= 1f)
            {
                interactable.CompleteHold(player.gameObject);
                holding = false;
                holdTimer = 0f;
                SetProgress(0f);
                HidePrompt();
            }
        }

        if (holding && Input.GetKeyUp(interactKey))
            CancelHold();
    }

    private void CancelHold()
    {
        if (holding && interactable != null && player != null)
            interactable.CancelHold(player.gameObject);

        holding = false;
        holdTimer = 0f;
        SetProgress(0f);
    }

    private void FindPlayer()
    {
        GameObject foundPlayer = GameObject.FindGameObjectWithTag(playerTag);
        if (foundPlayer != null)
            player = foundPlayer.transform;
    }

    private void CacheDistanceSources()
    {
        targetColliders = GetComponentsInChildren<Collider>(true);
        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private float GetDistanceToTarget(Vector3 worldPosition)
    {
        float bestDistance = Vector3.Distance(transform.position, worldPosition);

        if (!useBoundsDistance)
            return bestDistance;

        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider targetCollider = targetColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                if (!HasUsableBounds(targetCollider.bounds))
                    continue;

                Vector3 closestPoint = targetCollider.ClosestPointSafe(worldPosition);
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                    continue;

                if (!HasUsableBounds(targetRenderer.bounds))
                    continue;

                Vector3 closestPoint = targetRenderer.bounds.ClosestPoint(worldPosition);
                bestDistance = Mathf.Min(bestDistance, Vector3.Distance(closestPoint, worldPosition));
            }
        }

        return bestDistance;
    }

    private bool HasUsableBounds(Bounds bounds)
    {
        Vector3 size = bounds.size;
        return size.x >= minValidBoundsThickness
            && size.y >= minValidBoundsThickness
            && size.z >= minValidBoundsThickness;
    }

    private bool IsPlayerFacingTarget()
    {
        Camera playerCamera = null;

        if (player != null)
            playerCamera = player.GetComponentInChildren<Camera>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            return false;

        Vector3 aimPoint = GetAimPoint();
        Vector3 direction = aimPoint - playerCamera.transform.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return true;

        float angle = Vector3.Angle(playerCamera.transform.forward, direction.normalized);
        return angle <= maxFacingAngle;
    }

    private Vector3 GetAimPoint()
    {
        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider targetCollider = targetColliders[i];
                if (targetCollider != null && targetCollider.enabled && HasUsableBounds(targetCollider.bounds))
                    return targetCollider.bounds.center;
            }
        }

        if (targetRenderers != null)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer != null && targetRenderer.enabled && HasUsableBounds(targetRenderer.bounds))
                    return targetRenderer.bounds.center;
            }
        }

        return transform.position;
    }

    private void ShowPrompt()
    {
        string title = GetPromptTitle();
        string actionText = GetPromptActionText(title);

        if (interactionUI != null)
        {
            HideLocalPrompt();
            interactionUI.ShowFor(this, title, actionText);
            return;
        }

        if (promptGroup == null)
            return;

        promptGroup.alpha = 1f;
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;

        if (labelText != null)
            labelText.text = InteractionPromptTextUtility.FormatHoldPrompt(title, actionText);
    }

    private string GetPromptTitle()
    {
        if (interactable == null)
            return "상호작용";

        string name = interactable.GetInteractionName();
        return string.IsNullOrWhiteSpace(name) ? "상호작용" : name.Trim();
    }

    private string GetPromptActionText(string title)
    {
        if (!string.IsNullOrWhiteSpace(promptText))
            return promptText.Trim();

        return InteractionPromptTextUtility.FormatHoldPrompt(title);
    }

    private void HidePrompt()
    {
        if (interactionUI != null)
        {
            HideLocalPrompt();
            interactionUI.HideFor(this);
            return;
        }

        if (promptGroup == null)
            return;

        promptGroup.alpha = 0f;
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;
    }

    private void SetProgress(float progress)
    {
        if (interactionUI != null)
        {
            HideLocalPrompt();
            interactionUI.UpdateProgressFor(this, progress);
            return;
        }

        if (radialFill != null)
            radialFill.fillAmount = Mathf.Clamp01(progress);
    }

    private void HideLocalPrompt()
    {
        if (promptGroup != null)
        {
            promptGroup.alpha = 0f;
            promptGroup.interactable = false;
            promptGroup.blocksRaycasts = false;
        }

        if (promptCanvas != null)
            promptCanvas.enabled = false;
    }

    private void EnsurePromptUI()
    {
        if (promptCanvas != null && promptGroup != null && radialFill != null)
            return;

        GameObject canvasObject = new GameObject("Proximity Interaction Prompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        promptCanvas = canvasObject.GetComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 350;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        promptGroup = canvasObject.GetComponent<CanvasGroup>();

        RectTransform root = canvasObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        GameObject circleObject = new GameObject("Hold Circle", typeof(RectTransform), typeof(Image));
        circleObject.transform.SetParent(root, false);
        Image circle = circleObject.GetComponent<Image>();
        circle.color = new Color(0f, 0.03f, 0.05f, 0.58f);
        circle.raycastTarget = false;
        RectTransform circleRect = circleObject.GetComponent<RectTransform>();
        circleRect.anchorMin = new Vector2(0.5f, 0.5f);
        circleRect.anchorMax = new Vector2(0.5f, 0.5f);
        circleRect.pivot = new Vector2(0.5f, 0.5f);
        circleRect.sizeDelta = new Vector2(circleDiameter, circleDiameter);
        circleRect.anchoredPosition = new Vector2(0f, circleOffsetY);

        GameObject fillObject = new GameObject("Hold Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(circleRect, false);
        radialFill = fillObject.GetComponent<Image>();
        radialFill.color = fillColor;
        radialFill.type = Image.Type.Filled;
        radialFill.fillMethod = Image.FillMethod.Radial360;
        radialFill.fillOrigin = 2;
        radialFill.fillClockwise = true;
        radialFill.fillAmount = 0f;
        radialFill.raycastTarget = false;
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        float inset = Mathf.Max(3f, circleDiameter * 0.09f);
        fillRect.offsetMin = new Vector2(inset, inset);
        fillRect.offsetMax = new Vector2(-inset, -inset);

        keyText = CreateText("Key Text", circleRect, "F", keyFontSize, circleColor, FontStyles.Bold);
        Stretch(keyText.rectTransform);

        labelText = CreateText("Prompt Label", root, string.Empty, labelFontSize, Color.white, FontStyles.Bold);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(420f, 34f);
        labelRect.anchoredPosition = new Vector2(0f, labelOffsetY);
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, FontStyles style)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
