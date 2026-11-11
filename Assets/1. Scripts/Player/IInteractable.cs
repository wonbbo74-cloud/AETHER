using UnityEngine;

public interface IInteractable
{
    // UI에 표시할 이름 (예: "물", "식량")
    string GetDisplayName();

    // UI에 표시할 아이콘
    Sprite GetIcon();

    // 실제 상호작용 로직
    void Interact(PlayerInteractor interactor);
}
