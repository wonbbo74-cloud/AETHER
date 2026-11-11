using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>저장 파일 한 개에 들어가는 값. JSON 으로 적는다.</summary>
[Serializable]
public class AetherSaveData
{
    [Serializable] public class ItemCount { public string name; public int count; }
    [Serializable] public class SlotPart { public string slot; public string name; }

    public int version = 1;
    public string savedAt;
    public string scene;
    public int character;
    public float px, py, pz, yaw;
    public bool hasPosition;

    public float hp = -1f, thirst, shield;

    public List<ItemCount> inventory = new List<ItemCount>();
    public List<string> craftedParts = new List<string>();
    public List<SlotPart> equipped = new List<SlotPart>();
    public List<string> collectedSceneItems = new List<string>();

    public int aetherExp;
    public List<string> aetherAwarded = new List<string>();

    public List<string> killed05 = new List<string>();
    public int wavesReleased05;
    public List<int> picked06 = new List<int>();
    public List<int> killed06 = new List<int>();
    public List<string> clearedPuzzles = new List<string>();

    public List<string> flags = new List<string>();
    public List<string> pickedFloorItems = new List<string>();
}

/// <summary>
/// 저장 / 이어하기.
///
/// 언제 저장하나: 04~07 장면에 들어서고 몇 초 뒤, 진행이 바뀔 때(물건 줍기·퍼즐·문·제작·장착·에테르)
/// 2초 모아서, 그리고 30초마다·게임을 끌 때. 퍼즐2 화면에서는 저장하지 않는다(돌아온 05 에서 저장).
/// 쓰러져 있을 때·공중에 떠 있을 때는 자리 대신 직전에 서 있던 자리를 쓴다.
///
/// 이어하기: 로비의 [이어하기] → 저장된 값으로 상태를 되살리고 저장된 장면을 연 뒤, 플레이어를 저장된 자리에 세운다.
/// 저장 파일: Application.persistentDataPath/aether_save.json (Windows: AppData/LocalLow/회사/AETHER)
/// To Be Continued(한 판 끝)에서 지운다.
/// </summary>
public class AetherSave : MonoBehaviour
{
    private const float AutoSaveInterval = 30f;
    private const float DirtyDelay = 2f;
    private const float SceneEnterDelay = 3f;

    private static AetherSave instance;
    private static bool dirty;
    private static float dirtySince;
    private static readonly HashSet<string> flags = new HashSet<string>();
    private static readonly HashSet<string> pickedFloorItems = new HashSet<string>();

    // 이어하기로 연 장면에서 플레이어를 세울 자리
    private static AetherSaveData pendingRestore;

    // 끝 화면이 도는 동안엔 저장하지 않는다(다음 장면을 열면 풀린다).
    private static bool suppressed;

    private float nextAutoSave;
    private float sceneEnteredAt;
    private Vector3 lastSafePosition;
    private float lastSafeYaw;
    private bool hasSafePosition;
    private TextMeshProUGUI indicator;
    private CanvasGroup indicatorGroup;
    private float indicatorUntil;

    public static string SavePath => Path.Combine(Application.persistentDataPath, "aether_save.json");
    public static bool HasSave => File.Exists(SavePath);

    // ------------------------------------------------------------------ 공용

    public static void MarkDirty()
    {
        if (!dirty) dirtySince = Time.unscaledTime;
        dirty = true;
    }

    public static bool HasFlag(string flag) => flags.Contains(flag);

    public static void SetFlag(string flag)
    {
        if (flags.Add(flag))
            MarkDirty();
    }

    public static void NotePickedFloorItem(GameObject item)
    {
        if (item == null) return;
        pickedFloorItems.Add(FloorKey(item));
        MarkDirty();
    }

    /// <summary>새 판: 깃발·주운 물건 기록을 지운다(AetherRunState.ResetRun 이 부른다).</summary>
    public static void ResetRun()
    {
        flags.Clear();
        pickedFloorItems.Clear();
        dirty = false;
    }

    public static void DeleteSave()
    {
        suppressed = true;
        try { if (File.Exists(SavePath)) File.Delete(SavePath); }
        catch (Exception e) { Debug.LogWarning("[저장] 삭제 실패: " + e.Message); }
    }

    /// <summary>로비에 보여 줄 한 줄 요약. 저장이 없으면 null.</summary>
    public static string Describe()
    {
        AetherSaveData data = Read();
        if (data == null) return null;

        string area = AreaName(data.scene);
        string who = AetherCharacterChoice.DisplayName((AetherCharacter)data.character);
        int level = 1;
        foreach (int threshold in new[] { 300, 900, 1800, 3000 })
            if (data.aetherExp >= threshold) level++;
        return $"{area} · {who} · LV.{level}  ({data.savedAt})";
    }

