using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class EnemyVarietySetupEditor
{
    private const string CombatGroupName = "05_Forward_Combat_Enemies";
    private const string VarietyGroupName = "Enemy_Variety_Units";
    private const string LegacyVarietyGroupName = "Enemy_Variety_Drones";

    [MenuItem("Tools/AETHER/Enemies/Build Current Scene Enemy Variety")]
    public static void BuildCurrentSceneEnemyVariety()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject combatGroup = GameObject.Find(CombatGroupName);
        if (combatGroup == null)
        {
            Debug.LogError($"[EnemyVariety] '{CombatGroupName}' was not found in {scene.name}.");
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("[EnemyVariety] A Player-tagged object is required for encounter placement.");
            return;
        }
        Transform player = playerObject.transform;

        Transform previousGroup = combatGroup.transform.Find(VarietyGroupName);
        if (previousGroup != null)
            Object.DestroyImmediate(previousGroup.gameObject);

        Transform legacyGroup = combatGroup.transform.Find(LegacyVarietyGroupName);
        if (legacyGroup != null)
            Object.DestroyImmediate(legacyGroup.gameObject);

        List<Robots> activeBaseRobots = ReduceAndProfileBaseRobots(scene, 10);
        PositionBaseRobotsNearPlayer(activeBaseRobots, player);
        ReduceExistingSuiciders(scene, player);

        GameObject varietyGroup = new GameObject(VarietyGroupName);
        varietyGroup.transform.SetParent(combatGroup.transform, false);

        CreateAttacker(scene, varietyGroup.transform, "Drone_Attacker_Variety_01",
            GetAirPlacement(player, -13f, 20f, 5f), player.eulerAngles.y + 180f);
        CreateAssaultRobot(scene, varietyGroup.transform, combatGroup.transform,
            "Robot_Soldier_Red_Assault_01", GetGroundPlacement(player, -5f, 11f), player.eulerAngles.y + 180f);
        CreateHeavyRobot(scene, varietyGroup.transform, combatGroup.transform,
            "Robot_Soldier_Camo_Heavy_01", GetGroundPlacement(player, 7f, 15f), player.eulerAngles.y + 180f);
        CreateObserver(scene, varietyGroup.transform, "Drone_Observer_Variety_01",
            GetAirPlacement(player, 0f, 24f, 6.2f), player.eulerAngles.y + 180f);
        CreateAttacker(scene, varietyGroup.transform, "Drone_Attacker_Variety_02",
            GetAirPlacement(player, 14f, 18f, 5.3f), player.eulerAngles.y + 180f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[EnemyVariety] Reduced the scene to {activeBaseRobots.Count + 6} active enemies with four ground movement profiles and five visual/combat types.");
    }

    private static void PositionBaseRobotsNearPlayer(List<Robots> robots, Transform player)
    {
        Vector2[] formation =
        {
            new Vector2(-18f, 18f),
            new Vector2(-10f, 25f),
            new Vector2(0f, 28f),
            new Vector2(11f, 24f),
            new Vector2(19f, 16f),
            new Vector2(-22f, 5f),
            new Vector2(22f, 4f),
            new Vector2(-15f, -8f),
            new Vector2(15f, -10f),
            new Vector2(0f, 17f)
        };

        for (int i = 0; i < robots.Count; i++)
        {
            Vector2 offset = formation[i % formation.Length];
            Robots robot = robots[i];
            robot.transform.position = GetGroundPlacement(player, offset.x, offset.y);
            FacePlayer(robot.transform, player.position);
        }
    }

    private static Vector3 GetGroundPlacement(Transform player, float rightOffset, float forwardOffset)
    {
        Vector3 desired = player.position + player.right * rightOffset + player.forward * forwardOffset;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            return hit.position;

        desired.y = player.position.y;
        return desired;
    }

    private static Vector3 GetAirPlacement(Transform player, float rightOffset, float forwardOffset, float height)
    {
        Vector3 position = player.position + player.right * rightOffset + player.forward * forwardOffset;
        position.y = player.position.y + height;
        return position;
    }

    private static void FacePlayer(Transform enemy, Vector3 playerPosition)
    {
        Vector3 direction = playerPosition - enemy.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            enemy.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static List<Robots> ReduceAndProfileBaseRobots(Scene scene, int keepCount)
    {
        Robots[] allRobots = Resources.FindObjectsOfTypeAll<Robots>();
        List<Robots> candidates = new List<Robots>();
        for (int i = 0; i < allRobots.Length; i++)
        {
            Robots robot = allRobots[i];
            if (robot == null || EditorUtility.IsPersistent(robot) || robot.gameObject.scene != scene)
                continue;

            robot.gameObject.SetActive(true);
            if (robot.name == "Robot (6)_Forward_03" || robot.name == "Robot (6)_Forward_12")
                continue;

            candidates.Add(robot);
        }

        List<Robots> selected = SelectSpreadRobots(candidates, Mathf.Min(keepCount, candidates.Count));
        HashSet<Robots> selectedSet = new HashSet<Robots>(selected);
        for (int i = 0; i < allRobots.Length; i++)
        {
            Robots robot = allRobots[i];
            if (robot == null || EditorUtility.IsPersistent(robot) || robot.gameObject.scene != scene)
                continue;

            bool keep = selectedSet.Contains(robot);
            robot.gameObject.SetActive(keep);
            if (keep)
                ApplyMovementProfile(robot, selected.IndexOf(robot));
        }

        return selected;
    }

    private static List<Robots> SelectSpreadRobots(List<Robots> candidates, int keepCount)
    {
        List<Robots> selected = new List<Robots>();
        if (keepCount <= 0 || candidates.Count == 0)
            return selected;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPosition = player != null ? player.transform.position : Vector3.zero;
        Robots first = candidates[0];
        float closestDistance = (first.transform.position - playerPosition).sqrMagnitude;
        for (int i = 1; i < candidates.Count; i++)
        {
            float distance = (candidates[i].transform.position - playerPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                first = candidates[i];
                closestDistance = distance;
            }
        }
        selected.Add(first);

        while (selected.Count < keepCount)
        {
            Robots best = null;
            float bestSeparation = -1f;
            for (int i = 0; i < candidates.Count; i++)
            {
                Robots candidate = candidates[i];
                if (selected.Contains(candidate))
                    continue;

                float nearestSelected = float.MaxValue;
                for (int j = 0; j < selected.Count; j++)
                {
                    Vector3 offset = candidate.transform.position - selected[j].transform.position;
                    offset.y = 0f;
                    nearestSelected = Mathf.Min(nearestSelected, offset.sqrMagnitude);
                }

                if (nearestSelected > bestSeparation)
                {
                    best = candidate;
                    bestSeparation = nearestSelected;
                }
            }

            if (best == null)
                break;
            selected.Add(best);
        }

        return selected;
    }

    private static void ApplyMovementProfile(Robots robot, int index)
    {
        int profile = index % 4;
        if (profile == 0) // Scout: long, quick patrol runs.
        {
            SetFloat(robot, "walkSpeed", 2.35f);
            SetFloat(robot, "runSpeed", 5.1f);
            SetFloat(robot, "walkDuration", 5.2f);
            SetFloat(robot, "stopDuration", 0.45f);
            SetFloat(robot, "maxPatrolDistance", 10f);
            SetInt(robot, "burstShotCount", 2);
            SetFloat(robot, "burstCooldown", 1.4f);
            SetFloat(robot, "fireInterval", 0.55f);
        }
        else if (profile == 1) // Sentry: mostly watches, then fires a long burst.
        {
            SetFloat(robot, "walkSpeed", 0.95f);
            SetFloat(robot, "runSpeed", 3.1f);
            SetFloat(robot, "walkDuration", 1.35f);
            SetFloat(robot, "stopDuration", 3.4f);
            SetFloat(robot, "maxPatrolDistance", 4.5f);
            SetInt(robot, "burstShotCount", 5);
            SetFloat(robot, "burstCooldown", 3f);
            SetFloat(robot, "fireInterval", 0.85f);
        }
        else if (profile == 2) // Hunter: fast pursuit with medium pauses.
        {
            SetFloat(robot, "walkSpeed", 1.85f);
            SetFloat(robot, "runSpeed", 4.75f);
            SetFloat(robot, "walkDuration", 3.8f);
            SetFloat(robot, "stopDuration", 0.8f);
            SetFloat(robot, "maxPatrolDistance", 8f);
            SetInt(robot, "burstShotCount", 3);
            SetFloat(robot, "burstCooldown", 1.9f);
            SetFloat(robot, "fireInterval", 0.48f);
        }
        else // Cautious: short, irregular movements.
        {
            SetFloat(robot, "walkSpeed", 1.35f);
            SetFloat(robot, "runSpeed", 3.65f);
            SetFloat(robot, "walkDuration", 2.15f);
            SetFloat(robot, "stopDuration", 1.7f);
            SetFloat(robot, "maxPatrolDistance", 6f);
            SetInt(robot, "burstShotCount", 1);
            SetFloat(robot, "burstCooldown", 1.1f);
            SetFloat(robot, "fireInterval", 1.05f);
            SetBool(robot, "avoidBackwardTooMuch", false);
        }

        SetVector2(robot, "initialPatrolDelayRange", new Vector2(index * 0.18f, index * 0.18f + 1.2f));
        SetVector2(robot, "moveSpeedMultiplierRange", new Vector2(0.9f, 1.1f));
        SetVector2(robot, "behaviorTimeMultiplierRange", new Vector2(0.85f, 1.2f));
        SetVector2(robot, "animationSpeedRange", new Vector2(0.88f, 1.12f));
        SetFloat(robot, "sightRange", 38f);
        SetFloat(robot, "viewAngle", 180f);
        SetFloat(robot, "wideChaseRange", 45f);
        SetFloat(robot, "giveUpRange", 60f);
        SetInt(robot, "obstacleMask", 1);
    }

    private static void ReduceExistingSuiciders(Scene scene, Transform player)
    {
        Drones[] drones = Resources.FindObjectsOfTypeAll<Drones>();
        bool keptOne = false;
        for (int i = 0; i < drones.Length; i++)
        {
            Drones drone = drones[i];
            if (drone == null || EditorUtility.IsPersistent(drone) || drone.gameObject.scene != scene)
                continue;
            bool keep = !keptOne && drone.name == "Drone_Suicider_Forward_02";
            drone.gameObject.SetActive(keep);
            if (keep)
            {
                drone.transform.position = GetAirPlacement(player, -18f, 16f, 4.8f);
                FacePlayer(drone.transform, player.position);
                SetFloat(drone, "visionRange", 38f);
                SetFloat(drone, "patrolRadius", 18f);
            }
            keptOne |= keep;
        }
    }

    private static void CreateAssaultRobot(
        Scene scene,
        Transform parent,
        Transform combatGroup,
        string name,
        Vector3 position,
        float yaw)
    {
        GameObject robot = InstantiateVisual(
            scene,
            parent,
            "Assets/Robot_Soldier/Prefabs/Robot_Soldier_Red.prefab",
            name,
            position,
            yaw,
            1.08f,
            false);

        Animator animator = robot.GetComponentInChildren<Animator>();
        Transform referenceRobot = combatGroup.Find("Robot (6)_Forward_01");
        Animator referenceAnimator = referenceRobot != null
            ? referenceRobot.GetComponentInChildren<Animator>()
            : null;
        if (animator != null && referenceAnimator != null)
            animator.runtimeAnimatorController = referenceAnimator.runtimeAnimatorController;

        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(robot);
        agent.radius = 0.48f;
        agent.height = 2.05f;
        agent.baseOffset = 0f;

        CapsuleCollider collider = GetOrAdd<CapsuleCollider>(robot);
        collider.center = new Vector3(0f, 1f, 0f);
        collider.radius = 0.48f;
        collider.height = 2.05f;

        GetOrAdd<RobotCombat>(robot);
        GetOrAdd<RobotHealth>(robot);
        GetOrAdd<EnemyReward>(robot);
        GetOrAdd<EMPStunReceiver>(robot);

        Transform firePoint = CreateChild(robot.transform, "FirePoint", new Vector3(0f, 1.35f, 0.65f));
        Robots movement = GetOrAdd<Robots>(robot);
        SetObject(movement, "animator", animator);
        SetObject(movement, "agent", agent);
        SetObject(movement, "bulletPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/6. Prefabs/NPC/EnemyBullet.prefab"));
        SetObject(movement, "firePoint", firePoint);
        SetFloat(movement, "walkSpeed", 2.05f);
        SetFloat(movement, "runSpeed", 4.4f);
        SetFloat(movement, "sightRange", 38f);
        SetFloat(movement, "viewAngle", 180f);
        SetFloat(movement, "fireRange", 14f);
        SetFloat(movement, "wideChaseRange", 45f);
        SetFloat(movement, "giveUpRange", 60f);
        SetFloat(movement, "fireInterval", 0.42f);
        SetInt(movement, "burstShotCount", 4);
        SetFloat(movement, "burstCooldown", 1.7f);
        SetBool(movement, "fireByAnimationEvent", false);
        SetFloat(movement, "rotateSpeed", 620f);
        SetInt(movement, "maxHP", 125);
        SetInt(movement, "obstacleMask", 1);
    }

    private static void CreateHeavyRobot(
        Scene scene,
        Transform parent,
        Transform combatGroup,
        string name,
        Vector3 position,
        float yaw)
    {
        GameObject robot = InstantiateVisual(
            scene,
            parent,
            "Assets/Robot_Soldier/Prefabs/Robot_Soldier_Camo1.prefab",
            name,
            position,
            yaw,
            1.18f,
            false);

        Animator animator = robot.GetComponentInChildren<Animator>();
        Transform referenceRobot = combatGroup.Find("Robot (6)_Forward_01");
        Animator referenceAnimator = referenceRobot != null
            ? referenceRobot.GetComponentInChildren<Animator>()
            : null;
        if (animator != null && referenceAnimator != null)
            animator.runtimeAnimatorController = referenceAnimator.runtimeAnimatorController;

        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(robot);
        agent.radius = 0.55f;
        agent.height = 2.15f;
        agent.baseOffset = 0f;

        CapsuleCollider collider = GetOrAdd<CapsuleCollider>(robot);
        collider.center = new Vector3(0f, 1.05f, 0f);
        collider.radius = 0.55f;
        collider.height = 2.15f;

        GetOrAdd<RobotCombat>(robot);
        GetOrAdd<RobotHealth>(robot);
        GetOrAdd<EnemyReward>(robot);
        GetOrAdd<EMPStunReceiver>(robot);

        Transform firePoint = CreateChild(robot.transform, "FirePoint", new Vector3(0f, 1.45f, 0.7f));
        Robots movement = GetOrAdd<Robots>(robot);
        SetObject(movement, "animator", animator);
        SetObject(movement, "agent", agent);
        SetObject(movement, "bulletPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/6. Prefabs/NPC/EnemyBullet.prefab"));
        SetObject(movement, "firePoint", firePoint);
        SetFloat(movement, "walkSpeed", 1.15f);
        SetFloat(movement, "runSpeed", 3.2f);
        SetFloat(movement, "walkDuration", 2.6f);
        SetFloat(movement, "stopDuration", 2.2f);
        SetFloat(movement, "sightRange", 38f);
        SetFloat(movement, "viewAngle", 180f);
        SetFloat(movement, "fireRange", 16f);
        SetFloat(movement, "wideChaseRange", 45f);
        SetFloat(movement, "giveUpRange", 60f);
        SetFloat(movement, "fireInterval", 0.68f);
        SetInt(movement, "burstShotCount", 2);
        SetFloat(movement, "burstCooldown", 2.6f);
        SetInt(movement, "bulletDamage", 18);
        SetBool(movement, "fireByAnimationEvent", false);
        SetFloat(movement, "rotateSpeed", 420f);
        SetInt(movement, "maxHP", 190);
        SetInt(movement, "obstacleMask", 1);
        SetVector2(movement, "moveSpeedMultiplierRange", new Vector2(0.92f, 1.06f));
        SetVector2(movement, "behaviorTimeMultiplierRange", new Vector2(0.9f, 1.15f));
    }

    private static void CreateAttacker(Scene scene, Transform parent, string name, Vector3 position, float yaw)
    {
        GameObject drone = InstantiateVisual(
            scene,
            parent,
            "Assets/6. Prefabs/NPC/Drone_Attacker.prefab",
            name,
            position,
            yaw,
            1.12f);

        DroneHealth health = GetOrAdd<DroneHealth>(drone);
        SetInt(health, "customMaxHP", 6);

        DroneAttackerCombat combat = GetOrAdd<DroneAttackerCombat>(drone);
        Transform firePoint = CreateChild(drone.transform, "FirePoint", new Vector3(0f, -0.05f, 1.35f));
        SetObject(combat, "bulletPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/6. Prefabs/NPC/EnemyBullet.prefab"));
        SetObject(combat, "firePoint", firePoint);
        SetFloat(combat, "fireRange", 22f);
        SetFloat(combat, "preferredDistance", 13f);
        SetFloat(combat, "fireInterval", 0.9f);
        SetFloat(combat, "bulletSpeed", 20f);
        SetInt(combat, "bulletDamage", 9);

        ConfigureMovement(drone, 6.8f, 7.5f, 20f, 14f, 0.9f, 38f);
    }

    private static void CreateObserver(Scene scene, Transform parent, string name, Vector3 position, float yaw)
    {
        GameObject drone = InstantiateVisual(
            scene,
            parent,
            "Assets/6. Prefabs/NPC/Drone_Observer.prefab",
            name,
            position,
            yaw,
            0.94f);

        DroneHealth health = GetOrAdd<DroneHealth>(drone);
        SetInt(health, "customMaxHP", 4);

        DroneObserverCombat combat = GetOrAdd<DroneObserverCombat>(drone);
        Transform detectionSpot = CreateChild(drone.transform, "DetectionSpot", new Vector3(0f, 0f, 0.8f));
        Light spotLight = detectionSpot.gameObject.AddComponent<Light>();
        spotLight.type = LightType.Spot;
        spotLight.range = 24f;
        spotLight.spotAngle = 62f;
        spotLight.intensity = 1.35f;
        spotLight.color = new Color(1f, 0.72f, 0.22f);
        spotLight.shadows = LightShadows.None;

        SetObject(combat, "detectOrigin", detectionSpot);
        SetObject(combat, "detectionSpotLight", spotLight);
        SetInt(combat, "enemyMask", EnemyLayerUtility.EnemyLayerMask.value);
        SetFloat(combat, "alertCallRadius", 32f);
        SetFloat(combat, "alertRepeatInterval", 1.8f);
        SetBool(combat, "drawDetectionGizmos", false);

        ConfigureMovement(drone, 5.2f, 5.8f, 15f, 10f, 1.8f, 38f);
    }

    private static GameObject InstantiateVisual(
        Scene scene,
        Transform parent,
        string prefabPath,
        string name,
        Vector3 position,
        float yaw,
        float scale,
        bool addDroneCollider = true)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (source == null)
            throw new MissingReferenceException($"Enemy visual prefab is missing: {prefabPath}");

        GameObject drone = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        drone.name = name;
        drone.transform.SetParent(parent, true);
        drone.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        drone.transform.localScale = Vector3.one * scale;
        drone.tag = "Enemy";

        EnemyLayerUtility.SetEnemyLayerRecursively(drone);

        if (addDroneCollider)
        {
            CapsuleCollider collider = GetOrAdd<CapsuleCollider>(drone);
            collider.isTrigger = false;
            collider.direction = 1;
            collider.center = new Vector3(0f, 0.15f, 0f);
            collider.radius = scale > 1f ? 1.15f : 0.9f;
            collider.height = scale > 1f ? 2.4f : 2f;
        }

        GetOrAdd<EnemyReward>(drone);
        return drone;
    }

    private static void ConfigureMovement(
        GameObject drone,
        float flySpeed,
        float rotateSpeed,
        float patrolRadius,
        float maxPatrolDistance,
        float patrolPauseTime,
        float visionRange)
    {
        Drones movement = GetOrAdd<Drones>(drone);
        SetFloat(movement, "flySpeed", flySpeed);
        SetFloat(movement, "rotateSpeed", rotateSpeed);
        SetFloat(movement, "patrolRadius", patrolRadius);
        SetFloat(movement, "minPatrolDistance", 5f);
        SetFloat(movement, "maxPatrolDistance", maxPatrolDistance);
        SetFloat(movement, "patrolPauseTime", patrolPauseTime);
        SetFloat(movement, "visionRange", visionRange);
        SetInt(movement, "obstacleMask", 1);
    }

    private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.layer = parent.gameObject.layer;
        return child.transform;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string propertyName, int value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).intValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).boolValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector2(Object target, string propertyName, Vector2 value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).vector2Value = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
