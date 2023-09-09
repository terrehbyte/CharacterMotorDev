using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [field: SerializeField]
    public PlayerInput Input { get; private set; }
    [field: SerializeField]
    public SimpleKinematicMotor Motor { get; private set; }

    private void Start()
    {
        if(Motor != null) { Motor.Possess(this); }
    }
}
