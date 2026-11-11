using System.Collections.Generic;
using UnityEngine;

public class WaterDrainGroup : MonoBehaviour
{
    [Header("Water objects to disable")]
    [SerializeField] private List<GameObject> waterObjects = new List<GameObject>();

    [Header("Run once")]
    [SerializeField] private bool oneTimeOnly = true;

    private bool isDrained = false;

    public bool IsDrained => isDrained;

    public void DrainAllWater()
    {
        if (oneTimeOnly && isDrained)
            return;

        CollectScenePondWaterObjects();

        for (int i = 0; i < waterObjects.Count; i++)
        {
            if (waterObjects[i] != null)
                waterObjects[i].SetActive(false);
        }

        isDrained = true;
        Debug.Log("[WaterDrainGroup] All pond water objects disabled.");
    }

    private void CollectScenePondWaterObjects()
    {
        GameObject[] sceneObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject candidate = sceneObjects[i];
            if (candidate == null || candidate == gameObject)
                continue;

            if (!candidate.name.ToLowerInvariant().Contains("pond water"))
                continue;

            if (candidate.GetComponent<WaterDrainGroup>() != null)
                continue;

            if (!waterObjects.Contains(candidate))
                waterObjects.Add(candidate);
        }
    }
}
