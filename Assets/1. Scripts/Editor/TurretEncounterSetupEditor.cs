using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class TurretEncounterSetupEditor
{
    private const string GroupName = "Enemy_Turrets_05";

    private static readonly string[] PrefabPaths =
    {
        "Assets/TD_Sci-Fi_Turret1_Example/Prefabs/Turret1/turret_1_1.prefab",
        "Assets/TD_Sci-Fi_Turret1_Example/Prefabs/Turret1/turret_1_2.prefab",
        "Assets/TD_Sci-Fi_Turret1_Example/Prefabs/Turret1/turret_1_3.prefab"
    };

    private static readonly Vector2[] PlayerRelativePlacements =
    {
        new Vector2(0f, 10f),
        new Vector2(-20f, 20f),
        new Vector2(22f, 25f),
        new Vector2(-8f, 34f)
    };

    [MenuItem("Tools/AETHER/Enemies/Place Combat Turrets Near Player")]
    public static void PlaceCombatTurretsNearPlayer()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("[TurretSetup] A Player-tagged object is required.");
            return;
        }

        GameObject previousGroup = GameObject.Find(GroupName);
        if (previousGroup != null && previousGroup.scene == scene)
            Object.DestroyImmediate(previousGroup);

        GameObject group = new GameObject(GroupName);
        SceneManager.MoveGameObjectToScene(group, scene);

        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/6. Prefabs/NPC/EnemyBullet.prefab");
        if (bulletPrefab == null)
        {
            Object.DestroyImmediate(group);
            Debug.LogError("[TurretSetup] EnemyBullet prefab is missing.");
            return;
        }

        Transform player = playerObject.transform;
        for (int i = 0; i < PlayerRelativePlacements.Length; i++)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPaths[i % PrefabPaths.Length]);
            if (source == null)
                continue;

            GameObject turret = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            turret.name = "Enemy_Turret_" + (i + 1).ToString("00");
            turret.transform.SetParent(group.transform, true);
            turret.transform.position = GroundPosition(player, PlayerRelativePlacements[i]);
            turret.transform.localScale = Vector3.one * 0.55f;
            FacePlayer(turret.transform, player.position);
            turret.tag = "Enemy";
            EnemyLayerUtility.SetEnemyLayerRecursively(turret);

            BoxCollider collider = turret.GetComponent<BoxCollider>();
            if (collider == null)
                collider = turret.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 2.15f, 0f);
            collider.size = new Vector3(3.6f, 4.4f, 3.2f);

            if (turret.GetComponent<RobotHealth>() == null)
                turret.AddComponent<RobotHealth>();
            if (turret.GetComponent<EnemyReward>() == null)
                turret.AddComponent<EnemyReward>();

            EnemyTurretController controller = turret.GetComponent<EnemyTurretController>();
            if (controller == null)
                controller = turret.AddComponent<EnemyTurretController>();
            controller.Configure(bulletPrefab);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TurretSetup] Placed 4 combat turrets around the player, including one close front turret.");
    }

    private static Vector3 GroundPosition(Transform player, Vector2 offset)
    {
        Vector3 desired = player.position + player.right * offset.x + player.forward * offset.y;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            return hit.position;

        desired.y = player.position.y;
        return desired;
    }

    private static void FacePlayer(Transform turret, Vector3 playerPosition)
    {
        Vector3 direction = playerPosition - turret.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            turret.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

}
