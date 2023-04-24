using UnityEngine;

public class SampleCharacter : MonoBehaviour
{
    public KinematicCharacterMotor motor;

    public bool useMockInput;
    public Vector3 mockInput;
    
    void Update()
    {
        if (useMockInput)
        {
            motor.MoveInput(mockInput);
            return;
        }

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0.0f, Input.GetAxisRaw("Vertical"));
        motor.MoveInput(input);

        if (Input.GetButtonDown("Jump"))
        {
            motor.JumpInput();
        }
    }
}
