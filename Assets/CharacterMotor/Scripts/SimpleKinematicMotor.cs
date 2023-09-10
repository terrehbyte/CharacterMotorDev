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
    
    private Collider[] lastStepCollisions = new Collider[32];
    private int lastStepCollisionCount = 0;
    
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
    [SerializeField]
    private bool useGroundNormalProjection = true;
    [SerializeField]
    private bool useStairStepping = true;
    public float stepHeight = 0.5f;
    
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
    
    public int Overlap(Vector3 bodyPosition, Vector3 halfExtents, Collider[] results)
    {
        return Physics.OverlapBoxNonAlloc(bodyPosition + transform.TransformVector(boxCollider.center), halfExtents, results);
    }

    public int Overlap(Vector3 bodyPosition, Collider[] results)
    {
        Vector3 worldBodyHalfSize = transform.TransformVector(boxCollider.size / 2.0f);
        return Overlap(bodyPosition, worldBodyHalfSize, results);
    }

    public int Cast(Vector3 bodyPosition, Vector3 halfExtents, Vector3 direction, RaycastHit[] hits, float distance)
    {
        return Physics.BoxCastNonAlloc(bodyPosition + transform.TransformVector(boxCollider.center), halfExtents, direction, hits, Quaternion.identity, distance);
    }
    
    public int Cast(Vector3 bodyPosition, Vector3 direction, RaycastHit[] hits, float distance)
    {
        Vector3 worldBodyHalfSize = transform.TransformVector(boxCollider.size / 2.0f);
        return Cast(bodyPosition, worldBodyHalfSize, direction, hits, distance);
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
        
        // todo probably could cache these
        Vector3 offsetToCenter = transform.TransformVector(boxCollider.center);
        Vector3 offsetToBottom = offsetToCenter + transform.TransformVector(new Vector3(0,-boxCollider.size.y / 2.0f,0));

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

        // process forces
        if (useGravity)
        {
            Velocity += Physics.gravity * (gravityMultiplier * Time.deltaTime);
        }
        
        Vector3 effectiveVelocity = Velocity;
        
        // reproject velocity if needed
        if (useGroundNormalProjection && wasGrounded && !jumpedThisFrame)
        {
            effectiveVelocity.y = 0.0f;
            effectiveVelocity = Vector3.ProjectOnPlane(effectiveVelocity, lastGroundNormal);
            effectiveVelocity.y = Velocity.y;
        }
        
        Debug.DrawRay(projectedPosition, effectiveVelocity.normalized * 5.0f, Color.cyan);
        
        projectedPosition += effectiveVelocity * Time.deltaTime;
        
        bool groundedThisFrame = false;
        
        const int MAX_ITERATIONS = 16;
        for (int solverIt = 0; solverIt < MAX_ITERATIONS; ++solverIt)
        {
            lastProjectedCollisionCount = Overlap(projectedPosition, lastProjectedCollisions);
            
            for (int i = 0; i < lastProjectedCollisionCount; ++i)
            {
                Collider otherCollider = lastProjectedCollisions[i];
                // ignore our own collider
                if (boxCollider == otherCollider) { continue; }

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
                        Debug.DrawRay(projectedPosition, lastGroundNormal * 5.0f, Color.red);

                        // only resolve Y on position
                        Vector3 mtv = penDir * penDepth;
                        projectedPosition.y += mtv.y;

                        // only resolve Y on velocity
                        Vector3 clippedVelocity = Vector3.ProjectOnPlane(Velocity, penDir);
                        velocity.y = clippedVelocity.y;

                        // nothing changed here, let's move onto the next object 
                        if (penDepth < skinWidth) { continue; }
                    }
                    // walls / other objects
                    else
                    {
                        // too small to care about
                        if (penDepth < skinWidth) { continue; }

                        Vector3 stepOffset = new Vector3(0, stepHeight, 0); 
                        
                        // can we avoid it by going over it?
                        if (useStairStepping && wasGrounded)
                        {
                            // TODO: consolidate the step ground detection logic into a reusable method since we do the same
                            //       thing later for the ground adhesion logic
                            
                            // TODO: always perform step ground detection logic followed by overlap detection in order to
                            //       prevent failure in situations where the ceiling is too low to step over but there is
                            //       technically enough clearance to walk under it / stand on the next step
                            
                            lastStepCollisionCount = Overlap(projectedPosition + stepOffset, lastStepCollisions);
                            if (lastStepCollisionCount > 0)
                            {
                                bool canStep = true;

                                for (int j = 0; j < lastStepCollisionCount; ++j)
                                {
                                    Collider currentStepCollider = lastStepCollisions[j];
                                    // ignore our own collider
                                    if (boxCollider == currentStepCollider)
                                    {
                                        continue;
                                    }

                                    Transform stepTransform = currentStepCollider.transform;

                                    bool isStepPen = Physics.ComputePenetration(boxCollider,
                                        projectedPosition + new Vector3(0, stepHeight, 0), Quaternion.identity,
                                        currentStepCollider, stepTransform.position, stepTransform.rotation,
                                        out Vector3 stepPenDir, out float stepPenDepth);

                                    if (isStepPen)
                                    {
                                        if (stepPenDepth > skinWidth)
                                        {
                                            canStep = false;
                                            break;
                                        }
                                    }
                                }

                                if (canStep)
                                {
                                    float yOffset = offsetToCenter.y - offsetToBottom.y;
        
                                    const float GROUND_ADHESION_THICKNESS = 1.0f;
                                    Vector3 worldSizeWithSkin = transform.TransformVector((boxCollider.size / 2.0f) - new Vector3(skinWidth,skinWidth,skinWidth));
            
                                    Vector3 boxHalfWithSkin = worldSizeWithSkin;
                                    boxHalfWithSkin.y = GROUND_ADHESION_THICKNESS / 2.0f;
            
                                    cachedGroundAdhesionResultsCount = Cast(projectedPosition + stepOffset, boxHalfWithSkin, Vector3.down, cachedGroundAdhesionResults, yOffset + groundAdhesionDistance);
                                    
                                    bool canGroundOnStep = false;
                                    for (int j = 0; j < cachedGroundAdhesionResultsCount; ++j)
                                    {
                                        if (cachedGroundAdhesionResults[j].collider != otherCollider) { continue; }

                                        // exit early w/ failure if we could step over but we can't actually stand on it
                                        if (!(Vector3.Angle(cachedGroundAdhesionResults[j].normal, Vector3.up) <
                                              maxGroundAngle)) { break; }

                                        canGroundOnStep = true;
                                        groundedThisFrame = true;
                                        lastGroundNormal = cachedGroundAdhesionResults[j].normal;
                                        
                                        projectedPosition = projectedPosition + stepOffset + Vector3.down * (cachedGroundAdhesionResults[j].distance - GROUND_ADHESION_THICKNESS / 2.0f);
                                    }

                                    // TODO: what happens about the grounded state?
                                    // do we have to catch it in the ground adhesion routine?
                                    if (canGroundOnStep)
                                    {
                                        goto TO_NEXT_ITERATION;
                                    }
                                }
                            }
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
            float yOffset = offsetToCenter.y - offsetToBottom.y;
        
            const float GROUND_ADHESION_THICKNESS = 1.0f;
            Vector3 worldSizeWithSkin = transform.TransformVector((boxCollider.size / 2.0f) - new Vector3(skinWidth,skinWidth,skinWidth));
            
            Vector3 boxHalfWithSkin = worldSizeWithSkin;
            boxHalfWithSkin.y = GROUND_ADHESION_THICKNESS / 2.0f;
            
            cachedGroundAdhesionResultsCount = Cast(projectedPosition, boxHalfWithSkin, Vector3.down, cachedGroundAdhesionResults, yOffset + groundAdhesionDistance);
            for(int i = 0; i < cachedGroundAdhesionResultsCount; ++i)
            {
                RaycastHit hit = cachedGroundAdhesionResults[i];

                // ignore ourselves
                if(hit.collider == boxCollider) { continue; }
                // ignore objects we're inside of
                if (hit.distance == 0.0f) { continue; }

                if (Vector3.Angle(hit.normal, Vector3.up) < maxGroundAngle)
                {
                    groundedThisFrame = true;
                    lastGroundNormal = hit.normal;
                    
                    projectedPosition += Vector3.down * (hit.distance - GROUND_ADHESION_THICKNESS / 2.0f);

                    Debug.DrawRay(projectedPosition, lastGroundNormal * 5.0f, Color.yellow);
                    
                    // only resolve Y on velocity
                    Vector3 clippedVelocity = Vector3.ProjectOnPlane(Velocity, hit.normal);
                    velocity.y = clippedVelocity.y;
                    
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
