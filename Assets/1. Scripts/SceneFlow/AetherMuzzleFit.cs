using UnityEngine;

/// <summary>
/// 총알이 나오는 자리를 모델에 맞춰 잡아 준다.
///
/// 적마다 총구 위치를 숫자로 박아 두었더니, 모델이 바뀔 때마다 엉뚱한 데서 총알이 나왔다.
/// (터렛은 포신 한가운데, 드론은 몸체에서 1.5m 떨어진 허공에서 쐈다.)
/// 메시의 실제 끝을 재서 거기에 놓는다.
/// </summary>
public static class AetherMuzzleFit
{
    /// <summary>
    /// 이 오브젝트에 달린 메시가 자기 기준 앞쪽(+Z)으로 얼마나 뻗어 있는지 잰다.
    /// 포신 끝에 총구를 놓을 때 쓴다.
    /// </summary>
    /// <param name="pivot">포신처럼 앞을 보고 있는 오브젝트.</param>
    /// <param name="fallback">메시를 못 찾았을 때 쓸 값.</param>
    public static float ForwardReach(Transform pivot, float fallback)
    {
        if (pivot == null)
            return fallback;

        float farthest = 0f;
        bool found = false;

        foreach (MeshFilter filter in pivot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || IsCollisionOnly(filter.name))
                continue;

            Reach(filter.sharedMesh.bounds, filter.transform, pivot, ref farthest, ref found);
        }

        foreach (SkinnedMeshRenderer skinned in pivot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned == null || skinned.sharedMesh == null || IsCollisionOnly(skinned.name))
                continue;

