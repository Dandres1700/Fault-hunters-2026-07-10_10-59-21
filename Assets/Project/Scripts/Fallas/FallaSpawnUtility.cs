using UnityEngine;

public static class FallaSpawnUtility
{
    private static readonly Collider[] OverlapResults = new Collider[16];

    public static bool TryFindValidPosition(
        Vector3 origin,
        float radius,
        float clearanceRadius,
        float minTargetDistance,
        Transform target,
        LayerMask groundLayers,
        LayerMask blockingLayers,
        int attempts,
        out Vector3 position)
    {
        int safeAttempts = Mathf.Max(1, attempts);
        for (int attempt = 0; attempt < safeAttempts; attempt++)
        {
            Vector2 circle = Random.insideUnitCircle * Mathf.Max(0f, radius);
            Vector3 probe = origin + new Vector3(circle.x, 3f, circle.y);
            if (!Physics.Raycast(
                    probe,
                    Vector3.down,
                    out RaycastHit hit,
                    8f,
                    groundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            Vector3 candidate = hit.point + Vector3.up * 0.05f;
            if (target != null && Vector3.Distance(candidate, target.position) < minTargetDistance)
            {
                continue;
            }

            int overlaps = Physics.OverlapSphereNonAlloc(
                candidate + Vector3.up * clearanceRadius,
                clearanceRadius,
                OverlapResults,
                blockingLayers,
                QueryTriggerInteraction.Ignore
            );
            for (int index = 0; index < overlaps; index++)
            {
                OverlapResults[index] = null;
            }
            if (overlaps == 0)
            {
                position = candidate;
                return true;
            }
        }

        position = origin;
        return false;
    }
}

