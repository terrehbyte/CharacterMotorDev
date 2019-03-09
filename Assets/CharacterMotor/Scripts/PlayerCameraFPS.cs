using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCameraFPS : MonoBehaviour
{
    public float pitchLimit = 85.0f;

    private float yaw;
    private float pitch;

    public Transform yawTarget;
    public Transform pitchTarget;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        yaw += Input.GetAxisRaw("Mouse X");
        pitch += -Input.GetAxisRaw("Mouse Y");
        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

        //yawTarget.GetComponent<Rigidbody>().rotation = Quaternion.AngleAxis(yaw, Vector3.up);
        yawTarget.localRotation = Quaternion.AngleAxis(yaw, Vector3.up);
        pitchTarget.localRotation = Quaternion.AngleAxis(pitch, Vector3.right);

        Physics.SyncTransforms();   // HACK: maybe randall was right
    }


}
