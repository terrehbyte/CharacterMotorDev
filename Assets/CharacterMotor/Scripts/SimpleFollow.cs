using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SimpleFollow : MonoBehaviour
{
    public Transform followTarget;

    public Vector3 localOffset = new Vector3(0, 1, 0);
    public Vector3 offset = new Vector3(0,0,-1);

    public Vector3 directionToTarget
    {
        get => targetLocation - transform.position;
    }
    public Vector3 targetLocation
    {
        get => followTarget.position + localOffset;
    }

    private void LateUpdate()
    {
        if(followTarget == null) { return; }
        
        transform.position = followTarget.position + offset;
        transform.forward = directionToTarget;
    }

    private void DrawGizmos(bool selected)
    {
        if(followTarget == null || !selected) { return; }

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, directionToTarget);
        Gizmos.DrawSphere(targetLocation, 0.5f);
    }

    private void OnDrawGizmos() => DrawGizmos(false);
    private void OnDrawGizmosSelected() => DrawGizmos(true);
}
