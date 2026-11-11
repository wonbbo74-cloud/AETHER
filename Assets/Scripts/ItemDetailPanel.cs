using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPanel : MonoBehaviour
{
    [Header("Preview")]
    public RawImage previewImage;

    [Header("Stats Text (TMP)")]
    public TMP_Text attackText;
    public TMP_Text defenseText;
    public TMP_Text speedText;

    [Header("Detail Text (TMP)")]
    public TMP_Text detailText;

    private void Awake()
    {
        Hide();
    }

    public void Show(CraftItemDataSO item)
    {
        if (item == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);

        // ÇÁ¸®ºä
        if (previewImage != null)
        {
            previewImage.texture = item.GetPreviewTexture();
            previewImage.enabled = previewImage.texture != null;
        }

        // ½ºÅÈ
        if (attackText != null)
            attackText.text = item.attack.ToString();

        if (defenseText != null)
            defenseText.text = item.defense.ToString();

        if (speedText != null)
            speedText.text = item.speed.ToString();

        // µðÅ×ÀÏ
        if (detailText != null)
            detailText.text = item.description;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
