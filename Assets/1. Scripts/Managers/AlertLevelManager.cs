using System;
using UnityEngine;

public enum AlertDifficulty
{
    Easy,
    Normal,
    Hard
}

public enum AlertEventType
{
    SuccessSmall,
    SuccessMedium,
    SuccessLarge,

    FailureSmall,
    FailureMedium,
    FailureLarge
}

public class AlertLevelManager : MonoBehaviour
{
    public static AlertLevelManager Instance { get; private set; }

    [Header("Alert Value")]
    [SerializeField] private int minAlert = 0;
    [SerializeField] private int maxAlert = 200;
    [SerializeField] private int defaultAlert = 100;

    [Header("Difficulty Threshold")]
    [SerializeField] private int easyThreshold = 50;
    [SerializeField] private int hardThreshold = 150;

    [Header("Alert Change Values")]
    [SerializeField] private int successSmallValue = -10;
    [SerializeField] private int successMediumValue = -20;
    [SerializeField] private int successLargeValue = -30;

    [SerializeField] private int failureSmallValue = 10;
    [SerializeField] private int failureMediumValue = 20;
    [SerializeField] private int failureLargeValue = 30;

    [Header("Runtime State")]
    [SerializeField] private int currentAlert;

    public int CurrentAlert => currentAlert;
    public AlertDifficulty CurrentDifficulty { get; private set; }

    public event Action<int> OnAlertChanged;
    public event Action<AlertDifficulty> OnDifficultyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        currentAlert = Mathf.Clamp(defaultAlert, minAlert, maxAlert);
        CurrentDifficulty = CalculateDifficulty(currentAlert);
    }

    private void Start()
    {
        OnAlertChanged?.Invoke(currentAlert);
        OnDifficultyChanged?.Invoke(CurrentDifficulty);
    }

    public void ApplyAlertEvent(AlertEventType eventType)
    {
        int amount = GetAlertChangeValue(eventType);
        ChangeAlert(amount);
    }

    private int GetAlertChangeValue(AlertEventType eventType)
    {
        switch (eventType)
        {
            case AlertEventType.SuccessSmall:
                return successSmallValue;

            case AlertEventType.SuccessMedium:
                return successMediumValue;

            case AlertEventType.SuccessLarge:
                return successLargeValue;

            case AlertEventType.FailureSmall:
                return failureSmallValue;

            case AlertEventType.FailureMedium:
                return failureMediumValue;

            case AlertEventType.FailureLarge:
                return failureLargeValue;

            default:
                Debug.LogWarning($"등록되지 않은 경계도 이벤트입니다: {eventType}");
                return 0;
        }
    }

    public void ChangeAlert(int amount)
    {
        int previousAlert = currentAlert;
        AlertDifficulty previousDifficulty = CurrentDifficulty;

        currentAlert = Mathf.Clamp(currentAlert + amount, minAlert, maxAlert);
        CurrentDifficulty = CalculateDifficulty(currentAlert);

        if (previousAlert != currentAlert)
        {
            OnAlertChanged?.Invoke(currentAlert);
        }

        if (previousDifficulty != CurrentDifficulty)
        {
            OnDifficultyChanged?.Invoke(CurrentDifficulty);
            Debug.Log($"경계도 난이도 변경: {previousDifficulty} → {CurrentDifficulty}");
        }
    }

    private AlertDifficulty CalculateDifficulty(int alertValue)
    {
        if (alertValue < easyThreshold)
            return AlertDifficulty.Easy;

        if (alertValue > hardThreshold)
            return AlertDifficulty.Hard;

        return AlertDifficulty.Normal;
    }

    public float GetAlertRatio()
    {
        return Mathf.InverseLerp(minAlert, maxAlert, currentAlert);
    }

    public void ResetAlert()
    {
        SetAlert(defaultAlert);
    }

    public void SetAlert(int value)
    {
        int previousAlert = currentAlert;
        AlertDifficulty previousDifficulty = CurrentDifficulty;

        currentAlert = Mathf.Clamp(value, minAlert, maxAlert);
        CurrentDifficulty = CalculateDifficulty(currentAlert);

        if (previousAlert != currentAlert)
        {
            OnAlertChanged?.Invoke(currentAlert);
        }

        if (previousDifficulty != CurrentDifficulty)
        {
            OnDifficultyChanged?.Invoke(CurrentDifficulty);
            Debug.Log($"경계도 난이도 변경: {previousDifficulty} → {CurrentDifficulty}");
        }
    }
}