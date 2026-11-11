using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class ShopManager : MonoBehaviour
{
    public enum ShopItemType
    {
        Food,
        Drink,
        EnergyCharge,
        EnergyPack,
        ShieldCharge,
        ShieldPack
    }

    [System.Serializable]
    public class ShopItemEntry
    {
        [Header("Item Info")]
        public string displayName;
        public ShopItemType itemType;
        public int cost = 10;
        public ItemData itemData;

        [Header("UI")]
        public Image iconImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI costText;
        public Button button;
    }

    [Header("Shop Panel")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button closeShopButton;

    [Header("Confirm Panel")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private TextMeshProUGUI confirmMessageText;
    [SerializeField] private Button confirmBuyButton;
    [SerializeField] private Button confirmCancelButton;

    [Header("Result Message")]
    [SerializeField] private TextMeshProUGUI resultMessageText;

    [Header("Interaction Panel")]
    [SerializeField] private GameObject interactionPanel;
    [SerializeField] private TextMeshProUGUI interactionText;
    [SerializeField] private Image interactionIconImage;
    [SerializeField] private Sprite interactionIconSprite;
    [SerializeField] private string interactionMessage = "상점 열기";

    [Header("Shop Items")]
    [SerializeField] private ShopItemEntry[] shopItems;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    private GameObject currentPlayerInRange;
    private ShopItemEntry currentSelectedItem;
    private bool isShopOpen;
    private bool isResultMessageVisible;
    private int resultMessageShownFrame = -1;

    // ---- 무료 상점 ----
    // 돈을 내지 않고 받는다. 대신 상점에 올 때(범위에 들어올 때)마다 품목별로 1개씩만 준다.
    private const string KoreanFontPath = "UI/Fonts/NanumGothicCoding-Bold SDF";
    private readonly HashSet<ShopItemEntry> takenThisVisit = new HashSet<ShopItemEntry>();
    private bool uiScreenOpen;
    private GameObject runtimePrompt;
    private Move_Yunseo cachedMoveYunseo;
    private bool cachedMoveYunseoAllowLook;

    private Hankyul_Move cachedHankyulMove;
    private Move_Hankyul cachedMoveHankyul;
    private Yunseo_Move cachedYunseoMove;
    private bool cachedHankyulAllowLook;
    private bool cachedMoveHankyulAllowLook;
    private bool cachedYunseoAllowLook;
    private bool hasCachedHankyulMove;
    private bool hasCachedMoveHankyul;
    private bool hasCachedYunseoMove;

    private void Start()
    {
        ApplyFreeShopLook();
        InitializeShopItems();
        InitializeConfirmUI();
        InitializeCloseButton();
        InitializeInteractionPanel();
        CloseShopImmediate();
    }

    private void Update()
    {
        if (isShopOpen && TryDismissResultMessage())
            return;

        if (isShopOpen)
            ApplyShopCursorState(true);

        if (isShopOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseShop();
            return;
        }

        if (currentPlayerInRange == null) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (isShopOpen)
                CloseShop();
            else
                OpenShop();
        }
    }

    private void InitializeShopItems()
    {
        if (shopItems == null) return;

        for (int i = 0; i < shopItems.Length; i++)
        {
            ShopItemEntry item = shopItems[i];
            if (item == null) continue;

            if (item.nameText != null)
            {
                // 인벤토리와 같은 이름을 쓴다(상점은 "음식", 인벤토리는 "식량"으로 달랐다).
                if (item.itemData != null && !string.IsNullOrEmpty(item.itemData.displayName))
                    item.nameText.text = item.itemData.displayName;
                else
                    item.nameText.text = string.IsNullOrEmpty(item.displayName)
                        ? GetItemDisplayName(item.itemType)
                        : item.displayName;
            }

            if (item.costText != null)
                item.costText.text = "무료";

            if (item.button != null)
            {
                // 무료로 받는 상점인데 버튼만 영어 "Buy" 였다.
                TMP_Text buttonLabel = item.button.GetComponentInChildren<TMP_Text>(true);
                if (buttonLabel != null)
                {
                    buttonLabel.text = "받기";
                    TMP_FontAsset korean = Resources.Load<TMP_FontAsset>(KoreanFontPath);
                    if (korean != null) buttonLabel.font = korean;
                }

                int index = i;
                item.button.onClick.RemoveAllListeners();
                item.button.onClick.AddListener(() => OnClickItemButton(shopItems[index]));
            }
        }
    }

    private void InitializeConfirmUI()
    {
        if (confirmBuyButton != null)
        {
            confirmBuyButton.onClick.RemoveAllListeners();
            confirmBuyButton.onClick.AddListener(OnClickConfirmBuy);
        }

        if (confirmCancelButton != null)
        {
            confirmCancelButton.onClick.RemoveAllListeners();
            confirmCancelButton.onClick.AddListener(OnClickConfirmCancel);
        }

        // 확인 창 바탕이 70% 투명이라 뒤 상점 칸 글씨("실드 충전")가 버튼 위로 비쳤다. 바탕만 불투명하게.
        if (confirmPanel != null)
        {
            foreach (Image image in confirmPanel.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "WindowSmall" || image.name == "Bg" || image.name == "Gradient")
                {
                    Color c = image.color;
                    c.a = 1f;
                    image.color = c;
                }
            }
        }

        CloseConfirm();
    }

    private void InitializeCloseButton()
    {
        if (closeShopButton != null)
        {
            closeShopButton.onClick.RemoveAllListeners();
            closeShopButton.onClick.AddListener(CloseShop);
        }
    }

    private void InitializeInteractionPanel()
    {
        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        SetInteractionPromptText();

        if (interactionIconImage != null)
        {
            interactionIconImage.sprite = interactionIconSprite;
            interactionIconImage.gameObject.SetActive(interactionIconSprite != null);
        }
    }

    private void OnClickItemButton(ShopItemEntry item)
    {
        if (!isShopOpen) return;
        if (item == null) return;

        OpenConfirm(item);
    }

    private void OnClickConfirmBuy()
    {
        if (currentSelectedItem == null)
        {
            CloseConfirm();
            return;
        }

        ShopItemEntry selectedItem = currentSelectedItem;
        CloseConfirm();
        TryBuy(selectedItem);
    }

    private void OnClickConfirmCancel()
    {
        CloseConfirm();
    }

    private void OpenShop()
    {
        isShopOpen = true;
        AetherAudio.Play2D("ui_open", 0.55f, 1f, 0f);

        if (shopPanel != null)
            shopPanel.SetActive(true);

        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        CachePlayerLookState();
        ClearResultMessage();
        CloseConfirm();
        SetShopInputState(true);
        RefreshRowStates();
        SetRuntimePrompt(false);

        // 시작 지점은 교전 구역이라 상점을 보는 동안 시간을 멈춘다.
        // 인벤토리 화면과 같은 규칙이라 ESC 가 일시정지 메뉴와 겹치지도 않는다.
        if (!uiScreenOpen)
        {
            uiScreenOpen = true;
            AetherUIState.BeginScreen();
        }
    }

    public void OpenShopFromInteractor(GameObject player)
    {
        if (player != null)
            currentPlayerInRange = player;

        if (!isShopOpen)
            OpenShop();
    }

    public void CloseShop()
    {
        if (isShopOpen)
            AetherAudio.Play2D("ui_close", 0.5f, 1f, 0f);
        isShopOpen = false;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        CloseConfirm();
        ClearResultMessage();
        SetShopInputState(false);

        if (uiScreenOpen)
        {
            uiScreenOpen = false;
            AetherUIState.EndScreen();
        }

        SetRuntimePrompt(currentPlayerInRange != null);

        if (currentPlayerInRange != null && interactionPanel != null)
        {
            interactionPanel.SetActive(true);

            SetInteractionPromptText();

            if (interactionIconImage != null)
            {
                interactionIconImage.sprite = interactionIconSprite;
                interactionIconImage.gameObject.SetActive(interactionIconSprite != null);
            }
        }
    }

    private void CloseShopImmediate()
    {
        isShopOpen = false;

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        ClearResultMessage();
        currentSelectedItem = null;
        SetShopInputState(false);
    }

    private void OpenConfirm(ShopItemEntry item)
    {
        currentSelectedItem = item;

        if (confirmPanel != null)
            confirmPanel.SetActive(true);

        if (confirmMessageText != null)
        {
            string itemName = string.IsNullOrEmpty(item.displayName)
                ? GetItemDisplayName(item.itemType)
                : item.displayName;

            // 줄바꿈하면 "받으시겠습니 / 까?" 로 끊겨서 한 줄로 두고, 넘치면 글자 크기를 줄인다.
            confirmMessageText.enableWordWrapping = false;
            confirmMessageText.overflowMode = TextOverflowModes.Overflow;
            if (!confirmMessageText.enableAutoSizing)
            {
                confirmMessageText.fontSizeMax = confirmMessageText.fontSize;
                confirmMessageText.fontSizeMin = Mathf.Min(18f, confirmMessageText.fontSize);
                confirmMessageText.enableAutoSizing = true;
            }
            confirmMessageText.text = $"{itemName}{ObjectParticle(itemName)} 무료로 받으시겠습니까?";
        }
    }

    /// <summary>받침이 있으면 '을', 없으면 '를'.</summary>
    private static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word))
            return "을(를)";

        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3)
            return "을(를)";

        return (last - 0xAC00) % 28 == 0 ? "를" : "을";
    }

    private void CloseConfirm()
    {
        currentSelectedItem = null;

        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    /// <summary>무료로 받는다. 이번 방문에 이미 받은 품목이나 다른 캐릭터 전용 품목은 안 된다.</summary>
    private void TryBuy(ShopItemEntry item)
    {
        if (item == null) return;
        if (currentPlayerInRange == null) return;

        ClearResultMessage();

        if (takenThisVisit.Contains(item))
        {
            ShowResultMessage("이번 방문에 이미 받은 품목입니다.", item, false);
            return;
        }

        if (!IsAvailableForCurrentPlayer(item, out string exclusiveLabel))
        {
            ShowResultMessage(exclusiveLabel + " 품목입니다.", item, false);
            return;
        }

        string failMessage;
        bool success = ApplyPurchase(item, out failMessage);

        if (!success)
        {
            ShowResultMessage(string.IsNullOrEmpty(failMessage) ? "받지 못했습니다." : failMessage, item, false);
            return;
        }

        takenThisVisit.Add(item);
        RefreshRowStates();
        ShowResultMessage(ItemName(item) + " 수령 완료", item, true);
    }

    /// <summary>
    /// 에너지는 한결의 총, 실드는 윤서(PlayerStatus.useShield)의 것이다.
    /// 다른 캐릭터 전용 품목은 '한결 전용' / '윤서 전용' 으로 표시하고 누를 수 없게 한다.
    /// </summary>
    private bool IsAvailableForCurrentPlayer(ShopItemEntry item, out string exclusiveLabel)
    {
        exclusiveLabel = string.Empty;
        if (item == null || currentPlayerInRange == null)
            return false;

        Transform root = currentPlayerInRange.transform.root;

        switch (item.itemType)
        {
            case ShopItemType.EnergyCharge:
            case ShopItemType.EnergyPack:
                exclusiveLabel = AetherCharacterChoice.HankyulDisplayName + " 전용";
                return root.GetComponentInChildren<Hankyul_Fire>() != null;

            case ShopItemType.ShieldCharge:
            case ShopItemType.ShieldPack:
            {
                exclusiveLabel = AetherCharacterChoice.YunseoDisplayName + " 전용";
                PlayerStatus status = root.GetComponentInChildren<PlayerStatus>();
                return status != null && status.useShield;
            }
        }

        return true;
    }

    /// <summary>줄마다 '무료 / 받음 / 한결 전용 / 윤서 전용' 을 보여 주고 버튼을 켜고 끈다.</summary>
    private void RefreshRowStates()
    {
        if (shopItems == null)
            return;

        foreach (ShopItemEntry item in shopItems)
        {
            if (item == null)
                continue;

            bool available = IsAvailableForCurrentPlayer(item, out string label);
            bool taken = takenThisVisit.Contains(item);

            if (item.costText != null)
                item.costText.text = taken ? "받음" : available ? "무료" : label;

            if (item.button != null)
                item.button.interactable = available && !taken;
        }
    }

    /// <summary>한글 글꼴·이름을 입히고 화폐 칸을 숨긴다. (기본 글꼴은 한글이 □ 로 나온다.)</summary>
    private void ApplyFreeShopLook()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(KoreanFontPath);
        if (font != null)
        {
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                text.font = font;
        }

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "CurrencyBox")
                child.gameObject.SetActive(false);
        }

        if (shopItems == null)
            return;

        foreach (ShopItemEntry item in shopItems)
        {
            if (item != null)
                item.displayName = KoreanName(item.itemType);
        }
    }

    private static string KoreanName(ShopItemType type)
    {
        switch (type)
        {
            case ShopItemType.Food: return "음식";
            case ShopItemType.Drink: return "물";
            case ShopItemType.EnergyCharge: return "에너지 충전";
            case ShopItemType.EnergyPack: return "에너지팩";
            case ShopItemType.ShieldCharge: return "실드 충전";
            case ShopItemType.ShieldPack: return "실드팩";
        }

        return "보급품";
    }

    /// <summary>안내 패널이 연결돼 있지 않으면 화면 아래에 'F 상점 열기' 를 직접 띄운다.</summary>
    private void SetRuntimePrompt(bool visible)
    {
        if (interactionPanel != null)
            return;

        if (visible && runtimePrompt == null)
        {
            runtimePrompt = new GameObject("ShopPrompt", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = runtimePrompt.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            CanvasScaler scaler = runtimePrompt.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // 아이템 줍기 안내와 같은 모양·크기·자리(조준점 아래)로 맞춘다. 예전엔 화면 아래쪽에 글자 30 으로 떠서
            // 1080p 전체 화면에서 너무 컸고, 다른 안내와 위치도 달랐다.
            GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(runtimePrompt.transform, false);
            RectTransform boxRect = (RectTransform)box.transform;
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 1f);
            boxRect.anchoredPosition = new Vector2(0f, -90f);
            Image boxImage = box.GetComponent<Image>();
            boxImage.color = new Color(0.03f, 0.05f, 0.07f, 0.78f);
            boxImage.raycastTarget = false;

            GameObject label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(box.transform, false);
            RectTransform rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 0f);
            rect.offsetMax = new Vector2(-16f, 0f);

            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(KoreanFontPath);
            if (font != null)
                text.font = font;
            text.text = "<color=#5FD3F0>[F]</color>  상점 열기";
            text.fontSize = 19f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            boxRect.sizeDelta = new Vector2(text.GetPreferredValues(text.text).x + 32f, 42f);
        }

        if (runtimePrompt != null)
            runtimePrompt.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (runtimePrompt != null)
            Destroy(runtimePrompt);

        if (uiScreenOpen)
        {
            uiScreenOpen = false;
            AetherUIState.EndScreen();
        }
    }

    private bool ApplyPurchase(ShopItemEntry item, out string message)
    {
        message = "";

        switch (item.itemType)
        {
            case ShopItemType.Food:
            case ShopItemType.Drink:
            case ShopItemType.EnergyPack:
            case ShopItemType.ShieldPack:
                return AddItemToCurrentPlayerInventory(item.itemData, 1, out message);

            case ShopItemType.EnergyCharge:
                return RechargeEnergyCharge(out message);

            case ShopItemType.ShieldCharge:
                return RechargeShieldCharge(out message);
        }

        message = "구매할 수 없는 품목입니다.";
        return false;
    }

    private bool AddItemToCurrentPlayerInventory(ItemData itemData, int amount, out string message)
    {
        message = "";

        if (currentPlayerInRange == null || itemData == null)
        {
            message = "아이템 정보를 찾을 수 없습니다.";
            return false;
        }

        PlayerInventory inventory = currentPlayerInRange.GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = currentPlayerInRange.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
        {
            message = "인벤토리를 찾을 수 없습니다.";
            return false;
        }

        bool added = inventory.AddItem(itemData, amount);
        if (!added)
        {
            message = "인벤토리가 가득 찼습니다.";
            return false;
        }

        return true;
    }

    private bool RechargeEnergyCharge(out string message)
    {
        message = "";

        // 총(Hankyul_Fire)은 몸 루트가 아니라 하위 오브젝트에 붙어 있다. 위로만 찾아서 한결도 못 받았다.
        Hankyul_Fire fire = currentPlayerInRange.GetComponent<Hankyul_Fire>();
        if (fire == null)
            fire = currentPlayerInRange.GetComponentInParent<Hankyul_Fire>();
        if (fire == null)
            fire = currentPlayerInRange.transform.root.GetComponentInChildren<Hankyul_Fire>();

        if (fire == null)
        {
            message = AetherCharacterChoice.HankyulDisplayName + " 전용 품목입니다.";
            return false;
        }

        return fire.TryBuyEnergyCharge(out message);
    }

    private bool RechargeShieldCharge(out string message)
    {
        message = "";

        PlayerStatus status = currentPlayerInRange.GetComponent<PlayerStatus>();
        if (status == null)
            status = currentPlayerInRange.GetComponentInParent<PlayerStatus>();

        if (status == null)
        {
            message = "상태 정보를 찾을 수 없습니다.";
            return false;
        }

        return status.TryBuyShieldCharge(out message);
    }

    private string GetItemDisplayName(ShopItemType itemType)
    {
        switch (itemType)
        {
            case ShopItemType.Food: return "Food";
            case ShopItemType.Drink: return "Water";
            case ShopItemType.EnergyCharge: return "Energy Charge";
            case ShopItemType.EnergyPack: return "Energy Pack";
            case ShopItemType.ShieldCharge: return "Shield Charge";
            case ShopItemType.ShieldPack: return "Shield Pack";
        }

        return "Item";
    }

    // ---- 결과 알림 ----
    // 예전엔 큰 흰 글씨 "받았습니다" 가 상점 칸 위에 바탕 없이 얹혀 읽기 어려웠다.
    // HUD(목표 패널·줍기 안내)와 같은 어두운 남색 판 + 청록 테두리 알림으로 띄운다.
    // 밝은 상점 칸 위에 뜨므로 완전히 불투명하게(0.94 면 뒤 글씨가 비쳤다).
    private static readonly Color ToastBg = new Color(0.02f, 0.06f, 0.10f, 1f);
    private static readonly Color ToastCyan = new Color(0.25f, 0.85f, 0.95f, 1f);
    private static readonly Color ToastAmber = new Color(1f, 0.62f, 0.25f, 1f);
    private const float ToastSeconds = 2.4f;

    private RectTransform resultToast;
    private CanvasGroup resultToastGroup;
    private Image resultToastAccent;
    private Image resultToastLine;
    private Image resultToastIconFrame;
    private Image resultToastIcon;
    private TextMeshProUGUI resultToastTitle;
    private TextMeshProUGUI resultToastBody;
    private Coroutine resultToastRoutine;

    private string ItemName(ShopItemEntry item)
    {
        if (item == null)
            return "보급품";

        return string.IsNullOrEmpty(item.displayName) ? GetItemDisplayName(item.itemType) : item.displayName;
    }

    private void ShowResultMessage(string message)
    {
        ShowResultMessage(message, null, true);
    }

    private void ShowResultMessage(string message, ShopItemEntry item, bool success)
    {
        // 옛 글씨는 다른 곳에서 읽을 수 있게 내용만 두고 화면에서는 숨긴다.
        if (resultMessageText != null)
        {
            resultMessageText.text = message;
            resultMessageText.enabled = false;
        }

        isResultMessageVisible = !string.IsNullOrEmpty(message);
        resultMessageShownFrame = Time.frameCount;

        if (!isResultMessageVisible)
            return;

        AetherAudio.Play2D(success ? "receive" : "ui_error", success ? 0.65f : 0.45f, 1f, 0f);
        EnsureResultToast();
        if (resultToast == null)
            return;

        Color accent = success ? ToastCyan : ToastAmber;
        resultToastAccent.color = accent;
        resultToastLine.color = new Color(accent.r, accent.g, accent.b, 0.55f);
        resultToastIconFrame.color = new Color(accent.r, accent.g, accent.b, 0.18f);
        resultToastTitle.color = accent;
        resultToastTitle.text = success ? "SUPPLY RECEIVED" : "UNAVAILABLE";
        resultToastBody.text = message;

        Sprite icon = ItemCardSprite(item);
        resultToastIcon.sprite = icon;
        resultToastIcon.enabled = icon != null;

        if (resultToastRoutine != null)
            StopCoroutine(resultToastRoutine);
        resultToast.gameObject.SetActive(true);
        resultToast.SetAsLastSibling();
        resultToastRoutine = StartCoroutine(PlayResultToast());
    }

    /// <summary>
    /// 상점 칸에 실제로 보이는 그림. iconImage 가 다섯 칸에서 칸 바탕(PanelSmall02)에 연결돼 있어
    /// 칸 안의 'ItemImage' 를 먼저 본다.
    /// </summary>
    private static Sprite ItemCardSprite(ShopItemEntry item)
    {
        if (item == null)
            return null;

        if (item.button != null && item.button.transform.parent != null)
        {
            Transform card = item.button.transform.parent.Find("ItemImage");
            Image cardImage = card != null ? card.GetComponent<Image>() : null;
            if (cardImage != null && cardImage.sprite != null)
                return cardImage.sprite;
        }

        if (item.itemData != null && item.itemData.itemSprite != null)
            return item.itemData.itemSprite;

        return item.iconImage != null ? item.iconImage.sprite : null;
    }

    private System.Collections.IEnumerator PlayResultToast()
    {
        // 상점이 열려 있는 동안은 시간이 멈춰 있어서 실제 시간으로 움직인다.
        // 상점 판 한가운데(상점 칸 위)에 띄운다. 좌표는 판 기준(판은 2배 크기를 0.5배로 줄여 씀).
        Vector2 rest = new Vector2(0f, -40f);
        Vector2 from = rest + new Vector2(0f, -36f);
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.18f);
            resultToastGroup.alpha = k;
            resultToast.anchoredPosition = Vector2.Lerp(from, rest, k);
            yield return null;
        }

        resultToastGroup.alpha = 1f;
        resultToast.anchoredPosition = rest;

        float wait = 0f;
        while (wait < ToastSeconds)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            resultToastGroup.alpha = 1f - t / 0.25f;
            yield return null;
        }

        resultToastRoutine = null;
        ClearResultMessage();
    }

    private void EnsureResultToast()
    {
        if (resultToast != null || shopPanel == null)
            return;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(KoreanFontPath);

        resultToast = NewRect("ResultToast", shopPanel.transform, typeof(Image), typeof(CanvasGroup));
        resultToast.anchorMin = resultToast.anchorMax = new Vector2(0.5f, 0.5f);
        resultToast.pivot = new Vector2(0.5f, 0.5f);
        resultToast.sizeDelta = new Vector2(680f, 128f);
        // 상점 판이 0.5배로 줄어 있어 그대로 두면 알림도 절반 크기가 된다. 화면(1920×1080) 기준 크기로 되돌린다.
        float panelScale = Mathf.Max(0.01f, shopPanel.transform.localScale.x);
        resultToast.localScale = Vector3.one / panelScale;
        Image bg = resultToast.GetComponent<Image>();
        bg.color = ToastBg;
        bg.raycastTarget = false;
        resultToastGroup = resultToast.GetComponent<CanvasGroup>();
        resultToastGroup.blocksRaycasts = false;
        resultToastGroup.interactable = false;

        // 왼쪽 굵은 색 줄 + 위쪽 얇은 빛줄(목표 패널·줍기 안내와 같은 구성)
        resultToastAccent = NewRect("Accent", resultToast, typeof(Image)).GetComponent<Image>();
        Stretch(resultToastAccent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(6f, 0f));
        resultToastLine = NewRect("TopLine", resultToast, typeof(Image)).GetComponent<Image>();
        Stretch(resultToastLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -2f), new Vector2(0f, 0f));

        resultToastIconFrame = NewRect("IconFrame", resultToast, typeof(Image)).GetComponent<Image>();
        RectTransform frame = resultToastIconFrame.rectTransform;
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(0f, 0.5f);
        frame.anchoredPosition = new Vector2(24f, 0f);
        frame.sizeDelta = new Vector2(72f, 72f);

        resultToastIcon = NewRect("Icon", frame, typeof(Image)).GetComponent<Image>();
        Stretch(resultToastIcon.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        resultToastIcon.preserveAspect = true;

        resultToastTitle = NewText("Title", resultToast, font, 20f);
        // 소제목과 본문 사이를 14 띄운다(붙어 있으면 한 덩어리로 읽혔다).
        Stretch(resultToastTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(116f, 7f), new Vector2(-20f, -14f));
        resultToastTitle.characterSpacing = 8f;
        resultToastTitle.alignment = TextAlignmentOptions.BottomLeft;

        resultToastBody = NewText("Body", resultToast, font, 30f);
        Stretch(resultToastBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(116f, 14f), new Vector2(-20f, -7f));
        resultToastBody.color = new Color(0.92f, 0.96f, 0.98f, 0.96f);
        resultToastBody.alignment = TextAlignmentOptions.TopLeft;

        foreach (Image image in resultToast.GetComponentsInChildren<Image>(true))
            image.raycastTarget = false;

        resultToast.gameObject.SetActive(false);
    }

    private static RectTransform NewRect(string name, Transform parent, params System.Type[] components)
    {
        var types = new List<System.Type> { typeof(RectTransform) };
        types.AddRange(components);
        GameObject go = new GameObject(name, types.ToArray());
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size)
    {
        TextMeshProUGUI text = NewRect(name, parent, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private void ClearResultMessage()
    {
        if (resultMessageText != null)
            resultMessageText.text = "";

        isResultMessageVisible = false;
        resultMessageShownFrame = -1;

        if (resultToastRoutine != null)
        {
            StopCoroutine(resultToastRoutine);
            resultToastRoutine = null;
        }

        if (resultToast != null)
            resultToast.gameObject.SetActive(false);
    }

    private void SetShopInputState(bool open)
    {
        ApplyShopCursorState(open);
        SetPlayerLookEnabled(!open);
    }

    private void ApplyShopCursorState(bool open)
    {
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        UIInteractionRuntimeGuard.SetCursorVisibleRequest("ShopManager", open);

        if (open)
            UIInteractionRuntimeGuard.EnsureInteractive(shopPanel != null ? shopPanel : gameObject);
    }

    private bool TryDismissResultMessage()
    {
        if (!isResultMessageVisible) return false;
        if (Time.frameCount <= resultMessageShownFrame) return false;
        if (!HasDismissMessageInput()) return false;

        ClearResultMessage();
        return true;
    }

    private bool HasDismissMessageInput()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        return Input.anyKeyDown || Input.GetMouseButtonDown(0);
    }

    private void CachePlayerLookState()
    {
        hasCachedHankyulMove = false;
        hasCachedMoveHankyul = false;
        hasCachedYunseoMove = false;
        cachedHankyulMove = null;
        cachedMoveHankyul = null;
        cachedYunseoMove = null;

        if (currentPlayerInRange == null) return;

        cachedHankyulMove = currentPlayerInRange.GetComponent<Hankyul_Move>();
        if (cachedHankyulMove == null)
            cachedHankyulMove = currentPlayerInRange.GetComponentInParent<Hankyul_Move>();

        if (cachedHankyulMove != null)
        {
            cachedHankyulAllowLook = cachedHankyulMove.allowLook;
            hasCachedHankyulMove = true;
        }

        cachedMoveHankyul = currentPlayerInRange.GetComponent<Move_Hankyul>();
        if (cachedMoveHankyul == null)
            cachedMoveHankyul = currentPlayerInRange.GetComponentInParent<Move_Hankyul>();

        if (cachedMoveHankyul != null)
        {
            cachedMoveHankyulAllowLook = cachedMoveHankyul.allowLook;
            hasCachedMoveHankyul = true;
        }

        cachedYunseoMove = currentPlayerInRange.GetComponent<Yunseo_Move>();
        if (cachedYunseoMove == null)
            cachedYunseoMove = currentPlayerInRange.GetComponentInParent<Yunseo_Move>();

        if (cachedYunseoMove != null)
        {
            cachedYunseoAllowLook = cachedYunseoMove.allowLook;
            hasCachedYunseoMove = true;
        }

        // 실제로 쓰는 윤서 이동 스크립트(Move_Yunseo). 옛 Yunseo_Move 만 보고 있어서 시점이 안 잠겼다.
        cachedMoveYunseo = currentPlayerInRange.GetComponentInParent<Move_Yunseo>();
        if (cachedMoveYunseo != null)
            cachedMoveYunseoAllowLook = cachedMoveYunseo.allowLook;
    }

    private void SetPlayerLookEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (hasCachedHankyulMove && cachedHankyulMove != null)
                cachedHankyulMove.allowLook = false;

            if (hasCachedMoveHankyul && cachedMoveHankyul != null)
                cachedMoveHankyul.allowLook = false;

            if (hasCachedYunseoMove && cachedYunseoMove != null)
                cachedYunseoMove.allowLook = false;

            if (cachedMoveYunseo != null)
                cachedMoveYunseo.SetLookEnabled(false);

            return;
        }

        if (cachedMoveYunseo != null)
        {
            cachedMoveYunseo.SetLookEnabled(cachedMoveYunseoAllowLook);
            cachedMoveYunseo = null;
        }

        if (hasCachedHankyulMove && cachedHankyulMove != null)
            cachedHankyulMove.allowLook = cachedHankyulAllowLook;

        if (hasCachedMoveHankyul && cachedMoveHankyul != null)
            cachedMoveHankyul.allowLook = cachedMoveHankyulAllowLook;

        if (hasCachedYunseoMove && cachedYunseoMove != null)
            cachedYunseoMove.allowLook = cachedYunseoAllowLook;

        hasCachedHankyulMove = false;
        hasCachedMoveHankyul = false;
        hasCachedYunseoMove = false;
        cachedHankyulMove = null;
        cachedMoveHankyul = null;
        cachedYunseoMove = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 상점에 새로 온 것이면 이번 방문 기록을 비운다(방문마다 품목별 1개).
        if (currentPlayerInRange == null)
            takenThisVisit.Clear();

        currentPlayerInRange = other.gameObject;

        if (!isShopOpen)
            SetRuntimePrompt(true);

        if (!isShopOpen && interactionPanel != null)
        {
            interactionPanel.SetActive(true);

            SetInteractionPromptText();

            if (interactionIconImage != null)
            {
                interactionIconImage.sprite = interactionIconSprite;
                interactionIconImage.gameObject.SetActive(interactionIconSprite != null);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (currentPlayerInRange == other.gameObject)
        {
            CloseShop();
            currentPlayerInRange = null;
        }

        SetRuntimePrompt(false);

        if (interactionPanel != null)
            interactionPanel.SetActive(false);

        if (interactionIconImage != null)
            interactionIconImage.gameObject.SetActive(false);
    }

    private void SetInteractionPromptText()
    {
        if (interactionText != null)
            interactionText.text = InteractionPromptTextUtility.FormatHoldPrompt(interactionMessage);
    }
}
