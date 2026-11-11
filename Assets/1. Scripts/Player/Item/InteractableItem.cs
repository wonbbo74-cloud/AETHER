using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractableItem : MonoBehaviour, IInteractable, IPickupInteractable
{
    [Header("������ ������")]
    public ItemData itemData;
    public int amount = 1;              // �� ���� �ݴ� ���� (�⺻ 1��)

    private void Reset()
    {
        // �������� Ʈ���� �ݶ��̴��� ����ϴ� ���� �⺻������
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    public string GetDisplayName()
    {
        if (itemData != null && !string.IsNullOrEmpty(itemData.displayName))
            return itemData.displayName;

        return gameObject.name;
    }

    public Sprite GetIcon()
    {
        if (itemData != null)
            return itemData.itemSprite;

        return null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (interactor == null || interactor.Inventory == null || itemData == null)
            return;

        interactor.Inventory.AddItem(itemData, amount);
        Debug.Log($"{GetDisplayName()} {amount}���� ȹ���߽��ϴ�.");

        // 이어하기로 돌아왔을 때 이미 주운 바닥 물건이 다시 놓이지 않게 기록한다.
        AetherSave.NotePickedFloorItem(gameObject);

        Destroy(gameObject);
    }
}
