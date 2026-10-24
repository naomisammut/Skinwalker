using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class SW_Movement : MonoBehaviour
{
    public Renderer floor;

    [Tooltip("Leave empty to auto-find every Wolf in the scene on Start")]
    public Wolf[] wolves;

    public float notice = 12f;
    public float viewAngle = 120f;
    public float sneakTime = 4f; // seconds of sneaking before it charges
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

    public bool seesWolf { get; private set; }
    public bool sneaking { get; private set; }
    public bool isWolf { get; private set; } // true once it has eaten a wolf and become one
    public Wolf Prey => prey;                // the wolf it is currently hunting

    NavMeshAgent agent;
    RangeCircle circle;
    Wolf prey; // the wolf it is currently hunting
    Vector3 target;
    Vector3 lastSeen;
    Vector3 suspicionCenter;
    float suspicionUntil;
    bool needTarget = true;
    bool busy;
    bool searching, lookingAround;
    float searchTimer;
    float sneakTimer;
    bool rushing;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        circle = gameObject.AddComponent<RangeCircle>();
        circle.color = Color.red;

        if (wolves == null || wolves.Length == 0)
            wolves = FindObjectsByType<Wolf>(FindObjectsSortMode.None);
    }

    void Update()
    {
        if (busy) return;

        // already a wolf: ignore all wolves, just wander
        if (isWolf)
        {
            Patrol();
            return;
        }

        // closest living wolf (used for the catch check)
        Wolf closest = null;
        float closestDist = float.MaxValue;
        foreach (Wolf w in wolves)
        {
            if (w == null) continue;
            float d = FlatDist(w);
            if (d < closestDist) { closest = w; closestDist = d; }
        }

        if (closest == null)
        {
            seesWolf = sneaking = false;
            Patrol();
            return;
        }

        if (closestDist < catchDistance)
        {
            StartCoroutine(Devour(closest));
            return;
        }

        // --- detection ---
        bool hadSight = seesWolf;
        Wolf seen = null;

        if (prey != null && CanSee(prey, hadSight))
        {
            seen = prey; // keep hunting the same wolf
        }
        else
        {
            // otherwise notice the nearest wolf we can see
            float best = float.MaxValue;
            foreach (Wolf w in wolves)
            {
                if (w == null || w == prey) continue;
                if (!CanSee(w, false)) continue;
                float d = FlatDist(w);
                if (d < best) { best = d; seen = w; }
            }
        }

        seesWolf = seen != null;
        if (seen != prey) { rushing = false; sneakTimer = 0f; }
        if (seen != null) prey = seen;

        circle.radius = seesWolf ? notice * 1.3f : notice;

        // --- hearing: any wolf running close by ---
        Wolf heardWolf = null;
        if (!seesWolf)
        {
            foreach (Wolf w in wolves)
            {
                if (w != null && w.isRunning && FlatDist(w) < hearRange)
                {
                    heardWolf = w;
                    break;
                }
            }
        }

        // --- reaction ---
        if (seesWolf)
        {
            lastSeen = prey.transform.position;
            searching = lookingAround = false;
            suspicionUntil = 0f;

            // sneak first; charge after sneakTime, or immediately if the wolf notices us
            if (!rushing)
            {
                sneakTimer += Time.deltaTime;
                if (prey.seesEnemy || sneakTimer >= sneakTime) rushing = true;
            }

            sneaking = !rushing;
            agent.speed = rushing ? runSpeed : sneakSpeed;
            agent.SetDestination(lastSeen);
            needTarget = true;
        }
        else
        {
            sneaking = false;
            rushing = false;
            sneakTimer = 0f;

            if (heardWolf != null) lastSeen = heardWolf.transform.position;

            if (hadSight || heardWolf != null)
            {
                searching = true;
                lookingAround = false;
                agent.SetDestination(lastSeen);
            }

            if (searching) Search();
            else Patrol();
        }
    }

    float FlatDist(Wolf w)
    {
        Vector3 d = w.transform.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    bool CanSee(Wolf w, bool hadSight)
    {
        Vector3 to = w.transform.position - transform.position;
        to.y = 0f;

        float range = hadSight ? notice * 1.3f : notice;
        if (to.magnitude >= range) return false;
        if (!hadSight && Vector3.Angle(transform.forward, to) >= viewAngle / 2f) return false;

        return Sight.Clear(transform, w.transform);
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

    IEnumerator Devour(Wolf victim)
    {
        busy = true;
        seesWolf = false;
        sneaking = false;
        rushing = false;
        sneakTimer = 0f;
        searching = lookingAround = false;
        suspicionUntil = 0f;
        prey = null;

        agent.ResetPath();
        agent.velocity = Vector3.zero;

        Mesh mesh = victim.GetComponentInChildren<MeshFilter>().sharedMesh;
        Material[] materials = victim.GetComponentInChildren<Renderer>().sharedMaterials;
        Vector3 wolfScale = victim.transform.localScale;
        float baseOffset = victim.GetComponent<NavMeshAgent>().baseOffset;

        Destroy(victim.gameObject);

        Destroy(circle.GetComponent<LineRenderer>());
        Destroy(circle);

        yield return new WaitForSeconds(waitTime);

        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = materials;
        transform.localScale = wolfScale;
        agent.baseOffset = baseOffset;

        isWolf = true; // from now on it ignores every wolf
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