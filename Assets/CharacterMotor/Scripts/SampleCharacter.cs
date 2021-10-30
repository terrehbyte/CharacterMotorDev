using UnityEngine;

public class SampleCharacter : MonoBehaviour
{
    public KinematicCharacterMotor motor;

    void Update()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0.0f, Input.GetAxisRaw("Vertical"));
        motor.MoveInput(input);

        if (Input.GetButtonDown("Jump"))
        {
            motor.JumpInput();
        }
    }
}
