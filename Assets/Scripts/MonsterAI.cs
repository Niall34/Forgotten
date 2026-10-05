using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

// controls the monster (only the master client actually runs this logic and moves it) everyone else just watches it move through the normal photon position sync (added a PhotonTransformView to the prefab for that, same as the player)
[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviourPun
{
    [Header("Detection")]
    public float chaseRange = 50f; // sight range achieved while sprinting or with your flashlight on, with a clear line of sight, this is the ceiling, veryCloseRange below is the floor for a silent player
    public float giveUpRange = 80f; // how far away before the monster loses interest mid chase
    public float searchTime = 8f; // how long it searches the last known spot before giving up
    public float flashlightDetectionBonus = 10f; // extra sight range on top of chaseRange while sprinting AND the player's flashlight is on

    [Header("Stealth")]
    public float veryCloseRange = 3f; // point-blank distance, a silent, dark, motionless player can only be spotted this close even with clear line of sight, and a moving player this close gets noticed no matter what else is going on
    public float argoRange = 9f; // a sprinting player within this range pulls the monster's patrol wandering toward them
    public LayerMask sightBlockingLayers = ~0; // what actually blocks the monster's view (walls, crates, barrels etc) whatever's flagged solid on the maps geometry
    public float monsterEyeHeight = 1.9f; // roughly where the monster's own "eyes" are for line of sight checks
    public float walkingSightRange = 40f; // sight range for anyone upright with no light on, walking or standing still, only crouching drops you back to the short point blank range
    public float loseSightDelay = 0.75f; // how long a solid object has to stay between it and its target before it loses track and heads for the last place it saw them

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 5.5f;

    [Header("Patrol")]
    public float wanderRadius = 15f; // how far from its current spot the monster picks its next wander target
    public float patrolPauseMin = 1f; // stands still for a bit at each waypoint instead of instantly moving on
    public float patrolPauseMax = 4f;
    private const int WanderSampleAttempts = 5; // tries a few random spots before giving up and falling back to a spawn point

    private bool isPausedAtWaypoint = false;
    private float patrolPauseTimer = 0f;

    [Header("Random Despawn")]
    public bool vanishOnlyAfterAttack = true; // when ticked it only vanishes after hitting a player, no random despawns and no teleporting away after losing one, untick it for the old behaviour
    public float despawnCheckInterval = 15f; // how often, in seconds, it rolls the dice on vanishing (only while patrolling, never mid-chase)
    [Range(0f, 1f)] public float despawnChance = 0.20f; // chance per check that it actually vanishes this time
    public float despawnHiddenDuration = 8f; // how long it stays gone before reappearing somewhere else
    public float attackRespawnDelay = 10f; // how long it stays gone after an attack before it reappears, the random despawn above keeps its own shorter time

    private Renderer[] monsterRenderers; // hidden/shown together instead of disabling the whole GameObject, which would mess with the NavMeshAgent
    private bool isDespawned = false;
    private RaycastHit[] sightHits = new RaycastHit[16]; // reused every line of sight check so it isn't allocating a new array each time
    private float timeSinceSeenTarget = 0f;
    private float despawnCheckTimer = 0f;

    [Header("Attack")]
    public float attackRange = 2f; // how close to the target before it attacks instead of continuing to chase
    public int attackDamage = 10;
    public float attackCooldown = 1.5f;
    private float lastAttackTime = -999f;
    public float attackAnimationDuration = 1.2f; // how long to let the attack animation play before vanishing (only used now if no clip with "attack" in its name is found on the Animator)
    public float attackVanishOffset = -2f; // nudges the vanish time earlier (negative) or later (positive) than the exact end of the attack clip
    public float attackHitDelay = 0.4f; // how far into the swing the hit actually lands, only then does it check the target's still in reach
    public float attackHitLeeway = 1.5f; // extra distance past attackRange a player can be when the swing lands and still get hit, run further than that and the swing misses and it carries on chasing
    public float detectionGraceAfterRespawn = 5f; // after reappearing (from an attack OR a random despawn), it can't re-detect anyone for this long to stop an instant re-attack loop

    private bool isAttacking = false;
    private float detectionGraceTimer = 0f;

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

    [Header("Sounds")]
    public AudioClip attackClip;
    public AudioClip spawnClip;
    public AudioClip despawnClip;
    public float soundRange = 30f; // volume fades evenly to silent out to here, so you can tell roughly how far away the monster is
    private AudioSource soundSource;

    public AudioClip[] stepClips; // the same clips are used for walking and running, running just plays them faster
    public float walkStepInterval = 0.8f; // seconds between steps
    public float runStepInterval = 0.4f;
    public float stepVolume = 0.8f;
    public float runPitchMultiplier = 1.3f; // how much faster (and higher) the step sound plays while it's running
    private AudioSource stepSource; // separate from soundSource, since changing the pitch on a source changes everything already playing on it
    private Vector3 lastPosition;
    private float smoothedSpeed = 0f;
    private float stepTimer = 0f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        monsterRenderers = GetComponentsInChildren<Renderer>();

        // 3D and sits on the monster, so every client hears it from wherever the monster actually is
        soundSource = MakeSoundSource();
        stepSource = MakeSoundSource();
        lastPosition = transform.position;
        agent.stoppingDistance = attackRange * 0.9f; // naturally slows down as it approaches attack range, instead of pathing all the way onto the player before the code catches up
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

        // if monster isn't sitting on a navmesh yet (bad spawn position, or navmesh not baked over that spot), it bails out for this frame rather than throwing on remainingDistance/SetDestination
        if (agent.isOnNavMesh == false)
        {
            return;
        }

        // frozen while hidden or mid-attack, the relevant coroutine handles bringing it back, nothing else should run in the meantime
        if (isDespawned || isAttacking)
        {
            return;
        }

        if (state == MonsterState.Patrol)
        {
            RunPatrol();
            CheckForRandomDespawn();
        }
        else if (state == MonsterState.Chase)
        {
            RunChase();
        }
        else if (state == MonsterState.Search)
        {
            RunSearch();
        }

        if (detectionGraceTimer > 0f)
        {
            detectionGraceTimer -= Time.deltaTime;
        }

        UpdateAnimator();
    }

    private void CheckForRandomDespawn() // only rolls the dice while patrolling
    {
        if (vanishOnlyAfterAttack)
        {
            return;
        }

        despawnCheckTimer += Time.deltaTime;
        if (despawnCheckTimer < despawnCheckInterval)
        {
            return;
        }

        despawnCheckTimer = 0f;
        if (Random.value < despawnChance)
        {
            StartCoroutine(DespawnAndRespawnRoutine(despawnHiddenDuration));
        }
    }

    private IEnumerator DespawnAndRespawnRoutine(float hiddenDuration) // vanishes for a bit, then reappears at a random spawn point
    {
        isDespawned = true;
        agent.isStopped = true;
        photonView.RPC(nameof(SetVisibleRPC), RpcTarget.All, false);

        yield return new WaitForSeconds(hiddenDuration);

        Vector3 respawnPosition = GetRandomSpawnPointPosition();
        if (respawnPosition != transform.position) // GetRandomSpawnPointPosition returns transform.position itself if there's nowhere to go
        {
            agent.Warp(respawnPosition);
        }

        photonView.RPC(nameof(SetVisibleRPC), RpcTarget.All, true);
        agent.isStopped = false;
        isDespawned = false;
        detectionGraceTimer = detectionGraceAfterRespawn; // reappearing shouldn't mean instantly spotting someone again, this is the timer that was meant to stop that
    }

    private AudioSource MakeSoundSource() // 3D source on the monster, volume fades evenly to silent at soundRange
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = soundRange;
        source.dopplerLevel = 0f;
        return source;
    }

    private void LateUpdate() // unlike Update this runs on every client, so everyone hears the footsteps
    {
        UpdateStepSounds();
    }

    private void UpdateStepSounds() // only the master knows the monster's state, so this works out how fast it's actually moving from its position each frame, which works the same on everyone
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        float rawSpeed = Vector3.Distance(transform.position, lastPosition) / deltaTime;
        lastPosition = transform.position;

        if (rawSpeed > chaseSpeed * 2f)
        {
            rawSpeed = 0f; // way too fast to be walking, it just teleported somewhere, not worth a footstep
        }

        smoothedSpeed = Mathf.Lerp(smoothedSpeed, rawSpeed, 10f * deltaTime);

        bool isVisible = monsterRenderers.Length > 0 && monsterRenderers[0].enabled;
        bool isMoving = isVisible && smoothedSpeed > 0.3f;
        if (isMoving == false)
        {
            stepTimer = 0f; // so the first step lands right as it starts moving
            return;
        }

        // running whenever it's faster than halfway between patrol and chase speed, which is when the run animation is playing
        bool isRunning = smoothedSpeed > (patrolSpeed + chaseSpeed) * 0.5f;

        stepTimer -= deltaTime;
        if (stepTimer > 0f)
        {
            return;
        }

        stepTimer = isRunning ? runStepInterval : walkStepInterval;
        if (stepClips == null || stepClips.Length == 0)
        {
            return;
        }

        stepSource.pitch = Random.Range(0.94f, 1.06f) * (isRunning ? runPitchMultiplier : 1f);
        stepSource.PlayOneShot(stepClips[Random.Range(0, stepClips.Length)], stepVolume);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            soundSource.PlayOneShot(clip);
        }
    }

    [PunRPC]
    private void SetVisibleRPC(bool visible) // runs on every client so the monster actually disappears/reappears for everyone, not just the master client
    {
        PlaySound(visible ? spawnClip : despawnClip);
        foreach (Renderer monsterRenderer in monsterRenderers)
        {
            monsterRenderer.enabled = visible;
        }
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

    private void RunPatrol() // wanders around near its spawn points, pausing briefly at each spot, until a player gets close enough to notice
    {
        agent.speed = patrolSpeed;

        if (isPausedAtWaypoint)
        {
            patrolPauseTimer -= Time.deltaTime;
            if (patrolPauseTimer <= 0f)
            {
                isPausedAtWaypoint = false;
                PickNewPatrolTarget();
            }
        }
        else
        {
            // arrived at the current wander target so stop and pause for a bit before picking the next one
            bool pathIsReady = agent.pathPending == false;
            bool closeToDestination = agent.remainingDistance <= agent.stoppingDistance + 0.5f;
            if (pathIsReady && closeToDestination)
            {
                isPausedAtWaypoint = true;
                patrolPauseTimer = Random.Range(patrolPauseMin, patrolPauseMax);
            }
        }

        if (detectionGraceTimer > 0f)
        {
            return;
        }

        PlayerController detected = FindPlayerToChase();
        if (detected != null)
        {
            currentTarget = detected;
            isPausedAtWaypoint = false; // drop whatever pause it was in mid-way through as chasing takes priority
            state = MonsterState.Chase;
            timeSinceSeenTarget = 0f;
        }
    }

    private float GetEffectiveSightRange(PlayerController player) // how far away the monster can spot someone it already has a clear line of sight to, silent and dark only gives you away up close, loud or lit gives you away from much further off (this is the "how visible are you" half of detectio)
    {
        bool sprintingOrLit = player.IsSprinting || player.IsFlashlightOn;
        if (sprintingOrLit == false)
        {
            // anyone upright in the open is easy to spot, standing still included, crouching is what keeps you hard to see
            if (player.IsCrouching == false)
            {
                return walkingSightRange;
            }

            return veryCloseRange;
        }

        float range = chaseRange;
        if (player.IsSprinting && player.IsFlashlightOn)
        {
            range += flashlightDetectionBonus;
        }

        return range;
    }

    private bool HasLineOfSightTo(PlayerController player) // raycasts from roughly the monster's eyes to the player's current eye height so crouching behind something like a barrel genuinely blocks this, since the raycast target drops down with them, but standing up wouldn't
    {
        Vector3 origin = transform.position + Vector3.up * monsterEyeHeight;
        Vector3 targetPoint = player.transform.position + Vector3.up * player.EyeHeight;
        Vector3 offset = targetPoint - origin;
        float distance = offset.magnitude;

        // triggers (win zones, pickup areas etc) shouldn't block the monster's view so they're ignored, and it checks every hit along the way rather than just the first, so its own body or the player's can't hide a real wall behind them
        int hitCount = Physics.RaycastNonAlloc(origin, offset.normalized, sightHits, distance, sightBlockingLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = sightHits[i].transform;
            if (hitTransform.IsChildOf(transform)) continue; // the monster's own body
            if (hitTransform == player.transform || hitTransform.IsChildOf(player.transform)) continue; // the player themselves doesn't block the line

            return false; // anything else between them is cover
        }

        return true; // nothing in the way
    }

    private bool ShouldStartChasing(PlayerController player, float distance) // the actual "does the monster notice this player right now" check and combines a point blank override with a proper line of sight gated sight range
    {
        // point blank override: moving right next to the monster gets noticed no matter what else is true, like walking right around a corner into it
        if (distance <= veryCloseRange && player.IsMoving)
        {
            return true;
        }

        // otherwise it actually has to be able to see them, a crouching, dark player tucked behind cover fails this raycast entirely, and the monster just walks or runs straight past like they were never there
        if (HasLineOfSightTo(player) == false)
        {
            return false;
        }

        return distance <= GetEffectiveSightRange(player);
    }

    private void PickNewPatrolTarget() // picks a random spot to wander to, normally around a random spawn point, but gets pulled toward a nearby sprinting player instead if one's caught the monster's attention
    {
        Vector3 anchor;

        PlayerController argoTarget = FindArgoTarget();
        if (argoTarget != null)
        {
            anchor = argoTarget.transform.position; // wander toward their general area rather than a random spot
        }
        else
        {
            MonsterSpawnPoint[] spawnPoints = FindObjectsByType<MonsterSpawnPoint>();
            if (spawnPoints.Length == 0)
            {
                return;
            }

            int randomIndex = Random.Range(0, spawnPoints.Length);
            anchor = spawnPoints[randomIndex].transform.position;
        }

        for (int attempt = 0; attempt < WanderSampleAttempts; attempt++)
        {
            Vector3 randomOffset = Random.insideUnitSphere * wanderRadius;
            randomOffset.y = 0f; // keep the sample flat, height gets handled by the NavMesh sample below
            Vector3 candidate = anchor + randomOffset;

            NavMeshHit hit;
            bool foundSpot = NavMesh.SamplePosition(candidate, out hit, wanderRadius, NavMesh.AllAreas);
            if (foundSpot)
            {
                agent.SetDestination(hit.position);
                return;
            }
        }

        // every random sample missed the navmesh, just fall back to the anchor point itself
        agent.SetDestination(anchor);
    }

    private void RunChase() // follows the target until it's close enough to attack, or gets too far away and gives up
    {
        agent.speed = chaseSpeed;

        if (currentTarget == null)
        {
            state = MonsterState.Patrol;
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);

        if (distance <= attackRange)
        {
            StartCoroutine(AttackAndRespawnRoutine());
            return;
        }

        if (distance > giveUpRange)
        {
            lastKnownPosition = currentTarget.transform.position;
            currentTarget = null;
            searchTimer = 0f;
            state = MonsterState.Search;
            return;
        }

        // a solid object between them breaks the chase, it keeps heading for the last spot it saw them for a moment, then loses track and searches there
        bool heardAtPointBlank = distance <= veryCloseRange && currentTarget.IsMoving; // same point blank override ShouldStartChasing uses, it can still hear someone moving right next to it
        bool canSeeTarget = HasLineOfSightTo(currentTarget) || heardAtPointBlank;
        if (canSeeTarget)
        {
            lastKnownPosition = currentTarget.transform.position;
            timeSinceSeenTarget = 0f;
        }
        else
        {
            timeSinceSeenTarget += Time.deltaTime;
            if (timeSinceSeenTarget >= loseSightDelay)
            {
                currentTarget = null;
                searchTimer = 0f;
                state = MonsterState.Search;
                return;
            }
        }

        agent.SetDestination(canSeeTarget ? currentTarget.transform.position : lastKnownPosition);
    }

    private IEnumerator AttackAndRespawnRoutine() // plays the attack animation, then reuses the despawn/respawn flow to vanish and reappear elsewhere
    {
        isAttacking = true;
        agent.isStopped = true;
        agent.velocity = Vector3.zero; // isStopped alone can still let it coast forward a bit on leftover momentum, this kills that immediately
        agent.ResetPath(); // also drop the current path entirely so there's nothing left for it to resume

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f); // Update() skips UpdateAnimator() while isAttacking, so Speed would otherwise stay frozen at its last (likely running) value the whole time
            animator.SetBool("IsChasing", false); // same goes for this, otherwise the Animator falls back into the run animation as soon as the attack clip ends
        }

        photonView.RPC(nameof(PlayAttackRPC), RpcTarget.All);

        // it only vanishes if the attack actually connects, so wait for the hit to land in the animation and check then
        float attackLength = Mathf.Max(0f, GetAttackAnimationLength() + attackVanishOffset);
        float hitDelay = Mathf.Min(attackHitDelay, attackLength);
        yield return new WaitForSeconds(hitDelay);

        bool hitLanded = TryAttack();

        yield return new WaitForSeconds(attackLength - hitDelay);

        if (hitLanded == false)
        {
            // it missed, so no vanishing, it carries straight on chasing once the swing's finished
            agent.isStopped = false;
            isAttacking = false;
            yield break;
        }

        // reuse the exact same hide then relocate flow the random despawn uses
        yield return StartCoroutine(DespawnAndRespawnRoutine(attackRespawnDelay));

        currentTarget = null;
        state = MonsterState.Patrol;
        isAttacking = false;
    }

    private float GetAttackAnimationLength() // looks for the attack clip on the Animator so it vanishes the moment that animation ends, falls back to attackAnimationDuration if it can't find one
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return attackAnimationDuration;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name.ToLower().Contains("attack"))
            {
                return clip.length / Mathf.Max(animator.speed, 0.01f); // animator.speed scales how fast every clip plays
            }
        }

        return attackAnimationDuration;
    }

    private bool TryAttack() // only counts as a hit if the target is still alive and within reach as the swing lands, returns whether it connected
    {
        if (currentTarget == null || Time.time - lastAttackTime < attackCooldown) return false;
        if (currentTarget.HasEscaped) return false;

        var health = currentTarget.GetComponent<Forgotten.Player.PlayerHealthStateMachine>();
        if (health != null && health.IsDead) return false;

        float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (distance > attackRange + attackHitLeeway) return false;

        lastAttackTime = Time.time;
        currentTarget.photonView.RPC("TakeDamage", RpcTarget.All, attackDamage);
        return true;
    }

    [PunRPC]
    private void PlayAttackRPC() // runs on every client so the attack animation actually plays for everyone watching, not just the master client
    {
        PlaySound(attackClip);
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    private void RunSearch() // goes to where the player was last seen and waits a bit before giving up
    {
        agent.speed = patrolSpeed;
        agent.SetDestination(lastKnownPosition);
        searchTimer = searchTimer + Time.deltaTime;

        // keep an eye out in case someone wanders back into range while searching
        if (detectionGraceTimer <= 0f)
        {
            PlayerController detected = FindPlayerToChase();
            if (detected != null)
            {
                currentTarget = detected;
                state = MonsterState.Chase;
                timeSinceSeenTarget = 0f;
                return;
            }
        }

        if (searchTimer >= searchTime)
        {
            if (vanishOnlyAfterAttack == false)
            {
                Relocate();
            }
            state = MonsterState.Patrol;
        }
    }

    private PlayerController FindPlayerToChase() // loops every player and returns the closest one the monster can actually detect right now via ShouldStartChasing, not just the geometrically nearest player, since a nearer one might be successfully hidden while someone further away is standing out in the open
    {
        PlayerController best = null;
        float bestDistance = 0f;

        foreach (PlayerController player in PlayerController.All)
        {
            if (player.HasEscaped) continue;
            var health = player.GetComponent<Forgotten.Player.PlayerHealthStateMachine>();
            if (health != null && health.IsDead) continue;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (ShouldStartChasing(player, distance) == false) continue;

            if (best == null || distance < bestDistance)
            {
                best = player;
                bestDistance = distance;
            }
        }

        return best;
    }

    private PlayerController FindArgoTarget() // closest sprinting player within argoRange, used to bias patrol wandering toward them without actually committing to a chase, deliberately doesn't care about line of sight or the flashlight, sprinting nearby is loud enough on its own to catch the monster's attention
    {
        PlayerController closest = null;
        float closestDistance = 0f;

        foreach (PlayerController player in PlayerController.All)
        {
            if (player.HasEscaped) continue;
            var health = player.GetComponent<Forgotten.Player.PlayerHealthStateMachine>();
            if (health != null && health.IsDead) continue;
            if (player.IsSprinting == false) continue;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > argoRange) continue;

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
        Vector3 position = GetRandomSpawnPointPosition();
        agent.Warp(position); // teleport, not walk, since the chase already gave up
    }

    private Vector3 GetRandomSpawnPointPosition() // picks a random MonsterSpawnPoint's position, shared by Relocate() and the random despawn/respawn
    {
        MonsterSpawnPoint[] spawnPoints = FindObjectsByType<MonsterSpawnPoint>();
        if (spawnPoints.Length == 0)
        {
            return transform.position; // nowhere to go, just stay put
        }

        int randomIndex = Random.Range(0, spawnPoints.Length);
        return spawnPoints[randomIndex].transform.position;
    }
}