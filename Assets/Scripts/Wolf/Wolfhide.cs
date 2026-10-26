using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Hiding: runs to a tree, then keeps circling it so the whole body stays
// behind the trunk. If the enemy gets too close, it bolts.
public class WolfHide
{
    static readonly float[] Angles = { 0f, 10f, -10f, 20f, -20f, 35f, -35f, 50f, -50f };
    static readonly float[] Extras = { 0f, 0.4f, 0.8f, 1.2f };   // extra distance from the trunk

    readonly Wolf w;
    readonly WolfEscape escape;
    readonly List<Collider> trees = new List<Collider>();
    readonly float bodyRadius, bodyBottom, bodyMid, bodyTop;   // wolf size, measured from its collider

    Collider tree;
    Vector3 spot;
    float timer, nextRepath;

    public bool Finished { get; private set; }
    public bool Spooked { get; private set; }   // finished because the enemy got too close

    public WolfHide(Wolf wolf, WolfEscape escapePlanner)
    {
        w = wolf;
        escape = escapePlanner;

        // Measure the wolf's body
        Collider body = w.GetComponentInChildren<Collider>();
        if (body)
        {
            Bounds b = body.bounds;
            float y = w.transform.position.y;
            bodyRadius = Mathf.Max(b.extents.x, b.extents.z);
            bodyBottom = b.min.y - y + 0.1f;
            bodyTop = b.max.y - y - 0.1f;
        }
        else
        {
            bodyRadius = w.Agent.radius;
            bodyBottom = 0.1f;
            bodyTop = 1.5f;
        }
        bodyMid = (bodyBottom + bodyTop) * 0.5f;

        // Trees = everything with a NavMeshObstacle (except the characters)
        foreach (NavMeshObstacle o in Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None))
        {
            if (o.GetComponent<Wolf>() || o.GetComponent<SW_Movement>()) continue;
            Collider c = o.GetComponent<Collider>();
            if (c) trees.Add(c);
        }
    }

    // Is something (not the wolf itself) between the enemy's eye and this point?
    bool Blocked(Vector3 eye, Vector3 pt) =>
        Physics.Linecast(eye, pt, out RaycastHit hit) && !hit.collider.transform.IsChildOf(w.transform);

    // Wolf standing at p: are the middle and both sides, at 3 heights, all out of sight?
    bool FullyHidden(Vector3 p, Vector3 enemyPos)
    {
        Vector3 eye = enemyPos + Vector3.up;
        Vector3 toWolf = p - enemyPos;
        toWolf.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, toWolf).normalized * bodyRadius;

        for (int s = -1; s <= 1; s++)
            foreach (float y in new[] { bodyBottom, bodyMid, bodyTop })
                if (!Blocked(eye, p + side * s + Vector3.up * y)) return false;
        return true;
    }

    // Best spot around this tree where the whole wolf is hidden (closest to straight behind it)
    bool FindSpot(Collider col, Vector3 enemyPos, out Vector3 result)
    {
        result = default;
        Bounds b = col.bounds;
        Vector3 away = b.center - enemyPos;
        away.y = 0f;
        away = away.sqrMagnitude < 0.01f ? w.transform.forward : away.normalized;

        float baseDist = Mathf.Max(b.extents.x, b.extents.z) + bodyRadius + w.hideGap;
        float floorY = w.Agent.nextPosition.y;

        foreach (float angle in Angles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;

            foreach (float extra in Extras)
            {
                Vector3 p = b.center + dir * (baseDist + extra);
                p.y = floorY;

                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas)) continue;
                if (!FullyHidden(hit.position, enemyPos)) continue;

                result = hit.position;
                return true;
            }
        }
        return false;
    }

    // Picks the closest valid tree. Returns false if none found.
    public bool Begin()
    {
        Finished = Spooked = false;
        timer = nextRepath = 0f;

        NavMeshAgent agent = w.Agent;
        float bestDist = float.MaxValue;
        NavMeshPath bestPath = null;

        foreach (Collider col in trees)
        {
            if (col == null) continue;
            if (!FindSpot(col, w.EnemyPos, out Vector3 p)) continue;
            if (Vector3.Distance(p, w.EnemyPos) < w.hideMinEnemyDistance) continue;

            float d = Vector3.Distance(w.transform.position, p);
            if (d > w.hideMaxDistance || d >= bestDist) continue;

            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(p, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            if (path.corners.Length < 2 || escape.RaceMargin(path) < 0f) continue;

            bestDist = d;
            bestPath = path;
            tree = col;
            spot = p;
        }

        if (bestPath == null)
        {
            Debug.Log("Wolf: no tree found to hide behind");
            return false;
        }

        agent.isStopped = false;
        agent.SetPath(bestPath);
        WolfEscape.DrawPath(bestPath, Color.cyan);
        return true;
    }

    // Call every frame while hiding. Returns true while running (noisy).
    public bool Tick()
    {
        NavMeshAgent agent = w.Agent;

        // Safety: no tree set, nothing to hide behind
        if (tree == null)
        {
            agent.isStopped = false;
            Finished = true;
            return false;
        }

        Vector3 enemyPos = w.enemy.transform.position;
        float enemyDist = Vector3.Distance(w.transform.position, enemyPos);
        bool enemyNear = enemyDist < w.hideAwareRange;

        // Enemy right on top of us: stop hiding and run
        if (enemyDist < w.hideBreakDistance)
        {
            w.EnemyPos = enemyPos;
            agent.isStopped = false;
            Spooked = Finished = true;
            return false;
        }

        // The wolf hears the enemy nearby, so it tracks them and keeps the tree in between
        if (enemyNear && Time.time >= nextRepath)
        {
            nextRepath = Time.time + 0.2f;
            w.EnemyPos = enemyPos;

            if (FindSpot(tree, enemyPos, out Vector3 p) && (p - spot).sqrMagnitude > 0.25f)
            {
                spot = p;
                agent.isStopped = false;
                agent.SetDestination(spot);
            }
        }

        Vector3 flat = spot - w.transform.position;
        flat.y = 0f;

        // Still moving to / around the tree
        if (flat.magnitude > 0.5f)
        {
            timer = 0f;
            bool run = !w.stamina.Exhausted && flat.magnitude > 4f;   // quiet for the last steps
            agent.speed = run ? w.fleeSpeed : w.avoidSpeed;
            return run;
        }

        // In position: freeze, and leave only once the enemy is gone for a while
        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        if (!enemyNear) timer += Time.deltaTime;
        if (timer >= w.hideTime)
        {
            agent.isStopped = false;
            Finished = true;
        }
        return false;
    }
}