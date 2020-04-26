using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMotorDebug : MonoBehaviour
{
    [SerializeField]
    CharacterMotor motor;
    [SerializeField]
    UnityEngine.UI.Text reportedText;
    [SerializeField]
    UnityEngine.UI.Text estimateText;

    Vector3 lastFixedPosition;
    float estimatedXZspeed;

    void Start()
    {
        lastFixedPosition = motor.transform.position;
    }

    void Update()
    {
        reportedText.text = (motor.velocity.Scaled(1,0,1).magnitude.ToString("0.00"));
        estimateText.text = (estimatedXZspeed.ToString("0.00"));

    }

    void FixedUpdate()
    {
        estimatedXZspeed = (motor.transform.position - lastFixedPosition).Scaled(1, 0, 1).magnitude / Time.deltaTime;
        lastFixedPosition = motor.transform.position;
    }
}
