using UnityEngine;
using UnityEngine.SceneManagement;

public static class CompassPartSceneBootstrap
{
    private const string TargetSceneName = "09.2구역_2";
    private const string CompassPartResourcePath = "AETHERParts/Part_compass_part";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryGrantCompassForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryGrantCompassForActiveScene();
    }

    private static void TryGrantCompassForActiveScene()
    {
        if (SceneManager.GetActiveScene().name != TargetSceneName)
            return;

        PartData compassPart = Resources.Load<PartData>(CompassPartResourcePath);
        if (compassPart == null)
        {
            Debug.LogWarning("[CompassPartSceneBootstrap] Compass part data was not found.");
            return;
        }

        PartSystemRuntimeState.RegisterCraftedEquipmentPart(compassPart);

        GameObject ui = PartsSlot2GlobalBootstrap.EnsureScenePlayerUI();
        if (ui == null)
            return;

        EquipmentUIController[] controllers = ui.GetComponentsInChildren<EquipmentUIController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null)
                controllers[i].FocusCraftedPart(compassPart);
        }
    }
}
