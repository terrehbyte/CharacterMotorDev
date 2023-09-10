// TODO: iterative depenetration

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[SelectionBase]
public class SimpleKinematicMotor : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    private Rigidbody rbody;
    [SerializeField]
    private BoxCollider boxCollider;
    private PlayerController controller;

    private Vector3 moveWish;
    private bool jumpWish;

    private CallbackGroup inputCallbackGroup = new();

    [Header("Motor")]
    [SerializeField]
    private float maxSpeed = 8.0f;
    [SerializeField]
    private float acceleration = 12.0f;
    [SerializeField]
    private float maxGroundAngle = 60.0f;
    private Vector3 lastGroundNormal;
    
    [Space]
    [SerializeField]
    private float groundFriction = 5.0f;
    
    [Space]
    public float groundAdhesionDistance = 0.1f;
    private RaycastHit[] cachedGroundAdhesionResults;
    private int cachedGroundAdhesionResultsCount = 0;

    [Space]
    [SerializeField]
    private float jumpForce = 8.0f;

    private Collider[] lastProjectedCollisions = new Collider[32];
    private int lastProjectedCollisionCount = 0;
    
    [Space]
    public bool useGravity = true;
    public float gravityMultiplier = 1.0f;

    [Header("Collision")]
    [SerializeField]
    private float skinWidth = 0.01f;

    [Header("Features")]
    [SerializeField]
    private bool preventClimbing = true;
    [SerializeField]
    private bool useGroundAdhesion = true;
    
    private Vector3 velocity;
    public Vector3 Velocity
    {
        get => velocity;
        set => velocity = value;
    }

    private bool wasGrounded;
    public bool Grounded => wasGrounded;

    private void SetupPlayerInputBindings()
    {
        if (controller != null && controller.Input != null)
        {
            inputCallbackGroup.BindActionMap(controller.Input.currentActionMap);

            inputCallbackGroup.AddBinding("Move", InputActionPhase.Performed, HandleMove);
            inputCallbackGroup.AddBinding("Move", InputActionPhase.Canceled, HandleMove);
            inputCallbackGroup.AddBinding("Jump", InputActionPhase.Performed, HandleJump);
        }
    }

    public void Possess(PlayerController controller)
    {
        this.controller = controller;
        SetupPlayerInputBindings();
    }

    public void Unpossess()
    {
        inputCallbackGroup.UnbindActionMap();
    }

    public void SetMovementInput(Vector3 newMove)
    {
        moveWish = newMove;
    }

    public void SetJumpInput(bool newJump)
    {
        jumpWish = newJump;
    }

    private void HandleJump(InputAction.CallbackContext obj)
    {
        jumpWish = true;
    }
    private void HandleMove(InputAction.CallbackContext obj)
    {
        if(obj.performed)
        {
            Vector2 moveInput = obj.ReadValue<Vector2>();
            moveWish.x = moveInput.x;
            moveWish.z = moveInput.y;
        }
        else if (obj.canceled)
        {
            moveWish.x = moveWish.z = 0.0f;
        }
    }

    private void OnEnable()
    {
        if(controller != null && !inputCallbackGroup.IsBound)
        {
            SetupPlayerInputBindings();
        }
    }
    private void OnDisable()
    {
        inputCallbackGroup.UnbindActionMap();
    }

    private void Start()
    {
        cachedGroundAdhesionResults = new RaycastHit[32];
    }

    private void FixedUpdate()
    {
        // calculate projected position
        Vector3 projectedPosition = rbody.position;

        // applying friction
        float keepY = Velocity.y;
        velocity.y = 0.0f;
        float groundSpeed = velocity.magnitude;
        if(groundSpeed != 0)
        {
            float frictionAccel = groundSpeed * groundFriction * Time.deltaTime;
            velocity *= Mathf.Max(groundSpeed - frictionAccel, 0) / groundSpeed;
        }
        velocity.y = keepY;

        // process player input
        float projectedVel = Vector3.Dot(Velocity, moveWish);
        float accelMag = acceleration * Time.deltaTime;

        if(projectedVel + accelMag > maxSpeed)
        {
            accelMag = maxSpeed - projectedVel;
        }
        Velocity += moveWish * accelMag;

        // process jump
        bool jumpedThisFrame = false;
        if(jumpWish)
        {
            jumpWish = false;

            if (wasGrounded)
            {
                jumpedThisFrame = true;
                velocity.y = jumpForce;
            }
        }
        
        // reproject velocity if needed
        if (wasGrounded && !jumpedThisFrame)
        {
            Velocity = Vector3.ProjectOnPlane(Velocity, lastGroundNormal);
        }

        // process forces
        if (useGravity)
        {
            Velocity += Physics.gravity * (gravityMultiplier * Time.deltaTime);
        }

        projectedPosition += Velocity * Time.deltaTime;

        const int MAX_ITERATIONS = 16;

        bool groundedThisFrame = false;
        
        for (int solverIt = 0; solverIt < MAX_ITERATIONS; ++solverIt)
        {
            Vector3 boxCenter = transform.TransformPoint(boxCollider.center);

            // check for collisions
            lastProjectedCollisionCount =
                Physics.OverlapBoxNonAlloc(boxCenter, boxCollider.size / 2.0f, lastProjectedCollisions);
            
            for (int i = 0; i < lastProjectedCollisionCount; ++i)
            {
                Collider otherCollider = lastProjectedCollisions[i];
                // ignore our own collider
                if (boxCollider == otherCollider)
                {
                    continue;
                }

                Transform otherTransform = otherCollider.transform;

                bool isPen = Physics.ComputePenetration(boxCollider, projectedPosition, Quaternion.identity,
                    otherCollider, otherTransform.position, otherTransform.rotation,
                    out Vector3 penDir, out float penDepth);

                // depenetrate if actually colliding
                if (isPen)
                {
                    // floor
                    if (Vector3.Angle(penDir, Vector3.up) < maxGroundAngle)
                    {
                        groundedThisFrame = true;
                        lastGroundNormal = penDir;

                        // only resolve Y on position
                        Vector3 mtv = penDir * penDepth;
                        projectedPosition.y += mtv.y;

                        // only resolve Y on velocity
                        Vector3 clippedVelocity = Vector3.ProjectOnPlane(Velocity, penDir);
                        velocity.y = clippedVelocity.y;
                    }
                    // walls / other objects
                    else
                    {
                        // too small to care about
                        if (penDepth < skinWidth)
                        {
                            continue;
                        }

                        // prevent climbing!!
                        float oldPosY = projectedPosition.y;
                        float oldVelY = Velocity.y;

                        // resolving the position
                        projectedPosition += penDir * penDepth;
                        velocity = Vector3.ProjectOnPlane(velocity, penDir);

                        if (preventClimbing)
                        {
                            projectedPosition.y = Mathf.Min(projectedPosition.y, oldPosY);
                            velocity.y = Mathf.Min(velocity.y, oldVelY);
                        }
                    }
                    
                    goto TO_NEXT_ITERATION;
                }
            }
            
            // if we didn't collide, we're done
            break;
            
            TO_NEXT_ITERATION: ;
        }

        // ground adhesion
        if(useGroundAdhesion && !jumpedThisFrame && wasGrounded && !groundedThisFrame)
        {
            Vector3 boxCenter = transform.TransformPoint(boxCollider.center);
            cachedGroundAdhesionResultsCount = Physics.BoxCastNonAlloc(boxCenter, boxCollider.size / 2.0f, Vector3.down, cachedGroundAdhesionResults, Quaternion.identity, groundAdhesionDistance);
            for(int i = 0; i < cachedGroundAdhesionResultsCount; ++i)
            {
                RaycastHit hit = cachedGroundAdhesionResults[i];

                // ignore ourselves
                if(hit.collider == boxCollider) { continue; }

                if (Vector3.Angle(hit.normal, Vector3.up) < maxGroundAngle)
                {
                    groundedThisFrame = true;
                    lastGroundNormal = hit.normal;
                    
                    projectedPosition += Vector3.down * hit.distance;

                    break;
                }
            }
        }

        // callbacks
        if(wasGrounded != groundedThisFrame)
        {
            if(groundedThisFrame)
            {
                Debug.Log("Grounded!");
            }
            else if (jumpedThisFrame)
            {
                Debug.Log("Dismount - Jumped!");
            }
            else
            {
                Debug.Log("Dismount - Falling!");
            }
        }

        // apply the move
        rbody.MovePosition(projectedPosition);

        wasGrounded = groundedThisFrame;
    }
}
