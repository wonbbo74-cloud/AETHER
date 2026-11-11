using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneObserverCombat : DroneCombatBase
{
    [Header("Detection")]
    [SerializeField] private Transform detectOrigin;
    [SerializeField] private Light detectionSpotLight;
    [SerializeField] private float detectRadius = 28f;
    [SerializeField, Range(1f, 179f)] private float detectAngle = 70f;
    [SerializeField] private float targetHeightOffset = 1f;
    [SerializeField] private bool requireSpotConeCheck = true;

    [Header("Alert Broadcast")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float alertCallRadius = 30f;
    [SerializeField] private float alertRepeatInterval = 1.5f;
    [SerializeField] private float searchDuration = 5f;
    [SerializeField] private float alertOffsetRadius = 2.5f;

    [Header("Alert Presentation")]
    [SerializeField] private AudioClip alertSfx;
    [SerializeField, Range(0f, 1f)] private float alertSfxVolume = 1f;
    [SerializeField] private GameObject alertMark;
    [SerializeField] private float alertMarkDuration = 1f;
    [SerializeField] private bool showAlertMarkInGameplay = false;

    [Header("Debug")]
    [SerializeField] private bool drawDetectionGizmos = true;

    private float nextAlertTime;
    private bool detectedThisFrame;
    private Vector3 lastDetectedPlayerPosition;
    private Vector3 lockedAlertPosition;
    private Coroutine alertMarkRoutine;
    private readonly HashSet<IObserverAlertReceiver> receivers = new HashSet<IObserverAlertReceiver>();

    public override void OnEnterCombat()
    {
        if (owner != null)
            lockedAlertPosition = owner.transform.position;

        nextAlertTime = Time.time + Random.Range(0f, Mathf.Min(0.8f, alertRepeatInterval));
    }

    public override bool TryOverrideCombatDetection(out bool detected)
    {
        detected = TryDetectPlayer(out Vector3 detectedPlayerPosition);
        detectedThisFrame = detected;

        if (detected)
            lastDetectedPlayerPosition = detectedPlayerPosition;

        return true;
    }

    public override void TickCombat()
    {
        if (owner == null || owner.Player == null || !detectedThisFrame) return;

        owner.MoveToPoint(lockedAlertPosition, lastDetectedPlayerPosition);
        owner.FaceTo(lastDetectedPlayerPosition);

        if (Time.time < nextAlertTime)
            return;

        nextAlertTime = Time.time + Mathf.Max(0.1f, alertRepeatInterval);
        BroadcastAlert(lastDetectedPlayerPosition);
    }

    private bool TryDetectPlayer(out Vector3 detectedPlayerPosition)
    {
        detectedPlayerPosition = Vector3.zero;

        if (owner == null || owner.Player == null)
            return false;

        Transform originTransform = detectOrigin != null ? detectOrigin : transform;
        Vector3 origin = originTransform.position;
        Vector3 target = owner.Player.position + Vector3.up * targetHeightOffset;
        Vector3 toTarget = target - origin;
        float distance = toTarget.magnitude;

        if (distance <= 0.001f || distance > GetEffectiveDetectRadius())
            return false;

        Vector3 direction = toTarget / distance;
        if (requireSpotConeCheck)
        {
            Vector3 forward = GetDetectionForward(originTransform);
            if (Vector3.Angle(forward, direction) > GetEffectiveDetectAngle() * 0.5f)
                return false;
        }

        if (!owner.HasLineOfSight(origin, target))
            return false;

        detectedPlayerPosition = owner.Player.position;
        return true;
    }

    private void BroadcastAlert(Vector3 playerPosition)
    {
        ShowAlertMark();
        PlayAlertSfx();
        receivers.Clear();

        Collider[] hits = Physics.OverlapSphere(
            owner.transform.position,
            alertCallRadius,
            enemyMask,
            QueryTriggerInteraction.Collide);

        CollectReceiversFromHits(hits, playerPosition);

        // Some older scenes still have Enemy-tagged objects on Default layer.
        // Keep the observer useful even before the scene layers are normalized.
        if (receivers.Count == 0)
        {
            hits = Physics.OverlapSphere(
                owner.transform.position,
                alertCallRadius,
                ~0,
                QueryTriggerInteraction.Collide);

            CollectReceiversFromHits(hits, playerPosition);
        }
    }

    private void CollectReceiversFromHits(Collider[] hits, Vector3 playerPosition)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            Transform hitTransform = hits[i].transform;
            if (!BelongsToEnemy(hitTransform)) continue;

            IObserverAlertReceiver receiver = hitTransform.GetComponentInParent<IObserverAlertReceiver>();
            if (receiver == null) continue;
            Component receiverComponent = receiver as Component;
            if (receiverComponent == null) continue;
            if (receiverComponent.transform.root == transform.root) continue;
            if (!receivers.Add(receiver)) continue;

            receiver.OnObserverAlert(GetAlertPositionWithOffset(playerPosition), searchDuration);
        }
    }

    private static bool BelongsToEnemy(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
        {
            if (current.CompareTag("Enemy"))
                return true;
        }

        return false;
    }

    private Vector3 GetAlertPositionWithOffset(Vector3 center)
    {
        Vector2 offset2D = Random.insideUnitCircle * alertOffsetRadius;
        return center + new Vector3(offset2D.x, 0f, offset2D.y);
    }

    private float GetEffectiveDetectRadius()
    {
        if (detectionSpotLight != null)
            return detectionSpotLight.range;

        return detectRadius;
    }

    private float GetEffectiveDetectAngle()
    {
        if (detectionSpotLight != null)
            return detectionSpotLight.spotAngle;

        return detectAngle;
    }

    private Vector3 GetDetectionForward(Transform originTransform)
    {
        if (detectionSpotLight != null)
            return detectionSpotLight.transform.forward;

        return originTransform.forward;
    }

    private void PlayAlertSfx()
    {
        if (alertSfx == null) return;

        AudioSource.PlayClipAtPoint(alertSfx, transform.position, Mathf.Clamp01(alertSfxVolume) * AetherAudio.Sfx);   // 효과음 설정을 따른다
    }

    private void ShowAlertMark()
    {
        if (alertMark == null) return;
        if (!showAlertMarkInGameplay)
        {
            alertMark.SetActive(false);
            return;
        }

        if (alertMarkRoutine != null)
            StopCoroutine(alertMarkRoutine);

        alertMarkRoutine = StartCoroutine(AlertMarkRoutine());
    }

    private IEnumerator AlertMarkRoutine()
    {
        alertMark.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(0.05f, alertMarkDuration));
        alertMark.SetActive(false);
        alertMarkRoutine = null;
    }

    private void OnDisable()
    {
        if (alertMarkRoutine != null)
        {
            StopCoroutine(alertMarkRoutine);
            alertMarkRoutine = null;
        }

        if (alertMark != null)
            alertMark.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDetectionGizmos) return;

        Transform originTransform = detectOrigin != null ? detectOrigin : transform;
        Vector3 origin = originTransform.position;
        float radius = GetEffectiveDetectRadius();

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(origin, radius);

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, alertCallRadius);

        if (requireSpotConeCheck)
        {
            Vector3 forward = GetDetectionForward(originTransform).normalized;
            Quaternion leftRot = Quaternion.AngleAxis(-GetEffectiveDetectAngle() * 0.5f, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(GetEffectiveDetectAngle() * 0.5f, Vector3.up);

            Gizmos.color = new Color(1f, 1f, 0.4f, 0.9f);
            Gizmos.DrawLine(origin, origin + leftRot * forward * radius);
            Gizmos.DrawLine(origin, origin + forward * radius);
            Gizmos.DrawLine(origin, origin + rightRot * forward * radius);
        }

        if (lastDetectedPlayerPosition != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastDetectedPlayerPosition, 0.4f);
            Gizmos.DrawLine(origin, lastDetectedPlayerPosition);
        }
    }
}
