using UnityEngine;

// Αναπαραγωγή μουσικής υπόκρουσης και ηχητικών εφέ σε απόκριση στις ειδοποιήσεις του παιχνιδιού
public class AudioManager : MonoBehaviour
{
    [Header("Αναφορές")]
    public GuardAI guard;

    [Header("Ήχοι")]
    public AudioClip backgroundMusic;
    public AudioClip gemPickup;
    public AudioClip guardAlert;
    public AudioClip playerCaught;
    public AudioClip roundWin;

    [Header("Ρυθμίσεις")]
    public float musicVolume = 0.4f;
    public float effectsVolume = 1f;
    public float alertCooldown = 2f;         // ελάχιστα δευτερόλεπτα ανάμεσα σε δύο ήχους εντοπισμού

    private AudioSource musicSource;
    private AudioSource effectsSource;
    private float lastAlertTime = -100f;     // αρνητική αρχική τιμή ώστε ο πρώτος ήχος να παίζει αμέσως

    void Awake()
    {
        // Δύο ξεχωριστές πηγές ήχου: μία για τη μουσική και μία για τα εφέ
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.playOnAwake = false;
    }

    void Start()
    {
        GameManager.Instance.OnGemPicked += HandleGemPicked;
        GameManager.Instance.OnRoundEnded += HandleRoundEnded;
        if (guard != null) guard.OnPlayerSpotted += HandlePlayerSpotted;

        if (backgroundMusic != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.Play();
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGemPicked -= HandleGemPicked;
            GameManager.Instance.OnRoundEnded -= HandleRoundEnded;
        }
        if (guard != null) guard.OnPlayerSpotted -= HandlePlayerSpotted;
    }

    private void HandleGemPicked()
    {
        PlayEffect(gemPickup);
    }

    private void HandleRoundEnded(bool playerWon)
    {
        PlayEffect(playerWon ? roundWin : playerCaught);
    }

    private void HandlePlayerSpotted()
    {
        // Ο ήχος παραλείπεται αν έχει ακουστεί πριν από λιγότερο από alertCooldown δευτερόλεπτα
        if (Time.time - lastAlertTime < alertCooldown) return;

        lastAlertTime = Time.time;
        PlayEffect(guardAlert);
    }

    // Το PlayOneShot επιτρέπει σε πολλούς ήχους να ακούγονται ταυτόχρονα από την ίδια πηγή
    private void PlayEffect(AudioClip clip)
    {
        if (clip != null) effectsSource.PlayOneShot(clip, effectsVolume);
    }
}
