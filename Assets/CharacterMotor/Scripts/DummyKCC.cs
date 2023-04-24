using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyKCC : MonoBehaviour
{
    [SerializeField]
    private CharacterController ctrl;

    public float speed;
    
    void Update()
    {
        ctrl.SimpleMove(new Vector3(Input.GetAxisRaw("Horizontal"), 0.0f, Input.GetAxisRaw("Vertical")) * speed);
    }
}
