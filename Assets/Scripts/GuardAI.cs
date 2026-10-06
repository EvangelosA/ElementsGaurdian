using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Συμπεριφορά του Φύλακα με βάση την τρέχουσα κατάστασή του
public class GuardAI : MonoBehaviour
{
    // Οι πιθανές καταστάσεις του Φύλακα
    public enum GuardState { Patrol, Chase, Search, Ambush }

    [Header("Αναφορές")]
    public Transform player;
    public Light OwnLight;
    public PlayerTracker tracker;
    public Transform[] patrolPoints;
    public LayerMask obstacleMask;           // layer των αντικειμένων που κρύβουν τη θέα

    [Header("Όραση")]
    public float viewDistance = 7f;          // μέγιστη απόσταση όρασης
    public float viewAngle = 90f;            // συνολικό άνοιγμα του κώνου όρασης σε μοίρες
    public float closeDetectRange = 1.5f;    // απόσταση όπου ο παίκτης εντοπίζεται προς κάθε κατεύθυνση
    public float catchDistance = 1.2f;       // απόσταση σύλληψης

    [Header("Περιπολία")]
    public float patrolSpeed = 3f;
    public float waitAtPoint = 1.5f;         // δευτερόλεπτα αναμονής σε κάθε σημείο
    public float lookAroundSpeed = 120f;     // μοίρες ανά δευτερόλεπτο κατά την αναμονή

    [Header("Καταδίωξη")]
    public float chaseSpeed = 4.5f;
    public float loseSightDelay = 0.5f;      // δευτερόλεπτα χωρίς οπτική επαφή πριν την αναζήτηση

    [Header("Αναζήτηση")]
    public float searchSpeed = 3.5f;
    public float searchDuration = 4f;        // δευτερόλεπτα περιστροφής στο τελευταίο γνωστό σημείο

    [Header("Χάρτης θερμότητας")]
    public float heatmapTrust = 0.6f;        // πιθανότητα (0 έως 1) ο προορισμός περιπολίας να προέλθει από το πλέγμα
    public float minHeatToUse = 3f;          // ελάχιστο άθροισμα του πλέγματος για να χρησιμοποιηθεί
    public float navMeshSearchRadius = 2f;   // ακτίνα αναζήτησης του κοντινότερου σημείου του NavMesh

    [Header("Ενέδρα")]
    public float ambushChance = 0.3f;        // πιθανότητα (0 έως 1) ενέδρας μετά από κάθε σημείο περιπολίας
    public int ambushMinRound = 2;           // πρώτος γύρος όπου επιτρέπεται ενέδρα
    public int ambushCandidates = 3;         // πλήθος θερμότερων κελιών από τα οποία γίνεται η επιλογή
    public float ambushDuration = 5f;        // δευτερόλεπτα αναμονής στο σημείο ενέδρας
    public float ambushLookSpeed = 40f;      // μοίρες ανά δευτερόλεπτο κατά την ενέδρα

    [Header("Χρώματα")]
    public Color patrolColor = new Color(1f, 1f, 1f);
    public Color chaseColor = new Color(1f, 0.2f, 0f);
    public Color searchColor = new Color(1f, 0.6f, 0f);
    public Color ambushColor = new Color(0f,0f,0f);

    public GuardState CurrentState { get; private set; } = GuardState.Patrol;
    // Ειδοποίηση προς άλλα scripts όταν ξεκινά καταδίωξη
    public event System.Action OnPlayerSpotted;

