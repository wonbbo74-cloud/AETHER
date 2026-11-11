using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NaN을 뱉는 Cloth(천 시뮬레이션)를 찾아 꺼 준다.
///
/// 05.폐허된 도시에서 매 프레임 쏟아지던
///   Invalid AABB / Assertion failed on expression: 'IsFinite(d)'
/// 오류의 원인은 Yunseo_cloth'의 바지(MPant_Cloth)에 붙은 Cloth였다.
/// 이 컴포넌트가 정점을 NaN으로 만들어 경계 상자 계산이 통째로 무너진다.
///
/// Cloth를 끄면 바지는 일반 스킨드 메시로 그려진다. 펄럭이는 연출만 사라지고
/// 겉보기에는 거의 차이가 없다.
/// </summary>
public static class AetherClothNaNFixer
{
    private const string ReportPath = "Temp/AetherClothNaN.txt";

    [MenuItem("AETHER/진단/NaN 천 찾기")]
    public static void Scan()
    {
        Run(false);
    }

    [MenuItem("AETHER/진단/NaN 천 끄기")]
    public static void Fix()
    {
        Run(true);
    }

    private static void Run(bool apply)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine($"모드: {(apply ? "끄기" : "찾기만")}");
        report.AppendLine();

        List<Cloth> broken = new List<Cloth>();
        HashSet<string> prefabsToFix = new HashSet<string>();
        int total = 0;

        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Cloth cloth in root.GetComponentsInChildren<Cloth>(true))
                {
                    total++;

                    if (!IsProducingNaN(cloth))
                        continue;

                    broken.Add(cloth);

                    string prefabPath = ResolvePrefabPath(cloth);
                    report.AppendLine($"{Path(cloth.transform)}");
                    report.AppendLine($"    원본 프리팹: {(prefabPath ?? "없음 (씬에만 있음)")}");

                    if (prefabPath != null)
                        prefabsToFix.Add(prefabPath);
                }
            }
        }

        if (apply)
        {
            // 원본 프리팹부터 고쳐야 다른 씬에서도 같은 문제가 안 생긴다.
            int prefabFixed = 0;
            foreach (string prefabPath in prefabsToFix)
                prefabFixed += DisableClothInPrefab(prefabPath);

            // 프리팹을 고쳐도 씬에 따로 덮어쓴 값이 남아 있을 수 있으니 씬 쪽도 확인한다.
            int sceneFixed = 0;
            foreach (Cloth cloth in broken)
            {
                if (cloth == null)
                    continue;

                if (cloth.enabled)
                {
                    cloth.enabled = false;
                    EditorUtility.SetDirty(cloth);
                    sceneFixed++;
                }

                SkinnedMeshRenderer renderer = cloth.GetComponent<SkinnedMeshRenderer>();
                if (renderer != null && RepairBounds(renderer))
                {
                    EditorUtility.SetDirty(renderer);
                    sceneFixed++;
                }
            }

            if (sceneFixed > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            }

            report.AppendLine();
            report.AppendLine($"프리팹에서 끈 Cloth: {prefabFixed}개");
            report.AppendLine($"씬에서 끈 Cloth: {sceneFixed}개");
        }

        report.Insert(0, $"씬 안의 Cloth: 총 {total}개, 그중 NaN을 뱉는 것: {broken.Count}개\n");
        System.IO.File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        Debug.Log($"[ClothNaN] NaN 천 {broken.Count}개. 결과: {ReportPath}");
    }

    /// <summary>이 Cloth가 붙은 렌더러의 경계가 NaN인가.</summary>
    private static bool IsProducingNaN(Cloth cloth)
    {
        SkinnedMeshRenderer renderer = cloth.GetComponent<SkinnedMeshRenderer>();
        if (renderer == null)
            return false;

        Bounds local = renderer.localBounds;
        Bounds world = renderer.bounds;

        return !IsFinite(local.center) || !IsFinite(local.size)
            || !IsFinite(world.center) || !IsFinite(world.size);
    }

    private static string ResolvePrefabPath(Cloth cloth)
    {
        Cloth source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(cloth);
        if (source == null)
            return null;

        string path = AssetDatabase.GetAssetPath(source);
        return string.IsNullOrEmpty(path) ? null : path;
    }

    /// <summary>프리팹 에셋을 열어 안에 있는 Cloth를 전부 끈다.</summary>
    private static int DisableClothInPrefab(string prefabPath)
    {
        // FBX 등 모델 파일은 직접 고칠 수 없다. 이 경우 씬 쪽에서만 끈다.
        if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[ClothNaN] {prefabPath} 은(는) 프리팹이 아니라 직접 고칠 수 없습니다. 씬에서만 껐습니다.");
            return 0;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
            return 0;

        int count = 0;
        try
        {
            foreach (Cloth cloth in root.GetComponentsInChildren<Cloth>(true))
            {
                if (cloth.enabled)
                {
                    cloth.enabled = false;
                    count++;
                }
            }

            // Cloth를 꺼도 렌더러에 저장된 경계 상자는 NaN인 채로 남는다. 같이 고쳐야 한다.
            foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (RepairBounds(renderer))
                    count++;
            }

            if (count > 0)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return count;
    }

    /// <summary>
    /// 렌더러에 저장된 경계 상자가 NaN이면 메시의 경계로 되돌린다.
    /// Cloth가 한 번 NaN을 써 넣으면 Cloth를 꺼도 그 값이 남아 오류가 계속 난다.
    /// </summary>
    private static bool RepairBounds(SkinnedMeshRenderer renderer)
    {
        Bounds local = renderer.localBounds;
        if (IsFinite(local.center) && IsFinite(local.size))
            return false;

        Mesh mesh = renderer.sharedMesh;
        if (mesh == null || !IsFinite(mesh.bounds.center) || !IsFinite(mesh.bounds.size))
        {
            // 메시 경계마저 못 믿을 때는 사람 크기 정도의 상자를 넣어 둔다.
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return true;
        }

        renderer.localBounds = mesh.bounds;
        return true;
    }

    private static bool IsFinite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
            && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        Transform parent = t.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
