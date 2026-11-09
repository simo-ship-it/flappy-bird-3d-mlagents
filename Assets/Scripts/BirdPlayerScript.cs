using UnityEngine;
using UnityEngine.InputSystem;

// Lab 3, slides 12-13: CharacterController movement, gravity and air flaps.
// FixedUpdate makes training and inference use the same physics timestep.
public class BirdPlayerScript : MonoBehaviour
{
    public FlappyWorld World;
    public bool freeMovement;
    public float moveSpeed = 8f, jumpHeight = 5f, gravity = -9.81f;
    public float VerticalVelocity { get; private set; }
    public CharacterController Controller { get; private set; }
    private bool flap;
    private Vector2 horizontal;

    private void Awake() { Controller = GetComponent<CharacterController>(); }
    public void RequestFlap() { flap = true; }
    public void ResetFlight(Vector3 position)
    {
        if (Controller == null) Controller = GetComponent<CharacterController>();
        Controller.enabled = false; transform.position = position;
        VerticalVelocity = 0; flap = false; Controller.enabled = true;
    }
    private void Update()
    {
        horizontal = Vector2.zero;
        var k = Keyboard.current;
        if (!freeMovement || k == null) return;
        horizontal.x = (k.rightArrowKey.isPressed ? 1 : 0) - (k.leftArrowKey.isPressed ? 1 : 0);
        horizontal.y = (k.upArrowKey.isPressed ? 1 : 0) - (k.downArrowKey.isPressed ? 1 : 0);
    }
    private void FixedUpdate()
    {
        if (World == null || World.Recorded) return;
        if (Controller.isGrounded && VerticalVelocity < 0) VerticalVelocity = -2;
        if (flap)
        {
            VerticalVelocity = Controller.isGrounded ?
                Mathf.Sqrt(jumpHeight * -2 * gravity) :
                VerticalVelocity + Mathf.Sqrt(jumpHeight / 4 * -2 * gravity);
            flap = false; World.Flaps++;
        }
        VerticalVelocity = Mathf.Clamp(VerticalVelocity + gravity * Time.fixedDeltaTime, -16, 16);
        Controller.Move(new Vector3(horizontal.x * moveSpeed, VerticalVelocity, horizontal.y * moveSpeed) * Time.fixedDeltaTime);
        // CharacterController depenetration can push a character sideways even
        // when Move receives no horizontal action. Keep the learning rail exact.
        var local = transform.localPosition;
        if (!freeMovement && (Mathf.Abs(local.x) > 0.01f || Mathf.Abs(local.z) > 0.01f))
        {
            World.PendingHit = true;
            Controller.enabled = false;
            transform.localPosition = new Vector3(0, local.y, 0);
            Controller.enabled = true;
        }
    }
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider.GetComponentInParent<PipesMoves>() != null) World.PendingHit = true;
    }
}