    private NavMeshAgent agent;
    private Renderer bodyRenderer;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private int currentPointIndex = -1;      // -1 σημαίνει ότι δεν έχει επιλεγεί ακόμα σημείο
    private float waitTimer = 0f;
    private float searchTimer = 0f;
    private float ambushTimer = 0f;
    private float timeSinceSeen = 0f;        // χρόνος από την τελευταία φορά που φάνηκε ο παίκτης
    private Vector3 lastSeenPosition;        // θέση του παίκτη την τελευταία φορά που φάνηκε
    private Vector3 ambushPosition;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        bodyRenderer = GetComponent<Renderer>();
    }

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

        // Εγγραφή στις ειδοποιήσεις έναρξης και τέλους γύρου
        GameManager.Instance.OnRoundStarted += HandleRoundStarted;
        GameManager.Instance.OnRoundEnded += HandleRoundEnded;

        EnterPatrol();
    }

    void OnDestroy()
    {
        // Διαγραφή από τις ειδοποιήσεις, εφόσον ο GameManager υπάρχει ακόμα
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStarted -= HandleRoundStarted;
            GameManager.Instance.OnRoundEnded -= HandleRoundEnded;
        }
    }

    void Update()
    {
        // Ο Φύλακας δεν ενεργεί όσο ο γύρος δεν είναι σε εξέλιξη
        if (!GameManager.Instance.RoundActive) return;

        // Σύλληψη όταν ο παίκτης είναι αρκετά κοντά, σε οποιαδήποτε κατάσταση
        if (FlatDistanceToPlayer() <= catchDistance)
        {
            GameManager.Instance.PlayerCaught();
            return;
        }

        // Εκτέλεση της συμπεριφοράς που αντιστοιχεί στην τρέχουσα κατάσταση
        switch (CurrentState)
        {
            case GuardState.Patrol:
                UpdatePatrol();
                break;
            case GuardState.Chase:
                UpdateChase();
                break;
            case GuardState.Search:
                UpdateSearch();
                break;
            case GuardState.Ambush:
                UpdateAmbush();
                break;
        }
    }

    // ---------------- Περιπολία ----------------

    private void EnterPatrol()
    {
        CurrentState = GuardState.Patrol;
        agent.speed = patrolSpeed;
        waitTimer = 0f;
        SetColor(patrolColor);
        GoToNextPatrolDestination();
    }

    private void UpdatePatrol()
    {
        // Μετάβαση σε καταδίωξη μόλις φανεί ο παίκτης
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        // Η διαδρομή υπολογίζεται ακόμα
        if (agent.pathPending) return;

        // Ο Φύλακας δεν έχει φτάσει ακόμα στον προορισμό
        if (agent.remainingDistance > agent.stoppingDistance) return;

        // Αναμονή στον προορισμό με ταυτόχρονη περιστροφή
        waitTimer += Time.deltaTime;
        transform.Rotate(0f, lookAroundSpeed * Time.deltaTime, 0f);

        if (waitTimer >= waitAtPoint)
        {
            waitTimer = 0f;

            // Ενέδρα με πιθανότητα ambushChance, εφόσον επιτρέπεται στον τρέχοντα γύρο
            bool ambushAllowed = GameManager.Instance.CurrentRound >= ambushMinRound && HasHeatData();
            if (ambushAllowed && Random.value < ambushChance)
            {
                EnterAmbush();
            }
            else
            {
                GoToNextPatrolDestination();
            }
        }
    }

    // Ορίζει τον επόμενο προορισμό, είτε από το πλέγμα είτε από τα σημεία περιπολίας
    private void GoToNextPatrolDestination()
    {
        // Προορισμός από το πλέγμα με πιθανότητα heatmapTrust
        if (HasHeatData() && Random.value < heatmapTrust)
        {
            Vector3 cellCenter = tracker.CellToWorld(tracker.PickWeightedCell());

            if (TryGetNavMeshPoint(cellCenter, out Vector3 destination))
            {
                agent.SetDestination(destination);
                return;
            }
        }

        GoToRandomPatrolPoint();
    }

    // Επιλέγει τυχαίο σημείο περιπολίας διαφορετικό από το τρέχον και ορίζει τον προορισμό
    private void GoToRandomPatrolPoint()
    {
        // Χωρίς σημεία περιπολίας ο Φύλακας μένει στη θέση του
        if (patrolPoints.Length == 0) return;

        int nextIndex = 0;

        // Με περισσότερα από ένα σημεία, η επιλογή επαναλαμβάνεται μέχρι να διαφέρει από το τρέχον
        if (patrolPoints.Length > 1)
        {
            nextIndex = currentPointIndex;
            while (nextIndex == currentPointIndex)
            {
                nextIndex = Random.Range(0, patrolPoints.Length);
            }
        }

        currentPointIndex = nextIndex;
        agent.SetDestination(patrolPoints[currentPointIndex].position);
    }

    // ---------------- Καταδίωξη ----------------

    private void EnterChase()
    {
        CurrentState = GuardState.Chase;
        agent.speed = chaseSpeed;
        timeSinceSeen = 0f;
        lastSeenPosition = player.position;
        SetColor(chaseColor);
        OnPlayerSpotted?.Invoke();
    }

    private void UpdateChase()
    {
        if (CanSeePlayer())
        {
            // Ανανέωση της τελευταίας γνωστής θέσης όσο υπάρχει οπτική επαφή
            lastSeenPosition = player.position;
            timeSinceSeen = 0f;
        }
        else
        {
            // Μετάβαση σε αναζήτηση αν ο παίκτης δεν φαίνεται για αρκετό χρόνο
            timeSinceSeen += Time.deltaTime;
            if (timeSinceSeen > loseSightDelay)
            {
                EnterSearch();
                return;
            }
        }

        agent.SetDestination(player.position);
    }

    // ---------------- Αναζήτηση ----------------

    private void EnterSearch()
    {
        CurrentState = GuardState.Search;
        agent.speed = searchSpeed;
        searchTimer = 0f;
        SetColor(searchColor);
        agent.SetDestination(lastSeenPosition);
    }

    private void UpdateSearch()
    {
        // Επιστροφή σε καταδίωξη αν ο παίκτης φανεί ξανά
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        if (agent.pathPending) return;
        if (agent.remainingDistance > agent.stoppingDistance) return;

        // Περιστροφή στο τελευταίο γνωστό σημείο
        searchTimer += Time.deltaTime;
        transform.Rotate(0f, lookAroundSpeed * Time.deltaTime, 0f);

        // Επιστροφή σε περιπολία όταν τελειώσει ο χρόνος αναζήτησης
        if (searchTimer >= searchDuration)
        {
            EnterPatrol();
        }
    }

    // ---------------- Ενέδρα ----------------

    private void EnterAmbush()
    {
        List<Vector2Int> candidates = tracker.GetTopCells(ambushCandidates);

        // Χωρίς διαθέσιμα κελιά συνεχίζεται η κανονική περιπολία
        if (candidates.Count == 0)
        {
            GoToNextPatrolDestination();
            return;
        }

        // Τυχαία επιλογή ανάμεσα στα θερμότερα κελιά
        Vector2Int cell = candidates[Random.Range(0, candidates.Count)];

        // Αν το κέντρο του κελιού δεν αντιστοιχεί σε σημείο του NavMesh, συνεχίζεται η περιπολία
        if (!TryGetNavMeshPoint(tracker.CellToWorld(cell), out ambushPosition))
        {
            GoToNextPatrolDestination();
            return;
        }

        CurrentState = GuardState.Ambush;
        agent.speed = patrolSpeed;
        ambushTimer = 0f;

        SetColor(ambushColor);
        agent.SetDestination(ambushPosition);
    }

    private void UpdateAmbush()
    {
        // Μετάβαση σε καταδίωξη μόλις φανεί ο παίκτης
        if (CanSeePlayer())
        {
            EnterChase();
            return;
        }

        if (agent.pathPending) return;
        if (agent.remainingDistance > agent.stoppingDistance) return;

        // Αργή περιστροφή στο σημείο ενέδρας
        ambushTimer += Time.deltaTime;
        transform.Rotate(0f, ambushLookSpeed * Time.deltaTime, 0f);

        // Επιστροφή σε περιπολία όταν τελειώσει ο χρόνος ενέδρας
        if (ambushTimer >= ambushDuration)
        {
            EnterPatrol();
        }
    }

    // ---------------- Βοηθητικές μέθοδοι ----------------

    // Ελέγχει απόσταση, γωνία και εμπόδια ανάμεσα στον Φύλακα και τον παίκτη
    private bool CanSeePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;   // η σύγκριση γίνεται μόνο στο οριζόντιο επίπεδο
        float distance = toPlayer.magnitude;

        // Εκτός εμβέλειας όρασης
        if (distance > viewDistance) return false;

        // Πέρα από την κοντινή εμβέλεια ελέγχεται και η γωνία
        if (distance > closeDetectRange)
        {
            float angle = Vector3.Angle(transform.forward, toPlayer);

            // Ο παίκτης είναι έξω από τον κώνο όρασης
            if (angle > viewAngle / 2f) return false;
        }

        // Το Linecast επιστρέφει true αν κάποιο εμπόδιο βρίσκεται ανάμεσα στα δύο σημεία
        if (Physics.Linecast(transform.position, player.position, obstacleMask)) return false;

        return true;
    }

    // Ελέγχει αν το πλέγμα έχει αρκετά δεδομένα για να χρησιμοποιηθεί
    private bool HasHeatData()
    {
        return tracker != null && tracker.TotalHeat >= minHeatToUse;
    }

    // Βρίσκει το κοντινότερο σημείο του NavMesh μέσα στην ακτίνα navMeshSearchRadius
    private bool TryGetNavMeshPoint(Vector3 point, out Vector3 result)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = point;
        return false;
    }

    // Απόσταση από τον παίκτη αγνοώντας τη διαφορά ύψους
    private float FlatDistanceToPlayer()
    {
        Vector3 difference = player.position - transform.position;
        difference.y = 0f;
        return difference.magnitude;
    }

    private void SetColor(Color color)
    {
        OwnLight.color = color;
    }

    // ---------------- Γεγονότα γύρου ----------------

    // Επαναφέρει τον Φύλακα στη θέση εκκίνησης και ξεκινά περιπολία
    private void HandleRoundStarted()
    {
        // Το Warp μεταφέρει ακαριαία τον agent χωρίς να υπολογίσει διαδρομή
        agent.Warp(startPosition);
        transform.rotation = startRotation;

        agent.isStopped = false;
        currentPointIndex = -1;
        EnterPatrol();
    }

    // Σταματά τον Φύλακα και ακυρώνει τη διαδρομή του
    private void HandleRoundEnded(bool playerWon)
    {
        agent.isStopped = true;
        agent.ResetPath();
    }

    // ---------------- Σχεδίαση στο Scene view ----------------

    // Σχεδιάζει τον κώνο όρασης, την κοντινή εμβέλεια, την απόσταση σύλληψης και τους στόχους
    void OnDrawGizmos()
    {
        Vector3 position = transform.position;

        // Τα δύο άκρα του κώνου όρασης
        Vector3 leftEdge = Quaternion.Euler(0f, -viewAngle / 2f, 0f) * transform.forward;
        Vector3 rightEdge = Quaternion.Euler(0f, viewAngle / 2f, 0f) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(position, position + leftEdge * viewDistance);
        Gizmos.DrawLine(position, position + rightEdge * viewDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(position, closeDetectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(position, catchDistance);

        if (!Application.isPlaying) return;

        // Η τελευταία γνωστή θέση του παίκτη κατά την αναζήτηση
        if (CurrentState == GuardState.Search)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawSphere(lastSeenPosition, 0.3f);
        }

        // Το σημείο ενέδρας και μια γραμμή προς αυτό
        if (CurrentState == GuardState.Ambush)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(ambushPosition, 0.4f);
            Gizmos.DrawLine(position, ambushPosition);
        }
    }
}
