using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemySimpleWander : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float changeDirectionTime = 1.5f;

    private Rigidbody2D rb;
    private Animator anim;
    private Vector2 velocity;
    private float timer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        PickNewDirection();
    }

    void FixedUpdate()
    {
        timer -= Time.fixedDeltaTime;

        if (timer <= 0)
        {
            PickNewDirection();
        }

        rb.linearVelocity = velocity;
        UpdateAnimation();
    }

    private void PickNewDirection()
    {
        // Pick a completely random direction
        velocity = Random.insideUnitCircle.normalized * moveSpeed;

        // How often the direction should change
        timer = changeDirectionTime;
    }

    // ------------------------------
    // LITTLE DUDE ANIMATIONS
    // ------------------------------
    private void UpdateAnimation()
    {
        if (velocity == Vector2.zero)
        {
            anim.Play("Idle_Little Dude");
            return;
        }

        if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y))
        {
            if (velocity.x > 0)
                anim.Play("Right_Little Dude");
            else
                anim.Play("Left_Little Dude");
        }
        else
        {
            if (velocity.y > 0)
                anim.Play("Back_Little Dude");
            else
                anim.Play("Front_Little Dude");
        }
    }
}
