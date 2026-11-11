using UnityEngine;

/// <summary>
/// 인벤토리 아이템 '사용'과 '버리기'. 새 인벤토리 화면(AetherInventoryScreen)과 퀵슬롯이 같이 쓴다.
///
/// 새 인벤토리 화면에는 사용·버리기가 없어서 물·식량·팩을 인벤토리에서 쓸 수 없었다(예전 화면은 꺼져 있다).
/// 퀵슬롯은 효과가 없어도 아이템을 없앴다(한결이 실드팩을 쓰면 그냥 사라졌다).
/// 이제 실제로 효과가 났을 때만 하나를 줄인다.
/// </summary>
public static class AetherItemUse
{
    public static bool IsUsable(ItemData item)
    {
        return item != null && !(item is PartData) && item.effects != null && item.effects.Length > 0;
    }

    /// <summary>제작 재료·설계서·강화파츠는 버릴 수 없다. 씬이 바뀌면 바닥에 둔 것이 사라져 게임을 끝낼 수 없게 되기 때문.</summary>
    public static bool IsDroppable(ItemData item)
    {
        if (item == null || item is PartData)
            return false;

        string id = ItemNameRuleUtility.GetBestItemId(item);
        return !ItemNameRuleUtility.IsModuleItem(id) && !ItemNameRuleUtility.IsPaperObject(id);
    }

    /// <summary>슬롯의 아이템을 하나 쓴다. 효과가 났을 때만 하나 줄인다.</summary>
    public static bool UseAt(PlayerInventory inventory, int slotIndex, out string message)
    {
        message = string.Empty;
        PlayerInventory.Slot slot = inventory != null ? inventory.GetSlot(slotIndex) : null;
        if (slot == null || slot.item == null || slot.count <= 0)
            return false;

        ItemData item = slot.item;
        if (!IsUsable(item))
        {
            message = "사용할 수 없는 아이템입니다.";
            return false;
        }

        GameObject owner = FindOwner(inventory);
        bool used;

        if (HasEffect(item, EffectType.EnergyPack))
        {
            Hankyul_Fire fire = owner != null ? owner.GetComponentInChildren<Hankyul_Fire>(true) : null;
            if (fire == null)
            {
                message = $"{AetherCharacterChoice.HankyulDisplayName} 전용 아이템입니다. (총 에너지)";
                used = false;
            }
            else
            {
                used = fire.UseEnergyPack(out message);
                // 충전 소리는 UseEnergyPack 이 낸다.
            }
        }
        else
        {
            PlayerStatus status = owner != null ? owner.GetComponentInChildren<PlayerStatus>(true) : null;
            if (status == null)
            {
                message = "상태 정보를 찾을 수 없습니다.";
                used = false;
            }
            else
            {
                used = status.ApplyItem(item, out message);
                if (!used && HasEffect(item, EffectType.ShieldPack) && !status.useShield)
                    message = $"{AetherCharacterChoice.YunseoDisplayName} 전용 아이템입니다. (실드)";
            }
        }

        if (used)
            inventory.ConsumeAt(slotIndex, 1);
        else
            AetherAudio.Play2D("ui_error", 0.45f, 1f, 0f);

        return used;
    }

    /// <summary>슬롯의 아이템을 하나 플레이어 앞 바닥에 내려놓는다. 다시 주울 수 있다.</summary>
    public static bool DropAt(PlayerInventory inventory, int slotIndex, out string message)
    {
        message = string.Empty;
        PlayerInventory.Slot slot = inventory != null ? inventory.GetSlot(slotIndex) : null;
        if (slot == null || slot.item == null || slot.count <= 0)
            return false;

        ItemData item = slot.item;
        if (!IsDroppable(item))
        {
            message = "제작 재료·설계서는 버릴 수 없습니다.";
            AetherAudio.Play2D("ui_error", 0.45f, 1f, 0f);
            return false;
        }

        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null)
            return false;

        inventory.ConsumeAt(slotIndex, 1);
        SpawnPickup(item, player);
        AetherAudio.Play2D("pickup", 0.4f, 0.8f, 0.03f);
        return true;
    }

    /// <summary>바닥에 놓인 아이템: 아이템 그림이 떠서 천천히 돈다. 다가가 F 로 줍는다.</summary>
    public static GameObject SpawnPickup(ItemData item, Transform player)
    {
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 spot = player.position + forward * 1.2f + Vector3.up * 1.5f;

        if (Physics.Raycast(spot, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            spot = hit.point;
        else
            spot = player.position + forward * 1.2f;

        return SpawnPickupAt(item, spot);
    }

    public static GameObject SpawnPickupAt(ItemData item, Vector3 groundPoint)
    {
        GameObject go = new GameObject("Dropped_" + item.name);
        go.transform.position = groundPoint + Vector3.up * 0.45f;

        SphereCollider col = go.AddComponent<SphereCollider>();
        col.radius = 0.5f;
        col.isTrigger = true;

        InteractableItem pickup = go.AddComponent<InteractableItem>();
        pickup.itemData = item;
        pickup.amount = 1;

        if (item.itemSprite != null)
        {
            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(go.transform, false);
            SpriteRenderer sr = icon.AddComponent<SpriteRenderer>();
            sr.sprite = item.itemSprite;
            float size = Mathf.Max(item.itemSprite.bounds.size.x, item.itemSprite.bounds.size.y);
            icon.transform.localScale = Vector3.one * (size > 0.001f ? 0.45f / size : 1f);
            go.AddComponent<AetherFloatingPickup>();
        }

        return go;
    }

    private static bool HasEffect(ItemData item, EffectType type)
    {
        if (item.effects == null) return false;
        foreach (ItemEffect e in item.effects)
            if (e.type == type) return true;
        return false;
    }

    private static GameObject FindOwner(PlayerInventory inventory)
    {
        GameObject active = AetherPlayerCharacter.FindActive();
        if (active != null) return active;
        return inventory != null ? inventory.transform.root.gameObject : null;
    }
}

/// <summary>바닥 아이템이 둥실 떠서 돈다(눈에 띄게).</summary>
public class AetherFloatingPickup : MonoBehaviour
{
    private Vector3 basePosition;

    private void Start() => basePosition = transform.position;

    private void Update()
    {
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.06f);
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
