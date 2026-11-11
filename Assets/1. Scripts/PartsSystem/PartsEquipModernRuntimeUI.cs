using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartsEquipModernRuntimeUI : MonoBehaviour
{
    private const string EquipReferenceResourcePath = "AETHERParts/UI/PartsEquipReference";

    private readonly Dictionary<PartSlotType, Image> equippedIcons = new Dictionary<PartSlotType, Image>();
    private readonly List<Button> ownedButtons = new List<Button>();
    private readonly List<Image> ownedIcons = new List<Image>();
    private readonly List<TMP_Text> ownedLabels = new List<TMP_Text>();

    private PlayerInventory sourceInventory;
    private EquipmentManager equipmentManager;
    private RectTransform root;
    private TMP_Text infoTitle;
    private TMP_Text infoType;
    private TMP_Text infoStats;
    private TMP_Text infoDetail;
    private Image infoIcon;
    private TMP_Text hintText;
    private PartData selectedPart;

    public void SetContext(PlayerInventory inventory, EquipmentManager manager)
    {
        sourceInventory = inventory;
        equipmentManager = manager;
        Refresh();
    }

    private void Awake()
    {
        BuildLayout();
    }

    private void OnEnable()
    {
        BuildLayout();
        Refresh();
    }

    private void BuildLayout()
    {
        if (root != null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = GetComponentInParent<CanvasScaler>();
        if (scaler == null)
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
        if (raycaster == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        GameObject rootGo = new GameObject("PartsEquipModernRuntimeRoot", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(canvas.transform, false);
        root = rootGo.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        Image bg = rootGo.GetComponent<Image>();
        bg.color = new Color(0.005f, 0.025f, 0.04f, 1f);

        HideLegacyChildren(canvas.transform);
        CreateReferenceBackground(root);

        BuildOwnedList(root);
        BuildEquipSlots(root);
        hintText = CreateText(root, string.Empty, new Vector2(610f, -932f), new Vector2(590f, 40f), 22, FontStyles.Normal, TextAlignmentOptions.Left, Color.white);

        infoIcon = CreateImage(root, "InfoIcon", new Vector2(1300f, -210f), new Vector2(210f, 240f), new Vector2(0f, 1f), new Color(1f, 1f, 1f, 0f));
        infoTitle = CreateText(root, "-", new Vector2(1550f, -240f), new Vector2(250f, 36f), 22, FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
        infoType = CreateText(root, "종류 -", new Vector2(1550f, -302f), new Vector2(250f, 34f), 20, FontStyles.Normal, TextAlignmentOptions.Left, SoftWhite());
        infoStats = CreateText(root, "Attack +0\nDefence +0\nSpeed +0", new Vector2(1550f, -360f), new Vector2(250f, 100f), 20, FontStyles.Normal, TextAlignmentOptions.Left, SoftWhite());
        infoDetail = CreateText(root, "-", new Vector2(1550f, -490f), new Vector2(250f, 70f), 18, FontStyles.Normal, TextAlignmentOptions.Left, SoftWhite(0.78f));

        BuildOwnedGrid(root);
    }

    private static void CreateReferenceBackground(RectTransform parent)
    {
        Texture2D texture = Resources.Load<Texture2D>(EquipReferenceResourcePath);
        if (texture == null)
        {
            Debug.LogWarning("[PartsEquipModernRuntimeUI] Missing UI reference image: " + EquipReferenceResourcePath);
            return;
        }

        GameObject go = new GameObject("PartsEquipReferenceImage", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage image = go.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private void HideLegacyChildren(Transform canvasRoot)
    {
        for (int i = 0; i < canvasRoot.childCount; i++)
        {
            Transform child = canvasRoot.GetChild(i);
            if (child == root)
                continue;

            if (child.GetComponent<PartsEquipModernRuntimeUI>() != null)
                continue;

            child.gameObject.SetActive(false);
        }
    }

    private void BuildOwnedList(RectTransform parent)
    {
        for (int i = 0; i < 6; i++)
        {
            Button button = CreateButton(parent, "OwnedListSlot_" + i, new Vector2(56f, -206f - i * 132f), new Vector2(460f, 112f), new Vector2(0f, 1f));
            Image icon = CreateImage((RectTransform)button.transform, "Icon", new Vector2(18f, -17f), new Vector2(82f, 82f), new Vector2(0f, 1f), Color.white);
            TMP_Text label = CreateText((RectTransform)button.transform, "NO DATA", new Vector2(122f, -38f), new Vector2(260f, 38f), 21, FontStyles.Normal, TextAlignmentOptions.Left, SoftWhite(0.65f));
            int index = i;
            button.onClick.AddListener(() => SelectOwnedIndex(index));
            ownedButtons.Add(button);
            ownedIcons.Add(icon);
            ownedLabels.Add(label);
        }
    }

    private void BuildOwnedGrid(RectTransform parent)
    {
        const int columns = 4;
        for (int i = 0; i < 8; i++)
        {
            int x = i % columns;
            int y = i / columns;
            Button button = CreateButton(parent, "OwnedGridSlot_" + i, new Vector2(1300f + x * 140f, -612f - y * 180f), new Vector2(130f, 160f), new Vector2(0f, 1f));
            Image icon = CreateImage((RectTransform)button.transform, "Icon", new Vector2(24f, -38f), new Vector2(82f, 82f), new Vector2(0f, 1f), Color.white);
            int index = i;
            button.onClick.AddListener(() => SelectOwnedIndex(index));
            ownedButtons.Add(button);
            ownedIcons.Add(icon);
            ownedLabels.Add(null);
        }
    }

    private void BuildEquipSlots(RectTransform parent)
    {
        AddEquipSlot(parent, PartSlotType.Head, new Vector2(856f, -236f), new Vector2(126f, 126f));
        AddEquipSlot(parent, PartSlotType.Arm, new Vector2(610f, -410f), new Vector2(126f, 126f));
        AddEquipSlot(parent, PartSlotType.Support, new Vector2(1092f, -410f), new Vector2(126f, 126f));
        AddEquipSlot(parent, PartSlotType.Leg, new Vector2(610f, -640f), new Vector2(126f, 126f));
        AddEquipSlot(parent, PartSlotType.Core, new Vector2(1092f, -640f), new Vector2(126f, 126f));
    }

    private void AddEquipSlot(RectTransform parent, PartSlotType slot, Vector2 anchoredPosition, Vector2 size)
    {
        Button button = CreateButton(parent, "Equip_" + slot, anchoredPosition, size, new Vector2(0f, 1f));
        Image icon = CreateImage((RectTransform)button.transform, "Icon", new Vector2(20f, -20f), new Vector2(86f, 86f), new Vector2(0f, 1f), Color.white);
        equippedIcons[PartSlotTypeUtility.Normalize(slot)] = icon;
        button.onClick.AddListener(() => TryEquipSelected(slot));
    }

    private void Refresh()
    {
        if (root == null)
            return;

        if (sourceInventory == null)
            sourceInventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>(true);

        List<PartData> owned = GetOwnedParts();
        for (int i = 0; i < ownedButtons.Count; i++)
        {
            PartData part = i < owned.Count ? owned[i] : null;
            Sprite sprite = GetPartIcon(part);
            if (i < ownedIcons.Count && ownedIcons[i] != null)
            {
                ownedIcons[i].enabled = sprite != null;
                ownedIcons[i].sprite = sprite;
                ownedIcons[i].preserveAspect = true;
            }

            if (i < ownedLabels.Count && ownedLabels[i] != null)
                ownedLabels[i].text = part == null ? "NO DATA" : ItemNameRuleUtility.GetBestItemName(null, part);

            ownedButtons[i].interactable = part != null;
        }

        foreach (KeyValuePair<PartSlotType, Image> pair in equippedIcons)
        {
            PartData part = equipmentManager != null ? equipmentManager.GetEquipped(pair.Key) : null;
            Sprite icon = part != null ? GetPartIcon(part) : null;
            pair.Value.enabled = icon != null;
            pair.Value.sprite = icon;
            pair.Value.preserveAspect = true;
        }

        ShowInfo(selectedPart);
    }

    private List<PartData> GetOwnedParts()
    {
        List<PartData> result = new List<PartData>();
        HashSet<PartData> seen = new HashSet<PartData>();

        if (sourceInventory != null && sourceInventory.slots != null)
        {
            for (int i = 0; i < sourceInventory.slots.Count; i++)
            {
                PlayerInventory.Slot slot = sourceInventory.slots[i];
                PartData part = slot != null ? slot.item as PartData : null;
                if (part == null || slot.count <= 0 || !IsEquippablePart(part) || seen.Contains(part))
                    continue;

                seen.Add(part);
                result.Add(part);
            }
        }

        IReadOnlyList<PartData> crafted = PartSystemRuntimeState.GetCraftedEquipmentParts();
        if (crafted != null)
        {
            for (int i = 0; i < crafted.Count; i++)
            {
                PartData part = crafted[i];
                if (part == null || !IsEquippablePart(part) || seen.Contains(part))
                    continue;

                seen.Add(part);
                result.Add(part);
            }
        }

        return result;
    }

    private static bool IsEquippablePart(PartData part)
    {
        if (part == null)
            return false;

        string id = ItemNameRuleUtility.GetBestItemId(part);
        string name = ItemNameRuleUtility.GetBestItemName(null, part);
        return !ItemNameRuleUtility.IsModuleItem(id)
            && !ItemNameRuleUtility.IsModuleItem(name)
            && !ItemNameRuleUtility.IsPaperObject(id)
            && !ItemNameRuleUtility.IsPaperObject(name);
    }

    private void SelectOwnedIndex(int index)
    {
        List<PartData> owned = GetOwnedParts();
        if (index < 0 || index >= owned.Count)
            return;

        selectedPart = owned[index];
        ShowInfo(selectedPart);
    }

    private void TryEquipSelected(PartSlotType slot)
    {
        if (selectedPart == null)
        {
            SetHint("장착할 파츠를 먼저 선택하세요.");
            return;
        }

        if (!PartSlotTypeUtility.Matches(selectedPart.slotType, slot))
        {
            SetHint("선택한 파츠는 " + PartSlotTypeUtility.GetKoreanLabel(slot) + " 슬롯에 장착할 수 없습니다.");
            return;
        }

        if (equipmentManager == null)
            equipmentManager = FindObjectOfType<EquipmentManager>(true);
        if (equipmentManager == null)
        {
            SetHint("장착 매니저를 찾을 수 없습니다.");
            return;
        }

        equipmentManager.SetInventory(sourceInventory);
        PartSystemRuntimeState.BindEquipment(equipmentManager);

        if (!equipmentManager.EquipPart(selectedPart))
        {
            SetHint("해당 파츠를 장착할 수 없습니다.");
            return;
        }

        RefreshAbilityState();
        SetHint(PartSlotTypeUtility.GetKoreanLabel(slot) + " 슬롯에 장착되었습니다.");
        selectedPart = null;
        Refresh();
    }

    private static void RefreshAbilityState()
    {
        // 지금 조작 중인 캐릭터만 갱신한다. 윤서의 능력은 EquipmentManager.HasPart 를 매 프레임 직접 본다.
        GameObject activePlayer = AetherPlayerCharacter.FindActive();
        if (activePlayer != null && AetherPlayerCharacter.Identify(activePlayer) == AetherCharacter.Yunseo)
            return;

        HangyeolPartAbilityController hangyeol = activePlayer != null
            ? activePlayer.GetComponentInChildren<HangyeolPartAbilityController>(true)
            : FindObjectOfType<HangyeolPartAbilityController>(true);
        if (hangyeol != null)
            hangyeol.RefreshSuperJumpState();

        Move_Hankyul move = activePlayer != null ? activePlayer.GetComponentInChildren<Move_Hankyul>(true) : FindObjectOfType<Move_Hankyul>(true);
        EquipmentManager manager = activePlayer != null ? activePlayer.GetComponentInChildren<EquipmentManager>(true) : FindObjectOfType<EquipmentManager>(true);
        if (move != null && manager != null)
            move.SetSuperJumpEnabled(manager.HasPart(PartType.SuperJump));
    }

    private void SetHint(string message)
    {
        if (hintText != null)
            hintText.text = message;
    }

    private void ShowInfo(PartData part)
    {
        EquipmentItemData item = FindEquipmentItem(part);
        Sprite icon = part != null ? GetPartIcon(part) : null;

        if (infoIcon != null)
        {
            infoIcon.enabled = icon != null;
            infoIcon.sprite = icon;
            infoIcon.preserveAspect = true;
        }

        if (infoTitle != null)
            infoTitle.text = part == null ? "-" : ItemNameRuleUtility.GetBestItemName(null, part);
        if (infoType != null)
            infoType.text = part == null ? "종류 -" : "종류 " + PartSlotTypeUtility.GetKoreanLabel(part.slotType) + " 파츠";

        int attack = item != null ? item.attack : part != null ? part.attackBonus : 0;
        int defense = item != null ? item.defense : part != null ? part.defenseBonus : 0;
        int speed = item != null ? item.speed : part != null ? part.speedBonus : 0;
        if (infoStats != null)
            infoStats.text = "Attack " + Signed(attack) + "\nDefence " + Signed(defense) + "\nSpeed " + Signed(speed);
        if (infoDetail != null)
            infoDetail.text = item != null && !string.IsNullOrWhiteSpace(item.description) ? item.description : "-";
    }

    private static string Signed(int value)
    {
        return value >= 0 ? "+" + value : value.ToString();
    }

    private static EquipmentItemData FindEquipmentItem(PartData part)
    {
        if (part == null)
            return null;

        EquipmentItemData[] items = Resources.LoadAll<EquipmentItemData>("AETHERParts");
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].sourceCraftedPart == part)
                return items[i];
        }

        return null;
    }

    private static Sprite GetPartIcon(PartData part)
    {
        if (part == null)
            return null;

        if (part.icon != null)
            return part.icon;

        return part.itemSprite;
    }

    private static Button CreateButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.015f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Image CreateImage(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.preserveAspect = true;
        return image;
    }

    private static TMP_Text CreateText(RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        return CreateText(parent, text, anchoredPosition, size, fontSize, style, alignment, color, new Vector2(0f, 1f));
    }

    private static TMP_Text CreateText(RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyles style, TextAlignmentOptions alignment, Color color, Vector2 anchor)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text label = go.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    private static Color SoftWhite(float alpha = 0.86f)
    {
        return new Color(0.86f, 0.93f, 1f, alpha);
    }

}
