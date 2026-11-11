using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 디자인 이미지(Resources/UI/PartsEquipPanel)를 배경으로 쓰는 파츠 장착 화면. P 키로 연다.
/// 왼쪽 목록에서 파츠를 고르고 가운데 인체 도식의 슬롯을 누르면 장착된다.
/// </summary>
public class AetherPartsEquipScreen : MonoBehaviour
{
    private const string SpritePath = "UI/PartsEquipPanel";
    private const int ListRows = 6;
    private const int OwnedCells = 8;

    private static readonly float[] ListY = { 496f, 732f, 968f, 1204f, 1440f, 1676f };
    private const float ListX0 = 184f;
    private const float ListX1 = 1072f;
    private const float ListH = 212f;

    private static readonly float[] OwnedX = { 2620f, 2888f, 3156f, 3424f };
    private static readonly float[] OwnedY = { 1252f, 1576f };
    private const float OwnedW = 244f;
    private const float OwnedH = 292f;

    private struct SlotSpec
    {
        public PartSlotType type;
        public string label;
        public float x0, y0, x1, y1;

        public SlotSpec(PartSlotType type, string label, float x0, float y0, float x1, float y1)
        {
            this.type = type; this.label = label;
            this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1;
        }
    }

    // 이미지에서 잰 장착 슬롯 위치
    private static readonly SlotSpec[] Slots =
    {
        new SlotSpec(PartSlotType.Head,    "머리", 1719f,  548f, 1990f,  814f),
        new SlotSpec(PartSlotType.Arm,     "팔",   1212f,  984f, 1464f, 1236f),
        new SlotSpec(PartSlotType.Support, "보조", 2242f,  984f, 2496f, 1236f),
        new SlotSpec(PartSlotType.Leg,     "다리", 1212f, 1420f, 1464f, 1672f),
        new SlotSpec(PartSlotType.Core,    "코어", 2242f, 1420f, 2496f, 1672f),
    };

    private static readonly Color InfoPanelColor = new Color(0.035f, 0.075f, 0.122f, 1f);
    private static readonly Color ListRowColor = new Color(0.047f, 0.094f, 0.141f, 1f);

    private static AetherPartsEquipScreen instance;
    private static bool subscribed;

    public static bool IsOpen { get; private set; }

    /// <summary>이 화면이 떠 있을 수 있는 상태(P 키를 이 화면이 받는다).</summary>
    public static bool Exists => instance != null && instance.isActiveAndEnabled;

    private Canvas canvas;
    private RectTransform design;
    private PlayerInventory inventory;
    private EquipmentManager equipment;

    private readonly List<TMP_Text> listNames = new List<TMP_Text>();
    private readonly List<Image> listIcons = new List<Image>();
    private readonly List<AetherHoverGlow> listGlows = new List<AetherHoverGlow>();
    private readonly List<GameObject> listCovers = new List<GameObject>();

    private readonly List<Image> slotIcons = new List<Image>();
    private readonly List<AetherHoverGlow> slotGlows = new List<AetherHoverGlow>();

    private readonly List<Image> ownedIcons = new List<Image>();
    private readonly List<AetherHoverGlow> ownedGlows = new List<AetherHoverGlow>();

    private GameObject infoCover;
    private Image infoIcon;
    private TMP_Text infoName;
    private TMP_Text infoSlot;
    private TMP_Text infoEffect;
    private TMP_Text listCountText;
    private TMP_Text equipCountText;
    private TMP_Text ownedCountText;

