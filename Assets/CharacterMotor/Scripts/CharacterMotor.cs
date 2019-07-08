using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MoreGizmos;

[SelectionBase]
public class CharacterMotor : MonoBehaviour
{
    [Header("Dependencies")]
    public Camera playerCamera;
    public Rigidbody playerRigidbody;
    public Collider playerCollider;

    [Header("Input")]
    private Vector3 moveWish;
    private bool jumpWish;
    public bool canQueueJump = false;
    public bool allowAutoBhop = false;

    [Header("Movement")]
    public Vector3 velocity;
    public bool shouldCollide = true;

    public float groundFriction = 11;
    public float groundAcceleration = 50;
    public float groundMaxSpeed = 5.0f;

    public float airAcceleration = 8;
    public float airMaxSpeed = 300.0f;
    private float gravityMultiplier = 2.0f;

    [Header("Ground Check")]
    public bool isGrounded;
    public float groundCheckLengthMultiplier = 2.0f;
    [SerializeField]
    private Collider groundCol;
    [SerializeField]
    private LayerMask groundCheckLayer;
    [SerializeField]
    private Vector3 groundNorm;

    // Returns the direction that the character will move in given a controller-space input
    // controllerDir: direction of controller input
    // maxMagnitude: maximum magnitude of the returned vector
    public Vector3 ControllerToWorldDirection(Vector3 controllerDir, float maxMagnitude = 1)
    {
        controllerDir = controllerDir.normalized * Mathf.Min(maxMagnitude, controllerDir.magnitude);
        controllerDir = Quaternion.Euler(0, playerCamera.transform.eulerAngles.y, 0) * controllerDir;
        controllerDir = Vector3.ProjectOnPlane(controllerDir, groundNorm);

        return controllerDir;
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
    private Collider QueryGroundCheck(out Vector3 groundPoint, out Vector3 groundNormal)
    {
        Collider groundCollider = null;
        groundPoint = Vector3.zero;
        groundNormal = Vector3.zero;
        RaycastHit groundHit;

        var hits = playerRigidbody.SweepTestAll(Vector3.down, Mathf.Abs(Physics.gravity.y * Time.fixedDeltaTime * groundCheckLengthMultiplier), QueryTriggerInteraction.Ignore);
        hits = hits.OrderByDescending(x => x.distance).Reverse().ToArray();
        foreach(var hit in hits)
        {
            if((hit.collider != playerCollider))
            {
                groundHit = hit;
                groundPoint = hit.point;
                groundNormal = hit.normal;
                return hit.collider;
            }
        }

        return groundCollider;
    }

    private void Start()
    {
        Vector3 potentialPosition = playerRigidbody.position;
        var hits = Physics.RaycastAll(playerCollider.bounds.center, Vector3.down, playerCollider.bounds.extents.y + Physics.defaultContactOffset, groundCheckLayer, QueryTriggerInteraction.Ignore);
        float mtvDist;
        Vector3 mtv;
        foreach(var hit in hits)
        {
            bool pen = Physics.ComputePenetration(playerCollider, transform.position, transform.rotation, hit.collider, hit.transform.position, hit.transform.rotation, out mtv, out mtvDist);
            if(pen)
            {
                potentialPosition += mtv * (mtvDist + Physics.defaultContactOffset);
            }
        }
        playerRigidbody.MovePosition(potentialPosition);
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
        Vector3 potentialPosition = transform.position;
        bool wasGrounded = isGrounded;
        isGrounded = (groundCol = QueryGroundCheck(out groundPoint, out groundNorm)) != null;
        if(isGrounded)
        {
            Vector3 closestOnPlayer = playerCollider.ClosestPoint(groundPoint);
            GizmosEx.DrawSphere(closestOnPlayer, 0.1f, Color.green);
            GizmosEx.DrawSphere(groundPoint, 0.1f, Color.red);
            Vector3 offset = closestOnPlayer - transform.position;
            GizmosEx.DrawSphere(groundPoint - offset, 0.05f, Color.blue);
            potentialPosition = (groundPoint - offset);
        }
        if(!wasGrounded && isGrounded)
        {
            velocity.y = 0.0f;
            Debug.Log("[MOTOR] Landing detected.");
        }

        // process handle player input
        Vector3 worldDir = ControllerToWorldDirection(moveWish);
        worldDir = worldDir.magnitude > 0 ? Vector3.ProjectOnPlane(worldDir, isGrounded ? groundNorm : Vector3.up) :
                                            Vector3.zero;

        if (jumpWish)
        {
            if (isGrounded)
            {
                Debug.Log("[MOTOR] Jumping! Now airborne.");
                isGrounded = false;
                velocity.y = 9.8f;
                jumpWish = false;
            }
            else
            {
                jumpWish = canQueueJump || false;
            }
        }

        if(isGrounded)
        {
            velocity = MoveGround(worldDir, velocity, groundAcceleration, groundMaxSpeed);
        }
        else
        {
            velocity = MoveAir(worldDir, velocity, airAcceleration, airMaxSpeed);
            velocity = Accelerate(Physics.gravity.normalized, velocity, Physics.gravity.magnitude * gravityMultiplier, float.MaxValue );
        }

        if (velocity.magnitude * velocity.magnitude * 0.5 < Physics.sleepThreshold)
        {
            velocity = Vector3.zero;
        }

        // determine estimated displacement
        Vector3 displacement = velocity * Time.deltaTime;
        potentialPosition = potentialPosition + displacement;

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
                if(pen && mtvDist > Physics.defaultContactOffset)
                {
                    finalMTV += mtv * mtvDist;
                }
            }

            potentialPosition += finalMTV;
        }

        CommitMove:

        // finally: move the player to the end result
        potentialPosition += -Physics.gravity.normalized * Physics.defaultContactOffset;

        Debug.AssertFormat(velocity.magnitude > 0 || (playerRigidbody.position == potentialPosition),
                           this, "CurrentPosition {0} != potentialPosition {1} but velocity is zero! Difference is {2}.",
                           playerRigidbody.position, potentialPosition, (playerRigidbody.position - potentialPosition).ToString("F7"));

        playerRigidbody.position = potentialPosition;

        Debug.DrawRay(playerRigidbody.position, groundNorm * 3.0f, Color.yellow);
    }

    private void Reset()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
    }

    private void DrawGizmo(bool selected)
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.down * Mathf.Abs(Physics.gravity.y * Time.fixedDeltaTime * groundCheckLengthMultiplier));

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
