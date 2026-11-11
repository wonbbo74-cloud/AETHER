using UnityEngine;

public interface IHoldInteractable
{
    string GetInteractionName();
    float GetHoldDuration();
    void BeginHold(GameObject interactor);
    void UpdateHold(GameObject interactor, float holdProgress);
    void CancelHold(GameObject interactor);
    void CompleteHold(GameObject interactor);
}
