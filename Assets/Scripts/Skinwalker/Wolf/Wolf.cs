using UnityEngine;
using UnityEngine.AI;

// Brain: senses the enemy and switches between Patrol / Flee / Hide.
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

    enum State { Patrol, Flee, Hide }
    State state = State.Patrol;

    WolfEscape escape;
    WolfHide hide;
    RangeCircle circle;
    bool needTarget = true, hideTried;
    float nextThink, fleeUntil;

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
            running = Flee();
        }
        else
        {
            // Lost sight: try once to find a tree
            if (state == State.Flee && !hideTried)
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
        float range = seesEnemy ? seeRange * 1.2f : (enemy.sneaking ? seeRange * 0.4f : seeRange);
        circle.radius = range;

        float dist = Vector3.Distance(transform.position, enemy.transform.position);
        seesEnemy = (dist < range && Sight.Clear(transform, enemy.transform)) || (dist < panicRange && state != State.Hide);
    }

    void Patrol()
    {
        Agent.speed = walkSpeed;
        Vector3 flat = Agent.destination - transform.position;
        flat.y = 0f;

        if (needTarget || flat.magnitude < 1.5f || (!Agent.pathPending && !Agent.hasPath))
            PickTarget();
    }

    void PickTarget()
    {
        needTarget = false;
        Bounds b = floor.bounds;
        b.Expand(-4f);
        Vector3 p = new Vector3(Random.Range(b.min.x, b.max.x), transform.position.y, Random.Range(b.min.z, b.max.z));

        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            Agent.SetDestination(hit.position);
    }

    // Returns true while running (noisy)
    bool Flee()
    {
        float dist = Vector3.Distance(transform.position, EnemyPos);
        bool run = (enemy.seesWolf || dist < dangerRange || !seesEnemy) && !stamina.Exhausted;

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