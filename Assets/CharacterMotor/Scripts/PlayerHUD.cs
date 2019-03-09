using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    public Text speedometer;

    public CharacterMotor motor;

    void LateUpdate()
    {
        speedometer.text = motor.velocity.magnitude.ToString("0.00");
    }
}
