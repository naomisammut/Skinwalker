using UnityEngine;
using UnityEngine.AI;

// Escape planning: samples paths, scores them, picks the best one.
public class WolfEscape
{
    readonly Wolf w;
    public WolfEscape(Wolf wolf) { w = wolf; }

    Transform T => w.transform;
    NavMeshAgent Agent => w.Agent;
    Vector3 EnemyPos => w.EnemyPos;

    public void Plan(bool close)
    {
        NavMeshPath best = null;
        float bestScore = float.MinValue;

        // keep the current path as a candidate (small bonus avoids jitter)
        if (Agent.hasPath && Agent.remainingDistance > 4f)
            Consider(Agent.path, close, 1.5f, ref best, ref bestScore);

        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(10f, 18f);

            if (!NavMesh.SamplePosition(T.position + dir, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;

            NavMeshPath path = new NavMeshPath();
            if (!Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            Consider(path, close, 0f, ref best, ref bestScore);
        }

        if (best == null) return;
        DrawPath(best, Color.green);
        Agent.SetPath(best);
    }

    void Consider(NavMeshPath path, bool close, float bonus, ref NavMeshPath best, ref float bestScore)
    {
        if (path.corners.Length < 2) return;

        float margin = RaceMargin(path);
        bool bad = close && HeadsTowardEnemy(path);
        bool safe = margin > w.safetyTime && !bad;

        float score = (safe ? 1000f : 0f) + margin * 10f + bonus + EndQuality(path) + HidingBonus(path);
        if (bad) score -= 500f;

        DrawPath(path, safe ? Color.yellow : Color.red);
        if (score > bestScore) { bestScore = score; best = path; }
    }

    // Seconds the wolf beats the enemy to the tightest point on the path (negative = gets caught)
    public float RaceMargin(NavMeshPath path)
    {
        Vector3[] c = path.corners;
        Vector3 first = c[1] - c[0];
        first.y = 0f;

        float turnTime = Vector3.Angle(T.forward, first) / Mathf.Max(Agent.angularSpeed, 1f);
        float enemySpeed = w.enemy.runSpeed;
        float walked = 0f, margin = float.MaxValue;

        for (int i = 1; i < c.Length; i++)
        {
            float len = Vector3.Distance(c[i - 1], c[i]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len));

            for (int s = 1; s <= steps; s++)
            {
                Vector3 p = Vector3.Lerp(c[i - 1], c[i], s / (float)steps);
                walked += len / steps;

                float wolfTime = turnTime + walked / w.RunSpeed;
                float enemyDist = Mathf.Max(0f, Vector3.Distance(p, EnemyPos) - 1.5f);
                margin = Mathf.Min(margin, enemyDist / enemySpeed - wolfTime);
            }
        }
        return margin;
    }

    bool HeadsTowardEnemy(NavMeshPath path)
    {
        Vector3 first = path.corners[1] - path.corners[0];
        Vector3 toEnemy = EnemyPos - T.position;
        first.y = 0f; toEnemy.y = 0f;
        return Vector3.Dot(first.normalized, toEnemy.normalized) > 0.2f;
    }

    // Far from the enemy and not stuck against the map edge
    float EndQuality(NavMeshPath path)
    {
        Vector3 end = path.corners[path.corners.Length - 1];
        Bounds b = w.floor.bounds;
        float edge = Mathf.Min(end.x - b.min.x, b.max.x - end.x, end.z - b.min.z, b.max.z - end.z);
        return Vector3.Distance(end, EnemyPos) + Mathf.Min(edge, 5f);
    }

    float HidingBonus(NavMeshPath path)
    {
        Vector3 end = path.corners[path.corners.Length - 1] + Vector3.up;
        return Physics.Linecast(EnemyPos, end) ? 8f : 0f;
    }

    public static void DrawPath(NavMeshPath path, Color color)
    {
        Vector3[] c = path.corners;
        for (int i = 0; i < c.Length - 1; i++)
            Debug.DrawLine(c[i] + Vector3.up * 0.2f, c[i + 1] + Vector3.up * 0.2f, color, 0.4f);
    }
}