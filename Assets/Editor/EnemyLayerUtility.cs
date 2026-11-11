using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyLayerUtility
{
    public static int EnemyLayer
    {
        get
        {
            int layer = LayerMask.NameToLayer("Enemy");
            if (layer < 0)
                Debug.LogError("[EnemyLayer] 'Enemy' layer is missing from TagManager.");
            return layer;
        }
    }

    public static void SetEnemyLayerRecursively(GameObject root)
    {
        int layer = EnemyLayer;
        if (layer < 0 || root == null)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
    }

    public static LayerMask EnemyLayerMask
    {
        get
        {
            int layer = EnemyLayer;
            return layer >= 0 ? 1 << layer : 0;
        }
    }

    [MenuItem("Tools/AETHER/Enemies/Fix Enemy Layers In Current Scene")]
    public static void FixEnemyLayersInCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        int enemyLayer = EnemyLayer;
        if (enemyLayer < 0)
            return;

        int fixedRoots = 0;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            fixedRoots += FixEnemyTaggedHierarchy(roots[i].transform, enemyLayer);

        if (fixedRoots > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[EnemyLayer] Updated {fixedRoots} Enemy object(s) to Enemy layer in '{scene.name}'.");
        }
        else
        {
            Debug.Log($"[EnemyLayer] No Enemy objects on Player layer were found in '{scene.name}'.");
        }
    }

    private static int FixEnemyTaggedHierarchy(Transform transform, int enemyLayer)
    {
        if (transform == null)
            return 0;

        if (transform.CompareTag("Enemy") && HasNonEnemyLayerInHierarchy(transform, enemyLayer))
        {
            SetEnemyLayerRecursively(transform.gameObject);
            return 1;
        }

        int fixedCount = 0;
        for (int i = 0; i < transform.childCount; i++)
            fixedCount += FixEnemyTaggedHierarchy(transform.GetChild(i), enemyLayer);

        return fixedCount;
    }

    private static bool HasNonEnemyLayerInHierarchy(Transform root, int enemyLayer)
    {
        if (enemyLayer < 0)
            return false;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].gameObject.layer != enemyLayer)
                return true;
        }

        return false;
    }
}
