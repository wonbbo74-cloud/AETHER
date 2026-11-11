using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EMPStunReceiver : MonoBehaviour, IEMPStunnable
{
    [Header("Targets")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private MonoBehaviour[] behavioursToDisable;
    [SerializeField] private GameObject empVfxObject;

    [Header("Options")]
    [SerializeField] private bool stopNavMeshAgent = true;
    [SerializeField] private bool freezeAnimator = true;
    [SerializeField] private bool resetVelocityOnStun = true;
    [SerializeField] private Rigidbody[] rigidbodiesToZero;

    private Coroutine empRoutine;
    private bool isEMPActive;
    private bool cachedAgentStopped;
    private float cachedAnimatorSpeed = 1f;

    public bool IsEMPActive => isEMPActive;

    public void ApplyEMP(float duration)
    {
        if (!gameObject.activeInHierarchy) return;

        if (empRoutine != null)
            StopCoroutine(empRoutine);

        empRoutine = StartCoroutine(EMPProcess(duration));
    }

    private IEnumerator EMPProcess(float duration)
    {
        EnterEMPState();

        yield return new WaitForSeconds(duration);

        ExitEMPState();
        empRoutine = null;
    }

    private void EnterEMPState()
    {
        isEMPActive = true;

        if (empVfxObject != null)
            empVfxObject.SetActive(true);

        if (stopNavMeshAgent && agent != null && agent.isOnNavMesh)
        {
            cachedAgentStopped = agent.isStopped;
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (freezeAnimator && animator != null)
        {
            cachedAnimatorSpeed = animator.speed;
            animator.speed = 0f;
        }

        if (resetVelocityOnStun && rigidbodiesToZero != null)
        {
            foreach (Rigidbody rb in rigidbodiesToZero)
            {
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (behavioursToDisable != null)
        {
            foreach (MonoBehaviour behaviour in behavioursToDisable)
            {
                if (behaviour == null) continue;
                if (behaviour == this) continue;
                behaviour.enabled = false;
            }
        }
    }

    private void ExitEMPState()
    {
        if (behavioursToDisable != null)
        {
            foreach (MonoBehaviour behaviour in behavioursToDisable)
            {
                if (behaviour == null) continue;
                if (behaviour == this) continue;
                behaviour.enabled = true;
            }
        }

        if (freezeAnimator && animator != null)
            animator.speed = cachedAnimatorSpeed <= 0f ? 1f : cachedAnimatorSpeed;

        if (stopNavMeshAgent && agent != null && agent.isOnNavMesh)
            agent.isStopped = cachedAgentStopped;

        if (empVfxObject != null)
            empVfxObject.SetActive(false);

        isEMPActive = false;
    }
}