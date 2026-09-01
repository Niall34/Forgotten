using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

// controls the monster - only the master client actually runs this logic and moves it
// everyone else just watches it move through the normal photon position sync (add a
// PhotonTransformView to the prefab for that, same as the player)
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviourPun
{
    [Header("Detection")]
    public float chaseRange = 12f; // how close a player needs to be before the chase starts
    public float giveUpRange = 22f; // how far away before the monster loses interest mid chase
    public float searchTime = 8f; // how long it searches the last known spot before giving up

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5.5f;

    private enum MonsterState
    {
        Patrol,
        Chase,
        Search
    }

    private NavMeshAgent agent;
    private Animator animator; // drives Idle/Walk/Run on the Monster's Animator Controller
    private PlayerController currentTarget;
    private Vector3 lastKnownPosition;
    private float searchTimer = 0f;
    private MonsterState state = MonsterState.Patrol;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Start() // snap onto the NavMesh right away in case the spawn point is slightly off the baked surface
    {
        if (agent.isOnNavMesh == false)
        {
            NavMeshHit hit;
            bool foundSpot = NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas);
            if (foundSpot)
            {
                agent.Warp(hit.position);
            }
        }
    }

    private void Update()
    {
        // only the master client drives the ai, everyone else is just along for the ride
        if (PhotonNetwork.IsMasterClient == false)
        {
            return;
        }

        // agent isn't sitting on a navmesh yet (bad spawn position, or navmesh not baked over that spot),
        // so bail out for this frame rather than throwing on remainingDistance/SetDestination
        if (agent.isOnNavMesh == false)
        {
            return;
        }

        if (state == MonsterState.Patrol)
        {
            RunPatrol();
        }
        else if (state == MonsterState.Chase)
        {
            RunChase();
        }
        else if (state == MonsterState.Search)
        {
            RunSearch();
        }

        UpdateAnimator();
    }

    private void UpdateAnimator() // feeds the current movement + chase state into the Animator every frame
    {
        if (animator == null)
        {
            return;
        }

        bool isChasing = state == MonsterState.Chase;
        animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetBool("IsChasing", isChasing);
    }

    private void RunPatrol() // wanders between spawn points until a player gets close enough to notice
    {
        agent.speed = patrolSpeed;

        // pick a new spot to wander to once we've basically arrived at the current one
        bool pathIsReady = agent.pathPending == false;
        bool closeToDestination = agent.remainingDistance <= agent.stoppingDistance + 0.5f;
        if (pathIsReady && closeToDestination)
        {
            PickNewPatrolTarget();
        }

        PlayerController closestPlayer = FindClosestPlayer();
        if (closestPlayer == null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, closestPlayer.transform.position);
        if (distance <= chaseRange)
        {
            currentTarget = closestPlayer;
            state = MonsterState.Chase;
        }
    }

    private void PickNewPatrolTarget() // reuses the monster spawn points as wander waypoints too, so no separate waypoint list is needed
    {
        MonsterSpawnPoint[] spawnPoints = FindObjectsOfType<MonsterSpawnPoint>();
        if (spawnPoints.Length == 0)
        {
            return;
        }

        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform chosenPoint = spawnPoints[randomIndex].transform;
        agent.SetDestination(chosenPoint.position);
    }

    private void RunChase() // follows the target until they escape or get too far away
    {
        agent.speed = chaseSpeed;

        if (currentTarget == null)
        {
            state = MonsterState.Patrol;
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distance > giveUpRange)
        {
            lastKnownPosition = currentTarget.transform.position;
            currentTarget = null;
            searchTimer = 0f;
            state = MonsterState.Search;
            return;
        }

        agent.SetDestination(currentTarget.transform.position);
    }

    private void RunSearch() // goes to where the player was last seen and waits a bit before giving up
    {
        agent.speed = patrolSpeed;
        agent.SetDestination(lastKnownPosition);
        searchTimer = searchTimer + Time.deltaTime;

        // keep an eye out in case someone wanders back into range while searching
        PlayerController closestPlayer = FindClosestPlayer();
        if (closestPlayer != null)
        {
            float distance = Vector3.Distance(transform.position, closestPlayer.transform.position);
            if (distance <= chaseRange)
            {
                currentTarget = closestPlayer;
                state = MonsterState.Chase;
                return;
            }
        }

        if (searchTimer >= searchTime)
        {
            Relocate();
            state = MonsterState.Patrol;
        }
    }

    private PlayerController FindClosestPlayer() // looks through every visible player and returns the nearest one
    {
        PlayerController closest = null;
        float closestDistance = 0f;

        foreach (PlayerController player in PlayerController.All)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (closest == null || distance < closestDistance)
            {
                closest = player;
                closestDistance = distance;
            }
        }

        return closest;
    }

    // this is just temporary until I write a script where it wonders and looks for the player and waits peridodically before spawning in again
    private void Relocate() // teleports to a random monster spawn point after giving up a chase
    {
        MonsterSpawnPoint[] spawnPoints = FindObjectsOfType<MonsterSpawnPoint>();
        if (spawnPoints.Length == 0)
        {
            return; // nowhere to relocate to, just keep going from wherever it already is
        }

        int randomIndex = Random.Range(0, spawnPoints.Length);
        Transform chosenPoint = spawnPoints[randomIndex].transform;
        agent.Warp(chosenPoint.position); // teleport, not walk, since the chase already gave up
    }
}
