using UnityEngine;

public class WaterDrainConsole : MonoBehaviour, IHoldInteractable
{
    [Header("Interaction")]
    [SerializeField] private string interactionName = "단수 시스템 작동";
    [SerializeField] private float holdDuration = 1.2f;
    [SerializeField] private bool canUseOnlyOnce = true;
    [SerializeField] private float maxActivationDistance = 3.2f;

    [Header("Water Target")]
    [SerializeField] private WaterDrainGroup waterDrainGroup;

    private bool alreadyUsed = false;

    /// <summary>
    /// 상호작용 가능 거리를 바꾼다.
    /// 1구역처럼 지형이 크게 배치된 씬에서는 플레이어도 커져서, 기본값 3.2m로는
    /// 콘솔에 몸이 닿을 만큼 붙어도 닿지 않는다.
    /// </summary>
    public void SetMaxActivationDistance(float distance)
    {
        maxActivationDistance = Mathf.Max(0.5f, distance);
    }

    public string GetInteractionName()
    {
        if (alreadyUsed && canUseOnlyOnce)
            return "이미 단수 완료됨";

        return interactionName;
    }

    public float GetHoldDuration()
    {
        return holdDuration;
    }

    public void BeginHold(GameObject interactor)
    {
        if (alreadyUsed && canUseOnlyOnce)
            return;

        if (!IsInteractorCloseEnough(interactor))
            return;

        Debug.Log("[WaterDrainConsole] Hold started.");
    }

    public void UpdateHold(GameObject interactor, float holdProgress)
    {
        if (alreadyUsed && canUseOnlyOnce)
            return;

        if (!IsInteractorCloseEnough(interactor))
            return;
    }

    public void CancelHold(GameObject interactor)
    {
        if (alreadyUsed && canUseOnlyOnce)
            return;

        Debug.Log("[WaterDrainConsole] Hold canceled.");
    }

    public void CompleteHold(GameObject interactor)
    {
        if (alreadyUsed && canUseOnlyOnce)
            return;

        if (!IsInteractorCloseEnough(interactor))
        {
            Debug.Log("[WaterDrainConsole] Ignored drain request because the player is not near ElectricalMeterBox.");
            return;
        }

        if (waterDrainGroup == null)
        {
            Debug.LogWarning("[WaterDrainConsole] WaterDrainGroup is not assigned.");
            return;
        }

        alreadyUsed = true;
        waterDrainGroup.DrainAllWater();

        Debug.Log("[WaterDrainConsole] Hold complete. Pond water drained.");
    }

    private bool IsInteractorCloseEnough(GameObject interactor)
    {
        if (interactor == null)
            return false;

        return Vector3.Distance(transform.position, interactor.transform.position) <= maxActivationDistance;
    }
}
