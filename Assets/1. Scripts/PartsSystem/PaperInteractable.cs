using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PaperInteractable : MonoBehaviour, ILongPressInteractable, IHoldInteractable, IPickupInteractable
{
    [SerializeField] private PaperData paperData;
    [SerializeField] private ItemData fallbackItemData;
    [SerializeField] private int amount = 1;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private float longPressDuration = 0.8f;
    [SerializeField] private PaperReadUI paperReadUI;

    private ItemData runtimeItemData;

    public PaperData PaperData => paperData;
    public float LongPressDuration => Mathf.Max(0.05f, longPressDuration);

    public void Configure(ItemData fallbackData, PaperData data = null)
    {
        if (fallbackData != null)
            fallbackItemData = fallbackData;

        if (data != null)
            paperData = data;
        else if (paperData == null)
            paperData = PartResourceResolver.FindPaperData(ItemNameRuleUtility.GetBestItemName(gameObject, fallbackItemData));
    }

    private void Reset()
    {
        Collider paperCollider = GetComponent<Collider>();
        if (paperCollider != null)
            paperCollider.isTrigger = true;
    }

    private void Awake()
    {
        if (paperData == null && ItemNameRuleUtility.IsPaperObject(gameObject, fallbackItemData))
            paperData = PartResourceResolver.FindPaperData(gameObject.name);

        if (paperReadUI == null)
            paperReadUI = FindObjectOfType<PaperReadUI>(true);
    }

    public string GetDisplayName()
    {
        ItemData data = ResolveItemData();
        string name = ItemNameRuleUtility.GetPickupDisplayName(gameObject, data);
        return string.IsNullOrWhiteSpace(name) ? gameObject.name : name;
    }

    public Sprite GetIcon()
    {
        ItemData data = ResolveItemData();
        return data != null ? data.itemSprite : null;
    }

    public string GetLongPressPrompt()
    {
        return InteractionPromptTextUtility.FormatHoldPrompt("주문서 읽기");
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor == null || interactor.Inventory == null)
        {
            Debug.LogWarning($"[PaperPickup] Inventory missing: {name}", this);
            return;
        }

        ItemData data = ResolveItemData();
        if (data == null)
        {
            Debug.LogWarning($"[PaperPickup] ItemData missing: {name}", this);
            return;
        }

        if (!ItemNameRuleUtility.IsPaperObject(gameObject, data))
        {
            Debug.LogWarning($"[PaperPickup] Not a paper item: {ItemNameRuleUtility.GetBestItemName(gameObject, data)}", this);
            return;
        }

        string itemName = ItemNameRuleUtility.GetBestItemName(gameObject, data);
        Debug.Log($"[PaperPickup] Try pickup: {itemName}", this);

        if (!interactor.Inventory.AddItem(data, Mathf.Max(1, amount)))
        {
            Debug.LogWarning($"[PaperPickup] AddItem failed: {itemName}", this);
            return;
        }

        AetherLevel.ReportPickup(this, data);
        Debug.Log($"[PaperPickup] AddItem success: {itemName}");

        // 설계서를 주웠으면 05 의 원래 목표(표적 → 연구소)로 돌아간다.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("05."))
            // Shiri 는 총이 없다. 05 첫 목표와 같이 충전 펀치로 안내한다.
            AetherModernHUD.SetCurrentObjective(AetherCharacterChoice.Selected == AetherCharacter.Yunseo
                ? "제작설계서를 얻었다. 이제 빛나는\n표적을 충전 펀치로 차례로 맞혀라."
                : "제작설계서를 얻었다. 이제 빛나는\n표적을 차례로 쏘아 문을 열어라.");

        if (destroyOnPickup)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    public void CompleteLongPress(PlayerInteractor interactor)
    {
        if (!ItemNameRuleUtility.IsPaperObject(gameObject, fallbackItemData))
            return;

        if (paperReadUI == null)
            paperReadUI = FindObjectOfType<PaperReadUI>(true);

        if (paperReadUI != null)
            paperReadUI.Open(this, interactor);
    }

    public string GetInteractionName()
    {
        return "주문서 읽기";
    }

    public float GetHoldDuration()
    {
        return LongPressDuration;
    }

    public void BeginHold(GameObject interactor)
    {
    }

    public void UpdateHold(GameObject interactor, float holdProgress)
    {
    }

    public void CancelHold(GameObject interactor)
    {
    }

    public void CompleteHold(GameObject interactor)
    {
        PlayerInteractor playerInteractor = interactor != null ? interactor.GetComponent<PlayerInteractor>() : null;
        CompleteLongPress(playerInteractor);
    }

    public string GetTitle()
    {
        if (paperData != null && !string.IsNullOrWhiteSpace(paperData.title))
            return paperData.title;

        if (fallbackItemData != null && !string.IsNullOrWhiteSpace(fallbackItemData.displayName))
            return fallbackItemData.displayName;

        return gameObject.name;
    }

    public string GetBody()
    {
        if (paperData != null && !string.IsNullOrWhiteSpace(paperData.bodyText))
            return paperData.bodyText;

        if (fallbackItemData != null && !string.IsNullOrWhiteSpace(fallbackItemData.description))
            return fallbackItemData.description;

        return "No manual data is connected yet.";
    }

    public string GetHint()
    {
        if (paperData != null && !string.IsNullOrWhiteSpace(paperData.hintText))
            return paperData.hintText;

        return "Place the modules on the crafting grid according to the blueprint.";
    }

    public string GetResultPartName()
    {
        if (paperData != null)
        {
            string resultName = paperData.GetResultPartName();
            if (!string.IsNullOrWhiteSpace(resultName))
                return resultName;
        }

        return "Unknown Part";
    }

    public string[] GetRequiredModules()
    {
        if (paperData == null)
            return null;

        if (paperData.requiredModuleIds != null && paperData.requiredModuleIds.Length > 0)
            return paperData.requiredModuleIds;

        return paperData.relatedRecipe != null ? paperData.relatedRecipe.requiredModuleIds : null;
    }

    private ItemData ResolveItemData()
    {
        if (fallbackItemData != null)
            return fallbackItemData;

        fallbackItemData = PartResourceResolver.FindItemData(gameObject.name);
        if (fallbackItemData != null)
            return fallbackItemData;

        if (runtimeItemData != null)
            return runtimeItemData;

        runtimeItemData = ScriptableObject.CreateInstance<ItemData>();
        string normalizedName = ItemNameRuleUtility.NormalizeName(gameObject.name);
        runtimeItemData.id = normalizedName;
        runtimeItemData.displayName = string.IsNullOrWhiteSpace(normalizedName) ? gameObject.name : normalizedName;
        runtimeItemData.description = paperData != null && !string.IsNullOrWhiteSpace(paperData.bodyText)
            ? paperData.bodyText
            : "Auto-detected parts paper.";
        return runtimeItemData;
    }
}

