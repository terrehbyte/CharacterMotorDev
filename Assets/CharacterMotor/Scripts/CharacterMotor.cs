using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MoreGizmos;

[SelectionBase]
[RequireComponent(typeof(CapsuleCollider))]
public class CharacterMotor : MonoBehaviour
{
    [Header("Dependencies")]
    public Camera playerCamera;
    public Rigidbody playerRigidbody;
    public CapsuleCollider playerCollider;

    [Header("Input")]
    private Vector3 moveWish;
    private bool jumpWish;
    public bool canQueueJump = false;
    public bool allowAutoBhop = false;

    [Header("Movement")]
    public Vector3 velocity;
    public bool shouldCollide = true;
    public bool forceNoCollision = false;

    public bool groundAdhesion = true;
    public float groundFriction = 11;
    public float groundAcceleration = 50;
    public float groundMaxSpeed = 5.0f;
    public float groundMaxAngle = 90.0f;
    // TODO: max ground angle!

    public float airAcceleration = 8;
    public float airMaxSpeed = 300.0f;
    private float gravityMultiplier = 2.0f;

    [Header("Ground Check")]
    public bool isGrounded;
    [SerializeField]
    private Collider groundCol;
    [SerializeField]
    private LayerMask groundCheckLayer;
    [SerializeField]
    private Vector3 groundNorm;

    Vector3 MovementNormal
    {
        get
        {
            return isGrounded ? groundNorm : Vector3.up;
        }
    }

    // Returns the direction that the character will move in given a controller-space input
    // controllerDir: direction of controller input
    // maxMagnitude: maximum magnitude of the returned vector
    public Vector3 ControllerToWorldDirection(Vector3 controllerDir, float maxMagnitude = 1)
    {
        controllerDir = controllerDir.normalized * Mathf.Min(maxMagnitude, controllerDir.magnitude);

        return controllerDir;
    }

    public Vector3 RotateByCameraYaw(Vector3 dir)
    {
        return Quaternion.Euler(0, playerCamera.transform.eulerAngles.y, 0) * dir;
    }

    // Returns the player's new velocity when moving on the ground
    // accelDir: world-space direction to accelerate in
    // prevVelocity: world-space velocity
    private Vector3 MoveGround(Vector3 accelDir, Vector3 prevVelocity, float acceleration, float maxSpeed)
    {
        float speed = prevVelocity.magnitude;

        if (speed != 0)
        {
            float drop = speed * groundFriction * Time.fixedDeltaTime;
            prevVelocity *= Mathf.Max(speed - drop, 0) / speed;
        }

        return Accelerate(accelDir, prevVelocity, acceleration, maxSpeed);
    }

    private Vector3 MoveAir(Vector3 accelDir, Vector3 prevVelocity, float acceleration, float maxSpeed)
    {
        return Accelerate(accelDir, prevVelocity, acceleration, maxSpeed);
    }

    // Returns the player's new velocity based on the given parameters
    // accelDir: world-space direction to accelerate in
    // prevVelocity: world-space velocity
    // accelerate: amount to accelerate by
    // maxSpeed: max player speed to achieve when accelerating
    private Vector3 Accelerate(Vector3 accelDir, Vector3 prevVelocity, float accelerate, float maxSpeed)
    {
        float projVel = Vector3.Dot(prevVelocity, accelDir);
        float accelVel = Mathf.Min(accelerate * Time.fixedDeltaTime, accelDir.magnitude);

        if(projVel + accelVel > maxSpeed)
        {
            accelVel = maxSpeed - projVel;
        }

        return prevVelocity + accelDir * accelVel;
    }