            Reach(skinned.sharedMesh.bounds, skinned.transform, pivot, ref farthest, ref found);
        }

        return found && farthest > 0.01f ? farthest : fallback;
    }

    /// <summary>
    /// 발사 지점을 몸체 메시의 앞 끝으로 옮긴다.
    /// 드론처럼 총 모델이 따로 없는 적에게 쓴다.
    /// </summary>
    /// <param name="firePoint">옮길 발사 지점. 몸체의 자식이어야 한다.</param>
    /// <param name="body">몸체. 보통 적의 뿌리.</param>
    /// <param name="margin">메시 끝에서 얼마나 더 앞에 둘지(m).</param>
    public static void SnapToNose(Transform firePoint, Transform body, float margin = 0.08f)
    {
        if (firePoint == null || body == null)
            return;

        // 발사 지점 자신이 계산에 끼어들지 않게 잠시 떼어 둔다.
        Transform parent = firePoint.parent;
        firePoint.SetParent(null, true);

        float reach = ForwardReach(body, -1f);
        float down = LowestPoint(body);

        firePoint.SetParent(parent != null ? parent : body, true);

        if (reach <= 0f)
            return;

        // 몸체 기준 좌표로 놓는다. 드론은 총이 배 밑에 달려 있으므로 아래쪽 끝 가까이 둔다.
        Vector3 local = new Vector3(0f, down * 0.9f, reach + margin);
        firePoint.position = body.TransformPoint(local);
        firePoint.rotation = body.rotation;
    }

    /// <summary>
    /// 총이 스킨메시로 붙어 있는 적의 총구를 찾아 발사 지점을 거기에 옮긴다.
    ///
    /// Robot_Soldier 류는 총이 따로 오브젝트가 아니라 뼈 하나에 묶인 스킨메시다.
    /// 그래서 발사 지점이 몸통에 고정되어 있었고, 팔을 들어 조준해도 총알은
    /// 가슴 앞 허공에서 나왔다. 총 메시의 앞 끝을 재서 그 뼈에 매단다.
    /// </summary>
    /// <returns>옮겼으면 true.</returns>
    public static bool AttachToSkinnedWeapon(Transform root, Transform firePoint)
    {
        if (root == null || firePoint == null)
            return false;

        SkinnedMeshRenderer weapon = FindSkinnedWeapon(root);
        if (weapon == null || weapon.sharedMesh == null || weapon.bones == null || weapon.bones.Length == 0)
            return false;

        Transform bone = weapon.bones[0];
        if (bone == null)
            return false;

        Matrix4x4[] bindposes = weapon.sharedMesh.bindposes;
        if (bindposes == null || bindposes.Length == 0)
            return false;

        // 뼈 하나짜리 스킨메시는 이 행렬 하나로 메시 좌표가 월드 좌표가 된다.
        Matrix4x4 skin = bone.localToWorldMatrix * bindposes[0];

        Bounds bounds = weapon.sharedMesh.bounds;
        Vector3 aim = root.forward;
        Vector3 tip = Vector3.zero;
        float best = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + new Vector3(
                (i & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                (i & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                (i & 4) == 0 ? -bounds.extents.z : bounds.extents.z);

            Vector3 world = skin.MultiplyPoint3x4(corner);
            float along = Vector3.Dot(world, aim);

            if (along > best)
            {
                best = along;
                tip = world;
            }
        }

        // 총구는 모서리가 아니라 총열 가운데다. 바깥으로 치우친 만큼 총 중심 쪽으로 당긴다.
        Vector3 centre = skin.MultiplyPoint3x4(bounds.center);
        Vector3 forwardOnly = Vector3.Project(tip - centre, aim);

        firePoint.SetParent(bone, true);
        firePoint.position = centre + forwardOnly;
        firePoint.rotation = Quaternion.LookRotation(aim, Vector3.up);
        return true;
    }

    /// <summary>
    /// 총 오브젝트에 붙은 발사 지점을 총열 끝에 맞춘다.
    ///
    /// Robot.prefab 의 Firepos 는 총열 끝보다 9cm 앞 허공에 있어서, 쏘는 순간을
    /// 가까이서 보면 총구와 총알 사이가 떠 보였다. 총 메시의 가장 긴 축을 총열로 보고
    /// 그 끝으로 당긴다. 옆으로 어긋난 정도는 건드리지 않는다.
    /// </summary>
    /// <param name="firePoint">총 오브젝트(발사 지점의 부모)의 자식인 발사 지점.</param>
    /// <param name="margin">총열 끝에서 얼마나 더 앞에 둘지(m).</param>
    /// <returns>옮겼으면 true.</returns>
    public static bool SnapToBarrelTip(Transform firePoint, float margin = 0.01f)
    {
        if (firePoint == null || firePoint.parent == null)
            return false;

        Transform gun = firePoint.parent;
        MeshFilter barrel = null;
        float biggest = 0f;

        foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || IsCollisionOnly(filter.name) || filter.transform.IsChildOf(firePoint))
                continue;

            float size = filter.sharedMesh.bounds.size.sqrMagnitude;
            if (size > biggest)
            {
                biggest = size;
                barrel = filter;
            }
        }

        if (barrel == null)
            return false;

        Bounds bounds = barrel.sharedMesh.bounds;
        Vector3 size3 = bounds.size;
        Vector3 localAxis = size3.x >= size3.y && size3.x >= size3.z ? Vector3.right
            : size3.y >= size3.z ? Vector3.up : Vector3.forward;

        Transform mesh = barrel.transform;
        Vector3 centre = mesh.TransformPoint(bounds.center);
        Vector3 tip = mesh.TransformPoint(bounds.center + Vector3.Scale(localAxis, bounds.extents));
        Vector3 axis = tip - centre;

        // 총구 쪽 끝이 어느 쪽인지는 원래 발사 지점이 가리킨다.
        if (Vector3.Dot(firePoint.position - centre, axis) < 0f)
        {
            tip = centre - axis;
            axis = -axis;
        }

        if (axis.sqrMagnitude < 0.0001f)
            return false;

        Vector3 direction = axis.normalized;
        float gap = Vector3.Dot(firePoint.position - tip, direction);
        firePoint.position -= direction * (gap - margin);
        return true;
    }

    private static SkinnedMeshRenderer FindSkinnedWeapon(Transform root)
    {
        string[] keywords = { "rifle", "gun", "weapon", "cannon" };

        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null)
                continue;

            string lower = renderer.name.ToLowerInvariant();

            for (int i = 0; i < keywords.Length; i++)
            {
                if (lower.Contains(keywords[i]))
                    return renderer;
            }
        }

        return null;
    }

    private static void Reach(Bounds meshBounds, Transform owner, Transform pivot, ref float farthest, ref bool found)
    {
        Vector3 center = meshBounds.center;
        Vector3 extents = meshBounds.extents;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);

            float z = pivot.InverseTransformPoint(owner.TransformPoint(corner)).z;

            if (!found || z > farthest)
            {
                farthest = z;
                found = true;
            }
        }
    }

    /// <summary>몸체 메시가 자기 기준 아래로 얼마나 내려가는지(음수).</summary>
    private static float LowestPoint(Transform body)
    {
        float lowest = 0f;
        bool found = false;

        foreach (MeshFilter filter in body.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || IsCollisionOnly(filter.name))
                continue;

            Lowest(filter.sharedMesh.bounds, filter.transform, body, ref lowest, ref found);
        }

        // 드론 몸체는 스킨 메시라 MeshFilter 가 없다. 이쪽도 같이 본다.
        foreach (SkinnedMeshRenderer skinned in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned == null || skinned.sharedMesh == null || IsCollisionOnly(skinned.name))
                continue;

            Lowest(skinned.sharedMesh.bounds, skinned.transform, body, ref lowest, ref found);
        }

        return lowest;
    }

    private static void Lowest(Bounds meshBounds, Transform owner, Transform body, ref float lowest, ref bool found)
    {
        Vector3 center = meshBounds.center;
        Vector3 extents = meshBounds.extents;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);

            float y = body.InverseTransformPoint(owner.TransformPoint(corner)).y;

            if (!found || y < lowest)
            {
                lowest = y;
                found = true;
            }
        }
    }

    /// <summary>UCX_ 로 시작하는 것은 눈에 안 보이는 충돌용 메시다. 크기 계산에서 뺀다.</summary>
    private static bool IsCollisionOnly(string name)
    {
        return name.StartsWith("UCX", System.StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("UBX", System.StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("USP", System.StringComparison.OrdinalIgnoreCase);
    }
}
