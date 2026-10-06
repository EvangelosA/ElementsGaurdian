using System;
using System.Collections;
using UnityEngine;
using TMPro;

// Διαχείριση γύρων, εμφάνισης λίθων, στατιστικών και κειμένου κατάστασης
public class GameManager : MonoBehaviour
{
    // Η μοναδική ενεργή παρουσία του GameManager, προσβάσιμη από κάθε script
    public static GameManager Instance { get; private set; }

    [Header("Αναφορές")]
    public PlayerController player;
    public GameObject gemPrefab;
    public TextMeshProUGUI statusText;     // μπορεί να μείνει κενό

    [Header("Γύρος")]
    public int gemsPerRound = 8;           // λίθοι που χρειάζονται για νίκη
    public float restartDelay = 2f;        // δευτερόλεπτα πριν τον επόμενο γύρο

    [Header("Εμφάνιση λίθων")]
    public float arenaHalfSize = 9f;       // οι θέσεις επιλέγονται στο διάστημα [-τιμή, +τιμή]
    public LayerMask obstacleMask;         // layer των εμποδίων
    public float minDistanceFromPlayer = 4f;
    public float clearanceRadius = 0.8f;   // ελεύθερος χώρος γύρω από τον λίθο
    public int maxSpawnAttempts = 30;

    // Κατάσταση παιχνιδιού, μόνο για ανάγνωση από άλλα scripts
    public int CurrentRound { get; private set; } = 0;
    public int GemsCollected { get; private set; } = 0;
    public bool RoundActive { get; private set; } = false;
    public float RoundTime { get; private set; } = 0f;
    public int RoundsWon { get; private set; } = 0;
    public int RoundsLost { get; private set; } = 0;

    // Ειδοποιήσεις προς άλλα scripts για την έναρξη και το τέλος γύρου
    public event Action OnRoundStarted;
    public event Action<bool> OnRoundEnded;   // true = νίκη, false = σύλληψη
    public event Action OnGemPicked;

    private Vector3 playerStartPosition;
    private GameObject currentGem;             // ο λίθος που υπάρχει τώρα στη σκηνή

    void Awake()
    {
        // Αν υπάρχει ήδη GameManager, ο νέος καταστρέφεται ώστε να μείνει μόνο ένας
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        playerStartPosition = player.transform.position;
        StartRound();
    }

    void Update()
    {
        // Ο χρόνος μετρά μόνο όσο ο γύρος είναι σε εξέλιξη
        if (!RoundActive) return;

        RoundTime += Time.deltaTime;

        UpdateUI(null);
    }

    // Μηδενίζει τους μετρητές, επαναφέρει τον παίκτη και δημιουργεί τον πρώτο λίθο
    public void StartRound()
    {
        CurrentRound++;
        GemsCollected = 0;
        RoundTime = 0f;
        RoundActive = true;

        player.ResetTo(playerStartPosition);
        player.canMove = true;

        SpawnGem();
        UpdateUI(null);

        // Το ?. καλεί το event μόνο αν έχει εγγραφεί τουλάχιστον ένα script
        OnRoundStarted?.Invoke();
    }

    // Καλείται από τον λίθο όταν τον μαζέψει ο παίκτης
    public void OnGemCollected(Gem gem)
    {
        // Αγνοείται αν ο γύρος έχει ήδη τελειώσει
        if (!RoundActive) return;

        GemsCollected++;
        currentGem = null;
        OnGemPicked?.Invoke();

        // Νίκη όταν μαζευτούν όλοι οι λίθοι, αλλιώς εμφανίζεται ο επόμενος
        if (GemsCollected >= gemsPerRound)
        {
            EndRound(true);
        }
        else
        {
            SpawnGem();
        }
    }

    // Τερματίζει τον γύρο με ήττα
    public void PlayerCaught()
    {
        if (!RoundActive) return;
        EndRound(false);
    }

    // Σταματά τον γύρο, ενημερώνει τα στατιστικά και ξεκινά την αναμονή για τον επόμενο
    private void EndRound(bool won)
    {
        RoundActive = false;
        player.canMove = false;

        // Αφαίρεση του λίθου που έμεινε στη σκηνή
        if (currentGem != null)
        {
            Destroy(currentGem);
            currentGem = null;
        }

        if (won) RoundsWon++;
        else RoundsLost++;

        UpdateUI(won ? "ROUND WON!" : "CAUGHT!");
        OnRoundEnded?.Invoke(won);

        StartCoroutine(RestartAfterDelay());
    }

    // Περιμένει restartDelay δευτερόλεπτα και ξεκινά νέο γύρο
    private IEnumerator RestartAfterDelay()
    {
        yield return new WaitForSeconds(restartDelay);
        StartRound();
    }

    // Δημιουργεί αντίγραφο του prefab σε ελεύθερη θέση
    private void SpawnGem()
    {
        Vector3 position = FindFreePosition();
        currentGem = Instantiate(gemPrefab, position, Quaternion.identity);
    }

    // Δοκιμάζει τυχαίες θέσεις μέχρι να βρει μία μακριά από εμπόδια και από τον παίκτη
    private Vector3 FindFreePosition()
    {
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            // Το UnityEngine.Random γράφεται ολόκληρο επειδή υπάρχει και το System.Random
            Vector3 candidate = new Vector3(
                UnityEngine.Random.Range(-arenaHalfSize, arenaHalfSize),
                0.5f,
                UnityEngine.Random.Range(-arenaHalfSize, arenaHalfSize));

            // Απόρριψη αν μια σφαίρα γύρω από τη θέση ακουμπά εμπόδιο
            if (Physics.CheckSphere(candidate, clearanceRadius, obstacleMask)) continue;

            // Απόρριψη αν η θέση είναι πολύ κοντά στον παίκτη
            if (Vector3.Distance(candidate, player.transform.position) < minDistanceFromPlayer) continue;

            return candidate;
        }

        // Αν καμία δοκιμή δεν πετύχει, χρησιμοποιείται το κέντρο της αρένας
        Debug.LogWarning("GameManager: δεν βρέθηκε ελεύθερη θέση για λίθο, χρησιμοποιείται το κέντρο.");
        return new Vector3(0f, 0.5f, 0f);
    }

    // Ενημερώνει το κείμενο κατάστασης, με προαιρετικό μήνυμα σε επιπλέον γραμμή
    private void UpdateUI(string message)
    {
        if (statusText == null) return;

        string text = $"Round {CurrentRound}   Gems {GemsCollected}/{gemsPerRound}   Time {RoundTime:0.0}s\n" +
                      $"Won {RoundsWon}   Lost {RoundsLost}";

        if (message != null) text += $"\n{message}";

        statusText.text = text;
    }
}
