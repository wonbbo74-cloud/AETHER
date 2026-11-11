using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlertLevelTextUI : MonoBehaviour
{
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;
    [SerializeField] private string format = "{0}";

    private AlertLevelManager subscribedManager;

    private void Awake()
    {
        if (tmpText == null)
            tmpText = GetComponent<TMP_Text>();
        if (tmpText == null)
            tmpText = GetComponentInChildren<TMP_Text>(true);

        if (legacyText == null)
            legacyText = GetComponent<Text>();
        if (legacyText == null)
            legacyText = GetComponentInChildren<Text>(true);
    }

    private void OnEnable()
    {
        TryBindManager();
    }

    private void Start()
    {
        TryBindManager();
    }

    private void Update()
    {
        if (subscribedManager == null)
            TryBindManager();
    }

    private void OnDisable()
    {
        UnbindManager();
    }

    private void OnDestroy()
    {
        UnbindManager();
    }

    private void TryBindManager()
    {
        AlertLevelManager manager = AlertLevelManager.Instance;
        if (manager == null || subscribedManager == manager)
            return;

        UnbindManager();
        subscribedManager = manager;
        subscribedManager.OnAlertChanged += UpdateAlertText;
        UpdateAlertText(subscribedManager.CurrentAlert);
    }

    private void UnbindManager()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.OnAlertChanged -= UpdateAlertText;
        subscribedManager = null;
    }

    private void UpdateAlertText(int alertValue)
    {
        string value = string.Format(format, alertValue);

        if (tmpText != null)
            tmpText.text = value;

        if (legacyText != null)
            legacyText.text = value;
    }
}
