using UnityEngine;

// Lab 3, slide 16: follow after the player has moved.
public class FollowPlayer : MonoBehaviour
{
    public Transform player;
    public Vector3 offset = new Vector3(8, 5, -42);
    public float smoothSpeed = 5f;
    private void LateUpdate()
    {
        if (player == null) return;
        transform.position = Vector3.Lerp(transform.position, player.position + offset, smoothSpeed * Time.deltaTime);
        transform.LookAt(player.position + Vector3.right * 8);
    }
}

