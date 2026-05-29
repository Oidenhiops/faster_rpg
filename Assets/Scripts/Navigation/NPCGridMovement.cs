using UnityEngine;

public class NPCGridMovement : CharacterGridNavigator
{
    public Rigidbody rb;

    public override void HandleInitialize() { }

    public override void HandleMovement()
    {
        if (rb == null) return;

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

        Vector3 velocity = rb.linearVelocity;
        velocity.x = dir.x * speed;
        velocity.z = dir.z * speed;
        rb.linearVelocity = velocity;

        UpdateAnimationDirection(dir);

        AdvanceIfReached(transform.position);
    }

    void UpdateAnimationDirection(Vector3 dir)
    {
        if (characterBase == null) return;

        if (characterBase.characterAnimations != null
            && characterBase.characterAnimations.characterAnimationsSO != null
            && characterBase.characterAnimations.characterAnimationsSO.isEightDirections)
        {
            characterBase.directionAnimation.x = Mathf.RoundToInt(dir.x);
            characterBase.directionAnimation.z = Mathf.RoundToInt(dir.z);
        }
        else
        {
            if (dir.x > 0.1f)      characterBase.directionAnimation.x =  1;
            else if (dir.x < -0.1f) characterBase.directionAnimation.x = -1;

            if (dir.z > 0.1f)      characterBase.directionAnimation.z =  1;
            else if (dir.z < -0.1f) characterBase.directionAnimation.z = -1;
        }

        if (!characterBase.isDashing && !characterBase.isJumping)
        {
            characterBase.characterAnimations?.MakeAnimation("Walk");
        }
    }

    int ResolveSpeed()
    {
        if (characterBase == null) return 0;
        if (characterBase.charactersData == null || characterBase.charactersData.Length == 0) return 0;
        var data = characterBase.charactersData[characterBase.characterIndex];
        if (data == null || data.statistics == null) return 0;
        if (!data.statistics.TryGetValue(CharacterData.TypeStatistic.Spd, out var stat)) return 0;
        return stat.currentValue;
    }
}
