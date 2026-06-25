using UnityEngine;

public class NPCGridMovement : CharacterGridNavigator
{
    public Rigidbody rb;
    public float jumpForce = 5f;
    public float verticalReachThreshold = 0.3f;
    public float jumpCooldown = 0.25f;

    float lastJumpTime = -999f;

    public override void HandleInitialize() { }

    public override void HandleMovement()
    {
        if (rb == null) return;

        if (characterBase != null && characterBase.isJumping
            && characterBase.isGrounded && rb.linearVelocity.y <= 0.01f
            && Time.time - lastJumpTime > 0.1f)
        {
            characterBase.isJumping = false;
        }

        if (!HasPath || IsAtDestination)
        {
            Vector3 stop = rb.linearVelocity;
            stop.x = 0f;
            stop.z = 0f;
            rb.linearVelocity = stop;

            if (characterBase != null && !characterBase.isDashing && !characterBase.isJumping)
            {
                characterBase.characterAnimations?.MakeAnimation("Idle");
            }
            return;
        }

        Vector3 dir = GetDesiredDirection(transform.position);
        int speed = ResolveSpeed();
        Vector3 wp = CurrentWaypoint;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = dir.x * speed;
        velocity.z = dir.z * speed;

        bool needsJump = wp.y - transform.position.y > verticalReachThreshold;
        bool canJump = characterBase != null
                    && characterBase.isGrounded
                    && !characterBase.isJumping
                    && Time.time - lastJumpTime > jumpCooldown;

        if (needsJump && canJump)
        {
            velocity.y = jumpForce;
            characterBase.isJumping = true;
            lastJumpTime = Time.time;
            characterBase.characterAnimations?.MakeAnimation("Jump");
        }

        rb.linearVelocity = velocity;

        UpdateAnimationDirection(dir);

        AdvanceIfReached(transform.position);
    }

    void UpdateAnimationDirection(Vector3 dir)
    {
        if (characterBase == null) return;

        if (!characterBase.isDashing && !characterBase.isJumping)
        {
            characterBase.characterAnimations?.MakeAnimation("Walk");
        }
    }

    int ResolveSpeed()
    {
        return characterBase.charactersData[characterBase.characterIndex]
                            .statistics[CharacterData.TypeStatistic.Spd]
                            .currentValue;
    }
}
