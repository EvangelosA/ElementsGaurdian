using UnityEngine;

// Λίθος που περιστρέφεται, κινείται πάνω-κάτω και μαζεύεται από τον παίκτη
public class Gem : MonoBehaviour
{
    public float spinSpeed = 90f;     // μοίρες ανά δευτερόλεπτο
    public float bobHeight = 0.15f;   // πλάτος της κίνησης πάνω-κάτω
    public float bobSpeed = 2f;       // ταχύτητα της κίνησης πάνω-κάτω

    private Vector3 startPosition;    // θέση γύρω από την οποία γίνεται η κίνηση πάνω-κάτω
    private bool collected = false;   // αποτρέπει να μετρηθεί ο λίθος δεύτερη φορά

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // Περιστροφή γύρω από τον κατακόρυφο άξονα του κόσμου
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        // Ομαλή κίνηση πάνω-κάτω με τη συνάρτηση ημιτόνου
        float offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPosition + Vector3.up * offset;
    }

    void OnTriggerEnter(Collider other)
    {
        // Αγνοείται αν έχει ήδη μαζευτεί
        if (collected) return;

        // Αγνοείται κάθε αντικείμενο που δεν είναι ο παίκτης
        if (!other.CompareTag("Player")) return;

        collected = true;
        GameManager.Instance.OnGemCollected(this);
        Destroy(gameObject);
    }
}
