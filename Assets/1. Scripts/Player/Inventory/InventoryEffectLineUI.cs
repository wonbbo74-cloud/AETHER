using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryEffectLineUI : MonoBehaviour
{
    [Header("UI")]
    public Image iconImage;               // Stat/Icon
    public TextMeshProUGUI valueText;     // Stat/Text (TMP)Value

    /// <summary>
    /// ItemEffect 정보를 받아서 한 줄 UI를 갱신한다.
    /// </summary>
    public void Setup(ItemEffect effect)
    {
        if (effect == null) return;

        // 아이콘
        if (iconImage != null)
        {
            iconImage.sprite = effect.icon;
            iconImage.enabled = (effect.icon != null);
        }

        // 텍스트
        if (valueText != null)
        {
            switch (effect.type)
            {
                case EffectType.CustomText:
                    // ex) "식량"
                    valueText.text = effect.customText;
                    break;

                case EffectType.Health:
                    // ex) "체력 + 3"
                    valueText.text = $"{effect.customText}";
                    break;

                case EffectType.Thirst:
                    // ex) "수분 - 1"
                    valueText.text = $"{effect.customText}";
                    break;
            }
        }
    }
}
