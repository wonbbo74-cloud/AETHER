using UnityEngine;

/// <summary>
/// 총알이 '정확히 어디서' 나왔는지 몇 초 동안 그 자리에 남겨 둔다.
///
/// 총알은 순식간에 지나가서, 눈으로는 총구에서 나왔는지 허공에서 나왔는지 알 수가 없다.
/// 켜 두면 발사한 자리에 초록 구슬이, 날아간 방향으로 초록 선이 남는다.
/// 구슬이 총구에 붙어 있으면 제대로 나온 것이고, 떨어져 있으면 그 거리만큼 어긋난 것이다.
///
/// 진단용이다. 기본은 꺼져 있다. (AETHER/진단/전투 - 발사 자리 표시 켜기/끄기)
/// </summary>
public static class AetherShotOrigin
{
    /// <summary>표시를 켤지. 진단 메뉴에서 바꾼다.</summary>
    public static bool Enabled;

    /// <summary>적이 실제로 쏠 때마다 불린다. (자리, 방향, 쏜 쪽 이름) 진단용.</summary>
    public static event System.Action<Vector3, Vector3, string> Shot;

    /// <summary>표시가 남아 있는 시간(초).</summary>
    private const float Life = 4f;

    private const float BallSize = 0.09f;

    /// <summary>발사 자리를 남긴다.</summary>
    /// <param name="position">총알이 생긴 자리.</param>
    /// <param name="direction">날아간 방향.</param>
    /// <param name="shooter">쏜 쪽 이름. 로그에 적는다.</param>
    public static void Mark(Vector3 position, Vector3 direction, string shooter)
    {
        // 진단 도구가 '실제로 쏜 순간'을 잡을 수 있게 알린다. 표시를 끈 상태에서도 알린다.
        Shot?.Invoke(position, direction, shooter);

        if (!Enabled)
            return;

        GameObject host = new GameObject("발사 자리 표시");
        host.transform.position = position;

        AddBall(host.transform, Vector3.zero, BallSize, new Color(0.2f, 1f, 0.3f));

        // 날아간 방향으로 가는 선. 어느 쪽으로 쐈는지 같이 보여 준다.
        if (direction.sqrMagnitude > 0.0001f)
        {
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, position);
            line.SetPosition(1, position + direction.normalized * 2.5f);
            line.widthMultiplier = 0.012f;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            Shader shader = Shader.Find("Unlit/Color");
            if (shader != null)
                line.material = new Material(shader) { color = new Color(0.2f, 1f, 0.3f, 1f) };

            line.startColor = line.endColor = new Color(0.2f, 1f, 0.3f, 1f);
        }

        Object.Destroy(host, Life);
        Debug.Log($"[발사자리] {shooter} → {position.x:F2}, {position.y:F2}, {position.z:F2}");
    }

    private static void AddBall(Transform parent, Vector3 localPosition, float size, Color color)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "표시";
        ball.transform.SetParent(parent, false);
        ball.transform.localPosition = localPosition;
        ball.transform.localScale = Vector3.one * size;

        Collider collider = ball.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        Renderer renderer = ball.GetComponent<Renderer>();
        Shader shader = Shader.Find("Unlit/Color");
        if (shader != null && renderer != null)
            renderer.material = new Material(shader) { color = color };
    }
}