    // Returns the collider that the player is standing on
    // TODO: refactor this to be non-stateful
    private Collider QueryGroundCheck(out Vector3 groundPoint, out Vector3 groundNormal)
    {
        groundPoint = Vector3.zero;
        groundNormal = Vector3.zero;
        RaycastHit groundHit;

        var castOrigin = transform.TransformPoint(playerCollider.center);
        float groundCheckRayLength = playerCollider.height / 2.0f + Mathf.Max(-Physics.gravity.y * Time.deltaTime, velocity.y * Time.deltaTime);

        // FIRST PASS: prioritize what's immediately below us

        // TODO: remove magic value for cast direction (should change with gravity)
        var rayHits = Physics.RaycastAll(castOrigin, Vector3.down, groundCheckRayLength, groundCheckLayer, QueryTriggerInteraction.Ignore);
        Debug.DrawRay(transform.TransformPoint(playerCollider.center), Vector3.down * groundCheckRayLength, Color.red);

        rayHits = rayHits.OrderByDescending(x => x.distance).Reverse().ToArray();
        for (int i = 0; i < rayHits.Length; ++i)
        {
            var hit = rayHits[i];
            if ((hit.collider != playerCollider)&& Vector3.Angle(Vector3.up, hit.normal) <= groundMaxAngle)
            {
                groundHit = hit;
                groundPoint = hit.point;
                groundNormal = hit.normal;

                //Debug.Log("[MOTOR] Ground is immediately below the player: " + hit.collider.name);
                return hit.collider;
            }
        }

        // SECOND PASS: prioritize what's below the volume of the player

        // TODO: remove magic value for collider
        var groundCheckHits = Physics.SphereCastAll(transform.TransformPoint(playerCollider.center),
                                                         playerCollider.radius,
                                                         Vector3.down,
                                                         groundCheckRayLength - playerCollider.radius,
                                                         groundCheckLayer,
                                                         QueryTriggerInteraction.Ignore);
        GizmosEx.DrawSphere(transform.TransformPoint(playerCollider.center) + Vector3.down * (groundCheckRayLength - playerCollider.radius), playerCollider.radius, Color.cyan);

        groundCheckHits = groundCheckHits.OrderByDescending(x => x.distance).Reverse().ToArray();
        for (int i = 0; i < groundCheckHits.Length; ++i)
        {
            var hit = groundCheckHits[i];
            if ((hit.collider != playerCollider) && Vector3.Angle(Vector3.up, hit.normal) <= groundMaxAngle && hit.point != Vector3.zero)
            {
                groundHit = hit;
                groundPoint = hit.point;
                groundNormal = hit.normal;

                GizmosEx.DrawSphere(groundHit.point, 0.35f, Color.green);

                //Debug.Log("[MOTOR] Ground is around the player: " + hit.collider.name);
                return hit.collider;
            }
        }

        return null;
    }

    private Collider QueryGroundingGroundCheck(out Vector3 groundPoint, out Vector3 groundNormal)
    {
        groundPoint = Vector3.zero;
        groundNormal = Vector3.zero;
        RaycastHit groundHit;

        var castOrigin = transform.TransformPoint(playerCollider.center);
        float groundCheckRayLength = playerCollider.height / 2.0f + Mathf.Max(-Physics.gravity.y * Time.deltaTime, velocity.y * Time.deltaTime);

        // TODO: remove magic value for collider
        var groundCheckHits = Physics.SphereCastAll(transform.TransformPoint(playerCollider.center),
                                                         playerCollider.radius,
                                                         Vector3.down,
                                                         groundCheckRayLength - playerCollider.radius,
                                                         groundCheckLayer,
                                                         QueryTriggerInteraction.Ignore);
        GizmosEx.DrawSphere(transform.TransformPoint(playerCollider.center) + Vector3.down * (groundCheckRayLength - playerCollider.radius), playerCollider.radius, Color.cyan);

        groundCheckHits = groundCheckHits.OrderByDescending(x => x.distance).Reverse().ToArray();
        for (int i = 0; i < groundCheckHits.Length; ++i)
        {
            var hit = groundCheckHits[i];
            if ((hit.collider != playerCollider) && Vector3.Angle(Vector3.up, hit.normal) <= groundMaxAngle && hit.point != Vector3.zero)
            {
                groundHit = hit;
                groundPoint = hit.point;
                groundNormal = hit.normal;

                return hit.collider;
            }
        }

        return null;
    }

