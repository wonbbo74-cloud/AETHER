using TMPro;
using UnityEngine;

public class SubtitleUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TextMeshProUGUI text;

    public void Show(string line)
    {
        if (panel) panel.SetActive(true);
        if (text) text.text = line;
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }
}

