using UnityEngine;

public class Trampoline : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float jumpForce = 20f;
    
    private static readonly int activatedAnim = Animator.StringToHash("Activated");

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            animator.SetTrigger(activatedAnim);

            PlayerMovement playerMovement = collision.gameObject.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.ResetJump();
            }
            
            collision.gameObject.GetComponent<Rigidbody2D>().AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }
}
