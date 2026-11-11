using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// 06 연구소 메인 장소: 엘리베이터로 가는 통로의 가벼운 전투와 바닥 보급품.
///
/// 연구소는 적이 하나도 없어 그냥 걸어서 지나가는 구간이었다. 통로를 따라 로봇 3기·드론 1기를 띄엄띄엄 두어
/// 한 번에 하나씩 만나게 한다. 05 보다 약하게(로봇 체력 70) 해서 충분히 처치할 수 있다.
/// 바닥에는 물·식량·에너지팩·실드팩을 하나씩 놓는다. 한 판에 한 번만 놓는다(다시 들어와도 늘어나지 않는다).
/// </summary>
public class AetherLabEncounter : MonoBehaviour
{
    private const int LabRobotHP = 70;

    // 통로(엘리베이터 쪽으로 z 가 커진다). x 는 통로 가운데 근처.
    private static readonly Vector3[] RobotSpots = { new Vector3(-12f, 0f, -42f), new Vector3(-16f, 0f, -12f), new Vector3(-9f, 0f, 16f) };
    private static readonly Vector3 DroneSpot = new Vector3(-13f, 3.2f, 2f);

    private static readonly (string path, Vector3 spot)[] Supplies =
    {
        ("AETHERParts/Water", new Vector3(-8f, 0f, -62f)),
        ("AETHERParts/Food", new Vector3(-17f, 0f, -30f)),
        ("AETHERParts/EnergyPack", new Vector3(-7f, 0f, -4f)),
        ("AETHERParts/ShieldPack", new Vector3(-15f, 0f, 8f)),
        ("AETHERParts/Water", new Vector3(-10f, 0f, 26f)),
    };

    // 이번 판에 이미 주운 보급품(번호). 죽고 다시 해도 안 주운 것은 다시 놓이고, 주운 것은 다시 생기지 않는다.
    private static readonly System.Collections.Generic.HashSet<int> pickedSupplies = new System.Collections.Generic.HashSet<int>();

    // 이번 판에 쓰러뜨린 적(번호). 파츠 제작실(07)에서 돌아오면 06 입구에 서게 되어(통로 끝이 아니라)
    // 쓰러뜨린 로봇 셋이 모두 되살아났다. 한 번 쓰러뜨린 적은 다시 놓지 않는다.
    private static readonly System.Collections.Generic.HashSet<int> killedEnemies = new System.Collections.Generic.HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 새 판(01·02)이면 보급품을 다시 놓을 수 있게 한다.
        if (scene.name.StartsWith("01.") || scene.name.StartsWith("02."))
        {
            pickedSupplies.Clear();
            killedEnemies.Clear();
        }

        if (!scene.name.StartsWith("06.") || FindFirstObjectByType<AetherLabEncounter>() != null)
            return;

        GameObject go = new GameObject("AETHER_LabEncounter");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<AetherLabEncounter>();
    }

    private IEnumerator Start()
    {
        Transform player = null;
        for (int i = 0; i < 120 && player == null; i++)
        {
            player = AetherPlayerCharacter.FindActiveTransform();
            if (player == null) yield return null;
        }

        if (player == null)
            yield break;

        // 엘리베이터에서 내려온(07→06 복귀) 경우처럼 플레이어가 이미 통로 끝에 있으면 싸움을 다시 열지 않는다.
        if (player.position.z > -20f)
            yield break;

        SpawnEnemies(player);
        PlaceSupplies(player.position.y);
    }

    private void SpawnEnemies(Transform player)
    {
        GameObject robotPrefab = Resources.Load<GameObject>("AETHERRuntime/Robot");
        GameObject dronePrefab = Resources.Load<GameObject>("AETHERRuntime/Drone_Attacker");
        GameObject robotBullet = Resources.Load<GameObject>("AETHERRuntime/RobotBullet");
        GameObject enemyBullet = Resources.Load<GameObject>("AETHERRuntime/EnemyBullet");
        float playerHeight = AetherZoneBootstrap.MeasurePlayerHeight(player);

        for (int i = 0; robotPrefab != null && i < RobotSpots.Length; i++)
        {
            if (killedEnemies.Contains(i))
                continue;
            if (!NavMesh.SamplePosition(RobotSpots[i], out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            GameObject robot = AetherZoneBootstrap.SpawnOne(robotPrefab, hit.position, transform, player, "Lab_Robot_" + (i + 1), robotBullet, false, i);
            if (robot == null) continue;
            AetherZoneBootstrap.MatchEnemyScale(robot, playerHeight, 1.05f);

            RobotHealth health = robot.GetComponentInChildren<RobotHealth>();
            if (health != null)
            {
                health.Configure(LabRobotHP, health.DeathDespawnDelay);
                int index = i;
                health.Died += _ => killedEnemies.Add(index);
            }
        }

        const int DroneIndex = 9;
        if (!killedEnemies.Contains(DroneIndex) && dronePrefab != null && NavMesh.SamplePosition(new Vector3(DroneSpot.x, 0f, DroneSpot.z), out NavMeshHit droneHit, 6f, NavMesh.AllAreas))
        {
            GameObject drone = AetherZoneBootstrap.SpawnOne(dronePrefab, droneHit.position + Vector3.up * DroneSpot.y, transform, player, "Lab_Drone_1", enemyBullet, true, DroneIndex);
            if (drone != null)
            {
                AetherZoneBootstrap.MatchEnemyScale(drone, playerHeight, 0.5f);
                DroneHealth droneHealth = drone.GetComponentInChildren<DroneHealth>();
                if (droneHealth != null) droneHealth.Died += _ => killedEnemies.Add(DroneIndex);
            }
        }
    }

    public static void MarkPicked(int index)
    {
        pickedSupplies.Add(index);
        AetherSave.MarkDirty();
    }

    public static void Export(AetherSaveData data)
    {
        data.picked06 = new System.Collections.Generic.List<int>(pickedSupplies);
        data.killed06 = new System.Collections.Generic.List<int>(killedEnemies);
    }

    public static void Import(AetherSaveData data)
    {
        pickedSupplies.Clear();
        killedEnemies.Clear();
        foreach (int i in data.picked06) pickedSupplies.Add(i);
        foreach (int i in data.killed06) killedEnemies.Add(i);
    }

    private static void PlaceSupplies(float floorY)
    {
        for (int i = 0; i < Supplies.Length; i++)
        {
            if (pickedSupplies.Contains(i))
                continue;

            (string path, Vector3 spot) = Supplies[i];
            ItemData item = Resources.Load<ItemData>(path);
            if (item == null || !NavMesh.SamplePosition(spot, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            // 길찾기 바닥이 실제 바닥보다 떠 있는 곳이 있다(물이 2m 공중에 떴다). 실제 바닥을 아래로 쏴서 찾는다.
            Vector3 ground = hit.position;
            if (Physics.Raycast(new Vector3(hit.position.x, floorY + 1.2f, hit.position.z), Vector3.down, out RaycastHit floor, 3f, ~0, QueryTriggerInteraction.Ignore))
                ground = floor.point;

            GameObject pickup = AetherItemUse.SpawnPickupAt(item, ground);
            SceneManager.MoveGameObjectToScene(pickup, SceneManager.GetActiveScene());
            pickup.AddComponent<SupplyMarker>().index = i;
        }
    }
}

/// <summary>보급품을 주우면(씬이 닫혀서 사라지는 경우가 아니라) 주운 것으로 기록한다.</summary>
public class SupplyMarker : MonoBehaviour
{
    public int index;

    private void OnDestroy()
    {
        if (gameObject.scene.isLoaded)
            AetherLabEncounter.MarkPicked(index);
    }
}
