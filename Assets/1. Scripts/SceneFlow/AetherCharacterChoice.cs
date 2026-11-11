using UnityEngine;

/// <summary>플레이할 수 있는 캐릭터.</summary>
public enum AetherCharacter
{
    Hankyul,
    Yunseo,
}

/// <summary>
/// 플레이어 선택 화면에서 고른 캐릭터를 기억한다.
///
/// 선택 화면은 고른 값을 화면 표시에만 쓰고 어디에도 남기지 않아서,
/// 무엇을 고르든 모든 씬이 한결로 진행됐다. 여기에 저장해 두고
/// 씬마다 AetherCharacterSpawner 가 이 값대로 캐릭터를 세운다.
/// </summary>
public static class AetherCharacterChoice
{
    private const string PrefsKey = "AETHER_SelectedCharacter";

    private static bool loaded;
    private static AetherCharacter selected = AetherCharacter.Hankyul;

    /// <summary>고른 캐릭터. 고른 적이 없으면 한결.</summary>
    public static AetherCharacter Selected
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                selected = PlayerPrefs.GetInt(PrefsKey, (int)AetherCharacter.Hankyul) == (int)AetherCharacter.Yunseo
                    ? AetherCharacter.Yunseo
                    : AetherCharacter.Hankyul;
            }

            return selected;
        }
        set
        {
            loaded = true;
            selected = value;
            PlayerPrefs.SetInt(PrefsKey, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>화면에 보이는 캐릭터 이름. 코드 안의 이름(Yunseo/Hankyul)과 다르다.</summary>
    public const string YunseoDisplayName = "Shiri";
    public const string HankyulDisplayName = "Meka";

    public static string DisplayName(AetherCharacter character)
    {
        return character == AetherCharacter.Yunseo ? YunseoDisplayName : HankyulDisplayName;
    }
}

/// <summary>
/// 지금 조작 중인 플레이어를 캐릭터와 상관없이 다루는 도우미.
///
/// 코드 곳곳이 Move_Hankyul 만 찾아서 윤서로 플레이하면 조용히 건너뛰었다.
/// 플레이어를 찾거나, 조작을 잠그거나, 시선을 돌리거나, 파츠 시스템을 붙이는 일은 여기로 모은다.
/// </summary>
public static class AetherPlayerCharacter
{
    /// <summary>지금 켜져 있는 플레이어의 뿌리. 없으면 null.</summary>
    public static GameObject FindActive()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null && Identify(tagged.transform.root.gameObject).HasValue)
            return tagged.transform.root.gameObject;

        Move_Yunseo yunseo = Object.FindFirstObjectByType<Move_Yunseo>(FindObjectsInactive.Exclude);
        Move_Hankyul hankyul = Object.FindFirstObjectByType<Move_Hankyul>(FindObjectsInactive.Exclude);

        // 둘 다 켜져 있으면 고른 쪽을 믿는다.
        if (yunseo != null && hankyul != null)
            return AetherCharacterChoice.Selected == AetherCharacter.Yunseo ? yunseo.transform.root.gameObject : hankyul.transform.root.gameObject;

        if (yunseo != null)
            return yunseo.transform.root.gameObject;

        if (hankyul != null)
            return hankyul.transform.root.gameObject;

        return tagged != null ? tagged.transform.root.gameObject : null;
    }

    /// <summary>지금 켜져 있는 플레이어의 트랜스폼. 적이 겨눌 대상.</summary>
    public static Transform FindActiveTransform()
    {
        GameObject root = FindActive();
        return root != null ? root.transform : null;
    }

    /// <summary>
    /// 적이 겨누던 플레이어가 없거나 꺼졌으면 지금 플레이어로 바꾼다.
    /// 적은 Awake 에서 한 번만 플레이어를 찾는데, 씬이 열린 뒤 고른 캐릭터로 바꾸거나
    /// 플레이어가 늦게 생성되면(06·08·09·11) 꺼진 한결이나 null 을 계속 겨눴다.
    /// </summary>
    /// <returns>대상을 바꿨으면 true.</returns>
    public static bool RefreshTarget(ref Transform target, ref float nextLookup)
    {
        if (target != null && target.gameObject.activeInHierarchy)
            return false;

        if (Time.time < nextLookup)
            return false;

        nextLookup = Time.time + 0.5f;

        Transform found = FindActiveTransform();
        if (found == null || found == target)
            return false;

        target = found;
        return true;
    }

    /// <summary>이 오브젝트가 누구인지. 플레이어가 아니면 null.</summary>
    public static AetherCharacter? Identify(GameObject root)
    {
        if (root == null)
            return null;

        if (root.GetComponentInChildren<Move_Yunseo>(true) != null || root.GetComponentInChildren<Yunseo_Battle>(true) != null)
            return AetherCharacter.Yunseo;

        if (root.GetComponentInChildren<Move_Hankyul>(true) != null || root.GetComponentInChildren<Hankyul_Fire>(true) != null)
            return AetherCharacter.Hankyul;

        return null;
    }

    /// <summary>
    /// 파츠를 장착할 수 있는 캐릭터인지. 파츠 장착은 한결만 한다(제작은 둘 다 할 수 있다).
    /// 플레이어가 아닌 오브젝트(테스트용 등)는 막지 않는다.
    /// </summary>
    public static bool CanEquipParts(GameObject root)
    {
        return root == null || Identify(root.transform.root.gameObject) != AetherCharacter.Yunseo;
    }

    public const string PartsEquipOnlyHankyulMessage = "파츠 장착은\n" + AetherCharacterChoice.HankyulDisplayName + "만 할 수 있습니다.";

    public static bool IsPlayer(Transform target)
    {
        return target != null && Identify(target.root.gameObject).HasValue;
    }

    /// <summary>이동·시점을 막거나 푼다. 두 캐릭터 모두.</summary>
    public static void SetControl(GameObject root, bool enabled)
    {
        if (root == null)
            return;

        Move_Hankyul hankyul = root.GetComponentInChildren<Move_Hankyul>(true);
        if (hankyul != null)
        {
            hankyul.allowInput = enabled;
            hankyul.allowMove = enabled;
            hankyul.allowLook = enabled;
        }

        Move_Yunseo yunseo = root.GetComponentInChildren<Move_Yunseo>(true);
        if (yunseo != null)
        {
            yunseo.SetMoveEnabled(enabled);
            yunseo.SetLookEnabled(enabled);
        }
    }

    /// <summary>몸과 시선을 이 방향으로 바로 돌린다.</summary>
    public static void SnapLook(GameObject root, Quaternion rotation, float pitch = 0f)
    {
        if (root == null)
            return;

        Move_Hankyul hankyul = root.GetComponentInChildren<Move_Hankyul>(true);
        if (hankyul != null)
            hankyul.SnapLookToWorldRotation(rotation, pitch);

        Move_Yunseo yunseo = root.GetComponentInChildren<Move_Yunseo>(true);
        if (yunseo != null)
            yunseo.SnapLookToWorldRotation(rotation, pitch);
    }

    /// <summary>씬을 옮긴 뒤 등에 남아 있던 잠금·입력을 푼다.</summary>
    public static void ResetControl(GameObject root)
    {
        if (root == null)
            return;

        Move_Hankyul hankyul = root.GetComponentInChildren<Move_Hankyul>(true);
        if (hankyul != null)
            hankyul.ResetPlayerControlState();

        Move_Yunseo yunseo = root.GetComponentInChildren<Move_Yunseo>(true);
        if (yunseo != null)
            yunseo.ResetPlayerControlState();
    }

    /// <summary>
    /// 플레이어가 파츠·인벤토리·생존 수치를 쓸 수 있게 붙인다. 여러 번 불러도 된다.
    /// (예전에는 AetherZoneBootstrap·AetherPlayerUnifier·PartSceneAutoBinder 가 한결에게만 따로 했다.)
    /// </summary>
    public static void Prepare(GameObject root)
    {
        if (root == null)
            return;

        try
        {
            root.tag = "Player";
        }
        catch (UnityException)
        {
            Debug.LogWarning("[AetherPlayerCharacter] 'Player' 태그가 프로젝트에 없습니다.");
        }

        AetherCharacter? who = Identify(root);
        if (!who.HasValue)
            return;

        // 파츠·인벤토리는 이동 스크립트가 붙은 오브젝트를 기준으로 붙어 있다.
        MonoBehaviour mover = who.Value == AetherCharacter.Yunseo
            ? (MonoBehaviour)root.GetComponentInChildren<Move_Yunseo>(true)
            : root.GetComponentInChildren<Move_Hankyul>(true);
        GameObject owner = mover != null ? mover.gameObject : root;

        PlayerInventory inventory = owner.GetComponent<PlayerInventory>() ?? owner.GetComponentInChildren<PlayerInventory>(true);

        EquipmentManager equipment = owner.GetComponent<EquipmentManager>();
        if (equipment == null)
            equipment = owner.AddComponent<EquipmentManager>();

        if (inventory != null)
            equipment.SetInventory(inventory);

        if (who.Value == AetherCharacter.Yunseo)
        {
            if (owner.GetComponent<YunseoPartAbilityController>() == null)
                owner.AddComponent<YunseoPartAbilityController>();
        }
        else if (owner.GetComponent<HangyeolPartAbilityController>() == null)
        {
            owner.AddComponent<HangyeolPartAbilityController>();
        }

        // 씬을 넘어온 인벤토리/장비/생존 수치를 이 플레이어에 붙인다.
        if (inventory != null)
            PartSystemRuntimeState.BindInventory(inventory);

        PartSystemRuntimeState.BindEquipment(equipment);
        AetherRunState.TryBind();
    }
}
