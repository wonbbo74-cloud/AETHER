using UnityEngine;

public interface ILongPressInteractable : IInteractable
{
    float LongPressDuration { get; }
    string GetLongPressPrompt();
    void CompleteLongPress(PlayerInteractor interactor);
}