    private Vector3[] ResolveCollision(Vector3 otherPos, Vector3 otherVel, float otherMass, Vector3 normal)
    {
        const float elasticity = 0.5f;

        Vector3 relativeVel = velocity - otherVel;
        float impulseMag = Vector3.Dot(-(1.0f + elasticity) * relativeVel, normal) /
                           Vector3.Dot(normal, normal * (1 / playerRigidbody.mass + 1 / otherMass));

        // TODO: remove allocation
        return new Vector3[] { velocity + (impulseMag / playerRigidbody.mass) * normal,
                               otherVel - (impulseMag / otherMass) * normal };
    }

    private Vector3 ResolvePhysics(Transform otherTm, Rigidbody otherRb, Vector3 normal, float pen)
    {
        Vector3 otherVel;
        float otherMass;
        if(otherRb == null)
        {
            otherVel = Vector3.zero;
            otherMass = Mathf.Infinity;
        }
        else
        {
            otherVel = otherRb.velocity;
            otherMass = otherRb.mass;
        }

        var res = ResolveCollision(otherTm.position,
                                   otherVel,
                                   otherMass,
                                   normal);

        // TODO: should we resolve overlaps here?

        // does the other object move?
        if(otherRb != null && !otherRb.isKinematic)
        {
            otherRb.velocity = res[1];
        }

        return res[0];
    }

    private void Update()
    {
        moveWish = new Vector3(Input.GetAxisRaw("Horizontal"), 0.0f, Input.GetAxisRaw("Vertical"));

        if(allowAutoBhop)
        {
            jumpWish = Input.GetButton("Jump");
        }
        else
        {
            jumpWish = jumpWish || Input.GetButtonDown("Jump");
        }
    }
    
