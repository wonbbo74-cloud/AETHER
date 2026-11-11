using UnityEngine;

public class SimpleAlertLevelManager : MonoBehaviour
{
    public static SimpleAlertLevelManager Instance;

    [SerializeField] private int currentAlertLevel = 0;
    [SerializeField] private int maxAlertLevel = 100;

    public int CurrentAlertLevel => currentAlertLevel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddAlert(int amount)
    {
        currentAlertLevel = Mathf.Clamp(currentAlertLevel + amount, 0, maxAlertLevel);
        Debug.Log($"[Alert Level] 현재 경계도: {currentAlertLevel}");
    }

    public void ReduceAlert(int amount)
    {
        currentAlertLevel = Mathf.Clamp(currentAlertLevel - amount, 0, maxAlertLevel);
        Debug.Log($"[Alert Level] 현재 경계도: {currentAlertLevel}");
    }
}