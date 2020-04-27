using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetYCheck : MonoBehaviour
{
    public Vector3 resetPosition;
    public Vector2 yBounds = new Vector2(-50, 50);

    // Start is called before the first frame update
    void OnEnable()
    {
        resetPosition = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if(transform.position.y < yBounds.x || transform.position.y > yBounds.y)
        {
            transform.position = resetPosition;
        }
    }
}
