using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryDetailUI : MonoBehaviour
{
    [Header("기본 정보 UI")]
    public Image itemImage;                 // 오른쪽 상단 큰 이미지
    public TextMeshProUGUI nameText;        // 아이템 이름
    public TextMeshProUGUI descriptionText; // 아이템 설명

    [Header("효과 리스트 UI")]
    public Transform effectContainer;       // 효과 라인들이 들어갈 부모(Vertical Layout Group 권장)
    public GameObject effectLinePrefab;     // "Icon + Text" 한 줄짜리 프리팹

    public void ShowItem(ItemData item)
    {
        if (item == null)
        {
            Clear();
            return;
        }

        // 기본 정보 표시
        if (itemImage != null)
        {
            itemImage.sprite = item.itemSprite;
            itemImage.enabled = (item.itemSprite != null);
        }

        if (nameText != null)
            nameText.text = item.displayName;

        if (descriptionText != null)
            descriptionText.text = item.description;

        // 기존 효과 라인 제거
        if (effectContainer != null)
        {
            foreach (Transform child in effectContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // 효과가 없으면 끝
        if (item.effects == null || item.effects.Length == 0 || effectContainer == null || effectLinePrefab == null)
            return;

        // 효과 리스트 생성
        foreach (var effect in item.effects)
        {
            GameObject line = Instantiate(effectLinePrefab, effectContainer);

            // 프리팹 구조: Icon(Image) + Text (TMP)Value(TextMeshProUGUI)
            Image iconImage = null;
            TextMeshProUGUI text = null;

            var iconTr = line.transform.Find("Icon");
            var textTr = line.transform.Find("Effect");

            if (iconTr != null)
                iconImage = iconTr.GetComponent<Image>();
            if (textTr != null)
                text = textTr.GetComponent<TextMeshProUGUI>();

            // 아이콘 처리
            if (iconImage != null)
            {
                if (effect.icon != null)
                {
                    iconImage.sprite = effect.icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            // 텍스트 생성
            if (text != null)
            {
                switch (effect.type)
                {
                    case EffectType.Health:
                        // ex) "체력 + 3"
                        text.text = effect.customText;   // 이미 "체력 + 3" 으로 넣어놨으니 그대로 사용
                        break;

                    case EffectType.Thirst:
                        // ex) "수분 - 1"
                        text.text = effect.customText;
                        break;

                    case EffectType.CustomText:
                        // ex) "식량"
                        text.text = effect.customText;
                        break;
                }
            }
        }
    }

    public void Clear()
    {
        if (itemImage != null)
        {
            itemImage.sprite = null;
            itemImage.enabled = false;
        }

        if (nameText != null)
            nameText.text = "";

        if (descriptionText != null)
            descriptionText.text = "";

        if (effectContainer != null)
        {
            foreach (Transform child in effectContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
