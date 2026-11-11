using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TileWeight
{
    public Tile tilePrefab;
    public float weight = 1f;
}

public class TileSpawner : MonoBehaviour
{
    [Header("Grid 설정")]
    public int width = 5;
    public int height = 5;

    [Header("SpawnPoint (TileSpawn 부모)")]
    public Transform spawnRoot;

    [Header("Start / End 타일 프리팹")]
    public Tile startPrefab;
    public Tile endPrefab;

    [Header("일반 타일 (가중치 랜덤)")]
    public TileWeight[] weightedTiles;

    private Transform[] spawnPoints;
    private TileManager tileManager;

    void Awake()
    {
        tileManager = GetComponent<TileManager>(); // 같은 오브젝트에 붙어 있음

        List<Transform> list = new List<Transform>();
        foreach (Transform child in spawnRoot)
            list.Add(child);
        spawnPoints = list.ToArray();

        if (spawnPoints.Length != width * height)
        {
            Debug.LogWarning(
                $"TileSpawner: spawnPoints 개수({spawnPoints.Length})가 width*height({width * height})와 다릅니다."
            );
        }
    }

    // GameManager에서만 호출
    public void RegenerateTiles()
    {
        ClearSpawnedTiles();
        SpawnAllTiles();

        Tile[] tiles = spawnRoot.GetComponentsInChildren<Tile>();
        tileManager.tileList = tiles;
        tileManager.width = width;
        tileManager.height = height;

        tileManager.UpdateConnections();
    }

    // 예전엔 모든 칸을 가중치 랜덤으로만 채워서 시작~끝을 잇는 길이 아예 없는 판이 자주 나왔다(풀 수 없는 퍼즐).
    // 이제 먼저 시작→끝 경로를 무작위로 하나 정하고, 그 경로 칸에는 경로를 이을 수 있는 모양을 놓은 뒤
    // 회전만 뒤섞는다. 나머지 칸은 예전처럼 랜덤. 그래서 언제나 풀 수 있고, 처음부터 풀린 채로 나오지도 않는다.
    private void SpawnAllTiles()
    {
        int count = spawnPoints.Length;
        Dictionary<int, bool> pathNeedsStraight = BuildSolutionPath(count);
        Tile[] spawned = new Tile[count];

        for (int i = 0; i < count; i++)
        {
            Transform sp = spawnPoints[i];
            Tile prefab;

            if (i == 0)              prefab = startPrefab;
            else if (i == count - 1) prefab = endPrefab;
            else if (pathNeedsStraight != null && pathNeedsStraight.TryGetValue(i, out bool straight))
                prefab = GetPathPrefab(straight);
            else                     prefab = GetWeightedRandomPrefab();

            if (prefab == null)
                continue;

            Tile tile = Instantiate(prefab, sp);
            tile.transform.localPosition = Vector3.zero;
            spawned[i] = tile;

            if (tile.kind == TileKind.Normal)
            {
                int rot = Random.Range(0, 4) * 90;
                tile.rotation = rot;
                tile.ApplyRotation();
            }
        }

        // 섞은 회전이 우연히 정답이면 경로 칸 하나를 돌려 둔다(열자마자 클리어되는 일 방지).
        tileManager.tileList = spawnRoot.GetComponentsInChildren<Tile>();
        tileManager.width = width;
        tileManager.height = height;
        for (int guard = 0; guard < 8 && tileManager.IsSolved(); guard++)
        {
            foreach (var kv in pathNeedsStraight ?? new Dictionary<int, bool>())
            {
                Tile t = spawned[kv.Key];
                if (t == null) continue;
                t.rotation = (t.rotation + 90) % 360;
                t.ApplyRotation();
                break;
            }
        }
    }

    /// <summary>
    /// 시작(왼쪽 위, 오른쪽으로 나감) → 끝(오른쪽 아래, 위나 왼쪽에서 들어옴)으로 가는 무작위 경로.
    /// 경로 칸 번호 → 그 칸이 직선이어야 하는지(아니면 꺾임).
    /// </summary>
    private Dictionary<int, bool> BuildSolutionPath(int count)
    {
        if (count != width * height || width < 2 || height < 2)
            return null;

        Vector2Int start = new Vector2Int(0, 0);
        Vector2Int end = new Vector2Int(width - 1, height - 1);
        Vector2Int first = new Vector2Int(1, 0); // 시작 타일의 출구가 오른쪽

        int minLen = Mathf.Max(5, (width + height) - 2);
        int maxLen = width * height / 2 + 2;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            var path = new List<Vector2Int> { start, first };
            var used = new HashSet<Vector2Int> { start, first, end };
            if (Walk(path, used, end, minLen, maxLen))
            {
                var result = new Dictionary<int, bool>();
                for (int k = 1; k < path.Count - 1; k++)
                {
                    Vector2Int a = path[k - 1] - path[k];
                    Vector2Int b = path[k + 1] - path[k];
                    bool straight = a + b == Vector2Int.zero;
                    result[path[k].y * width + path[k].x] = straight;
                }
                return result;
            }
        }

