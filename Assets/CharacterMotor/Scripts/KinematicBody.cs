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
    /// <summary>
    /// Size of the box body in local space
    /// </summary>
    public Vector3 LocalBodySize => col.size;
    public Vector3 LocalBodySizeWithSkin => col.size + Vector3.one * skinWidth;
    public Vector3 LocalBodySizeWithContactOffset => col.size + Vector3.one * (contactOffset + skinWidth);
    
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

    public bool useSweeping = true;

    public bool useInterpolation = true;
    
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

        if (isOverlap)
        {
            if (!other.isTrigger)
            {
                motor.ResolvePosition(ref bodyPosition, ref bodyRotation, ref bodyVelocity, other, mtv, pen);
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
        return Cast(bodyPosition, direction, LocalBodySize / 2, distance, layerMask, queryMode);
    }
    
    public RaycastHit[] Cast(Vector3 bodyPosition, Vector3 direction, Vector3 halfExtents, float distance, int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        bodyPosition = GetCenterAtBodyPosition(bodyPosition);
        Debug.DrawRay(bodyPosition, direction * 0.1f);
        var allHits = Physics.BoxCastAll(bodyPosition, halfExtents, direction, rbody.rotation, distance, layerMask, queryMode);
        return allHits;
    }

    public RaycastHit[] Trace(Vector3 startBodyPosition, Vector3 endBodyPosition, int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        return Trace(startBodyPosition, endBodyPosition, LocalBodySize/2, layerMask, queryMode);
    }

    public RaycastHit[] Trace(Vector3 startBodyPosition, Vector3 endBodyPosition, Vector3 halfExtents,
        int layerMask = ~0, QueryTriggerInteraction queryMode = QueryTriggerInteraction.UseGlobal)
    {
        Vector3 offset = endBodyPosition - startBodyPosition;
        float len = offset.magnitude;
        if (len == 0.0f) { return Array.Empty<RaycastHit>(); }
        
        Vector3 dir = offset / len;
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
        
        Vector3 checkSize = LocalBodySizeWithSkin;
        
        Vector3 origSize = col.size;
        col.size = checkSize;
        
        // sweep before moving?
        if (useSweeping)
        {
            var sweepCandidates = Trace(startPosition, projectedPos, checkSize / 2, -1,
                    QueryTriggerInteraction.Ignore);
            
            if (sweepCandidates.Length != 0 &&
                !(sweepCandidates.Length == 1 && sweepCandidates[0].collider == col))
            {
                Vector3 sweepDirection = (projectedPos - startPosition).normalized;
                
                for (int i = 0; i < sweepCandidates.Length; ++i)
                {
                    var other = sweepCandidates[i].collider;

                    if (other == col || // ignore self collision OR 
                        sweepCandidates[i].point == Vector3.zero) // unresolvable collisions
                    {
                        continue;
                    }
                    
                    Vector3 sweepPos = startPosition + (sweepCandidates[i].distance) * sweepDirection;
                    projectedPos = sweepPos;
                    
                    motor.ResolveVelocity(ref sweepPos,
                            ref projectedRot,
                            ref projectedVel,
                            other,
                            sweepCandidates[i].normal,
                            float.NaN);

                    bool isOverlap = Physics.ComputePenetration(col,
                        projectedPos,
                        projectedRot,
                        sweepCandidates[i].collider,
                        sweepCandidates[i].transform.position,
                        sweepCandidates[i].transform.rotation,
                        out var mtv,
                        out var pen);

                    Debug.Assert(isOverlap == false);
                    
                    break;
                }   
            }
        }

        //
        // depenetrate from overlapping objects
        //

        // scale check
        Debug.Assert(Mathf.Approximately(transform.lossyScale.sqrMagnitude, 3) == true, "Scaling is not supported on KinematicBody game objects.");
        
        var candidates = Overlap(projectedPos, checkSize/2, -1, QueryTriggerInteraction.Collide);

        Debug.DrawRay(rbody.position + Vector3.up, projectedVel.GetXZ() * 5.0f, Color.magenta);
        Debug.DrawRay(rbody.position + Vector3.up, projectedVel.GetY() * 5.0f, Color.magenta);

        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            
            // ignore self collision
            if (candidate == col) { continue; }
            
            bool isOverlap = Physics.ComputePenetration(col,
                projectedPos,
                projectedRot,
                candidate,
                candidate.transform.position,
                candidate.transform.rotation,
                out var mtv,
                out var pen);

            if (isOverlap)
            {
                motor.ResolvePosition(ref projectedPos, ref projectedRot, ref projectedVel, candidate, mtv, pen);
                motor.ResolveVelocity(ref projectedPos, ref projectedRot, ref projectedVel, candidate, mtv, pen);
            }
            
            //Debug.DrawRay(rbody.position + Vector3.up * (index+1), projectedVel * 4.0f);
        }
        
        col.size = origSize;

        //Debug.DrawRay(rbody.position + Vector3.up, projectedVel * 5.0f, Color.green);

        // callback: pre-processing move before applying 
        motor.OnFinishMove(ref projectedPos, ref projectedRot, ref projectedVel);

        // apply move
        if (useInterpolation)
        {
            rbody.MovePosition(projectedPos);
            rbody.MoveRotation(projectedRot);
        }
        else
        {
            rbody.position = projectedPos;
            rbody.rotation = projectedRot;
        }

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

        // draw box with skin width
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(col.center, LocalBodySizeWithSkin);
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
    void OnFinishMove(ref Vector3 projectedPos, ref Quaternion projectedRot, ref Vector3 projectedVel);
    void OnPostMove();

    void ResolvePosition(ref Vector3 bodyPosition, ref Quaternion bodyRotation, ref Vector3 bodyVelocity, Collider other, Vector3 direction, float distance);
    void ResolveVelocity(ref Vector3 bodyPosition, ref Quaternion bodyRotation, ref Vector3 bodyVelocity, Collider other, Vector3 direction, float distance);
    Vector3 UpdateVelocity(Vector3 internalVelocity);
}
