using UnityEngine;

public class NetworkManagerPersistent : MonoBehaviour
{
    private static NetworkManagerPersistent instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}