using UnityEngine;
using System.Collections;

public class PlatformCharacterController : MonoBehaviour {
	
	private CharacterMotor motor;
	
	public float walkMultiplier = 0.5f;
	public bool defaultIsWalk = false;
	
	private static bool loggedInputInfo = false;
	
	// Use this for initialization
	void Start () {
		motor = GetComponent(typeof(CharacterMotor)) as CharacterMotor;
		if (motor==null) Debug.Log("Motor is null!!");
	}
	
	// Update is called once per frame
	void Update () {
		// Get input vector from kayboard or analog stick and make it length 1 at most
		Vector3 directionVector = new Vector3(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"), 0);
		if (directionVector.magnitude>1) directionVector = directionVector.normalized;
		directionVector = directionVector.normalized * Mathf.Pow(directionVector.magnitude, 2);
		
		// Rotate input vector into camera space so up is camera's up and right is camera's right
		directionVector = Camera.main.transform.rotation * directionVector;
		
		// Rotate input vector to be perpendicular to character's up vector
		Quaternion camToCharacterSpace = Quaternion.FromToRotation(Camera.main.transform.forward*-1, transform.up);
		directionVector = (camToCharacterSpace * directionVector);
		
		// Make input vector relative to Character's own orientation
		directionVector = Quaternion.Inverse(transform.rotation) * directionVector;
		
		if (walkMultiplier!=1) {
			bool sneak = false;
			try {
				sneak = Input.GetButton("Sneak");
			}
			catch (UnityException e) {
				if (!loggedInputInfo) {
					Debug.Log ("Hint: Setup button \"Sneak\" to support walking slowly. This is optional.\nYou can map it to whichever key or joystick button you want to control walking speed.\n"+e.StackTrace, this);
					loggedInputInfo = true;
				}
			}
			if ( (Input.GetKey("left shift") || Input.GetKey("right shift") || sneak) != defaultIsWalk ) {
				directionVector *= walkMultiplier;
			}
		}
		
		// Apply direction
		motor.desiredMovementDirection = directionVector;
	}
}
