using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First-person camera for both players — competitive FPS feel like Overwatch.
///
/// Principles: the view sticks to the mouse 1:1 instantly, and the camera itself barely shakes.
/// The motion lives in the weapon and hands (<see cref="AetherViewModelSway"/>).
///
/// 1. Smooth position: the camera is a child of the physics body, so it moved only 50 times a second and juddered on faster screens.
///    Interpolate the body position between physics steps and move only the camera there.
///    (Rigidbody interpolation would overwrite the turn the Move scripts apply directly every frame, so it isn't used.)
/// 2. Landing: only a very slight dip and return when landing from a high place. No head bob, strafe tilt or running FOV.
/// 3. FOV: horizontal 103° for both characters (the Overwatch default). Meka's aim zoom works from this as its base.
/// 4. Free look: hold Alt to look around without turning the body.
/// 5. Crouch camera: Shiri's crouch only shrank the collider, so the camera is lowered smoothly here.
/// 6. Shiri's camera near clip 0.3 m → 0.05 m (no seeing through walls up close).
///
/// Everything is laid on just before drawing (LateUpdate) and removed at the start of the next frame (order -900),
/// so shooting, interaction and stance code always see the original camera values.
/// </summary>
[DefaultExecutionOrder(-900)]
public class AetherCameraFeel : MonoBehaviour
{
    /// <summary>
    /// The mouse turn used to be (pixels moved × sensitivity × frame time). Mouse movement is already counted per frame,
    /// so multiplying by frame time again made the look change with frame rate. Now it is pixels × sensitivity × this constant
    /// (same feel as before at 60 fps).
    /// </summary>
    public const float MouseScale = 1f / 60f;

    public const float LookUpLimit = -80f;
    // 10/12 사용자: 아래를 끝까지 보면 자기 몸 안쪽이 보인다 → 아래로 보는 각도를 제한.
    public const float LookDownLimit = 35f;
    public const float HorizontalFov = 103f;

    private const float FreeLookYawLimit = 100f;
    private const float FreeLookPitchLimit = 60f;

