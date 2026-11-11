using UnityEngine;

public class PlayerEMPThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform throwPoint;
    [SerializeField] private GameObject empGrenadePrefab;
    [SerializeField] private Camera aimCamera;

    [Header("Input")]
    [SerializeField] private KeyCode throwKey = KeyCode.G;

    [Header("Throw")]
    [SerializeField] private float throwCooldown = 1.2f;
    [SerializeField] private float throwForce = 15f;
    [SerializeField] private float upwardForce = 2.5f;
    [SerializeField] private int grenadeCount = 3;
    [SerializeField] private bool useCameraForward = true;

    [Header("Animation")]
    [SerializeField] private string throwTriggerName = "ThrowEMP";

    private int throwTriggerHash;
    private float nextThrowTime;
    private bool throwQueued;
    private bool grenadeSpawnedThisThrow;

    private void Awake()
    {
        throwTriggerHash = Animator.StringToHash(throwTriggerName);
    }

    private void Update()
    {
        if (Input.GetKeyDown(throwKey))
            TryStartThrow();
    }

    public bool TryStartThrow()
    {
        if (Time.time < nextThrowTime) return false;
        if (grenadeCount <= 0) return false;
        if (throwQueued) return false;

        throwQueued = true;
        grenadeSpawnedThisThrow = false;
        nextThrowTime = Time.time + throwCooldown;

        if (animator != null)
        {
            animator.ResetTrigger(throwTriggerHash);
            animator.SetTrigger(throwTriggerHash);
        }
        else
        {
            SpawnGrenade();
            throwQueued = false;
        }

        return true;
    }

    // 애니메이션 이벤트에 연결
    public void AE_ThrowEMP()
    {
        if (!throwQueued) return;
        if (grenadeSpawnedThisThrow) return;

        SpawnGrenade();
    }

    // 애니메이션 마지막 프레임 이벤트에 연결
    public void AE_EndThrowEMP()
    {
        throwQueued = false;
        grenadeSpawnedThisThrow = false;
    }

    private void SpawnGrenade()
    {
        if (empGrenadePrefab == null || throwPoint == null) return;
        if (grenadeCount <= 0) return;

        grenadeSpawnedThisThrow = true;
        grenadeCount--;

        Vector3 spawnPos = throwPoint.position;
        Vector3 aimDir = GetAimDirection();

        GameObject grenadeObj = Instantiate(
            empGrenadePrefab,
            spawnPos,
            Quaternion.LookRotation(aimDir)
        );
        AetherAudio.Play2D("emp_throw", 0.55f, 1f, 0.05f); // 핀 빠지는 딸깍 + 던지는 바람 소리

        EMPGrenade grenade = grenadeObj.GetComponent<EMPGrenade>();
        if (grenade != null)
        {
            Vector3 velocity = aimDir.normalized * throwForce + Vector3.up * upwardForce;
            grenade.Initialize(velocity, gameObject);
        }
    }

    private Vector3 GetAimDirection()
    {
        if (useCameraForward && aimCamera != null)
            return aimCamera.transform.forward;

        return transform.forward;
    }

    public void AddGrenades(int amount)
    {
        grenadeCount += amount;
    }

    public int GetGrenadeCount()
    {
        return grenadeCount;
    }
}