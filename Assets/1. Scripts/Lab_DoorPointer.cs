using System.Collections.Generic;
using UnityEngine;

public class Lab_DoorPointer : MonoBehaviour
{
    [Header("Door Reference")]
    [SerializeField] private Lab_Door door;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color originalColor = Color.white;
    [SerializeField] private Color activeColor = Color.red;
    [SerializeField] private Color clearedColor = Color.black;

    [Header("Linked Lasers")]
    [SerializeField] private List<GameObject> linkedLasers = new List<GameObject>();

    [Header("Auto Setup")]
    [SerializeField] private bool autoFindDoorFromParent = true;
    [SerializeField] private bool autoReadOriginalColorOnAwake = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private bool isCleared;
    private bool hasCachedOriginalColor;
    private Material runtimeMaterial;

    public bool IsCleared => isCleared;
    public Lab_Door Door => door;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    public void AssignDoor(Lab_Door ownerDoor)
    {
        if (ownerDoor == null) return;
        door = ownerDoor;
    }

    public void Hit()
    {
        if (isCleared) return;
        if (door == null) return;
        if (door.IsNeutralized) return;
        if (!door.IsCurrentPointer(this)) return;

        // 한 발에 바로 다음 표적으로 넘어가 너무 쉬웠다(사용자 요청). 표적마다 여러 번 맞혀야 넘어간다.
        hitsTaken += Time.frameCount == lastHitFrame ? 0 : 1;
        lastHitFrame = Time.frameCount;
        AetherAudio.PlayAt("target_hit", transform.position, 0.6f, 2f, 40f, 0.05f, 0.8f + 0.1f * hitsTaken);
        if (hitsTaken < HitsRequired)
            return;

        MarkCleared();
        door.RegisterHit(this);
    }

    /// <summary>표적 하나를 끄는 데 필요한 명중 수.</summary>
    public const int HitsRequired = 3;
    private int hitsTaken;
    private int lastHitFrame = -1;

    public void ResetPointer()
    {
        EnsureSetup();
        hitsTaken = 0;
        isCleared = false;
        SetLaserState(true);
        SetToOriginalVisual();
    }

    public void ResetPointerState()
    {
        ResetPointer();
    }

    public void SetActiveVisual(bool isActive)
    {
        if (isCleared) return;

        SetColor(isActive ? activeColor : originalColor);
    }

    public void SetToOriginalVisual()
    {
        if (isCleared) return;

        SetColor(originalColor);
    }

    public void MarkCleared()
    {
        if (isCleared) return;

        isCleared = true;
        SetColor(clearedColor);
        SetLaserState(false);
    }

    private void SetLaserState(bool state)
    {
        for (int i = 0; i < linkedLasers.Count; i++)
        {
            GameObject laser = linkedLasers[i];
            if (laser == null) continue;

            laser.SetActive(state);
        }
    }

    private void CacheRuntimeMaterial()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer == null) return;

        runtimeMaterial = targetRenderer.material;
    }

    private void TryCacheOriginalColorFromMaterial()
    {
        if (hasCachedOriginalColor) return;
        if (runtimeMaterial == null) return;

        if (runtimeMaterial.HasProperty(BaseColorId))
        {
            originalColor = runtimeMaterial.GetColor(BaseColorId);
            hasCachedOriginalColor = true;
            return;
        }

        if (runtimeMaterial.HasProperty(ColorId))
        {
            originalColor = runtimeMaterial.GetColor(ColorId);
            hasCachedOriginalColor = true;
        }
    }

    private void SetColor(Color targetColor)
    {
        EnsureReferences();
        if (targetRenderer == null) return;

        if (runtimeMaterial == null)
            CacheRuntimeMaterial();

        if (runtimeMaterial == null) return;

        if (runtimeMaterial.HasProperty(BaseColorId))
            runtimeMaterial.SetColor(BaseColorId, targetColor);

        if (runtimeMaterial.HasProperty(ColorId))
            runtimeMaterial.SetColor(ColorId, targetColor);

        // 표적 재질에 강한 초록 발광이 들어 있어, 바탕색(주황 = 지금 맞힐 표적)이 묻혀 넷 다 똑같이 초록으로 보였다.
        // 맞힐 순서가 정해져 있는데 어느 것을 쏴야 하는지 알 수 없어 진행이 막혔다. 발광도 상태에 맞춘다.
        if (runtimeMaterial.HasProperty(EmissionColorId))
        {
            Color glow;
            if (isCleared)
                glow = Color.black;
            else if (targetColor == activeColor)
                glow = activeColor * 3.2f;          // 지금 맞힐 표적: 밝은 주황
            else
                glow = IdleGlow;                    // 기다리는 표적: 어둑한 초록
            runtimeMaterial.EnableKeyword("_EMISSION");
            runtimeMaterial.SetColor(EmissionColorId, glow);
        }
    }

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Color IdleGlow = new Color(0.44f, 2.2f, 0.77f) * 0.18f;

    private void EnsureSetup()
    {
        EnsureReferences();

        if (runtimeMaterial == null)
            CacheRuntimeMaterial();

        if (autoReadOriginalColorOnAwake)
            TryCacheOriginalColorFromMaterial();
    }

    private void EnsureReferences()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (autoFindDoorFromParent && door == null)
            door = GetComponentInParent<Lab_Door>();
    }
}
