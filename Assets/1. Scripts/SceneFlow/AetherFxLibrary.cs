using UnityEngine;

/// <summary>
/// 실행 중에 쓰는 효과 묶음. 원본 효과 프리팹(Unity Particle Pack 등)을 복사하지 않고 가리키기만 한다.
/// Resources/AETHERFX/AetherFxLibrary.asset 하나를 둔다.
/// </summary>
[CreateAssetMenu(menuName = "AETHER/FX Library", fileName = "AetherFxLibrary")]
public class AetherFxLibrary : ScriptableObject
{
    [Header("05 폭격")]
    public GameObject bigExplosion;
    public GameObject dustExplosion;
    [Tooltip("폭발 뒤에 남는 짙은 흰 연기(연막탄처럼)")]
    public GameObject smokeColumn;
    [Tooltip("계속 타오르는 잔해 불길 + 연기")]
    public GameObject burningWreck;

    [Header("바닥 연기(06)")]
    public Texture2D smokeTexture;

    private static AetherFxLibrary cached;

    public static AetherFxLibrary Load()
    {
        if (cached == null)
            cached = Resources.Load<AetherFxLibrary>("AETHERFX/AetherFxLibrary");
        return cached;
    }
}
