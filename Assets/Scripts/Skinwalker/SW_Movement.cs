using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class SW_Movement : MonoBehaviour
{
    public Renderer floor;
    public Wolf wolf;
    public float notice = 12f;
    public float viewAngle = 120f;
    public float rushDistance = 5f;
    public float catchDistance = 2f;
    public float waitTime = 3f;
    public float walkSpeed = 2f;
    public float sneakSpeed = 1.5f;
    public float runSpeed = 6f;

    [Header("Hearing")]
    public float hearRange = 5f;

    [Header("Search")]
    public float searchSpeed = 3f;
    public float searchTime = 4f;
    public float lookSpeed = 90f;

    [Header("Suspicion")]
    public float suspicionTime = 15f;
    public float suspicionRadius = 10f;

    public Stamina stamina = new Stamina { max = 6f };

    public bool seesWolf { get; private set; }
    public bool sneaking { get; private set; }
    public bool exhausted => stamina.Exhausted;

    NavMeshAgent agent;
    RangeCircle circle;
    Vector3 target;
    Vector3 lastSeen;
    Vector3 suspicionCenter;
    float suspicionUntil;
    bool needTarget = true;
    bool busy;
    bool searching, lookingAround;
    float searchTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        stamina.Init();
        circle = gameObject.AddComponent<RangeCircle>();
        circle.color = Color.red;
    }

    void Update()
    {
        if (busy) return;

        if (wolf == null)
        {
            stamina.Tick(false, Time.deltaTime);
            Patrol();
            return;
        }

        Vector3 toWolf = wolf.transform.position - transform.position;
        toWolf.y = 0f;
        float dist = toWolf.magnitude;

        if (dist < catchDistance)
        {
            StartCoroutine(Devour());
            return;
        }

        float range = seesWolf ? notice * 1.3f : notice;
        circle.radius = range;

        bool hadSight = seesWolf;
        bool inCone = hadSight || Vector3.Angle(transform.forward, toWolf) < viewAngle / 2f;
        seesWolf = dist < range && inCone && Sight.Clear(transform, wolf.transform);

        bool heard = !seesWolf && wolf.isRunning && dist < hearRange;

        bool running = false;

        if (seesWolf)
        {
            lastSeen = wolf.transform.position;
            searching = lookingAround = false;
            suspicionUntil = 0f;

            bool rush = wolf.seesEnemy || dist < rushDistance;
            running = rush && !stamina.Exhausted;
            sneaking = !rush;

            agent.speed = running ? runSpeed : (rush ? walkSpeed : sneakSpeed);
            agent.SetDestination(lastSeen);
            needTarget = true;
        }
        else
        {
            sneaking = false;

            if (heard) lastSeen = wolf.transform.position;

            if (hadSight || heard)
            {
                searching = true;
                lookingAround = false;
                agent.SetDestination(lastSeen);
            }

            if (searching) Search();
            else Patrol();
        }

        stamina.Tick(running, Time.deltaTime);
    }

    void Search()
    {
        agent.speed = searchSpeed;

        if (!lookingAround)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
            {
                lookingAround = true;
                searchTimer = 0f;
                agent.ResetPath();
            }
            return;
        }

        searchTimer += Time.deltaTime;
        transform.Rotate(0f, lookSpeed * Time.deltaTime, 0f);

        if (searchTimer >= searchTime)
        {
            searching = lookingAround = false;
            needTarget = true;

            suspicionCenter = lastSeen;
            suspicionUntil = Time.time + suspicionTime;
        }
    }

    IEnumerator Devour()
    {
        busy = true;
        seesWolf = false;
        sneaking = false;
        searching = lookingAround = false;
        suspicionUntil = 0f;

        agent.ResetPath();
        agent.velocity = Vector3.zero;

        Mesh mesh = wolf.GetComponentInChildren<MeshFilter>().sharedMesh;
        Material[] materials = wolf.GetComponentInChildren<Renderer>().sharedMaterials;
        Vector3 wolfScale = wolf.transform.localScale;
        float baseOffset = wolf.GetComponent<NavMeshAgent>().baseOffset;

        Destroy(wolf.gameObject);
        wolf = null;

        Destroy(circle.GetComponent<LineRenderer>());
        Destroy(circle);

        yield return new WaitForSeconds(waitTime);

        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
        transform.localScale = wolfScale;
        agent.baseOffset = baseOffset;

        needTarget = true;
        busy = false;
    }

    void Patrol()
    {
        agent.speed = walkSpeed;

        Vector3 flat = target - transform.position;
        flat.y = 0f;

        if (needTarget || flat.magnitude < 1.5f || (!agent.pathPending && !agent.hasPath))
            PickTarget();
    }

    void PickTarget()
    {
        needTarget = false;
        Bounds b = floor.bounds;
        b.Expand(-4f);

        Vector3 p;
        if (Time.time < suspicionUntil)
        {
            Vector2 o = Random.insideUnitCircle * suspicionRadius;
            p = new Vector3(
                Mathf.Clamp(suspicionCenter.x + o.x, b.min.x, b.max.x),
                transform.position.y,
                Mathf.Clamp(suspicionCenter.z + o.y, b.min.z, b.max.z));
        }
        else
        {
            p = new Vector3(Random.Range(b.min.x, b.max.x), transform.position.y, Random.Range(b.min.z, b.max.z));
        }

        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 10f, NavMesh.AllAreas))
        {
            target = hit.position;
            agent.SetDestination(target);
        }
    }
}