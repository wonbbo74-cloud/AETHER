using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 런타임으로 만드는 오브젝트에 쓸 셰이더를 고른다.
///
/// 이 프로젝트에는 URP와 HDRP 패키지가 둘 다 설치돼 있지만
/// GraphicsSettings에 렌더 파이프라인 에셋이 지정돼 있지 않아 <b>빌트인</b>으로 돌아간다.
/// 그래서 Shader.Find("Universal Render Pipeline/Lit")는 셰이더를 '찾기는' 하지만
/// 실제로는 그릴 수 없어 전부 분홍색으로 나온다.
///
/// 지금 켜져 있는 파이프라인을 보고 맞는 셰이더를 고르면 그 문제가 사라진다.
/// </summary>
public static class AetherShaders
{
    private static Shader litCache;
    private static Shader unlitCache;

    /// <summary>스크립터블 렌더 파이프라인(URP/HDRP)이 실제로 켜져 있는가.</summary>
    public static bool UsingScriptableRenderPipeline =>
        GraphicsSettings.currentRenderPipeline != null;

    /// <summary>빛을 받는 표면용 셰이더.</summary>
    public static Shader Lit
    {
        get
        {
            if (litCache != null)
                return litCache;

            if (UsingScriptableRenderPipeline)
            {
                litCache = Shader.Find("Universal Render Pipeline/Lit");
                if (litCache == null)
                    litCache = Shader.Find("HDRP/Lit");
            }

            if (litCache == null)
                litCache = Shader.Find("Standard");

            if (litCache == null)
                litCache = Shader.Find("Legacy Shaders/Diffuse");

            return litCache;
        }
    }

    /// <summary>빛을 받지 않는 표시용 셰이더(아이콘·빛기둥 등).</summary>
    public static Shader Unlit
    {
        get
        {
            if (unlitCache != null)
                return unlitCache;

            if (UsingScriptableRenderPipeline)
                unlitCache = Shader.Find("Universal Render Pipeline/Unlit");

            if (unlitCache == null)
                unlitCache = Shader.Find("Unlit/Transparent");

            if (unlitCache == null)
                unlitCache = Shader.Find("Sprites/Default");

            if (unlitCache == null)
                unlitCache = Shader.Find("Standard");

            return unlitCache;
        }
    }

    /// <summary>단색 + 발광 재질. 파이프라인에 관계없이 안전하게 만든다.</summary>
    public static Material CreateSolid(Color color, float emissionBoost = 1.6f)
    {
        Material material = new Material(Lit);
        material.color = color;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (emissionBoost > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emissionBoost);
        }

        return material;
    }

    /// <summary>반투명 발광 재질(빛기둥·발판 표시용).</summary>
    public static Material CreateGlow(Color color, float alpha = 0.35f)
    {
        Material material = new Material(Unlit);
        Color tinted = new Color(color.r, color.g, color.b, alpha);

        material.color = tinted;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tinted);

        SetupTransparency(material);
        return material;
    }

    /// <summary>스프라이트(아이템 아이콘)를 세워 두는 판에 쓸 재질.</summary>
    public static Material CreateIcon(Texture texture, Color fallbackTint)
    {
        Material material = new Material(Unlit);

        if (texture != null)
        {
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);

            material.color = Color.white;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
        }
        else
        {
            material.color = fallbackTint;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", fallbackTint);
        }

        SetupTransparency(material);
        return material;
    }

    private static void SetupTransparency(Material material)
    {
        // URP 계열
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        // 빌트인 Standard 계열
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f); // Transparent

        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
