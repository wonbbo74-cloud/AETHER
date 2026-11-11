using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 분홍색으로 보이는 머티리얼을 찾아내고 고친다.
///
/// 이 프로젝트는 URP·HDRP 패키지가 설치돼 있지만 GraphicsSettings에 파이프라인 에셋이
/// 지정돼 있지 않아 빌트인으로 돌아간다. 그래서 URP/HDRP 셰이더를 쓰는 머티리얼은
/// 셰이더를 찾기는 해도 실제로 그릴 수 없어 전부 분홍색이 된다.
/// 셰이더가 통째로 없어진 머티리얼도 같은 증상이 난다.
///
/// 먼저 '찾기'로 목록만 보고, 확인한 뒤 '고치기'를 실행한다.
/// </summary>
public static class AetherPinkMaterialFixer
{
    private const string ReportPath = "Temp/AetherPinkMaterials.txt";

    // 빌트인에서 못 쓰는 셰이더 이름 앞부분
    private static readonly string[] ForeignShaderPrefixes =
    {
        "Universal Render Pipeline/",
        "URP/",
        "HDRP/",
        "Shader Graphs/",
        "Lightweight Render Pipeline/",
    };

    [MenuItem("AETHER/진단/분홍 머티리얼 찾기")]
    public static void Scan()
    {
        Run(false);
    }

    [MenuItem("AETHER/진단/분홍 머티리얼 고치기")]
    public static void Fix()
    {
        Run(true);
    }

    /// <summary>
    /// 고치기 전에 원본 .mat 파일을 프로젝트 밖(Assets 바깥)에 그대로 복사해 둔다.
    /// 되돌리고 싶으면 이 폴더에서 같은 경로로 덮어쓰면 된다.
    /// </summary>
    private static string BackupRoot
    {
        get
        {
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            return System.IO.Path.Combine(projectRoot, "AetherBackup", "Materials");
        }
    }

    private static void BackUp(string assetPath)
    {
        string destination = System.IO.Path.Combine(BackupRoot, assetPath.Replace('/', System.IO.Path.DirectorySeparatorChar));

        // 이미 백업해 둔 게 있으면 덮어쓰지 않는다 (두 번 실행해도 원본이 지켜지도록).
        if (System.IO.File.Exists(destination))
            return;

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
        System.IO.File.Copy(assetPath, destination);
    }

    private static void Run(bool apply)
    {
        bool usingSrp = GraphicsSettings.currentRenderPipeline != null;
        StringBuilder report = new StringBuilder();
        report.AppendLine($"현재 렌더 파이프라인: {(usingSrp ? GraphicsSettings.currentRenderPipeline.name : "빌트인(Built-in)")}");
        report.AppendLine($"모드: {(apply ? "고치기" : "찾기만")}");
        report.AppendLine();

        if (usingSrp)
        {
            report.AppendLine("SRP가 켜져 있어 URP/HDRP 머티리얼은 정상입니다. 셰이더가 없어진 것만 봅니다.");
            report.AppendLine();
        }

        HashSet<Material> usedInOpenScenes = CollectMaterialsUsedInOpenScenes();

        string[] guids = AssetDatabase.FindAssets("t:Material");
        List<string> broken = new List<string>();
        int brokenInScene = 0;
        int fixedCount = 0;
        int inspected = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            // 패키지 안의 머티리얼은 건드리지 않는다.
            if (!path.StartsWith("Assets/", System.StringComparison.Ordinal))
                continue;

            // .shadergraph 안에도 머티리얼이 들어 있지만, 그건 셰이더 원본이라 손대면 안 된다.
            if (!path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase))
                continue;

            if (i % 50 == 0 &&
                EditorUtility.DisplayCancelableProgressBar("머티리얼 검사", path, (float)i / guids.Length))
                break;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            inspected++;

            string reason = DiagnoseReason(material, usingSrp);
            if (reason == null)
                continue;

            bool inScene = usedInOpenScenes.Contains(material);
            if (inScene)
                brokenInScene++;

            broken.Add($"{(inScene ? "[열린 씬에서 사용]" : "                 ")} {reason,-18} {path}");