    private static readonly FieldInfo YunseoCrouchField = typeof(Move_Yunseo).GetField("isCrouching", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo YunseoCrouchRatioField = typeof(Move_Yunseo).GetField("crouchHeightRatio", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly FieldInfo BattleDefaultFov = typeof(Hankyul_Battle).GetField("defaultFieldOfView", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private static readonly FieldInfo BattleHasDefaultFov = typeof(Hankyul_Battle).GetField("hasDefaultFieldOfView", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private Rigidbody body;
    private Move_Hankyul hankyul;
    private Move_Yunseo yunseo;
    private Transform cam;
    private Camera camComponent;
    private float fovAppliedForAspect = -1f;

    private Vector3 physPrev;
    private Vector3 physCurr;
    private bool hasPhys;

    private bool applied;
    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;

    private Vector3 velocitySmoothed;
    private float crouchDrop;
    private float airMinVy;
    private float dipPos;
    private float dipVel;

    private bool freeLookHeld;
    private float freeYaw;
    private float freePitch;

    // Movement state the weapon sway reads
    public Transform CameraTransform => cam;
    public float HorizontalSpeed { get; private set; }
    public float LateralSpeed { get; private set; }
    public bool Grounded { get; private set; } = true;
    public float LandingDip => dipPos;

    // ---------- attach ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (AetherRuntimeSwitch.DisableAll)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        AetherCameraFeelAttacher.Ensure();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => AetherCameraFeelAttacher.Ensure();

    public static void AttachTo(Transform player)
    {
        if (player == null || player.GetComponent<AetherCameraFeel>() != null)
            return;
        if (player.GetComponent<Move_Hankyul>() == null && player.GetComponent<Move_Yunseo>() == null)
            return;
        player.gameObject.AddComponent<AetherCameraFeel>();
        if (player.GetComponent<AetherViewModelSway>() == null)
            player.gameObject.AddComponent<AetherViewModelSway>();
    }

    /// <summary>
    /// Called from the Move scripts' look handling. While Alt is held, turn the camera instead of the body and report that it was used.
    /// </summary>
    public static bool TryCaptureFreeLook(Component owner, float yawDegrees, float pitchDegrees)
    {
        if (owner == null)
            return false;

        AetherCameraFeel feel = owner.GetComponent<AetherCameraFeel>();
        if (feel == null || !feel.freeLookHeld)
            return false;

        feel.freeYaw = Mathf.Clamp(feel.freeYaw + yawDegrees, -FreeLookYawLimit, FreeLookYawLimit);
        feel.freePitch = Mathf.Clamp(feel.freePitch + pitchDegrees, -FreeLookPitchLimit, FreeLookPitchLimit);
        return true;
    }

    // ---------- lifecycle ----------

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        hankyul = GetComponent<Move_Hankyul>();
        yunseo = GetComponent<Move_Yunseo>();

        if (hankyul != null)
        {
            hankyul.minLookAngle = Mathf.Min(hankyul.minLookAngle, LookUpLimit);
            hankyul.maxLookAngle = LookDownLimit;   // 아래로는 35도까지(몸 안쪽이 보이지 않게)
            // In competitive FPS games the view follows the mouse instantly. Smoothing (0.03 s) only showed up as lag.
            hankyul.smoothLook = false;
        }
    }

    private void OnEnable()
    {
        hasPhys = false;
        StartCoroutine(TrackPhysics());
    }

    private void OnDisable()
    {
        Restore();
        freeYaw = freePitch = 0f;
        dipPos = dipVel = 0f;
    }

    private IEnumerator TrackPhysics()
    {
        var wait = new WaitForFixedUpdate();
        while (true)
        {
            yield return wait;
            if (body == null)
                continue;

            Vector3 p = body.position;
            if (!hasPhys || (p - physCurr).sqrMagnitude > 9f)
            {
                physPrev = physCurr = p;
                hasPhys = true;
            }
            else
            {
                physPrev = physCurr;
                physCurr = p;
            }
        }
    }

    private void Update()
    {
        Restore();

#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        bool altHeld = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
#else
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
#endif
        freeLookHeld = altHeld && CanLook() && Time.timeScale > 0f;
    }

    private void LateUpdate()
    {
        ResolveCamera();
        if (cam == null || body == null)
            return;

        // AetherPlayerUnifier copies the prefab setting (smoothLook on) back onto the player after attaching, so keep it off every frame.
        if (hankyul != null && hankyul.smoothLook)
            hankyul.smoothLook = false;

        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        ApplyFovIfNeeded();

        baseLocalPos = cam.localPosition;
        baseLocalRot = cam.localRotation;

        // When other code moved the body directly (respawn, fall recovery, elevator), don't trail behind the old position.
        if (hasPhys && (transform.position - physCurr).sqrMagnitude > 0.5f)
            physPrev = physCurr = transform.position;

        // 1) Physics interpolation
        Vector3 renderOffset = Vector3.zero;
        if (hasPhys && !body.isKinematic)
        {
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Mathf.Max(0.0001f, Time.fixedDeltaTime));
            Vector3 interp = Vector3.Lerp(physPrev, physCurr, alpha);
            renderOffset = Vector3.ClampMagnitude(interp - transform.position, 0.6f);
        }
        else if (body.isKinematic)
        {
            physPrev = physCurr = body.position;
        }

        Vector3 vel = (hasPhys && !body.isKinematic) ? (physCurr - physPrev) / Mathf.Max(0.0001f, Time.fixedDeltaTime) : Vector3.zero;
        velocitySmoothed = Vector3.Lerp(velocitySmoothed, vel, 1f - Mathf.Exp(-12f * dt));
        Vector3 flat = new Vector3(velocitySmoothed.x, 0f, velocitySmoothed.z);
        HorizontalSpeed = flat.magnitude;
        LateralSpeed = Vector3.Dot(flat, transform.right);

        UpdateGroundAndLanding(dt);

        // 5) Crouch camera (Shiri only; Meka has his own stance camera)
        float targetDrop = 0f;
        if (yunseo != null && IsCrouched())
        {
            float ratio = YunseoCrouchRatioField != null ? (float)YunseoCrouchRatioField.GetValue(yunseo) : 0.6f;
            targetDrop = Mathf.Clamp(Mathf.Max(0.5f, cam.position.y - transform.position.y) * (1f - ratio), 0f, 0.8f);
        }
        crouchDrop = Mathf.Lerp(crouchDrop, targetDrop, 1f - Mathf.Exp(-12f * dt));

        // 4) Free look: return quickly on release
        if (!freeLookHeld)
        {
            float k = 1f - Mathf.Exp(-14f * dt);
            freeYaw = Mathf.Lerp(freeYaw, 0f, k);
            freePitch = Mathf.Lerp(freePitch, 0f, k);
            if (Mathf.Abs(freeYaw) < 0.01f) freeYaw = 0f;
            if (Mathf.Abs(freePitch) < 0.01f) freePitch = 0f;
        }

        // ---- lay it on ----
        Transform parent = cam.parent;
        Vector3 worldOffset = renderOffset + transform.up * (dipPos - crouchDrop);
        cam.localPosition = baseLocalPos + (parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset);

        KeepCameraOutOfWalls();

        if (freeYaw != 0f || freePitch != 0f)
        {
            Vector3 e = baseLocalRot.eulerAngles;
            float pitch = Mathf.Clamp(NormalizeAngle(e.x) + freePitch, LookUpLimit - 5f, LookDownLimit + 5f);
            cam.localRotation = Quaternion.Euler(0f, freeYaw, 0f) * Quaternion.Euler(pitch, NormalizeAngle(e.y), NormalizeAngle(e.z));
        }

        applied = true;
    }

    // ---------- helpers ----------

    private const float WallClearance = 0.1f;
    private int wallMask;

    /// <summary>
    /// 카메라는 몸 중심보다 0.22m 앞에 있는데 몸 반지름은 0.24m 라, 벽에 얼굴을 대면 카메라가 벽에서 2cm 까지 붙어
    /// 벽 너머가 보였다(05·06 기둥·벽에서 실측). 몸 중심에서 카메라 쪽으로 구를 쏴서 벽에 닿으면 그만큼 뒤로 당긴다.
    /// </summary>
    private void KeepCameraOutOfWalls()
    {
        if (wallMask == 0)
        {
            int ignore = 1 << 2; // Ignore Raycast
            foreach (string layer in new[] { "Player", "ViewModel", "UI", "Enemy" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0) ignore |= 1 << index;
            }
            wallMask = ~ignore;
        }

        Vector3 camPos = cam.position;
        Vector3 origin = new Vector3(transform.position.x, camPos.y, transform.position.z);
        Vector3 toCam = camPos - origin;
        float distance = toCam.magnitude;
        if (distance < 0.001f)
            return;

        Vector3 dir = toCam / distance;
        if (Physics.SphereCast(origin, WallClearance, dir, out RaycastHit hit, distance, wallMask, QueryTriggerInteraction.Ignore))
            cam.position = origin + dir * Mathf.Max(0f, hit.distance);
    }

    private void ApplyFovIfNeeded()
    {
        if (camComponent == null)
            return;

        // 미니맵 전용 표식(드론 위의 주황 원판 등, 'Minimap' 층)이 게임 화면에도 그려져
        // 05 시작 지점 하늘에 주황 타원이 떠 있었다. 게임 카메라에서는 그 층을 뺀다.
        int minimapLayer = LayerMask.NameToLayer("Minimap");
        if (minimapLayer >= 0 && (camComponent.cullingMask & (1 << minimapLayer)) != 0)
            camComponent.cullingMask &= ~(1 << minimapLayer);

        float aspect = camComponent.aspect;
        if (Mathf.Abs(aspect - fovAppliedForAspect) < 0.01f)
            return;

        float vertical = Camera.HorizontalToVerticalFieldOfView(HorizontalFov, aspect);
        float previousBase = camComponent.fieldOfView;

        Hankyul_Battle battle = GetComponent<Hankyul_Battle>();
        if (battle != null && BattleDefaultFov != null && BattleHasDefaultFov != null && (bool)BattleHasDefaultFov.GetValue(battle))
        {
            // Keep the aim zoom's resting FOV in step.
            previousBase = (float)BattleDefaultFov.GetValue(battle);
            BattleDefaultFov.SetValue(battle, vertical);
        }

        // Even mid-zoom, only shift by the base difference so the zoom isn't broken.
        camComponent.fieldOfView += vertical - previousBase;
        fovAppliedForAspect = aspect;
    }

    private void Restore()
    {
        if (!applied || cam == null)
        {
            applied = false;
            return;
        }

        cam.localPosition = baseLocalPos;
        cam.localRotation = baseLocalRot;
        applied = false;
    }

    private void ResolveCamera()
    {
        Transform wanted = hankyul != null ? hankyul.camTransform : (yunseo != null ? yunseo.camTransform : null);
        if (wanted == cam)
            return;

        Restore();
        cam = wanted;
        camComponent = cam != null ? cam.GetComponent<Camera>() : null;
        fovAppliedForAspect = -1f;

        if (camComponent != null && camComponent.nearClipPlane > 0.05f)
            camComponent.nearClipPlane = 0.05f;
    }

    private bool CanLook()
    {
        if (hankyul != null)
            return hankyul.allowInput && hankyul.allowLook && hankyul.enabled;
        if (yunseo != null)
            return yunseo.allowLook && yunseo.enabled;
        return false;
    }

    public bool IsCrouched()
    {
        if (yunseo != null && YunseoCrouchField != null)
            return (bool)YunseoCrouchField.GetValue(yunseo);
        return false;
    }

    private void UpdateGroundAndLanding(float dt)
    {
        bool nowGrounded = false;
        Vector3 origin = body.position + Vector3.up * 0.45f;
        RaycastHit[] hits = Physics.SphereCastAll(origin, 0.22f, Vector3.down, 0.5f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != null && !hits[i].collider.transform.IsChildOf(transform))
            {
                nowGrounded = true;
                break;
            }
        }

        float vy = body.isKinematic ? 0f : body.linearVelocity.y;
        if (!nowGrounded)
            airMinVy = Mathf.Min(airMinVy, vy);

        if (nowGrounded && !Grounded)
        {
            float fall = -airMinVy;
            if (fall > 4f)
                dipVel -= Mathf.Clamp((fall - 4f) * 0.08f, 0f, 0.7f);
        }
        if (nowGrounded)
            airMinVy = 0f;
        Grounded = nowGrounded;

        // Landing dip spring: firm and quick, no overshoot
        const float omega = 16f;
        const float zeta = 0.9f;
        dipVel += (-omega * omega * dipPos - 2f * zeta * omega * dipVel) * dt;
        dipPos += dipVel * dt;
        dipPos = Mathf.Clamp(dipPos, -0.06f, 0.02f);
    }

    private static float NormalizeAngle(float a)
    {
        a %= 360f;
        if (a > 180f) a -= 360f;
        if (a < -180f) a += 360f;
        return a;
    }
}

/// <summary>
/// Weapon and hand motion (Overwatch style). The camera stays still; only the weapon in view moves.
/// - Turning: the hands lag a little behind the turn and catch up quickly (up to ~2°).
/// - Walking: a small sway in step with the stride (a few mm to 1–2 cm).
/// - Sideways movement: the weapon leans slightly.
/// - Landing: the weapon dips briefly.
///
/// The hands and gun aren't children of the camera; they hang off the character skeleton (chest → arms → hand → gun).
/// So the chest bone is rotated and shifted slightly around the camera position. This runs late (order 900),
/// after animation and other bone corrections, and is restored at the start of the next frame.
/// </summary>
[DefaultExecutionOrder(900)]
public class AetherViewModelSway : MonoBehaviour
{
    private const float BobStride = 1.7f;

