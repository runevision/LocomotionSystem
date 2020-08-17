using UnityEngine;
using System.Collections;

public class MouseLookCharacterController : MonoBehaviour {
	
	private CharacterMotor motor;
	
	public float sensitivityX = 15F;
	public float sensitivityY = 15F;

	private float rotationX = 0F;
	private float rotationY = 0F;
	
	private Quaternion originalRotation;
	
	private static bool loggedInputInfo = false;
	
	// Use this for initialization
	void Start () {
		motor = GetComponent(typeof(CharacterMotor)) as CharacterMotor;
		if (motor==null) Debug.Log("Motor is null!!");
		
		originalRotation = transform.localRotation;
	}
	
	// Update is called once per frame
	void Update () {
		
		// Read the mouse input axis
		rotationX += Input.GetAxis("Mouse X") * sensitivityX;
		rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
		
		try {
			rotationX += Input.GetAxis("Horizontal2") * Time.deltaTime * 300;
			rotationY += Input.GetAxis("Vertical2") * Time.deltaTime * 300;
		}
		catch (UnityException e) {
			if (!loggedInputInfo) {
				Debug.Log ("Hint: Setup axes \"Horizontal2\" and \"Vertical2\" to support aiming direction. This is optional.\nYou can map them to whichever keys or joystick axes you want to control aiming direction.\n"+e.StackTrace, this);
				loggedInputInfo = true;
			}
		}
		
		rotationX = Mathf.Repeat(rotationX, 360);
		rotationY = Mathf.Clamp(rotationY, -85, 85);
		
		Quaternion xQuaternion = Quaternion.AngleAxis (rotationX, Vector3.up);
		Quaternion yQuaternion = Quaternion.AngleAxis (rotationY, -Vector3.right);
		
		motor.desiredFacingDirection = originalRotation * xQuaternion * yQuaternion * Vector3.forward;
	}
}
