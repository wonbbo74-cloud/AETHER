#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class ThirdZoneSceneSetupEditor
{
    private const string TargetSceneFilePrefix = "11.3";
    private const string GeneratedRootName = "AETHER_ThirdZone_CombatSetup";
    private const string RuntimeResourceFolder = "Assets/Resources/AETHERRuntime";
    private const string RobotResourcePath = "AETHERRuntime/Robot";
    private const string DroneAttackerResourcePath = "AETHERRuntime/Drone_Attacker";
    private const string DroneObserverResourcePath = "AETHERRuntime/Drone_Observer";
    private const string DroneAiPrefabPath = "Assets/6. Prefabs/NPC/Drone_Suicider.prefab";
    private const string TurretResourcePath = "AETHERRuntime/turret_1_1";
    private const string RobotBulletResourcePath = "AETHERRuntime/RobotBullet";
    private const string EnemyBulletResourcePath = "AETHERRuntime/EnemyBullet";

    private static readonly Vector3[] RobotOffsets =
    {
        new Vector3(0f, -1.79f, 1.81f),
        new Vector3(-3.1f, -1.79f, 3.41f),
        new Vector3(3.1f, -1.79f, 3.41f),
        new Vector3(-4.5f, -1.79f, 7.21f),
        new Vector3(4.5f, -1.79f, 7.21f),
        new Vector3(0f, -1.79f, 10.01f),
        new Vector3(-5.3f, -1.79f, 13.41f),
        new Vector3(5.3f, -1.79f, 13.41f),
        new Vector3(-1.5f, -1.79f, 16.81f),
        new Vector3(1.5f, -1.79f, 16.81f),
        new Vector3(-3.3f, -1.79f, 20.41f),
        new Vector3(3.3f, -1.79f, 20.41f),
    };

    private static readonly Vector3[] DroneOffsets =
    {
        new Vector3(4f, 6f, 12f),
        new Vector3(-14f, 7f, 22f),
        new Vector3(18f, 7f, 25f),
        new Vector3(-24f, 8f, -22f),
        new Vector3(26f, 8f, -26f),
        new Vector3(0f, 9f, 46f),
    };

    private static readonly Vector3[] TurretOffsets =
    {
        new Vector3(-10f, 0f, 18f),
        new Vector3(12f, 0f, 22f),
        new Vector3(-24f, 0f, -24f),
        new Vector3(26f, 0f, -28f),
        new Vector3(3f, 0f, 52f),
    };

    [MenuItem("AETHER/Scene Setup/Setup 11 Third Zone Combat")]
    public static void SetupThirdZoneCombatMenu()
    {
        SetupThirdZoneCombat();
    }

    public static void SetupThirdZoneCombat()
    {
        string targetScenePath = FindThirdZoneScenePath();
        if (string.IsNullOrEmpty(targetScenePath))
        {
            Debug.LogError("[ThirdZoneSetup] Could not find a scene whose file name starts with 11.3.");
            return;
        }

        Scene targetScene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Single);
        Transform player = FindOrCreatePlayer(targetScene);
        if (player == null)
        {
            Debug.LogError("[ThirdZoneSetup] Could not find or create Hankyul player.");
            return;
        }

        EnsurePlayerTag(player.gameObject);
        ClearGeneratedRoot(targetScene);

        GameObject rootObject = new GameObject(GeneratedRootName);
        SceneManager.MoveGameObjectToScene(rootObject, targetScene);
        Transform root = rootObject.transform;

        GameObject robotPrefab = Resources.Load<GameObject>(RobotResourcePath);
        GameObject droneAttackerPrefab = LoadDroneAiPrefab();
        GameObject droneObserverPrefab = Resources.Load<GameObject>(DroneObserverResourcePath);
        GameObject turretPrefab = Resources.Load<GameObject>(TurretResourcePath);
        GameObject robotBulletPrefab = Resources.Load<GameObject>(RobotBulletResourcePath);
        GameObject enemyBulletPrefab = Resources.Load<GameObject>(EnemyBulletResourcePath);

        PlaceGroup(robotPrefab, RobotOffsets, root, player, "ThirdZone_Robot", robotBulletPrefab);
        PlaceDroneGroup(droneAttackerPrefab, droneObserverPrefab, DroneOffsets, root, player);
        PlaceGroup(turretPrefab, TurretOffsets, root, player, "ThirdZone_Turret", enemyBulletPrefab);

        TryBuildNavMesh();

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        Debug.Log("[ThirdZoneSetup] Scene 11 combat setup complete.");
    }

    private static string FindThirdZoneScenePath()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/0. Scenes" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith(TargetSceneFilePrefix, StringComparison.Ordinal))
                return path;
        }

        return null;
    }

    private static Transform FindOrCreatePlayer(Scene targetScene)
    {
        Move_Hankyul existingPlayer = FindComponentInScene<Move_Hankyul>(targetScene);
        if (existingPlayer != null)
            return existingPlayer.transform;

        GameObject playerPrefab = LoadHankyulPrefab();
        if (playerPrefab == null)
            return null;

        GameObject clone = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        clone.name = "Hankyul_ThirdZone";
        SceneManager.MoveGameObjectToScene(clone, targetScene);
        clone.transform.position = Vector3.up;
        clone.transform.rotation = Quaternion.identity;
        return clone.transform;
    }

    private static GameObject LoadHankyulPrefab()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { RuntimeResourceFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponentInChildren<Move_Hankyul>(true) != null)
                return prefab;
        }

        return null;
    }

    private static GameObject LoadDroneAiPrefab()
    {
        GameObject dronePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DroneAiPrefabPath);
        if (dronePrefab != null && dronePrefab.GetComponentInChildren<Drones>(true) != null)
            return dronePrefab;

        dronePrefab = Resources.Load<GameObject>(DroneAttackerResourcePath);
        if (dronePrefab != null)
            return dronePrefab;

        return Resources.Load<GameObject>(DroneObserverResourcePath);
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            T component = rootObject.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static void PlaceDroneGroup(GameObject attackerPrefab, GameObject observerPrefab, Vector3[] offsets, Transform parent, Transform player)
    {
        GameObject[] variants = attackerPrefab != null && attackerPrefab.GetComponentInChildren<Drones>(true) != null
            ? new[] { attackerPrefab }
            : new[] { observerPrefab };

        if (variants.Length == 0 || variants[0] == null)
        {
            Debug.LogWarning("[ThirdZoneSetup] No drone prefab found.");
            return;
        }

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject prefab = variants[i % variants.Length];
            PlaceOne(prefab, player.position + offsets[i], parent, player, $"ThirdZone_Drone_{i + 1:00}", null, true, i);
        }
    }

    private static void PlaceGroup(GameObject prefab, Vector3[] offsets, Transform parent, Transform player, string namePrefix, GameObject bulletPrefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[ThirdZoneSetup] Missing prefab for {namePrefix}.");
            return;
        }

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 targetPosition = player.position + offsets[i];
            bool isDrone = prefab.GetComponentInChildren<Drones>(true) != null;
            if (!isDrone)
                targetPosition = SnapToGround(targetPosition);

            PlaceOne(prefab, targetPosition, parent, player, $"{namePrefix}_{i + 1:00}", bulletPrefab, isDrone, i);
        }
    }

    private static void PlaceOne(GameObject prefab, Vector3 position, Transform parent, Transform player, string objectName, GameObject bulletPrefab, bool isDrone, int index)
    {
        GameObject clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        clone.name = objectName;
        clone.transform.SetParent(parent, true);
        clone.transform.position = position;
        if (!isDrone && objectName.StartsWith("ThirdZone_Robot", StringComparison.Ordinal))
            clone.transform.localScale = Vector3.one * 1.1f;

        Vector3 lookDirection = player.position - clone.transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.001f)
            clone.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        EnsureEnemyTagAndLayer(clone);
        EnsurePlayerReferences(clone, player);
        ConfigureProjectile(clone, bulletPrefab);
        ConfigureHealth(clone, isDrone);
        RandomizeEnemyStart(clone, isDrone, index);
    }

    private static void ConfigureHealth(GameObject enemyRoot, bool isDrone)
    {
        RobotHealth robotHealth = enemyRoot.GetComponentInChildren<RobotHealth>(true);
        if (robotHealth != null)
        {
            robotHealth.Configure(100, 2f);
            SetSerializedInt(robotHealth, "currentHP", 100);
            return;
        }

        DroneHealth droneHealth = enemyRoot.GetComponentInChildren<DroneHealth>(true);
        if (droneHealth != null)
        {
            droneHealth.Configure(isDrone ? 5 : 3);
            SetSerializedInt(droneHealth, "currentHP", isDrone ? 5 : 3);
        }
    }

    private static Vector3 SnapToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 40f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point;

        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 20f, NavMesh.AllAreas))
            return navHit.position;

        return position;
    }

    private static void EnsurePlayerReferences(GameObject enemyRoot, Transform player)
    {
        MonoBehaviour[] behaviours = enemyRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            SerializedObject serializedObject = new SerializedObject(behaviour);
            TrySetObjectReference(serializedObject, "player", player);
            TrySetObjectReference(serializedObject, "target", player);
            TrySetObjectReference(serializedObject, "targetTransform", player);
            TrySetObjectReference(serializedObject, "playerTransform", player);
            TrySetString(serializedObject, "playerTag", "Player");
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void ConfigureProjectile(GameObject enemyRoot, GameObject bulletPrefab)
    {
        if (bulletPrefab == null)
            return;

        MonoBehaviour[] behaviours = enemyRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            SerializedObject serializedObject = new SerializedObject(behaviour);
            TrySetObjectReference(serializedObject, "bulletPrefab", bulletPrefab);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        DroneAttackerCombat[] droneAttackers = enemyRoot.GetComponentsInChildren<DroneAttackerCombat>(true);
        foreach (DroneAttackerCombat droneAttacker in droneAttackers)
            droneAttacker.ConfigureLegacy(bulletPrefab, null, 18f, 10f, 0.8f, 18f, 10, 4f);

        EnemyTurretController[] turrets = enemyRoot.GetComponentsInChildren<EnemyTurretController>(true);
        foreach (EnemyTurretController turret in turrets)
            turret.Configure(bulletPrefab);
    }

    private static void TrySetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
            property.objectReferenceValue = value;
    }

    private static void TrySetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.propertyType == SerializedPropertyType.String)
            property.stringValue = value;
    }

    private static void SetSerializedInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.propertyType == SerializedPropertyType.Integer)
            property.intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RandomizeEnemyStart(GameObject enemyRoot, bool isDrone, int index)
    {
        Animator animator = enemyRoot.GetComponentInChildren<Animator>(true);
        if (animator != null)
            animator.speed = Mathf.Clamp(0.9f + (index % 5) * 0.05f, 0.9f, 1.12f);

        NavMeshAgent agent = enemyRoot.GetComponentInChildren<NavMeshAgent>(true);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.avoidancePriority = Mathf.Clamp(40 + index * 4, 0, 99);
            agent.Warp(enemyRoot.transform.position);
        }

        if (isDrone)
            enemyRoot.transform.position += Vector3.up * (index % 2 == 0 ? 0.5f : 1.2f);
    }

    private static void EnsurePlayerTag(GameObject player)
    {
        try
        {
            player.tag = "Player";
        }
        catch (UnityException)
        {
            Debug.LogWarning("[ThirdZoneSetup] Player tag does not exist in this project.");
        }
    }

    private static void EnsureEnemyTagAndLayer(GameObject enemyRoot)
    {
        try
        {
            enemyRoot.tag = "Enemy";
        }
        catch (UnityException)
        {
        }

        SetLayerRecursively(enemyRoot, LayerMask.NameToLayer("Enemy"));
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static void ClearGeneratedRoot(Scene targetScene)
    {
        foreach (GameObject rootObject in targetScene.GetRootGameObjects())
        {
            if (rootObject.name == GeneratedRootName)
                UnityEngine.Object.DestroyImmediate(rootObject);
        }
    }

    private static void TryBuildNavMesh()
    {
        try
        {
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            Debug.Log("[ThirdZoneSetup] NavMesh build requested for scene 11.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ThirdZoneSetup] NavMesh build skipped: {exception.Message}");
        }
    }
}
#endif