    private AetherCameraFeel feel;
    private Transform chest;
    private bool searched;

    private bool applied;
    private Vector3 basePos;
    private Quaternion baseRot;
    private Vector3 appliedPos;
    private Quaternion appliedRot;

    private Quaternion lastCamRot;
    private bool hasLastCam;
    private Vector2 sway;
    private float roll;
    private float bobPhase;
    private float bobWeight;

    private void Awake()
    {
        feel = GetComponent<AetherCameraFeel>();
    }

    private void OnDisable()
    {
        RestoreIfUntouched();
        hasLastCam = false;
    }

    private void FindChest()
    {
        searched = true;
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        }

        if (chest == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "spine_03" || n.EndsWith("spine2") || n == "upperchest" || n == "chest")
                {
                    chest = t;
                    break;
                }
            }
        }
    }

    private void RestoreIfUntouched()
    {
        if (!applied || chest == null)
        {
            applied = false;
            return;
        }

        // Animation overwrote the bone: leave it. Otherwise (bone not animated) put back exactly what was moved.
        if ((chest.position - appliedPos).sqrMagnitude < 1e-8f && Quaternion.Angle(chest.rotation, appliedRot) < 0.001f)
        {
            chest.position = basePos;
            chest.rotation = baseRot;
        }
        applied = false;
    }

    private void LateUpdate()
    {
        if (feel == null) feel = GetComponent<AetherCameraFeel>();
        if (!searched) FindChest();

        RestoreIfUntouched();

        Transform cam = feel != null ? feel.CameraTransform : null;
        float dt = Time.deltaTime;
        if (chest == null || cam == null || dt <= 0f)
            return;

        // How much the view turned this frame (local yaw/pitch)
        Quaternion camRot = cam.rotation;
        Vector2 turnRate = Vector2.zero;
        if (hasLastCam)
        {
            Quaternion delta = Quaternion.Inverse(lastCamRot) * camRot;
            Vector3 e = delta.eulerAngles;
            float yaw = e.y > 180f ? e.y - 360f : e.y;
            float pitch = e.x > 180f ? e.x - 360f : e.x;
            turnRate = new Vector2(yaw, pitch) / dt;
        }
        lastCamRot = camRot;
        hasLastCam = true;

        // Lag behind the turn, then catch up
        Vector2 targetSway = new Vector2(
            Mathf.Clamp(-turnRate.x * 0.006f, -2.2f, 2.2f),
            Mathf.Clamp(-turnRate.y * 0.005f, -1.6f, 1.6f));
        sway = Vector2.Lerp(sway, targetSway, 1f - Mathf.Exp(-14f * dt));

        float speed = feel.HorizontalSpeed;
        bool moving = feel.Grounded && speed > 0.4f;
        bobWeight = Mathf.MoveTowards(bobWeight, moving ? 1f : 0f, dt * 5f);
        bobPhase += speed * dt * (Mathf.PI * 2f / BobStride);
        if (bobPhase > Mathf.PI * 200f) bobPhase -= Mathf.PI * 200f;
        float amp = Mathf.Lerp(0.006f, 0.014f, Mathf.InverseLerp(3f, 7f, speed)) * bobWeight;
        if (feel.IsCrouched()) amp *= 0.5f;

        float targetRoll = -Mathf.Clamp(feel.LateralSpeed / 6f, -1f, 1f) * 2f;
        roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-10f * dt));

        Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * amp, -Mathf.Abs(Mathf.Cos(bobPhase)) * amp * 0.8f, 0f);
        bob.y += feel.LandingDip * 0.6f;

        basePos = chest.position;
        baseRot = chest.rotation;

        Vector3 pivot = cam.position;
        Quaternion swayRot =
            Quaternion.AngleAxis(sway.x, cam.up) *
            Quaternion.AngleAxis(sway.y, cam.right) *
            Quaternion.AngleAxis(roll, cam.forward);

        Vector3 newPos = pivot + swayRot * (basePos - pivot) + cam.rotation * bob;
        chest.SetPositionAndRotation(newPos, swayRot * baseRot);

        appliedPos = chest.position;
        appliedRot = chest.rotation;
        applied = true;
    }
}

/// <summary>Attaches the camera feel to the active player. Also catches players created or swapped mid-scene.</summary>
public class AetherCameraFeelAttacher : MonoBehaviour
{
    private static AetherCameraFeelAttacher instance;
    private float nextCheck;

    public static void Ensure()
    {
        if (instance != null)
            return;
        var go = new GameObject("AETHER_CameraFeel");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AetherCameraFeelAttacher>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;
        nextCheck = Time.unscaledTime + 0.5f;

        Transform player = AetherPlayerCharacter.FindActiveTransform();
        if (player != null)
            AetherCameraFeel.AttachTo(player);
    }
}
