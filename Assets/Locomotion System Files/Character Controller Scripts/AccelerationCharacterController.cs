using UnityEngine;
using System.Collections;

public class AccelerationCharacterController : MonoBehaviour {
	
	private CharacterMotor motor;
	
	public float sensitivityX = 15F;
	public float sensitivityY = 15F;
	public float accelerationSpeed = 1.0f;

	private float rotationX = 0F;
	private float rotationY = 0F;
	
	private Quaternion originalRotation;
	
	// Use this for initialization
	void Start () {
		motor = GetComponent(typeof(CharacterMotor)) as CharacterMotor;
		if (motor==null) Debug.Log("Motor is null!!");
		
		originalRotation = transform.localRotation;
	}
	
	// Update is called once per frame
	void Update () {
		// Get input vector from kayboard or analog stick and make it length 1 at most
		Vector3 directionVector = motor.desiredMovementDirection;
		directionVector.z += Input.GetAxis("Vertical")*Time.deltaTime*accelerationSpeed;
		directionVector.x += Input.GetAxis("Horizontal")*Time.deltaTime*accelerationSpeed;
		if (directionVector.magnitude>1) directionVector = directionVector.normalized;
		
		// Read the mouse input axis
		rotationX += Input.GetAxis("Mouse X") * sensitivityX;
		rotationY += Input.GetAxis("Mouse Y") * sensitivityY;

		rotationX = Mathf.Repeat(rotationX, 360);
		rotationY = Mathf.Clamp(rotationY, -90, 90);
		
		Quaternion xQuaternion = Quaternion.AngleAxis (rotationX, Vector3.up);
		Quaternion yQuaternion = Quaternion.AngleAxis (rotationY, -Vector3.right);
		
		motor.desiredFacingDirection = originalRotation * xQuaternion * yQuaternion * Vector3.forward;
		
		// Apply direction
		motor.desiredMovementDirection = directionVector;
	}
}
