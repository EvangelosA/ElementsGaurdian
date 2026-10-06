using System.Collections.Generic;
using UnityEngine;

// Καταγραφή του χρόνου κίνησης του παίκτη σε κάθε κελί ενός πλέγματος πάνω στην αρένα
public class PlayerTracker : MonoBehaviour
{
    [Header("Αναφορές")]
    public PlayerController player;

    [Header("Πλέγμα")]
    public float arenaHalfSize = 10f;        // το πλέγμα καλύπτει το διάστημα [-τιμή, +τιμή] σε X και Z
    public int cellsPerSide = 10;            // πλήθος κελιών σε κάθε πλευρά

    [Header("Καταγραφή")]
    public float sampleInterval = 0.25f;     // δευτερόλεπτα ανάμεσα σε δύο καταγραφές
    public float minSpeedToRecord = 0.5f;    // κάτω από αυτή την ταχύτητα δεν γίνεται καταγραφή
    public float decayPerRound = 0.85f;      // πολλαπλασιαστής όλων των τιμών στην αρχή κάθε γύρου

    // Το άθροισμα όλων των τιμών του πλέγματος
    public float TotalHeat { get; private set; } = 0f;

    // Το μήκος της πλευράς ενός κελιού σε μονάδες του κόσμου
    public float CellSize => (arenaHalfSize * 2f) / cellsPerSide;

    private float[,] heat;                   // χρόνος κίνησης του παίκτη ανά κελί
    private float sampleTimer = 0f;

    void Awake()
    {
        heat = new float[cellsPerSide, cellsPerSide];
    }

    void Start()
    {
        GameManager.Instance.OnRoundStarted += HandleRoundStarted;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnRoundStarted -= HandleRoundStarted;
        }
    }

    void Update()
    {
        // Καταγραφή μόνο όσο ο γύρος είναι σε εξέλιξη
        if (!GameManager.Instance.RoundActive) return;

        sampleTimer += Time.deltaTime;
        if (sampleTimer < sampleInterval) return;
        sampleTimer = 0f;

        // Ο παίκτης που στέκεται ακίνητος δεν καταγράφεται
        if (player.CurrentVelocity.magnitude < minSpeedToRecord) return;

        Vector2Int cell = WorldToCell(player.transform.position);
        heat[cell.x, cell.y] += sampleInterval;
        TotalHeat += sampleInterval;
    }

    // Μετατρέπει μια θέση του κόσμου στις συντεταγμένες του κελιού που την περιέχει
    public Vector2Int WorldToCell(Vector3 position)
    {
        int x = Mathf.FloorToInt((position.x + arenaHalfSize) / CellSize);
        int z = Mathf.FloorToInt((position.z + arenaHalfSize) / CellSize);

        // Περιορισμός μέσα στα όρια του πίνακα
        x = Mathf.Clamp(x, 0, cellsPerSide - 1);
        z = Mathf.Clamp(z, 0, cellsPerSide - 1);

        return new Vector2Int(x, z);
    }

    // Επιστρέφει τη θέση του κόσμου στο κέντρο ενός κελιού
    public Vector3 CellToWorld(Vector2Int cell)
    {
        float x = -arenaHalfSize + (cell.x + 0.5f) * CellSize;
        float z = -arenaHalfSize + (cell.y + 0.5f) * CellSize;
        return new Vector3(x, 0f, z);
    }

    public float GetHeat(Vector2Int cell)
    {
        return heat[cell.x, cell.y];
    }

    // Η μεγαλύτερη τιμή του πλέγματος
    public float MaxHeat()
    {
        float max = 0f;
        for (int x = 0; x < cellsPerSide; x++)
        {
            for (int z = 0; z < cellsPerSide; z++)
            {
                if (heat[x, z] > max) max = heat[x, z];
            }
        }
        return max;
    }

    // Επιλέγει τυχαίο κελί, με πιθανότητα ανάλογη της τιμής του
    public Vector2Int PickWeightedCell()
    {
        // Τυχαίος αριθμός από 0 έως το συνολικό άθροισμα
        float target = Random.value * TotalHeat;
        float runningSum = 0f;

        // Το κελί επιλέγεται όταν το τρέχον άθροισμα ξεπεράσει τον τυχαίο αριθμό
        for (int x = 0; x < cellsPerSide; x++)
        {
            for (int z = 0; z < cellsPerSide; z++)
            {
                runningSum += heat[x, z];
                if (target < runningSum) return new Vector2Int(x, z);
            }
        }

        // Επιστρέφεται το θερμότερο κελί αν ο βρόχος τελειώσει χωρίς επιλογή
        List<Vector2Int> top = GetTopCells(1);
        return top.Count > 0 ? top[0] : new Vector2Int(cellsPerSide / 2, cellsPerSide / 2);
    }

    // Επιστρέφει έως count κελιά με τις μεγαλύτερες τιμές, σε φθίνουσα σειρά
    public List<Vector2Int> GetTopCells(int count)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        // Συλλογή όλων των κελιών με τιμή μεγαλύτερη από το μηδέν
        for (int x = 0; x < cellsPerSide; x++)
        {
            for (int z = 0; z < cellsPerSide; z++)
            {
                if (heat[x, z] > 0f) cells.Add(new Vector2Int(x, z));
            }
        }

        // Ταξινόμηση από τη μεγαλύτερη προς τη μικρότερη τιμή
        cells.Sort((a, b) => heat[b.x, b.y].CompareTo(heat[a.x, a.y]));

        // Αφαίρεση των κελιών μετά τα πρώτα count
        if (cells.Count > count) cells.RemoveRange(count, cells.Count - count);

        return cells;
    }

    // Μειώνει όλες τις τιμές και υπολογίζει ξανά το άθροισμα
    private void HandleRoundStarted()
    {
        TotalHeat = 0f;
        for (int x = 0; x < cellsPerSide; x++)
        {
            for (int z = 0; z < cellsPerSide; z++)
            {
                heat[x, z] *= decayPerRound;
                TotalHeat += heat[x, z];
            }
        }
    }

    // Σχεδιάζει κάθε κελί με χρώμα από μπλε (χαμηλή τιμή) έως κόκκινο (υψηλή τιμή)
    void OnDrawGizmos()
    {
        if (heat == null) return;

        float max = MaxHeat();
        if (max <= 0f) return;

        Color low = new Color(0f, 0f, 1f, 0.3f);
        Color high = new Color(1f, 0f, 0f, 0.7f);
        Vector3 size = new Vector3(CellSize * 0.95f, 0.05f, CellSize * 0.95f);

        for (int x = 0; x < cellsPerSide; x++)
        {
            for (int z = 0; z < cellsPerSide; z++)
            {
                // Τα κελιά χωρίς τιμή δεν σχεδιάζονται
                if (heat[x, z] <= 0f) continue;

                Gizmos.color = Color.Lerp(low, high, heat[x, z] / max);
                Gizmos.DrawCube(CellToWorld(new Vector2Int(x, z)) + Vector3.up * 0.05f, size);
            }
        }
    }
}
