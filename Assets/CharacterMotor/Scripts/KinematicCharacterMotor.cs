using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Basic kinematic player motor demonstrating how to implement a motor for a KinematicBody.
/// 
/// Feel free to modify or ideally duplicate this to add custom behavior.
/// </summary>
public class KinematicCharacterMotor : MonoBehaviour, IKinematicMotor
{
    [Header("Body")]
    public KinematicBody body;
    /// <summary>
    /// Get/set the position of the kinematic body of the motor.
    /// </summary>
    public Vector3 Position
    {
        get => body.BodyRigidbody.position;
        set => body.BodyRigidbody.position = value;
    }
    /// <summary>
    /// Get/set the rotation of the kinematic body of the motor.
    /// </summary>
    public Quaternion Rotation
    {
        get => body.BodyRigidbody.rotation;
        set => body.BodyRigidbody.rotation = value;
    }

    [Header("Common Movement Settings")]
    public float moveSpeed = 8.0f;
    public float jumpHeight = 2.0f;
    
    [Header("Ground Movement")]
    public float maxGroundAngle = 75f;
    public float groundAccel = 200.0f;
    public float groundFriction = 12.0f;
    public LayerMask groundLayers = 1 << 0;
    public float maxGroundAdhesionDistance = 0.1f;
    
    public bool Grounded { get; private set; }
    private bool wasGrounded;
    
    [Header("Air Movement")]
    public float airAccel = 50.0f;
    public float airFriction = 3.0f;
    
    public Vector3 gravityScale = new Vector3(0, 2, 0);
    // velocity of the final object inclusive of external forces, given in world-space
    public Vector3 EffectiveGravity
    {
        get
        {
            Vector3 g = Physics.gravity;
            g.Scale(gravityScale);
            return g;
        }
    }
    
    public bool JumpedThisFrame { get; private set; }

    // Input handling
    private Vector3 moveWish;
    private bool jumpWish;

    //
    // Motor API
    //

    /// <summary>
    /// Sets the desired movement direction in world-space. Overwrites previous values.
    /// </summary>
    /// <param name="move">World space input direction</param>
    public void MoveInput(Vector3 move)
    {
        moveWish = move;
    }

    /// <summary>
    /// Queues a jump for processing on the next frame. Motor will not jump if it is not grounded at the start of the frame.
    /// </summary>
    public void JumpInput()
    {
        jumpWish = true;
    }

    /// <summary>
    /// Sets the position of the motor's kinematic body, bypassing interpolation.
    /// 
    /// This should not be called while the body is performing a move.
    /// </summary>
    /// <param name="newBodyPosition">The new position of the object.</param>
    public void SetPosition(Vector3 newBodyPosition) => Position = newBodyPosition;
    /// <summary>
    /// Sets the rotation of the motor's kinematic body, bypassing interpolation.
    /// 
    /// This should not be called while the body is performing a move.
    /// </summary>
    /// <param name="newBodyRotation">The new rotation of the object.</param>
    public void SetRotation(Quaternion newBodyRotation) => Rotation = newBodyRotation;
    /// <summary>
    /// Sets the rotation of the motor's kinematic body, bypassing interpolation.
    /// 
    /// This should not be called while the body is performing a move.
    /// </summary>
    /// <param name="newBodyRotationEuler">The new rotation of the object.</param>
    public void SetRotation(Vector3 newBodyRotationEuler) => Quaternion.Euler(newBodyRotationEuler);

    //
    // Motor Utilities
    //

    public Vector3 ClipVelocity(Vector3 inputVelocity, Vector3 normal)
    {
        return Vector3.ProjectOnPlane(inputVelocity, normal);
    }

    //
    // IKinematicMotor implementation
    //

    public Vector3 UpdateVelocity(Vector3 oldVelocity)
    {
        Vector3 velocity = oldVelocity;
        
        //
        // integrate player forces
        //
        
        if (jumpWish)
        {
            jumpWish = false;

            if(wasGrounded)
            {
                JumpedThisFrame = true;

                velocity.y += Mathf.Sqrt(-2.0f * EffectiveGravity.y * jumpHeight);
            }
        }
        
        bool isGrounded = !JumpedThisFrame && wasGrounded;

        float effectiveAccel = (isGrounded ? groundAccel : airAccel);
        float effectiveFriction = (isGrounded ? groundFriction : airFriction);
        
        // apply friction
        float keepY = velocity.y;
        velocity.y = 0.0f; // don't consider vertical movement in friction calculation
        float prevSpeed = velocity.magnitude;
        if (prevSpeed != 0)
        {
            float frictionAccel = prevSpeed * effectiveFriction * Time.deltaTime;
            velocity *= Mathf.Max(prevSpeed - frictionAccel, 0) / prevSpeed;
        }

        velocity.y = keepY;

        // apply movement
        moveWish = Vector3.ClampMagnitude(moveWish, 1);
        float velocityProj = Vector3.Dot(velocity, moveWish);
        float accelMag = effectiveAccel * Time.deltaTime;

        // clamp projection onto movement vector
        if (velocityProj + accelMag > moveSpeed)
        {
            accelMag = moveSpeed - velocityProj;
        }

        velocity += (moveWish * accelMag);
        
        if (body.BodyRigidbody.useGravity)
        {
            velocity += EffectiveGravity * Time.deltaTime;
        }

        return velocity;
    }

    public void OnResolveMove(ref Vector3 curPosition, ref Quaternion curRotation, ref Vector3 curVelocity, Collider other, Vector3 direction, float pen)
    {
        Vector3 clipped = ClipVelocity(curVelocity, direction);

        // floor
        if (groundLayers.Test(other.gameObject.layer) &&  // require ground layer
            direction.y > 0 &&                                      // direction check
            Vector3.Angle(direction, Vector3.up) < maxGroundAngle)  // angle check
        {
            // only change Y-position if bumping into the floor
            curPosition.y += direction.y * (pen);
            curVelocity.y = clipped.y;
            
            Grounded = true;
        }
        // other
        else
        {
            curPosition += direction * (pen);
            curVelocity = clipped;
        }
    }

    public void OnPreMove()
    {
        // reset frame data
        JumpedThisFrame = false;
        Grounded = false;
    }

    public void OnFinishMove(ref Vector3 curPosition, ref Quaternion curRotation, ref Vector3 curVelocity)
    {
        // Ground Adhesion

        // early exit if we're already grounded or jumping
        if (Grounded || JumpedThisFrame || !wasGrounded) return;
        
        var groundCandidates = body.Cast(curPosition, Vector3.down, maxGroundAdhesionDistance, groundLayers, QueryTriggerInteraction.Ignore);
        Vector3 snapPosition = curPosition;
        foreach (var candidate in groundCandidates)
        {
            // ignore colliders that we start inside of - it's either us or something bad happened
            if(candidate.point == Vector3.zero) { continue; }

            // NOTE: This code assumes that the ground will always be below us
            snapPosition.y = candidate.point.y - body.FootOffset.y - body.contactOffset;
            
            // Snap to the ground - perform any necessary collision and sliding logic
            body.DeferredCollideAndSlide(ref snapPosition, ref curRotation, ref curVelocity, candidate.collider);
            break;
        }

        curPosition = snapPosition;
    }

    public void OnPostMove()
    {
        // record grounded status for next frame
        wasGrounded = Grounded;
    }
    
    //
    // Unity Messages
    //

    private void Start()
    {
        body.motor = this;
    }

    private void Reset()
    {
        if(body == null) body = GetComponent<KinematicBody>();
    }
}