    private void FixedUpdate()
    {
        // determine grounded status
        Vector3 groundPoint;
        Vector3 initialPosition = transform.position;
        Vector3 potentialPosition = transform.position;
        bool wasGrounded = isGrounded;
        isGrounded = (groundCol = QueryGroundCheck(out groundPoint, out groundNorm)) != null;

        if (isGrounded)
        {
            var groundAlignmentCollider = QueryGroundingGroundCheck(out Vector3 groundAlignmentPoint, out Vector3 groundAlignmentNorm);
            if (groundAlignmentCollider != null && groundAdhesion)
            {
                Vector3 closestOnPlayer = playerCollider.ClosestPoint(groundAlignmentPoint);
                GizmosEx.DrawSphere(closestOnPlayer, 0.1f, Color.green);
                GizmosEx.DrawSphere(groundAlignmentPoint, 0.1f, Color.red);
                Vector3 offset = closestOnPlayer - transform.position;
                GizmosEx.DrawSphere(groundAlignmentPoint - offset, 0.05f, Color.blue);
                potentialPosition = (groundAlignmentPoint - offset);

                if (Vector3.Distance(initialPosition, potentialPosition) > 2.0f)
                {
                    Debug.LogFormat("{0} to {1}", initialPosition.ToString("F3"), potentialPosition.ToString("F3"));
                }
            }
        }

        if (jumpWish)
        {
            if (isGrounded)
            {
                Debug.Log("[MOTOR] Jumping! Now airborne.");
                isGrounded = false;
                velocity.y = 9.8f;
                potentialPosition.y += velocity.y * Time.fixedDeltaTime; // HACK
                jumpWish = false;
            }
            else
            {
                jumpWish = canQueueJump || false;
            }
        }

        if (!wasGrounded && isGrounded)
        {
            velocity.y = 0.0f;
            Debug.Log("[MOTOR] Landing detected.");
        }

        // process handle player input
        Vector3 worldDir = ControllerToWorldDirection(RotateByCameraYaw(moveWish));

        if(isGrounded)
        {
            velocity = MoveGround(worldDir, velocity, groundAcceleration, groundMaxSpeed);
        }
        else
        {
            velocity = MoveAir(worldDir, velocity, airAcceleration, airMaxSpeed);
            if (playerRigidbody.useGravity)
            {
                velocity = Accelerate(Physics.gravity.normalized, velocity, Physics.gravity.magnitude * gravityMultiplier, float.MaxValue);
            }
        }

        if (velocity.magnitude * velocity.magnitude * 0.5 < Physics.sleepThreshold)
        {
            velocity = Vector3.zero;
        }

        // determine estimated displacement
        Vector3 displacement = velocity * Time.deltaTime;
        potentialPosition += displacement;

        if(shouldCollide)
        {
            // first test: where would we end up if we moved directly?
            var hits = Physics.RaycastAll(transform.position, displacement, displacement.magnitude, groundCheckLayer, QueryTriggerInteraction.Ignore);
            potentialPosition = (hits.Length > 0 ? hits.OrderByDescending(x => x.distance).Last().point : potentialPosition);
            Vector3 offsetFromCenter = playerCollider.bounds.center - transform.position;

            // could we potentially overlap w/ something near us?
            var candidates = Physics.OverlapBox(potentialPosition + offsetFromCenter, playerCollider.bounds.extents, transform.rotation, groundCheckLayer, QueryTriggerInteraction.Ignore);
            if(candidates.Length == 0)
            {
                // exit early if nothing is overlapping
                goto CommitMove;
            }

            // second test: depenetrate player from any overlapping geometry
            // attempt to move player away from contacts
            Vector3 finalMTV = Vector3.zero;
            foreach(var candidate in candidates)
            {
                float mtvDist;
                Vector3 mtv;
                bool pen = Physics.ComputePenetration(playerCollider, potentialPosition, transform.rotation, candidate, candidate.transform.position, candidate.transform.rotation, out mtv, out mtvDist);
                ResolvePhysics(candidate.transform, candidate.GetComponent<Rigidbody>(), mtv.normalized, mtvDist);
                if(pen && mtvDist > Physics.defaultContactOffset)
                {
                    finalMTV += mtv * mtvDist;
                }
            }

            if (!forceNoCollision)
            {
                potentialPosition += finalMTV;
            }
        }

        CommitMove:

        // finally: move the player to the end result
        playerRigidbody.position = potentialPosition;

        // clip the player's velocity on XZ plane
        // velocity.x = (potentialPosition.x - initialPosition.x) / Time.deltaTime;
        // velocity.z = (potentialPosition.z - initialPosition.z) / Time.deltaTime;
    

        //Debug.DrawRay(playerRigidbody.position, groundNorm * 0.5f, Color.yellow);
    }

    private void Reset()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        playerCollider = GetComponent<CapsuleCollider>();
    }

    private void DrawGizmo(bool selected)
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, isGrounded ? groundNorm : Vector3.up);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(playerCollider.bounds.center, 0.1f);
        
        Gizmos.color = Color.black;
        Gizmos.DrawRay(transform.position, velocity);
    }

    private void OnDrawGizmos() => DrawGizmo(false);
    private void OnDrawGizmosSelected() => DrawGizmo(true);

    private bool CompareVector3Exact(Vector3 a, Vector3 b)
    {
        for (int i = 0; i < 3; ++i)
        {
            if(a[i] != b[i]) { return false; }
        }

        return true;
    }
}

public static class Vector3Extensions
{
    public static string ToStringPrecision(this Vector3 v3, int precisionLevel=9)
    {
        string returnValue = "(";

        for (int i = 0; i < 2; ++i)
        {
            returnValue += v3[i].ToString("G" + precisionLevel) + ", ";
        }

        returnValue += v3[2].ToString("G" + precisionLevel) + ")";

        return returnValue;
    }

    public static Vector3 Scaled(this Vector3 target, Vector3 scaling)
    {
        return new Vector3(target.x * scaling.x, target.y * scaling.y, target.z * scaling.z);
    }

    public static Vector3 Scaled(this Vector3 target, float x, float y, float z)
    {
        return new Vector3(target.x * x, target.y * y, target.z * z);
    }
}