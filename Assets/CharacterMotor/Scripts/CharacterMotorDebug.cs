using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterMotorDebug : MonoBehaviour
{
    [SerializeField]
    CharacterMotor motor;
    [SerializeField]
    UnityEngine.UI.Text text;

    void Update()
    {
        text.text = (motor.velocity.Scaled(1,0,1).magnitude.ToString("0.00"));
    }
}
