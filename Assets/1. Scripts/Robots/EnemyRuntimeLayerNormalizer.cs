using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyRuntimeLayerNormalizer
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        NormalizeActiveScene();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NormalizeScene(scene);
    }

    private static void NormalizeActiveScene()
    {
        NormalizeScene(SceneManager.GetActiveScene());
    }

    private static void NormalizeScene(Scene scene)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0 || !scene.IsValid())
            return;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null || enemy.scene != scene)
                continue;

            SetLayerRecursively(enemy.transform, enemyLayer);
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
