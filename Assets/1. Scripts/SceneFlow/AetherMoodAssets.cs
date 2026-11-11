using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// 씬 분위기(AetherAtmosphere)가 실행 중에 쓰는 에셋 모음.
///
/// 플레이어 카메라가 씬마다 다르고 런타임에 생성되기도 해서, 카메라에 후처리를
/// 미리 꽂아 둘 수가 없다. 실행 중에 PostProcessLayer 를 붙이려면 패키지의
/// PostProcessResources 가 필요한데 패키지 에셋은 Resources 로 못 읽으므로 여기서 들고 있는다.
/// 위치: Resources/AETHERRuntime/Mood/AetherMoodAssets.asset
/// </summary>
[CreateAssetMenu(menuName = "AETHER/Mood Assets", fileName = "AetherMoodAssets")]
public class AetherMoodAssets : ScriptableObject
{
    [System.Serializable]
    public struct SceneProfile
    {
        [Tooltip("씬 이름 앞부분 (예: \"05.\")")]
        public string scenePrefix;
        public PostProcessProfile profile;
    }

    public PostProcessResources postProcessResources;
    public SceneProfile[] profiles;

    public PostProcessProfile FindProfile(string sceneName)
    {
        if (profiles == null || string.IsNullOrEmpty(sceneName))
            return null;

        for (int i = 0; i < profiles.Length; i++)
        {
            if (!string.IsNullOrEmpty(profiles[i].scenePrefix) &&
                sceneName.StartsWith(profiles[i].scenePrefix, System.StringComparison.Ordinal))
                return profiles[i].profile;
        }

        return null;
    }
}
