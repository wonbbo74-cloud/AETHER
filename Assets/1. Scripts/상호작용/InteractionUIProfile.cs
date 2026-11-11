using UnityEngine;

public enum InteractionUIType
{
    ItemArrow,
    PuzzleCircle
}

public class InteractionUIProfile : MonoBehaviour
{
    [Header("UI Type")]
    public InteractionUIType uiType = InteractionUIType.ItemArrow;

    [Header("Display Text")]
    [Tooltip("鍮꾩썙?먮㈃ IInteractable.GetDisplayName()???ъ슜")]
    public string titleOverride = "";

    [Tooltip("鍮꾩썙?먮㈃ 湲곕낯 ?덈궡 臾멸뎄瑜??ъ슜")]
    public string actionTextOverride = "";

    [Header("????곹샇?묒슜")]
    [Tooltip("?쇱쫹/肄섏넄泥섎읆 袁??뚮윭???대━寃??좎? ?щ?")]
    public bool useHoldInteraction = false;

    [Tooltip("????쒓컙")]
    public float holdDuration = 1.2f;

    public string GetTitle(string fallback)
    {
        return string.IsNullOrWhiteSpace(titleOverride) ? fallback : titleOverride;
    }

    public string GetActionText(string fallback)
    {
        return string.IsNullOrWhiteSpace(actionTextOverride) ? fallback : actionTextOverride;
    }
}
