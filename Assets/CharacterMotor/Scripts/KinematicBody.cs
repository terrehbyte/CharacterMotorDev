using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public class KinematicBody : MonoBehaviour
{
    public IKinematicMotor motor;
    
    [Header("Body Definition")]
#pragma warning disable 0649 // assigned in Unity inspector
    [SerializeField]
    private BoxCollider col;
    public BoxCollider BodyCollider => col;
    
    [SerializeField]
    private Rigidbody rbody;
    public Rigidbody BodyRigidbody => rbody;
#pragma warning restore 0649 // assigned in Unity inspector

    public Vector3 LocalBodySize => col.size;
    public Vector3 LocalBodySizeWithOffset => col.size + Vector3.one * contactOffset;
    
    public float contactOffset = 0.005f;
    public float skinWidth = 0.01f;

    public Vector3 GetCenterAtBodyPosition(Vector3 bodyPosition)
    {
        return bodyPosition + col.center;
    }

    public Vector3 FootPosition => transform.TransformPoint(col.center + Vector3.down * col.size.y/2.0f);
    public Vector3 FootOffset => (FootPosition - transform.position);
    
    public Vector3 InternalVelocity { get; set; }
    public Vector3 Velocity { get; private set; }
    
    public void CollideAndSlide(Vector3 bodyPosition, Quaternion bodyRotation, Vector3 bodyVelocity, Collider other)
    {
        DeferredCollideAndSlide(ref bodyPosition, ref bodyRotation, ref bodyVelocity, other);
        
        // apply movement immediately
        rbody.MovePosition(bodyPosition);
        rbody.MoveRotation(bodyRotation);
        InternalVelocity = bodyVelocity;
    }

    public void DeferredCollideAndSlide(ref Vector3 bodyPosition, ref Quaternion bodyRotation, ref Vector3 bodyVelocity, Collider other)
    {
        // ignore self collision
        if(other == col) { return; }
            
        bool isOverlap = Physics.ComputePenetration(col,
            bodyPosition,
            bodyRotation,
            other,
            other.transform.position,
            other.transform.rotation,
            out var mtv,
            out var pen);

        if (isOverlap && pen > skinWidth)
        {
            if (!other.isTrigger)
            {
                motor.OnResolveMove(ref bodyPosition, ref bodyRotation, ref bodyVelocity, other, mtv, pen);
            }
        }
    }
    
    public Collider[] Overlap(Vector3 bodyPosition, int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        return Overlap(bodyPosition, LocalBodySize / 2, layerMask, queryMode);
    }
    
    public Collider[] Overlap(Vector3 bodyPosition, Vector3 bodyHalfExtents, int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        bodyPosition = GetCenterAtBodyPosition(bodyPosition);
        return Physics.OverlapBox(bodyPosition, bodyHalfExtents, rbody.rotation, layerMask, queryMode);
    }
    
    public RaycastHit[] Cast(Vector3 bodyPosition, Vector3 direction, float distance, int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        return Cast(bodyPosition, direction, LocalBodySizeWithOffset / 2, distance, layerMask, queryMode);
    }
    
    public RaycastHit[] Cast(Vector3 bodyPosition, Vector3 direction, Vector3 halfExtents, float distance, int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        bodyPosition = GetCenterAtBodyPosition(bodyPosition);
        var allHits = Physics.BoxCastAll(bodyPosition, halfExtents, direction, rbody.rotation, distance, layerMask, queryMode);
        return allHits;
    }

    public RaycastHit[] Trace(Vector3 startBodyPosition, Vector3 endBodyPosition, int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        return Trace(startBodyPosition, endBodyPosition, LocalBodySizeWithOffset/2, layerMask, queryMode);
    }

    public RaycastHit[] Trace(Vector3 startBodyPosition, Vector3 endBodyPosition, Vector3 halfExtents,
        int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        Vector3 offset = endBodyPosition - startBodyPosition;
        float len = offset.magnitude;

        Vector3 dir = offset / len;
        if (len == 0.0f) { return Array.Empty<RaycastHit>(); }
        Debug.Assert(Mathf.Approximately(dir.magnitude, 1.0f));
        return Cast(startBodyPosition, dir, halfExtents, len, layerMask, queryMode);
    }
    
    //
    // Unity Messages
    //

    private void Start()
    {
        OnValidate();
    }

    private void FixedUpdate()
    {
        Vector3 startPosition = rbody.position;
        
        motor.OnPreMove();

        InternalVelocity = motor.UpdateVelocity(InternalVelocity);

        //
        // integrate external forces
        //

        Vector3 projectedPos = rbody.position + (InternalVelocity * Time.deltaTime);
        Vector3 projectedVel = InternalVelocity;
        Quaternion projectedRot = rbody.rotation;
        
        //
        // sweep towards goal position
        //
        const int MAX_SWEEP_ITERATIONS = 32;

        for (int sweepNo = 0; sweepNo < MAX_SWEEP_ITERATIONS; ++sweepNo)
        {
            var sweepCandidates = Trace(startPosition, projectedPos, LocalBodySize/2, -1, QueryTriggerInteraction.Ignore);
            if (sweepCandidates.Length == 0 ||
                (sweepCandidates.Length == 1 && sweepCandidates[0].collider == col))
            {
                break;
            }
            
            for (int i = 0; i < sweepCandidates.Length; ++i)
            {
                var other = sweepCandidates[i].collider;

                // ignore self collision
                if (other == col || sweepCandidates[i].point == Vector3.zero)
                {
                    continue;
                }

                Debug.DrawRay(sweepCandidates[i].point, sweepCandidates[i].normal * 5.0f, Color.red);
                
                bool isOverlap = Physics.ComputePenetration(col,
                    projectedPos,
                    projectedRot,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out var mtv,
                    out var pen);

                motor.ResolveVelocity(ref projectedPos, ref projectedRot, ref projectedVel, other, mtv, pen);
                break;
            }
        }

        //
        // depenetrate from overlapping objects
        //

        // scale check
        Debug.Assert(Mathf.Approximately(transform.lossyScale.sqrMagnitude, 3) == true, "Scaling is not supported on KinematicBody game objects.");

        Vector3 sizeOriginal = col.size;
        Vector3 sizeWithSkin = col.size + Vector3.one * contactOffset;

        var candidates = Overlap(projectedPos, sizeWithSkin / 2, -1, QueryTriggerInteraction.Collide);

        // HACK: since we can't pass a custom size to Physics.ComputePenetration (see below),
        //       we need to assign it directly to the collide prior to calling it and then
        //       revert the change afterwards
        col.size = sizeWithSkin;

        //Debug.DrawRay(rbody.position + Vector3.up, projectedVel * 5.0f, Color.magenta);

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            DeferredCollideAndSlide(ref projectedPos, ref projectedRot, ref projectedVel, candidate);
            //Debug.DrawRay(rbody.position + Vector3.up * (index+1), projectedVel * 4.0f);
        }

        //Debug.DrawRay(rbody.position + Vector3.up, projectedVel * 5.0f, Color.green);

        // HACK: restoring size (see above HACK)
        col.size = sizeOriginal;
        
        // callback: pre-processing move before applying 
        motor.OnFinishMove(ref projectedPos, ref projectedRot, ref projectedVel);

        // apply move
        rbody.MovePosition(projectedPos);
        rbody.MoveRotation(projectedRot);
        InternalVelocity = projectedVel;

        // update velocity
        Velocity = (projectedPos - startPosition) / Time.fixedDeltaTime;
        
        // callback for after move is complete
        motor.OnPostMove();
    }

    private void OnValidate()
    {
        contactOffset = Mathf.Clamp(contactOffset, 0.001f, float.PositiveInfinity);
        skinWidth = Mathf.Clamp(skinWidth, 0.001f, float.PositiveInfinity);

        if (rbody != null)
        {
            rbody.isKinematic = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // early exit for missing collider
        if(col == null) { return; }

        // don't support scaling at this time - recreate the TRS matrix assuming unit scale
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // draw box with contact offset
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(col.center, col.size + Vector3.one * contactOffset);

        // draw box with skin width
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(col.center, col.size - Vector3.one * skinWidth);
    }

    private void Reset()
    {
        if (col == null) { col = GetComponent<BoxCollider>(); }
        if (rbody == null) { rbody = GetComponent<Rigidbody>(); }

        OnValidate();
    }
}

public interface IKinematicMotor
{
    void OnPreMove();
    void OnResolveMove(ref Vector3 bodyPosition, ref Quaternion bodyRotation, ref Vector3 bodyVelocity, Collider other, Vector3 direction, float distance);
    void OnFinishMove(ref Vector3 projectedPos, ref Quaternion projectedRot, ref Vector3 projectedVel);
    void OnPostMove();

    void ResolvePosition(ref Vector3 bodyPosition, ref Quaternion bodyRotation, ref Vector3 bodyVelocity, Collider other, Vector3 direction, float distance);
    void ResolveVelocity(ref Vector3 bodyPosition, ref Quaternion bodyRotation, ref Vector3 bodyVelocity, Collider other, Vector3 direction, float distance);
    Vector3 UpdateVelocity(Vector3 internalVelocity);
}
