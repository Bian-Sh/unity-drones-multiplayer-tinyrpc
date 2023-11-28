using UnityEngine;
using zFramework.TinyRPC;

public class Player
{
    public Session session;
    public int playerid;
    public string playerName;
    public GameObject avatar;
    public Vector3 position;
    public Vector3 velocity;
    public Quaternion rotation;
    public Rigidbody rigidbody;

    internal void CapturePlayerState()
    {
        if (session != null && avatar)
        {
            position = avatar.transform.position;
            velocity = rigidbody.velocity;
            rotation = avatar.transform.rotation;
        }
    }
}
