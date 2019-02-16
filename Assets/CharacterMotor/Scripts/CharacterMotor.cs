using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[SelectionBase]
public class CharacterMotor : MonoBehaviour
{
    [Header("Dependencies")]
    public Camera playerCamera;
    public Rigidbody playerRigidbody;
    public Collider playerCollider;

    [Header("Input")]
    private Vector3 moveWish;

    [Header("Movement")]
    public Vector3 velocity;

    public float groundFriction = 11;
    public float groundAcceleration = 50;
    public float groundMaxSpeed = 5.0f;

    [Header("Ground Check")]
    public bool isGrounded;
    [SerializeField]
    private Collider groundCol;
    [SerializeField]
    private float groundCheckLength = 1.5f;
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

    // Returns the player's new velocity based on the given parameters
    // accelDir: world-space direction to accelerate in
    // prevVelocity: world-space velocity
    // accelerate: amount to accelerate by
    // maxSpeed: max player speed to achieve when accelerating
    private Vector3 Accelerate(Vector3 accelDir, Vector3 prevVelocity, float accelerate, float maxSpeed)
    {
        float projVel = Vector3.Dot(prevVelocity, accelDir);
        float accelVel = accelerate * Time.fixedDeltaTime;

        if(projVel + accelVel > maxSpeed)
        {
            accelVel = maxSpeed - projVel;
        }

        return prevVelocity + accelDir * accelVel;
    }

    // Returns the collider that the player is standing on
    private Collider QueryGroundCheck(out Vector3 groundPoint)
    {
        Collider groundCollider = null;
        groundPoint = Vector3.zero;
        RaycastHit groundHit;

        var hits = playerRigidbody.SweepTestAll(Vector3.down, Mathf.Abs(Physics.gravity.y * Time.fixedDeltaTime), QueryTriggerInteraction.Ignore);
        foreach(var hit in hits)
        {
            if((hit.collider != playerCollider)) { groundHit = hit; groundPoint = hit.point; return hit.collider; }
        }

        return groundCollider;
    }

    private void DrawGizmo(bool selected)
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.down * Mathf.Abs(Physics.gravity.y * Time.fixedDeltaTime));

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(playerCollider.bounds.center, 0.1f);
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
    }

    private void FixedUpdate()
    {
        // determine grounded status
        Vector3 groundPoint;
        isGrounded = QueryGroundCheck(out groundPoint) != null;

        // process handle player input
        Vector3 worldDir = ControllerToWorldDirection(moveWish);
        velocity = MoveGround(worldDir, velocity, groundAcceleration, groundMaxSpeed);

        Vector3 offset = velocity * Time.deltaTime;
        Vector3 potentialPosition = transform.position + offset;
        var hits = Physics.RaycastAll(transform.position, offset, offset.magnitude, groundCheckLayer, QueryTriggerInteraction.Ignore);
        potentialPosition = (hits.Length > 0 ? hits.OrderByDescending(x => x.distance).Last().point : potentialPosition);
        Vector3 offsetFromCenter = playerCollider.bounds.center - transform.position;

        var candidates = Physics.OverlapBox(potentialPosition + offsetFromCenter, playerCollider.bounds.extents, transform.rotation, groundCheckLayer, QueryTriggerInteraction.Ignore);
        if(candidates.Length == 0)
        {
            playerRigidbody.MovePosition(potentialPosition);
            return;
        }

        float mtvDist;
        Vector3 mtv;
        bool pen = Physics.ComputePenetration(playerCollider, potentialPosition, transform.rotation, candidates[0], candidates[0].transform.position, candidates[0].transform.rotation, out mtv, out mtvDist);
        if(pen)
        {
            potentialPosition += mtv * (mtvDist + Physics.defaultContactOffset);
        }
        playerRigidbody.MovePosition(potentialPosition);
    }

    private void Reset()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
    }

    private void OnDrawGizmos() => DrawGizmo(false);
    private void OnDrawGizmosSelected() => DrawGizmo(true);
}