    private readonly List<PartData> ownedParts = new List<PartData>();
    private int selectedIndex = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (AetherRuntimeSwitch.DisableAll || AetherRuntimeSwitch.DisableScreens)
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
        {
            instance.Close();
            instance.inventory = null;
            instance.equipment = null;
        }
    }

    private static void Ensure()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("AetherPartsEquipScreen");
        instance = host.AddComponent<AetherPartsEquipScreen>();
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
        BuildUI();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool togglePressed = keyboard != null
            ? keyboard.pKey.wasPressedThisFrame
            : Input.GetKeyDown(KeyCode.P);

        if (IsOpen)
        {
            bool closePressed = keyboard != null
                ? keyboard.escapeKey.wasPressedThisFrame
                : Input.GetKeyDown(KeyCode.Escape);

            if (closePressed || togglePressed)
                Close();

            return;
        }

        if (togglePressed && !AetherPauseMenu.IsPaused && !AetherUIState.AnyScreenOpen)
            Open();
    }

    public void Open()
    {
        if (IsOpen)
            return;

        // 파츠 장착은 한결만 한다. 윤서로는 화면을 열지 않고 안내만 띄운다.
        if (!AetherPlayerCharacter.CanEquipParts(AetherPlayerCharacter.FindActive()))
        {
            AetherObjectiveToast.Show(AetherPlayerCharacter.PartsEquipOnlyHankyulMessage, 3f);
            return;
        }

        if (!Resolve())
            return;

        IsOpen = true;
        AetherUIState.BeginScreen();
        AetherAudio.Play2D("ui_open", 0.55f, 1f, 0f);

        canvas.gameObject.SetActive(true);

        selectedIndex = -1;
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        AetherAudio.Play2D("ui_close", 0.5f, 1f, 0f);

        if (canvas != null)
            canvas.gameObject.SetActive(false);

        AetherUIState.EndScreen();
    }

    private bool Resolve()
    {
        if (inventory == null)
            inventory = PartSystemRuntimeState.GetOrBindBestInventory();

        if (inventory == null)
            return false;

        if (equipment == null)
        {
            equipment = inventory.GetComponent<EquipmentManager>();
            if (equipment == null)
                equipment = inventory.GetComponentInParent<EquipmentManager>();

            // 꺼진 캐릭터의 장비를 집지 않도록 지금 조작 중인 캐릭터부터 본다.
            GameObject activePlayer = AetherPlayerCharacter.FindActive();
            if (equipment == null && activePlayer != null)
            {
                AetherPlayerCharacter.Prepare(activePlayer);
                equipment = activePlayer.GetComponentInChildren<EquipmentManager>(true);
            }

            if (equipment == null)
                equipment = FindFirstObjectByType<EquipmentManager>(FindObjectsInactive.Include);
        }

        if (equipment == null)
            return false;

        // 예전 파츠 UI가 P키로 같이 열리지 않도록 정지시킨다.
        PartsUIHotkeyController[] hotkeys = FindObjectsByType<PartsUIHotkeyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < hotkeys.Length; i++)
        {
            if (hotkeys[i] != null)
                hotkeys[i].enabled = false;
        }

        inventory.OnInventoryChanged -= Refresh;
        inventory.OnInventoryChanged += Refresh;
        return true;
    }

    // ------------------------------------------------------------------ 표시

    private void Refresh()
    {
        if (inventory == null || equipment == null)
            return;

        // 강화파츠는 인벤토리가 아니라 파츠 보관함에 있다. 인벤토리에 남아 있던 것도 보관함으로 옮긴다.
        PartSystemRuntimeState.MovePartsOutOfInventory(inventory);
        ownedParts.Clear();
        ownedParts.AddRange(PartSystemRuntimeState.GetStoredParts(equipment));

        // 왼쪽 목록
        for (int i = 0; i < ListRows; i++)
        {
            PartData part = i < ownedParts.Count ? ownedParts[i] : null;
            bool has = part != null;

            if (listCovers[i] != null)
                listCovers[i].SetActive(has);

            if (listNames[i] != null)
                listNames[i].text = has ? GetName(part) : string.Empty;

            if (listIcons[i] != null)
            {
                listIcons[i].sprite = has ? part.itemSprite : null;
                listIcons[i].enabled = has && part.itemSprite != null;
            }

            if (listGlows[i] != null)
                listGlows[i].SetSelected(has && i == selectedIndex);
        }

        // 보유 파츠 격자
        for (int i = 0; i < OwnedCells; i++)
        {
            PartData part = i < ownedParts.Count ? ownedParts[i] : null;

            if (ownedIcons[i] != null)
            {
                ownedIcons[i].sprite = part != null ? part.itemSprite : null;
                ownedIcons[i].enabled = part != null && part.itemSprite != null;
            }

            if (ownedGlows[i] != null)
                ownedGlows[i].SetSelected(part != null && i == selectedIndex);
        }

        // 장착 슬롯
        int equipped = 0;
        for (int i = 0; i < Slots.Length; i++)
        {
            PartData part = equipment.GetEquipped(Slots[i].type);
            if (part != null)
                equipped++;

            if (slotIcons[i] != null)
            {
                slotIcons[i].sprite = part != null ? part.itemSprite : null;
                slotIcons[i].enabled = part != null && part.itemSprite != null;
            }

            if (i < slotPlates.Count && slotPlates[i] != null)
                slotPlates[i].SetActive(part != null);
        }

        if (listCountText != null)
            listCountText.text = $"{ownedParts.Count:00} / {ListRows:00}";

        if (ownedCountText != null)
            ownedCountText.text = $"{Mathf.Min(ownedParts.Count, OwnedCells):00} / {OwnedCells:00}";

        if (equipCountText != null)
            equipCountText.text = $"{equipped:00} / {Slots.Length:00}";

        ShowInfo(selectedIndex);
    }

    private void SelectPart(int index)
    {
        selectedIndex = index >= 0 && index < ownedParts.Count ? index : -1;
        Refresh();
    }

    private void TryEquip(int slotIndex)
    {
        if (equipment == null)
            return;

        SlotSpec spec = Slots[slotIndex];
        PartData equippedHere = equipment.GetEquipped(spec.type);

        // 이미 장착된 슬롯을 누르면 해제
        if (selectedIndex < 0)
        {
            if (equippedHere != null)
            {
                equipment.Unequip(spec.type);
                AetherAudio.Play2D("ui_back", 0.5f, 1f, 0f);
            }

            Refresh();
            return;
        }

        PartData part = ownedParts[selectedIndex];
        if (!PartSlotTypeUtility.Matches(part.slotType, spec.type))
        {
            Debug.Log($"[AetherPartsEquipScreen] '{GetName(part)}'는 {spec.label} 슬롯에 맞지 않습니다.");
            AetherAudio.Play2D("ui_error", 0.45f, 1f, 0f);
            Refresh();
            // 거절음만 나고 화면엔 아무 변화가 없어 왜 안 되는지 알 수 없었다. 정보 칸에 맞는 슬롯을 적어 준다.
            if (infoEffect != null)
                infoEffect.text = part.description + "\n\n<color=#FF7A6B>" + spec.label + " 슬롯에는 장착할 수 없습니다. "
                    + PartSlotTypeUtility.GetKoreanLabel(part.slotType) + " 슬롯을 누르세요.</color>";
            return;
        }

        // 장착이 실제로 됐을 때만 체결음, 실패하면 거절음
        bool equipped = equipment.EquipPart(part);
        AetherAudio.Play2D(equipped ? "equip" : "ui_error", equipped ? 0.7f : 0.45f, 1f, 0f);
        selectedIndex = -1;
        Refresh();
    }

    private void ShowInfo(int index)
    {
        PartData part = index >= 0 && index < ownedParts.Count ? ownedParts[index] : null;
        bool has = part != null;

        if (infoCover != null)
            infoCover.SetActive(has);

        if (infoIcon != null)
        {
            infoIcon.sprite = has ? part.itemSprite : null;
            infoIcon.enabled = has && part.itemSprite != null;
        }

        if (infoName != null)
            infoName.text = has ? GetName(part) : string.Empty;

        if (infoSlot != null)
            infoSlot.text = has ? PartSlotTypeUtility.GetKoreanLabel(part.slotType) + " 슬롯" : string.Empty;

        if (infoEffect != null)
            infoEffect.text = has ? part.description : string.Empty;
    }

    private static string GetName(PartData part)
    {
        if (!string.IsNullOrWhiteSpace(part.displayName))
            return part.displayName;

        return string.IsNullOrWhiteSpace(part.id) ? part.name : part.id;
    }

    // ------------------------------------------------------------------ UI 생성

    private void BuildUI()
    {
        canvas = AetherScreenKit.CreateCanvas(transform, "PartsEquipCanvas", 30010);

        RectTransform dim = AetherScreenKit.CreateRect(canvas.transform as RectTransform, "Dim", 0f, 0f, 0f, 0f,
            typeof(RectTransform), typeof(Image));
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        dim.offsetMin = Vector2.zero;
        dim.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        design = AetherScreenKit.CreateBackdrop(canvas.transform, SpritePath, out _);

        BuildList();
        BuildEquipSlots();
        BuildOwnedGrid();
        BuildInfoPanel();

        // 배경 그림에 "00 / 06" 같은 숫자가 박혀 있어 실제 숫자와 겹쳐 보였다. 같은 색으로 덮는다(제작 화면과 같은 방법).
        CreateCover("ListCountCover", 780f, 362f, 990f, 434f, new Color(0.035f, 0.075f, 0.122f, 1f));
        CreateCover("EquipCountCover", 2150f, 362f, 2500f, 434f, new Color(0.035f, 0.075f, 0.122f, 1f));
        CreateCover("OwnedCountCover", 3380f, 1110f, 3690f, 1190f, new Color(0.031f, 0.067f, 0.118f, 1f));
        // 왼쪽 아래 "R 정렬 : 최신순" 도 그림에 박혀 있는데 정렬 기능은 없다. 눌러도 아무 일이 없어 가린다.
        AetherScreenKit.CreatePatch(design, "SortHintCover", 160f, 1904f, 540f, 2010f, 420f);

        listCountText = AetherScreenKit.CreateText(design, "ListCount", 700f, 362f, 980f, 434f,
            "00 / 06", AetherScreenKit.Font(38f), TextAlignmentOptions.Right, AetherScreenKit.Accent);
        equipCountText = AetherScreenKit.CreateText(design, "EquipCount", 2180f, 362f, 2460f, 434f,
            "00 / 05", AetherScreenKit.Font(38f), TextAlignmentOptions.Right, AetherScreenKit.Accent);
        ownedCountText = AetherScreenKit.CreateText(design, "OwnedCount", 3380f, 1114f, 3660f, 1186f,
            "00 / 08", AetherScreenKit.Font(38f), TextAlignmentOptions.Right, AetherScreenKit.Accent);

        AetherScreenKit.CreateHitButton(design, "Close", 3490f, 130f, 3790f, 226f, Close, out _);

        canvas.gameObject.SetActive(false);
    }

    private void CreateCover(string name, float x0, float y0, float x1, float y1, Color color)
    {
        RectTransform cover = AetherScreenKit.CreateRect(design, name, x0, y0, x1, y1, typeof(RectTransform), typeof(Image));
        Image image = cover.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void BuildList()
    {
        for (int i = 0; i < ListRows; i++)
        {
            int index = i;
            float y0 = ListY[i];

            AetherScreenKit.CreateHitButton(design, $"ListRow_{i}", ListX0, y0, ListX1, y0 + ListH,
                () => SelectPart(index), out _);

            RectTransform row = design.Find($"ListRow_{i}") as RectTransform;
            listGlows.Add(row.GetComponent<AetherHoverGlow>());

            // 디자인의 "EMPTY" 문구를 덮는 판
            RectTransform cover = AetherScreenKit.CreateRect(row, "Cover", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            cover.anchorMin = new Vector2(0.02f, 0.08f);
            cover.anchorMax = new Vector2(0.98f, 0.92f);
            cover.offsetMin = Vector2.zero;
            cover.offsetMax = Vector2.zero;
            Image coverImage = cover.GetComponent<Image>();
            coverImage.color = ListRowColor;
            coverImage.raycastTarget = false;
            cover.SetAsFirstSibling();
            listCovers.Add(cover.gameObject);
            cover.gameObject.SetActive(false);

            RectTransform iconRect = AetherScreenKit.CreateRect(row, "Icon", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            iconRect.anchorMin = new Vector2(0.03f, 0.15f);
            iconRect.anchorMax = new Vector2(0.15f, 0.85f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            Image icon = iconRect.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            listIcons.Add(icon);

            RectTransform nameRect = AetherScreenKit.CreateRect(row, "Name", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            nameRect.anchorMin = new Vector2(0.18f, 0f);
            nameRect.anchorMax = new Vector2(0.97f, 1f);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            TMP_Text nameText = nameRect.GetComponent<TextMeshProUGUI>();
            nameText.fontSize = AetherScreenKit.Font(40f);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            nameText.color = AetherScreenKit.TextBright;
            nameText.raycastTarget = false;
            listNames.Add(nameText);
        }
    }

    private void BuildEquipSlots()
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            int index = i;
            SlotSpec spec = Slots[i];

            AetherScreenKit.CreateHitButton(design, $"EquipSlot_{spec.label}", spec.x0, spec.y0, spec.x1, spec.y1,
                () => TryEquip(index), out _);

            RectTransform slot = design.Find($"EquipSlot_{spec.label}") as RectTransform;
            slotGlows.Add(slot.GetComponent<AetherHoverGlow>());

            // 장착되면 슬롯 배경의 'EMPTY' 글자가 그림 뒤로 비치지 않게 바탕으로 덮는다.
            RectTransform plate = AetherScreenKit.CreateRect(slot, "FilledPlate", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            plate.anchorMin = new Vector2(0.03f, 0.03f);
            plate.anchorMax = new Vector2(0.97f, 0.97f);
            plate.offsetMin = Vector2.zero;
            plate.offsetMax = Vector2.zero;
            Image plateImage = plate.GetComponent<Image>();
            plateImage.color = new Color(0.05f, 0.1f, 0.16f, 1f);
            plateImage.raycastTarget = false;
            plate.SetAsFirstSibling();
            plate.gameObject.SetActive(false);
            slotPlates.Add(plate.gameObject);

            RectTransform iconRect = AetherScreenKit.CreateRect(slot, "Icon", 0f, 0f, 0f, 0f,
                typeof(RectTransform), typeof(Image));
            iconRect.anchorMin = new Vector2(0.08f, 0.08f);   // 장착한 파츠 그림을 크게
            iconRect.anchorMax = new Vector2(0.92f, 0.92f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            Image icon = iconRect.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = false;
            slotIcons.Add(icon);
        }
    }

    private readonly List<GameObject> slotPlates = new List<GameObject>();

    private void BuildOwnedGrid()
    {
        for (int row = 0; row < OwnedY.Length; row++)
        {
            for (int col = 0; col < OwnedX.Length; col++)
            {
                int index = row * OwnedX.Length + col;
                float x0 = OwnedX[col];
                float y0 = OwnedY[row];

                AetherScreenKit.CreateHitButton(design, $"Owned_{index}", x0, y0, x0 + OwnedW, y0 + OwnedH,
                    () => SelectPart(index), out _);

                RectTransform cell = design.Find($"Owned_{index}") as RectTransform;
                ownedGlows.Add(cell.GetComponent<AetherHoverGlow>());

                RectTransform iconRect = AetherScreenKit.CreateRect(cell, "Icon", 0f, 0f, 0f, 0f,
                    typeof(RectTransform), typeof(Image));
                iconRect.anchorMin = new Vector2(0.16f, 0.16f);
                iconRect.anchorMax = new Vector2(0.84f, 0.84f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                Image icon = iconRect.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                ownedIcons.Add(icon);
            }
        }
    }

    private void BuildInfoPanel()
    {
        RectTransform cover = AetherScreenKit.CreateRect(design, "InfoCover", 2592f, 364f, 3696f, 1020f,
            typeof(RectTransform), typeof(Image));
        Image coverImage = cover.GetComponent<Image>();
        coverImage.color = InfoPanelColor;
        coverImage.raycastTarget = false;
        infoCover = cover.gameObject;
        infoCover.SetActive(false);

        RectTransform iconRect = AetherScreenKit.CreateRect(design, "InfoIcon", 2640f, 460f, 2940f, 760f,
            typeof(RectTransform), typeof(Image));
        infoIcon = iconRect.GetComponent<Image>();
        infoIcon.preserveAspect = true;
        infoIcon.raycastTarget = false;
        infoIcon.enabled = false;

        infoName = AetherScreenKit.CreateText(design, "InfoName", 3000f, 470f, 3660f, 550f,
            string.Empty, AetherScreenKit.Font(48f), TextAlignmentOptions.Left, AetherScreenKit.TextBright);

        infoSlot = AetherScreenKit.CreateText(design, "InfoSlot", 3000f, 560f, 3660f, 630f,
            string.Empty, AetherScreenKit.Font(38f), TextAlignmentOptions.Left, AetherScreenKit.Accent);

        infoEffect = AetherScreenKit.CreateText(design, "InfoEffect", 2640f, 800f, 3660f, 1000f,
            string.Empty, AetherScreenKit.Font(36f), TextAlignmentOptions.TopLeft, AetherScreenKit.TextDim);
        infoEffect.enableWordWrapping = true;
    }
}