    public static string AreaName(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return "?";
        if (scene.StartsWith("04.")) return "지하 벙커";
        if (scene.StartsWith("05.")) return "폐허된 도시";
        if (scene.StartsWith("06.")) return "연구소";
        if (scene.StartsWith("07.")) return "파츠 제작실";
        return scene;
    }

    /// <summary>이어하기. 성공하면 장면을 연다.</summary>
    public static bool Continue()
    {
        AetherSaveData data = Read();
        if (data == null || string.IsNullOrEmpty(data.scene))
            return false;

        Ensure();
        AetherCharacterChoice.Selected = (AetherCharacter)data.character;

        PartSystemRuntimeState.Import(data);
        AetherRunState.Import(data);
        AetherLevel.Import(data);
        AetherEncounterDirector.Import(data);
        AetherLabEncounter.Import(data);
        PuzzleSceneFlowState.ImportCleared(data.clearedPuzzles);

        flags.Clear();
        foreach (string f in data.flags) flags.Add(f);
        pickedFloorItems.Clear();
        foreach (string k in data.pickedFloorItems) pickedFloorItems.Add(k);

        pendingRestore = data;
        dirty = false;
        AetherSceneFade.LoadScene(data.scene);
        return true;
    }

    // ------------------------------------------------------------------ 설치

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Ensure();
    }

    private static void Ensure()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AETHER_Save");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AetherSave>();
        SceneManager.sceneLoaded += instance.HandleSceneLoaded;
        instance.HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        sceneEnteredAt = Time.unscaledTime;
        hasSafePosition = false;
        suppressed = false;

        if (!IsSaveScene(scene.name))
            return;

        RemovePickedFloorItems(scene);
        // 장면이 막 열린 순간엔 다른 준비 코드가 문짝 자리를 다시 잡아 덮어썼다. 조금 뒤에 적용한다.
        StartCoroutine(ApplySceneFlagsLater(scene.name));

        if (pendingRestore != null && pendingRestore.scene == scene.name)
            StartCoroutine(PlacePlayer(pendingRestore));
        pendingRestore = null;
    }

    private static bool IsSaveScene(string name)
    {
        return name.StartsWith("04.") || name.StartsWith("05.") || name.StartsWith("06.") || name.StartsWith("07.");
    }

    // ------------------------------------------------------------------ 이어하기: 장면을 연 뒤

    private System.Collections.IEnumerator PlacePlayer(AetherSaveData data)
    {
        Transform player = null;
        for (int i = 0; i < 180 && player == null; i++)
        {
            player = AetherPlayerCharacter.FindActiveTransform();
            if (player == null) yield return null;
        }

        // 장면의 자기 자리 맞추기(엘리베이터 도착·퍼즐 복귀 등)가 끝난 뒤 옮긴다.
        yield return null;
        yield return null;

        if (player == null || !data.hasPosition)
            yield break;

        GameObject root = player.root.gameObject;
        Rigidbody body = root.GetComponentInChildren<Rigidbody>();
        Vector3 position = new Vector3(data.px, data.py + 0.05f, data.pz);
        Quaternion rotation = Quaternion.Euler(0f, data.yaw, 0f);

        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
        }
        root.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        AetherPlayerCharacter.SnapLook(root, rotation);

        lastSafePosition = position;
        lastSafeYaw = data.yaw;
        hasSafePosition = true;
        AetherObjectiveToast.Show("저장된 곳에서 이어서 시작합니다.", 3f);
    }

    private static System.Collections.IEnumerator ApplySceneFlagsLater(string sceneName)
    {
        for (int i = 0; i < 10; i++)
            yield return null;
        if (SceneManager.GetActiveScene().name == sceneName)
            ApplySceneFlags(sceneName);
    }

    /// <summary>이미 지난 일(문 열기·퍼즐 풀기)을 장면에 다시 적용한다.</summary>
    private static void ApplySceneFlags(string sceneName)
    {
        if (sceneName.StartsWith("04.") && HasFlag("04-power-box"))
        {
            PuzzleManager puzzle = FindFirstObjectByType<PuzzleManager>(FindObjectsInactive.Include);
            if (puzzle != null)
                puzzle.RestoreCleared();
        }
        // 05 연구소 문은 AetherFactoryGateBreach 가 깃발을 보고 조용히 열어 둔다.
    }

    private static void RemovePickedFloorItems(Scene scene)
    {
        if (pickedFloorItems.Count == 0) return;
        foreach (InteractableItem item in FindObjectsByType<InteractableItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item != null && item.gameObject.scene == scene && pickedFloorItems.Contains(FloorKey(item.gameObject)))
                Destroy(item.gameObject);
    }

    private static string FloorKey(GameObject item)
    {
        Vector3 p = item.transform.position;
        return $"{item.scene.name}|{item.name}@{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.y)},{Mathf.RoundToInt(p.z)}";
    }

    // ------------------------------------------------------------------ 저장

    private void Update()
    {
        UpdateIndicator();

        string sceneName = SceneManager.GetActiveScene().name;
        if (!IsSaveScene(sceneName))
            return;

        TrackSafePosition();

        float now = Time.unscaledTime;
        if (now - sceneEnteredAt < SceneEnterDelay)
            return;

        bool firstInScene = sceneEnteredAt > 0f && now - sceneEnteredAt < SceneEnterDelay + 0.5f;
        bool dirtyDue = dirty && now - dirtySince >= DirtyDelay;
        bool periodic = now >= nextAutoSave;

        if (firstInScene || dirtyDue || periodic)
        {
            if (SaveNow(firstInScene || dirtyDue))
                sceneEnteredAt = -999f;
        }
    }

    /// <summary>서 있던 자리 중 마지막 안전한 곳(살아 있고 땅 위)을 기억한다.</summary>
    private void TrackSafePosition()
    {
        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player == null) return;

        PlayerStatus status = player.GetComponentInParent<PlayerStatus>() ?? player.GetComponentInChildren<PlayerStatus>();
        if (status != null && status.currentHP <= 0f) return;
        if (Time.timeScale <= 0f) return;

        Vector3 p = player.position;
        int mask = ~(1 << LayerMask.NameToLayer("Player"));
        if (!Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.6f, mask, QueryTriggerInteraction.Ignore))
            return;   // 공중

        lastSafePosition = p;
        lastSafeYaw = player.eulerAngles.y;
        hasSafePosition = true;
    }

    /// <summary>지금 상태를 파일에 적는다.</summary>
    public static bool SaveNow(bool showIndicator = true)
    {
        if (instance == null || suppressed) return false;
        string sceneName = SceneManager.GetActiveScene().name;
        if (!IsSaveScene(sceneName)) return false;
        if (AetherPlayerCharacter.FindActiveTransform() == null) return false;

        AetherSaveData data = new AetherSaveData
        {
            savedAt = DateTime.Now.ToString("MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            scene = sceneName,
            character = (int)AetherCharacterChoice.Selected,
            hasPosition = instance.hasSafePosition,
            px = instance.lastSafePosition.x,
            py = instance.lastSafePosition.y,
            pz = instance.lastSafePosition.z,
            yaw = instance.lastSafeYaw,
        };

        PartSystemRuntimeState.Export(data);
        AetherRunState.Export(data);
        AetherLevel.Export(data);
        AetherEncounterDirector.Export(data);
        AetherLabEncounter.Export(data);
        data.clearedPuzzles = PuzzleSceneFlowState.ExportCleared();
        data.flags = new List<string>(flags);
        data.pickedFloorItems = new List<string>(pickedFloorItems);

        try
        {
            string tmp = SavePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(tmp, SavePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[저장] 저장 실패: " + e.Message);
            return false;
        }

        dirty = false;
        instance.nextAutoSave = Time.unscaledTime + AutoSaveInterval;
        if (showIndicator)
            instance.ShowIndicator();
        return true;
    }

    private static AetherSaveData Read()
    {
        try
        {
            if (!File.Exists(SavePath)) return null;
            return JsonUtility.FromJson<AetherSaveData>(File.ReadAllText(SavePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[저장] 읽기 실패: " + e.Message);
            return null;
        }
    }

    private void OnApplicationQuit()
    {
        SaveNow(false);
    }

    // ------------------------------------------------------------------ "저장됨" 표시

    private void ShowIndicator()
    {
        if (indicator == null)
        {
            GameObject canvasObject = new GameObject("SaveIndicator", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            indicatorGroup = canvasObject.GetComponent<CanvasGroup>();
            indicatorGroup.blocksRaycasts = false;
            indicatorGroup.interactable = false;

            GameObject label = new GameObject("Text", typeof(RectTransform));
            label.transform.SetParent(canvasObject.transform, false);
            indicator = label.AddComponent<TextMeshProUGUI>();
            RectTransform rect = indicator.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-34f, -122f);   // 오른쪽 위 에테르 레벨 칸 바로 아래
            rect.sizeDelta = new Vector2(220f, 24f);
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("UI/Fonts/NanumGothicCoding-Bold SDF");
            if (font != null) indicator.font = font;
            indicator.fontSize = 16f;
            indicator.alignment = TextAlignmentOptions.Right;
            indicator.color = new Color(0.62f, 0.88f, 1f, 0.9f);
            indicator.text = "● 자동 저장됨";
            indicator.raycastTarget = false;
        }

        indicatorUntil = Time.unscaledTime + 2f;
    }

    private void UpdateIndicator()
    {
        if (indicatorGroup == null) return;
        float left = indicatorUntil - Time.unscaledTime;
        indicatorGroup.alpha = Mathf.Clamp01(left / 0.5f);
    }
}
