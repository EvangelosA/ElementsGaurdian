using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public GameObject Player;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3(5*Player.transform.position.x/10f,17,-12 +2*Player.transform.position.z/10f);
    }
}
