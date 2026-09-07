using UnityEngine;

/// <summary>
/// Shared ground-edge queries for enemy movement. Geometry is identified by the
/// Ground tag; the Default layer is never treated as ground implicitly.
/// </summary>
public static class EnemyGroundDetector
{
    public const float ForwardMargin = 0.18f;
    public const float DownwardProbeDistance = 2.0f;
    public const float MaxSafeDrop = 1.75f;
    // Charge runway remains conservative: a 1.5-unit step is not a valid charge path.
    public const float ChargeMaxSafeDrop = 0.75f;
    public const float ProbeStep = 0.5f;
    public const float BodyBottomTolerance = 0.05f;

    public static bool HasGroundAhead(GameObject enemy, float directionX)
    {
        if (enemy == null)
            return false;

        return HasGroundAhead(enemy.transform, directionX);
    }

    public static bool HasGroundAhead(Transform root, float directionX)
    {
        if (!TryGetBodyCollider(root, out Collider2D body))
            return false;

        float sign = Mathf.Sign(directionX);
        if (Mathf.Approximately(sign, 0f))
            return true;

        Bounds bounds = body.bounds;
        float probeX = sign > 0f
            ? bounds.max.x + ForwardMargin
            : bounds.min.x - ForwardMargin;

        return TryGetSafeGroundHit(probeX, bounds.min.y, bounds.min.y + BodyBottomTolerance,
            bounds.min.y - MaxSafeDrop, MaxSafeDrop, DownwardProbeDistance, out _);
    }

    public static bool HasGroundAheadForDistance(
        Transform root,
        float directionX,
        float lookAheadDistance)
    {
        if (!TryGetBodyCollider(root, out Collider2D body))
            return false;

        float sign = Mathf.Sign(directionX);
        if (Mathf.Approximately(sign, 0f) || lookAheadDistance <= 0f)
            return true;

        Bounds bounds = body.bounds;
        float startX = sign > 0f
            ? bounds.max.x + ForwardMargin
            : bounds.min.x - ForwardMargin;
        int sampleCount = Mathf.Clamp(
            Mathf.CeilToInt(lookAheadDistance / ProbeStep),
            1,
            32
        );

        for (int i = 0; i <= sampleCount; i++)
        {
            float distance = lookAheadDistance * i / sampleCount;
            float probeX = startX + sign * distance;
            if (!TryGetSafeGroundHit(probeX, bounds.min.y, bounds.min.y + BodyBottomTolerance,
                bounds.min.y - ChargeMaxSafeDrop, ChargeMaxSafeDrop, DownwardProbeDistance, out _))
                return false;
        }

        return true;
    }

    public static bool TryGetBodyCollider(Transform root, out Collider2D body)
    {
        body = null;
        if (root == null)
            return false;

        Collider2D[] rootColliders = root.GetComponents<Collider2D>();
        Collider2D rootBody = null;
        int rootBodyCount = 0;
        for (int i = 0; i < rootColliders.Length; i++)
        {
            Collider2D candidate = rootColliders[i];
            if (candidate != null && candidate.enabled && !candidate.isTrigger)
            {
                rootBody = candidate;
                rootBodyCount++;
            }
        }

        if (rootBodyCount == 1)
        {
            body = rootBody;
            return true;
        }

        if (rootBodyCount > 1)
            return false;

        Rigidbody2D rigidbody = root.GetComponent<Rigidbody2D>();
        if (rigidbody == null)
            rigidbody = root.GetComponentInChildren<Rigidbody2D>();

        if (rigidbody != null)
        {
            Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger &&
                    candidate.attachedRigidbody == rigidbody)
                {
                    body = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetSafeGroundHit(
        float probeX,
        float bodyBottom,
        float originY,
        float minimumAllowedY,
        float maxDrop,
        float probeDistance,
        out RaycastHit2D groundHit)
    {
        Vector2 origin = new Vector2(probeX, originY);
        float distance = Mathf.Max(probeDistance, originY - minimumAllowedY);
        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            Vector2.down,
            distance,
            Physics2D.DefaultRaycastLayers
        );

        RaycastHit2D closest = default;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            Collider2D collider = hit.collider;
            if (!IsGroundCollider(collider))
                continue;

            float hitY = hit.point.y;
            float drop = bodyBottom - hitY;
            if (drop < -BodyBottomTolerance || drop > maxDrop + BodyBottomTolerance)
                continue;

            if (!found || hit.distance < closest.distance)
            {
                closest = hit;
                found = true;
            }
        }

        groundHit = closest;
        return found;
    }

    public static bool IsGroundCollider(Collider2D collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
            return false;

        return string.Equals(collider.tag, "Ground", System.StringComparison.OrdinalIgnoreCase);
    }

    public static void DrawProbeGizmo(Transform root, float directionX, bool enabled)
    {
        if (!enabled || root == null || !TryGetBodyCollider(root, out Collider2D body))
            return;

        float sign = Mathf.Sign(directionX);
        if (Mathf.Approximately(sign, 0f))
            return;

        Bounds bounds = body.bounds;
        float probeX = sign > 0f
            ? bounds.max.x + ForwardMargin
            : bounds.min.x - ForwardMargin;
        Vector3 start = new Vector3(probeX, bounds.min.y + BodyBottomTolerance, 0f);
        Vector3 end = start + Vector3.down * (DownwardProbeDistance + MaxSafeDrop);
        Gizmos.color = HasGroundAhead(root, sign)
            ? Color.green
            : Color.red;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.04f);
    }
}



