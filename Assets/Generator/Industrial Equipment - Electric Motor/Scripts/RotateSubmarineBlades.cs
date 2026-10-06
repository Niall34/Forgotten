using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateSubmarineBlades : MonoBehaviour
{
    // Set rotate to equal false
    public bool rotate = false;
    
    // List of Rotatable Objects
    public List<GameObject> rotatableObjects;
    
    // Object Rotation Speed
    public float rotationSpeed = 20;

    private void Start()
    {
        // Set rotate to false at start. I did this because I use a button to start the rotation.
        // Set rotate to true at start, if you want it to rotate on start.
        rotate = false;
    }

    private void FixedUpdate()
    {   
        // If Not rotate
        if (!rotate)
        {
            // Return
            return;
        }
        else
        {
            // Foreach rotatableObject of type GameObject in rotatableObjects
            foreach (GameObject rotatableObject in rotatableObjects)
            {
                // Rotate the rotatableObject 
                // Change Vector3.up if it's not the desired rotation. I had it originally set to Vector3.forward.
                rotatableObject.transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
            }
        }
    }
    // Used to make the object rotate on button press.
    public void RotateObject()
    {
        rotate = !rotate;
        return;
    }
    // Used to make the object rotate on button press and play animation.
    public void RotateObject(bool enable)
    {
        rotate = enable;
    }
}
