using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMotorDebug : MonoBehaviour
{
    [SerializeField]
    CharacterMotor motor;
    [SerializeField]
    Transform playerTransform;
    [SerializeField]
    UnityEngine.UI.Text reportedText;
    [SerializeField]
    UnityEngine.UI.Text estimateText;

    Vector3 lastFixedPosition;
    float estimatedXZspeed;

    void Start()
    {
        lastFixedPosition = playerTransform.transform.position;
    }

    void Update()
    {
        reportedText.text = motor.velocity.Scaled(1,0,1).magnitude.ToString("0.00");
        estimateText.text = estimatedXZspeed.ToString("0.00");

    }

    void FixedUpdate()
    {
        estimatedXZspeed = (playerTransform.transform.position - lastFixedPosition).Scaled(1, 0, 1).magnitude / Time.deltaTime;
        lastFixedPosition = playerTransform.transform.position;
    }
}
