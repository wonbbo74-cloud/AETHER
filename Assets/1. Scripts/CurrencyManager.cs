using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    [Header("Currency")]
    public int currentGold = 0;

    [Header("UI")]
    public TextMeshProUGUI goldText;

    private void Start()
    {
        UpdateUI();
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;

        currentGold += amount;
        UpdateUI();
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0) return false;
        if (currentGold < amount) return false;

        currentGold -= amount;
        UpdateUI();
        return true;
    }

    public void UpdateUI()
    {
        if (goldText != null)
            goldText.text = currentGold.ToString();
    }
}