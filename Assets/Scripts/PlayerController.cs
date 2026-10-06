using UnityEngine;
using UnityEngine.InputSystem;

// Κίνηση του παίκτη με WASD ή βελάκια μέσω Rigidbody
public class PlayerController : MonoBehaviour
{
    [Header("Κίνηση")]
    public float moveSpeed = 5f;      // μονάδες ανά δευτερόλεπτο
    public float turnSpeed = 720f;    // μοίρες ανά δευτερόλεπτο

    public bool canMove = true;       // όταν είναι false, ο παίκτης μένει ακίνητος

    private Rigidbody rb;
    private Vector3 moveInput;        // κατεύθυνση από το πληκτρολόγιο, μήκους 0 ή 1

    // Η τρέχουσα ταχύτητα του παίκτη
    public Vector3 CurrentVelocity => rb.linearVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Η είσοδος διαβάζεται κάθε frame ώστε να μη χάνεται κανένα πάτημα
        moveInput = canMove ? ReadKeyboard() : Vector3.zero;
    }

    void FixedUpdate()
    {
        Vector3 velocity = moveInput * moveSpeed;

        // Η ταχύτητα στον άξονα Y μένει 0 ώστε ο παίκτης να κινείται μόνο στο επίπεδο
        rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);

        // Μηδενισμός της περιστροφής που προκαλεί η φυσική (τριβή, συγκρούσεις)
        rb.angularVelocity = Vector3.zero;

        // Στροφή μόνο όταν υπάρχει κίνηση, αλλιώς ο παίκτης κρατά την κατεύθυνσή του
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput, Vector3.up);
            Quaternion newRotation = Quaternion.RotateTowards(
                rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }
    }

    // Μεταφέρει ακαριαία τον παίκτη σε μια θέση και μηδενίζει κίνηση και περιστροφή
    public void ResetTo(Vector3 position)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Αλλάζουν και το Rigidbody και το Transform ώστε η interpolation
        // να μη δείξει τον κύβο να "γλιστράει" από την παλιά θέση στη νέα
        rb.position = position;
        transform.position = position;

        rb.rotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;
    }

    // Επιστρέφει την κατεύθυνση στο επίπεδο XZ σύμφωνα με τα πλήκτρα που είναι πατημένα
    private Vector3 ReadKeyboard()
    {
        Keyboard kb = Keyboard.current;

        // Χωρίς συνδεδεμένο πληκτρολόγιο δεν υπάρχει κίνηση
        if (kb == null) return Vector3.zero;

        float x = 0f;   // -1 αριστερά, +1 δεξιά
        float z = 0f;   // -1 κάτω, +1 πάνω

        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  z -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    z += 1f;

        // Το normalized κρατά το μήκος στο 1, ώστε η διαγώνια κίνηση να μην είναι γρηγορότερη
        return new Vector3(x, 0f, z).normalized;
    }
}
