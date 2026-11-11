using UnityEngine;

public class EnemyReward : MonoBehaviour
{
    [Header("Reward Range")]
    public int minGold = 5;
    public int maxGold = 10;

    private bool rewardGiven = false;

    public void GiveReward()
    {
        if (rewardGiven) return;
        rewardGiven = true;

        int reward = Random.Range(minGold, maxGold + 1);

        if (GlobalManager.Currency != null)
        {
            GlobalManager.Currency.AddGold(reward);
        }
    }
}