        return null;
    }

    private bool Walk(List<Vector2Int> path, HashSet<Vector2Int> used, Vector2Int end, int minLen, int maxLen)
    {
        Vector2Int cur = path[path.Count - 1];
        if (path.Count > maxLen)
            return false;

        // 끝 칸은 위(y-1 쪽 칸)나 왼쪽 칸에서만 들어갈 수 있다(끝 타일은 회전하지 않음).
        bool nextToEnd = cur == end + Vector2Int.left || cur == end + Vector2Int.down;
        if (nextToEnd && path.Count >= minLen)
        {
            path.Add(end);
            return true;
        }

        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        for (int i = dirs.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
        }

        foreach (Vector2Int d in dirs)
        {
            Vector2Int n = cur + d;
            if (n.x < 0 || n.y < 0 || n.x >= width || n.y >= height || used.Contains(n))
                continue;

            used.Add(n);
            path.Add(n);
            if (Walk(path, used, end, minLen, maxLen))
                return true;
            path.RemoveAt(path.Count - 1);
            used.Remove(n);
        }

        return false;
    }

    /// <summary>경로 칸에 놓을 타일: 직선이면 마주 보는 두 구멍, 꺾임이면 이웃한 두 구멍을 가진 모양 중 가중치 랜덤.</summary>
    private Tile GetPathPrefab(bool straight)
    {
        float total = 0f;
        var candidates = new List<TileWeight>();
        foreach (var wt in weightedTiles)
        {
            if (wt == null || wt.tilePrefab == null || wt.weight <= 0f) continue;
            bool[] p = wt.tilePrefab.basePorts;
            if (p == null || p.Length != 4) continue;
            bool hasStraight = (p[0] && p[2]) || (p[1] && p[3]);
            bool hasCorner = (p[0] && p[1]) || (p[1] && p[2]) || (p[2] && p[3]) || (p[3] && p[0]);
            if (straight ? hasStraight : hasCorner)
            {
                candidates.Add(wt);
                total += wt.weight;
            }
        }

        if (candidates.Count == 0)
            return GetWeightedRandomPrefab();

        float rand = Random.value * total;
        foreach (var wt in candidates)
        {
            rand -= wt.weight;
            if (rand <= 0f)
                return wt.tilePrefab;
        }
        return candidates[candidates.Count - 1].tilePrefab;
    }

    private void ClearSpawnedTiles()
    {
        foreach (Transform sp in spawnPoints)
        {
            for (int i = sp.childCount - 1; i >= 0; i--)
            {
                // Destroy 는 프레임 끝에 지워서, 바로 이어지는 GetComponentsInChildren 에 옛 타일이 섞여
                // 다시 시작할 때마다 판이 꼬였다. 먼저 떼어 낸 뒤 지운다.
                Transform child = sp.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }
    }

    private Tile GetWeightedRandomPrefab()
    {
        if (weightedTiles == null || weightedTiles.Length == 0)
        {
            Debug.LogError("TileSpawner: weightedTiles가 비어 있습니다.");
            return null;
        }

        float total = 0f;
        foreach (var wt in weightedTiles)
            if (wt != null && wt.tilePrefab != null && wt.weight > 0f)
                total += wt.weight;

        if (total <= 0f)
        {
            Debug.LogError("TileSpawner: 모든 weight가 0 이하입니다.");
            return weightedTiles[0].tilePrefab;
        }

        float rand = Random.value * total;
        float cur = 0f;

        foreach (var wt in weightedTiles)
        {
            if (wt == null || wt.tilePrefab == null || wt.weight <= 0f)
                continue;

            cur += wt.weight;
            if (rand <= cur)
                return wt.tilePrefab;
        }

        return weightedTiles[weightedTiles.Length - 1].tilePrefab;
    }
}