            if (apply)
            {
                BackUp(path);

                if (Convert(material))
                {
                    fixedCount++;
                    EditorUtility.SetDirty(material);
                }
            }
        }

        EditorUtility.ClearProgressBar();

        if (apply && fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        report.AppendLine($"검사한 머티리얼: {inspected}개");
        report.AppendLine($"문제 있는 머티리얼: {broken.Count}개 (그중 지금 열린 씬에서 쓰는 것: {brokenInScene}개)");
        if (apply)
        {
            report.AppendLine($"고친 머티리얼: {fixedCount}개");
            report.AppendLine($"원본 백업 위치: {BackupRoot}");
        }
        report.AppendLine();
        broken.Sort();
        foreach (string line in broken)
            report.AppendLine(line);

        System.IO.File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        Debug.Log($"[PinkMaterial] 문제 {broken.Count}개" + (apply ? $", 수정 {fixedCount}개" : "") + $". 결과: {ReportPath}");
    }

    /// <summary>지금 열려 있는 씬의 렌더러들이 실제로 쓰는 머티리얼을 모은다.</summary>
    private static HashSet<Material> CollectMaterialsUsedInOpenScenes()
    {
        HashSet<Material> used = new HashSet<Material>();

        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null)
                            used.Add(material);
                    }
                }
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    if (terrain.materialTemplate != null)
                        used.Add(terrain.materialTemplate);
                }
            }
        }

        return used;
    }

    private static string DiagnoseReason(Material material, bool usingSrp)
    {
        Shader shader = material.shader;

        if (shader == null)
            return "셰이더 없음";

        if (shader.name == "Hidden/InternalErrorShader")
            return "셰이더 깨짐";

        if (usingSrp)
            return null;

        for (int i = 0; i < ForeignShaderPrefixes.Length; i++)
        {
            if (shader.name.StartsWith(ForeignShaderPrefixes[i], System.StringComparison.Ordinal))
                return "빌트인에서 못 씀";
        }

        return null;
    }

    /// <summary>색·텍스처를 살린 채 빌트인 Standard로 옮긴다.</summary>
    private static bool Convert(Material material)
    {
        Shader standard = Shader.Find("Standard");
        if (standard == null)
            return false;

        // 셰이더를 바꾸면 없는 속성은 날아가므로 먼저 값을 챙겨 둔다.
        Color baseColor = ReadColor(material, "_BaseColor", "_Color", Color.white);
        Texture baseMap = ReadTexture(material, "_BaseMap", "_MainTex");
        Vector2 tiling = ReadScale(material);
        Vector2 offset = ReadOffset(material);
        Texture bumpMap = ReadTexture(material, "_BumpMap", "_NormalMap");
        Texture emissionMap = ReadTexture(material, "_EmissionMap", "_EmissiveColorMap");
        Color emissionColor = ReadColor(material, "_EmissionColor", "_EmissiveColor", Color.black);
        float metallic = ReadFloat(material, "_Metallic", "_Metallic", 0f);
        float smoothness = ReadFloat(material, "_Smoothness", "_Glossiness", 0.5f);
        bool transparent = IsTransparent(material);

        material.shader = standard;

        material.SetColor("_Color", baseColor);

        if (baseMap != null)
        {
            material.SetTexture("_MainTex", baseMap);
            material.SetTextureScale("_MainTex", tiling);
            material.SetTextureOffset("_MainTex", offset);
        }

        if (bumpMap != null)
        {
            material.SetTexture("_BumpMap", bumpMap);
            material.EnableKeyword("_NORMALMAP");
        }

        if (emissionMap != null || emissionColor.maxColorComponent > 0.001f)
        {
            if (emissionMap != null)
                material.SetTexture("_EmissionMap", emissionMap);

            material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);

        if (transparent)
            MakeTransparent(material);

        return true;
    }

    private static void MakeTransparent(Material material)
    {
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static bool IsTransparent(Material material)
    {
        if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f)
            return true;

        if (material.HasProperty("_Mode") && material.GetFloat("_Mode") >= 2f)
            return true;

        if (material.renderQueue >= (int)RenderQueue.Transparent)
            return true;

        Color color = ReadColor(material, "_BaseColor", "_Color", Color.white);
        return color.a < 0.99f;
    }

    private static Color ReadColor(Material material, string first, string second, Color fallback)
    {
        if (material.HasProperty(first))
            return material.GetColor(first);

        if (material.HasProperty(second))
            return material.GetColor(second);

        return fallback;
    }

    private static Texture ReadTexture(Material material, string first, string second)
    {
        if (material.HasProperty(first))
        {
            Texture texture = material.GetTexture(first);
            if (texture != null)
                return texture;
        }

        if (material.HasProperty(second))
            return material.GetTexture(second);

        return null;
    }

    private static float ReadFloat(Material material, string first, string second, float fallback)
    {
        if (material.HasProperty(first))
            return material.GetFloat(first);

        if (material.HasProperty(second))
            return material.GetFloat(second);

        return fallback;
    }

    private static Vector2 ReadScale(Material material)
    {
        if (material.HasProperty("_BaseMap"))
            return material.GetTextureScale("_BaseMap");

        if (material.HasProperty("_MainTex"))
            return material.GetTextureScale("_MainTex");

        return Vector2.one;
    }

    private static Vector2 ReadOffset(Material material)
    {
        if (material.HasProperty("_BaseMap"))
            return material.GetTextureOffset("_BaseMap");

        if (material.HasProperty("_MainTex"))
            return material.GetTextureOffset("_MainTex");

        return Vector2.zero;
    }
}
