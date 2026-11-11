using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// _module / _paper 오브젝트에 상호작용 컴포넌트를 자동 세팅하는 에디터 툴.
/// 메뉴: Tools > AETHER > Setup Items In Scene
/// </summary>
public static class ItemSetupTool
{
    [MenuItem("Tools/AETHER/Setup Items In Scene")]
    public static void SetupItemsInScene()
    {
        int moduleCount = 0;
        int paperCount = 0;
        List<string> log = new List<string>();

        // 씬의 모든 루트 오브젝트 + 그 하위까지 탐색
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            string name = go.name.ToLower();

            if (name.EndsWith("_module"))
            {
                bool changed = SetupModule(go, log);
                if (changed) moduleCount++;
            }
            else if (name.EndsWith("_paper"))
            {
                bool changed = SetupPaper(go, log);
                if (changed) paperCount++;
            }
        }

        // 씬 저장 요청 (dirty 마킹)
        if (moduleCount > 0 || paperCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // 결과 로그 출력
        string summary = $"[ItemSetupTool] 완료!\n_module 처리: {moduleCount}개\n_paper 처리: {paperCount}개\n\n상세:\n" + string.Join("\n", log);
        Debug.Log(summary);
        EditorUtility.DisplayDialog("아이템 세팅 완료", $"_module: {moduleCount}개\n_paper: {paperCount}개 처리됨\n\nConsole 창에서 상세 내용 확인 가능", "확인");
    }

    // ─────────────────────────────────────────
    // _module 세팅
    // ─────────────────────────────────────────
    private static bool SetupModule(GameObject go, List<string> log)
    {
        bool changed = false;

        // 1) Collider (isTrigger) 확인
        Collider col = go.GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            log.Add($"  [Module] {go.name}: BoxCollider 추가 (isTrigger)");
            changed = true;
        }
        else if (!col.isTrigger)
        {
            Undo.RecordObject(col, "Set isTrigger");
            col.isTrigger = true;
            log.Add($"  [Module] {go.name}: Collider isTrigger 켬");
            changed = true;
        }

        // 2) ModulePickupInteractable 확인
        ModulePickupInteractable pickup = go.GetComponent<ModulePickupInteractable>();
        if (pickup == null)
        {
            Undo.AddComponent<ModulePickupInteractable>(go);
            log.Add($"  [Module] {go.name}: ModulePickupInteractable 추가");
            changed = true;
        }

        // 3) ModuleWorldMarker 확인 (머리 위 아이콘)
        ModuleWorldMarker marker = go.GetComponent<ModuleWorldMarker>();
        if (marker == null)
        {
            Undo.AddComponent<ModuleWorldMarker>(go);
            log.Add($"  [Module] {go.name}: ModuleWorldMarker 추가");
            changed = true;
        }

        if (!changed)
            log.Add($"  [Module] {go.name}: 이미 세팅됨 (스킵)");

        return changed;
    }

    // ─────────────────────────────────────────
    // _paper 세팅
    // ─────────────────────────────────────────
    private static bool SetupPaper(GameObject go, List<string> log)
    {
        bool changed = false;

        // 1) Collider (isTrigger) 확인
        Collider col = go.GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = Undo.AddComponent<BoxCollider>(go);
            box.isTrigger = true;
            log.Add($"  [Paper] {go.name}: BoxCollider 추가 (isTrigger)");
            changed = true;
        }
        else if (!col.isTrigger)
        {
            Undo.RecordObject(col, "Set isTrigger");
            col.isTrigger = true;
            log.Add($"  [Paper] {go.name}: Collider isTrigger 켬");
            changed = true;
        }

        // 2) PaperInteractable 확인
        PaperInteractable paper = go.GetComponent<PaperInteractable>();
        if (paper == null)
        {
            Undo.AddComponent<PaperInteractable>(go);
            log.Add($"  [Paper] {go.name}: PaperInteractable 추가");
            changed = true;
        }

        // 3) ProximityHoldPrompt 확인 (F 길게 누르기 UI)
        ProximityHoldPrompt prompt = go.GetComponent<ProximityHoldPrompt>();
        if (prompt == null)
        {
            Undo.AddComponent<ProximityHoldPrompt>(go);
            log.Add($"  [Paper] {go.name}: ProximityHoldPrompt 추가");
            changed = true;
        }

        // 4) ModuleWorldMarker 확인 (머리 위 아이콘 - paper도 동일하게 적용)
        ModuleWorldMarker marker = go.GetComponent<ModuleWorldMarker>();
        if (marker == null)
        {
            Undo.AddComponent<ModuleWorldMarker>(go);
            log.Add($"  [Paper] {go.name}: ModuleWorldMarker 추가");
            changed = true;
        }

        if (!changed)
            log.Add($"  [Paper] {go.name}: 이미 세팅됨 (스킵)");

        return changed;
    }

    // ─────────────────────────────────────────
    // 현재 씬의 _module / _paper 오브젝트 목록만 확인 (설정 안 함)
    // ─────────────────────────────────────────
    [MenuItem("Tools/AETHER/List Items In Scene")]
    public static void ListItemsInScene()
    {
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        List<string> modules = new List<string>();
        List<string> papers = new List<string>();

        foreach (GameObject go in allObjects)
        {
            string lname = go.name.ToLower();
            if (lname.EndsWith("_module")) modules.Add(go.name);
            else if (lname.EndsWith("_paper")) papers.Add(go.name);
        }

        string msg = $"_module 오브젝트 ({modules.Count}개):\n" + string.Join("\n", modules)
                   + $"\n\n_paper 오브젝트 ({papers.Count}개):\n" + string.Join("\n", papers);

        Debug.Log("[ItemSetupTool] " + msg);
        EditorUtility.DisplayDialog("씬 아이템 목록", msg, "확인");
    }

    // 씬 전체 오브젝트 이름 출력 (디버그용)
    [MenuItem("Tools/AETHER/Print All Object Names")]
    public static void PrintAllObjectNames()
    {
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        List<string> names = new List<string>();
        foreach (GameObject go in allObjects)
            names.Add(go.name);

        names.Sort();
        string result = string.Join("\n", names);
        Debug.Log($"[ItemSetupTool] 씬 전체 오브젝트 ({names.Count}개):\n{result}");
        EditorUtility.DisplayDialog("전체 오브젝트 목록", $"총 {names.Count}개\nConsole 창에서 전체 목록 확인하세요.", "확인");
    }

    // module / paper 이름이 포함된 오브젝트만 찾기
    [MenuItem("Tools/AETHER/Find Module and Paper Objects")]
    public static void FindModuleAndPaperObjects()
    {
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        List<string> found = new List<string>();

        foreach (GameObject go in allObjects)
        {
            string lname = go.name.ToLower();
            if (lname.Contains("module") || lname.Contains("paper"))
                found.Add(go.name);
        }

        found.Sort();
        string result = string.Join("\n", found);
        Debug.Log($"[ItemSetupTool] module/paper 포함 오브젝트 ({found.Count}개):\n{result}");

        string dialogMsg = found.Count > 0
            ? $"총 {found.Count}개 발견:\n\n{string.Join("\n", found)}"
            : "module 또는 paper가 포함된 오브젝트가 없습니다.\n\nUnity Hierarchy에서 해당 오브젝트의 정확한 이름을 확인해주세요.";

        EditorUtility.DisplayDialog("module/paper 오브젝트", dialogMsg, "확인");
    }
}
