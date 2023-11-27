using UnityEngine;

public class CameraFollowScript : MonoBehaviour
{

    // The 'drone' will be set by the client once it has instanciated a player game object
    public GameObject drone = null;
    private Rigidbody droneRB = null;
    private DroneMovementScript droneMovementScript;

    private Vector3 velocityCameraFollow;
    public Vector3 behindPosition = new Vector3(0, 2, -4);
    public float angle;
    public float calcangle;
    private void FixedUpdate()
    {
        if (drone == null) { return; }
        if (droneRB == null)
        {
            droneRB = drone.GetComponent<Rigidbody>();
        }

        transform.position = Vector3.SmoothDamp(transform.position, drone.transform.TransformPoint(behindPosition) + Vector3.up * Input.GetAxis("Vertical"), ref velocityCameraFollow, 0.1f);

        if (!droneMovementScript)
        {
            droneMovementScript = drone.GetComponent<DroneMovementScript>();
        }
        calcangle = angle + (droneMovementScript.tiltAmountForward / 2) - droneRB.velocity.y;
        transform.rotation = Quaternion.Euler(new Vector3(calcangle, droneMovementScript.currentYRotation, 0));
    }
}
