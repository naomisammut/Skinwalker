using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Brain: senses the enemy and switches between Patrol / Flee / Hide.
// Wolves roam as a pack (followers stay near a leader), warn each other,
// and avoid the skinwalker for good once it has eaten one of them.
public class Wolf : MonoBehaviour
{
    public Renderer floor;
    public SW_Movement enemy;
    public float seeRange = 9f;
    public float dangerRange = 7f;
    public float panicRange = 3.5f;   // sensed even without line of sight
    public float walkSpeed = 2f;
    public float avoidSpeed = 2.5f;
    public float fleeSpeed = 4.5f;
    public float safetyTime = 0.5f;
    public float keepRunningTime = 3f;
    public Stamina stamina = new Stamina { max = 10f };

    [Header("Group")]
    public float groupRadius = 6f;          // followers wander within this distance of the leader
    public float leaderSpeedFactor = 0.7f;  // leader walks slower so the pack keeps up
    public float alertRange = 25f;          // pack mates this close are warned when one sees the enemy

    [Header("After the skinwalker ate a wolf")]
    public float avoidRange = 14f;          // they remember it and keep at least this far away

    [Header("Hiding")]
    public float hideTime = 5f;
    public float hideMaxDistance = 30f;
    public float hideMinEnemyDistance = 8f;
    public float hideGap = 0.15f;             // gap between trunk and wolf body
    public float hideAwareRange = 15f;        // wolf hears the enemy this close and circles the tree
    public float hideBreakDistance = 2.5f;    // enemy this close: stop hiding and run

    public bool seesEnemy { get; private set; }
    public bool isRunning { get; private set; }   // the enemy can hear this

    // shared with WolfEscape / WolfHide
    public NavMeshAgent Agent { get; private set; }
    public Vector3 EnemyPos { get; set; }
    public float RunSpeed => stamina.Exhausted ? avoidSpeed : fleeSpeed;

    // every living wolf; the first one is the pack leader
    public static readonly List<Wolf> All = new List<Wolf>();
    static Wolf Leader => All.Count > 0 ? All[0] : null;

    enum State { Patrol, Flee, Hide }
    State state = State.Patrol;

    WolfEscape escape;
    WolfHide hide;
    RangeCircle circle;
    bool needTarget = true, hideTried;
    float nextThink, fleeUntil;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start() => Init();

    // Safe to call more than once (also covers script reloads in Play mode)
    void Init()
    {
        Agent = GetComponent<NavMeshAgent>();
        Agent.autoBraking = false;
        Agent.stoppingDistance = 0f;

        if (escape == null && hide == null) stamina.Init();

        circle = GetComponent<RangeCircle>();
        if (circle == null) circle = gameObject.AddComponent<RangeCircle>();
        circle.color = Color.yellow;

        escape = new WolfEscape(this);
        hide = new WolfHide(this, escape);
        state = State.Patrol;
        needTarget = true;
    }

    void Update()
    {
        if (hide == null || escape == null || Agent == null) Init();

        UpdateSight();
        bool running = false;

        if (seesEnemy)
        {
            EnemyPos = enemy.transform.position;
            fleeUntil = Time.time + keepRunningTime;
            hideTried = false;
            Agent.isStopped = false;
            state = State.Flee;
            AlertPack();
            running = Flee();
        }
        else
        {
            // Lost sight: try once to find a tree (not when the skinwalker is already disguised as a wolf)
            if (state == State.Flee && !hideTried && !enemy.isWolf)
            {
                hideTried = true;
                if (hide.Begin()) state = State.Hide;
            }

            switch (state)
            {
                case State.Hide:
                    running = hide.Tick();
                    if (hide.Finished)
                    {
                        needTarget = true;
                        if (hide.Spooked) { fleeUntil = Time.time + keepRunningTime; nextThink = 0f; state = State.Flee; }
                        else state = State.Patrol;
                    }
                    break;
                case State.Flee:
                    if (Time.time < fleeUntil) running = Flee();
                    else state = State.Patrol;
                    break;
                default:
                    Patrol();
                    break;
            }
        }

        isRunning = running;
        stamina.Tick(running, Time.deltaTime);
    }

