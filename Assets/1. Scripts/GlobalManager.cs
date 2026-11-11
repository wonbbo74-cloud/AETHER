using UnityEngine;

public class GlobalManager : MonoBehaviour
{
    private static GlobalManager instance;

    [Header("Manager References")]
    public GameManager gameManager;
    public AudioManager audioManager;
    public CurrencyManager currencyManager;

    // 씬에 진짜 GlobalManager가 없어서 런타임에 임시로 만든 인스턴스인지 여부.
    // 임시 인스턴스는 나중에 진짜가 등장하면 자리를 넘겨준다.
    private bool isRuntimeFallback;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveManagers();
            return;
        }

        if (instance == this)
            return;

        // 임시 인스턴스가 자리를 잡고 있었다면, 인스펙터 설정이 살아있는 진짜에게 넘긴다.
        if (instance.isRuntimeFallback && !isRuntimeFallback)
        {
            int carriedGold = instance.currencyManager != null ? instance.currencyManager.currentGold : 0;

            GlobalManager old = instance;
            instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveManagers();

            if (currencyManager != null)
                currencyManager.AddGold(carriedGold);

            Destroy(old.gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void ResolveManagers()
    {
        if (gameManager == null)
            gameManager = GetComponent<GameManager>();
        if (gameManager == null)
            gameManager = gameObject.AddComponent<GameManager>();

        if (audioManager == null)
            audioManager = GetComponent<AudioManager>();
        if (audioManager == null)
            audioManager = gameObject.AddComponent<AudioManager>();

        if (currencyManager == null)
            currencyManager = GetComponent<CurrencyManager>();
        if (currencyManager == null)
            currencyManager = gameObject.AddComponent<CurrencyManager>();
    }

    /// <summary>
    /// 씬을 직접 열어 테스트할 때도 매니저가 없어서 NullReference로 터지지 않도록 보장한다.
    /// 씬에 이미 있으면 그것을 쓰고, 없으면 임시 인스턴스를 만든다.
    /// </summary>
    public static GlobalManager Instance
    {
        get
        {
            if (instance != null)
                return instance;

            GlobalManager found = FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            if (found != null)
            {
                instance = found;
                DontDestroyOnLoad(found.gameObject);
                found.ResolveManagers();
                return instance;
            }

            GameObject host = new GameObject("GlobalManager (Runtime Fallback)");
            GlobalManager runtime = host.AddComponent<GlobalManager>();
            runtime.isRuntimeFallback = true;
            DontDestroyOnLoad(host);

            if (instance == null)
            {
                instance = runtime;
                runtime.ResolveManagers();
            }

            return instance;
        }
    }

    public static bool HasInstance => instance != null;

    // 전역 접근 프로퍼티 (호출 시점에 인스턴스가 없으면 자동 생성되므로 null이 아니다)
    public static GameManager Game => Instance.gameManager;
    public static AudioManager Audio => Instance.audioManager;
    public static CurrencyManager Currency => Instance.currencyManager;
}
