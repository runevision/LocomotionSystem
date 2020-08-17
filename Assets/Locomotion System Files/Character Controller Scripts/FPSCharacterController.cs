using UnityEngine;
using System.Collections;

public class FPSCharacterController : MonoBehaviour {
	
	private CharacterMotor motor;
	
	public float walkMultiplier = 0.5f;
	public bool defaultIsWalk = false;
	
	// Use this for initialization
	void Start () {
		motor = GetComponent(typeof(CharacterMotor)) as CharacterMotor;
		if (motor==null) Debug.Log("Motor is null!!");
		motor.desiredFacingDirection = transform.forward;
	}
	
	// Update is called once per frame
	void Update () {
		// Get input vector from kayboard or analog stick and make it length 1 at most
		Vector3 directionVector = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
		if (directionVector.magnitude>1) directionVector = directionVector.normalized;
		
		if (walkMultiplier!=1) {
			if ( (Input.GetKey("left shift") || Input.GetKey("right shift")) != defaultIsWalk ) {
				directionVector *= walkMultiplier;
			}
		}
		
		// Apply direction
		motor.desiredMovementDirection = directionVector;
	}
}
