using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HoldInteractionUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject interactionUIRoot;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text interactionText;

    [Header("Default")]
    [SerializeField] private string defaultMessage = "해킹 시작";

    private void Awake()
    {
        HideUI();
        SetProgress(0f);
        SetText(defaultMessage);
    }

    public void ShowUI(string message = "")
    {
        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(true);

        SetText(string.IsNullOrWhiteSpace(message) ? defaultMessage : message);
    }

    public void HideUI()
    {
        if (interactionUIRoot != null)
            interactionUIRoot.SetActive(false);

        SetProgress(0f);
    }

    public void SetProgress(float normalizedValue)
    {
        if (progressFill == null)
            return;

        progressFill.fillAmount = Mathf.Clamp01(normalizedValue);
    }

    public void SetText(string message)
    {
        if (interactionText != null)
            interactionText.text = InteractionPromptTextUtility.FormatHoldPrompt(message);
    }
}
