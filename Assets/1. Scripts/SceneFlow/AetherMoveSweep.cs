using UnityEngine;

/// <summary>
/// Pre-checks that the players' (Shiri and Meka) MovePosition steps don't go through walls.
///
/// Both characters move by MovePosition on a physics body, jumping to the next position every physics step.
/// At a 9 m/s boost that is 18 cm per step. When it lands inside a door or gate under 16–24 cm thick,
/// physics pushes the body out the far side, and the player went straight through. Found in 10/8 checks: 04 bunker gate, doors in the 05 streets.
///
/// Sweep the capsule along the path first and stop just short of a vertical surface, then slide along it.
/// The bottom of the capsule is raised by the step height, so floor seams, steps and ramps (sloped surfaces) don't block.
/// </summary>
public static class AetherMoveSweep
{
    private const float Skin = 0.03f;
    private static readonly RaycastHit[] Hits = new RaycastHit[24];

    /// <param name="stepLift">Height from the feet to ignore. Lower obstacles are left to the existing step/physics handling.</param>
    public static Vector3 ClampHorizontal(Rigidbody body, CapsuleCollider capsule, Vector3 delta, float stepLift)
    {
        if (body == null || capsule == null || !capsule.enabled)
            return delta;

        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        if (flat.sqrMagnitude < 1e-8f)
            return delta;

        Transform t = body.transform;
        Vector3 scale = t.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.95f;
        float height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);

        Vector3 center = body.position + (t.TransformPoint(capsule.center) - t.position);
        Vector3 feet = center - Vector3.up * (height * 0.5f);
        Vector3 p1 = feet + Vector3.up * (radius + stepLift);
        Vector3 p2 = feet + Vector3.up * Mathf.Max(height - radius, radius + stepLift);

        Vector3 moved = Vector3.zero;
        Vector3 remaining = flat;

        for (int iteration = 0; iteration < 2; iteration++)
        {
            float distance = remaining.magnitude;
            if (distance < 1e-5f)
                break;

            Vector3 direction = remaining / distance;
            if (!CastWall(p1 + moved, p2 + moved, radius, direction, distance + Skin, t, out RaycastHit hit))
            {
                moved += remaining;
                break;
            }

            float allowed = Mathf.Max(0f, hit.distance - Skin);
            moved += direction * allowed;

            Vector3 normal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (normal.sqrMagnitude < 1e-4f)
                break;

            // Slide along the wall with what's left.
            remaining = Vector3.ProjectOnPlane(direction * (distance - allowed), normal.normalized);
        }

        return new Vector3(moved.x, delta.y, moved.z);
    }

    private static bool CastWall(Vector3 p1, Vector3 p2, float radius, Vector3 direction, float distance, Transform self, out RaycastHit best)
    {
        best = default;
        int count = Physics.CapsuleCastNonAlloc(p1, p2, radius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(self))
                continue;

            // Already overlapping at the start (distance 0, empty point): leave it to physics to push out.
            if (hit.distance <= 0f && hit.point == Vector3.zero)
                continue;

            // Floors and ramps you can walk on aren't walls.
            if (hit.normal.y > 0.5f)
                continue;

            // Surfaces you're moving away from don't block.
            if (Vector3.Dot(hit.normal, direction) > -0.05f)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit;
            }
        }

        return bestDistance < float.MaxValue;
    }
}