    void UpdateSight()
    {
        float dist = Vector3.Distance(transform.position, enemy.transform.position);

        // It ate one of us: everyone remembers it and keeps away, no line of sight needed
        if (enemy.isWolf)
        {
            circle.radius = avoidRange;
            seesEnemy = dist < avoidRange;
            return;
        }

        float range = seesEnemy ? seeRange * 1.2f : (enemy.sneaking ? seeRange * 0.4f : seeRange);
        circle.radius = range;

        seesEnemy = (dist < range && Sight.Clear(transform, enemy.transform)) || (dist < panicRange && state != State.Hide);
    }

    // --- pack behaviour ---

    // Warn nearby pack mates so the whole group runs together
    void AlertPack()
    {
        foreach (Wolf other in All)
        {
            if (other == this) continue;
            if (Vector3.Distance(transform.position, other.transform.position) < alertRange)
                other.Alarm(EnemyPos);
        }
    }

    public void Alarm(Vector3 enemyPos)
    {
        if (Agent == null) return;       // not initialised yet
        if (state == State.Hide) return; // already tucked away behind a tree

        EnemyPos = enemyPos;
        fleeUntil = Time.time + keepRunningTime;

        if (state != State.Flee)
        {
            state = State.Flee;
            nextThink = 0f;
            Agent.isStopped = false;
        }
    }

    // Small score penalty for escape routes that end far from the pack (used by WolfEscape)
    public float GroupPull(Vector3 p)
    {
        if (All.Count < 2) return 0f;

        Vector3 center = Vector3.zero;
        foreach (Wolf o in All) center += o.transform.position;
        center /= All.Count;

        return -Mathf.Min(Vector3.Distance(p, center), 30f) * 0.3f;
    }

    // --- patrol ---

    void Patrol()
    {
        Wolf leader = Leader;
        bool follower = leader != null && leader != this;

        Agent.speed = (!follower && All.Count > 1) ? walkSpeed * leaderSpeedFactor : walkSpeed;

        Vector3 flat = Agent.destination - transform.position;
        flat.y = 0f;

        // a follower re-targets when the leader has wandered off
        bool leaderMoved = follower && Vector3.Distance(Agent.destination, leader.transform.position) > groupRadius * 1.5f;

        if (needTarget || leaderMoved || flat.magnitude < 1.5f || (!Agent.pathPending && !Agent.hasPath))
            PickTarget();
    }

    void PickTarget()
    {
        needTarget = false;
        Bounds b = floor.bounds;
        b.Expand(-4f);

        Wolf leader = Leader;
        bool follower = leader != null && leader != this;

        for (int i = 0; i < 8; i++)
        {
            Vector3 p;
            if (follower)
            {
                // stay near the leader
                Vector2 o = Random.insideUnitCircle * groupRadius;
                p = new Vector3(
                    Mathf.Clamp(leader.transform.position.x + o.x, b.min.x, b.max.x),
                    transform.position.y,
                    Mathf.Clamp(leader.transform.position.z + o.y, b.min.z, b.max.z));
            }
            else
            {
                p = new Vector3(Random.Range(b.min.x, b.max.x), transform.position.y, Random.Range(b.min.z, b.max.z));
            }

            // never wander toward the skinwalker once it has eaten a wolf
            if (enemy.isWolf && Vector3.Distance(p, enemy.transform.position) < avoidRange) continue;

            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                Agent.SetDestination(hit.position);
                return;
            }
        }
    }

    // Returns true while running (noisy)
    bool Flee()
    {
        float dist = Vector3.Distance(transform.position, EnemyPos);
        bool hunted = enemy.seesWolf && enemy.Prey == this;
        bool run = (hunted || dist < dangerRange || !seesEnemy) && !stamina.Exhausted;

        Agent.speed = run ? fleeSpeed : avoidSpeed;
        needTarget = true;

        bool nearlyDone = !Agent.hasPath || Agent.remainingDistance < 3f;
        if (Time.time >= nextThink || nearlyDone)
        {
            nextThink = Time.time + 0.4f;
            escape.Plan(dist < dangerRange);
        }
        return run;
    }
}