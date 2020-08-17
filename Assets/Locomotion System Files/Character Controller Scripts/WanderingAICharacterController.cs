using UnityEngine;
using System.Collections;

public class WanderingAICharacterController : MonoBehaviour {
	
	public bool onlyWalkForward;
	public float idleThreshold = 0.1f;
	
	private CharacterMotor motor;
	private float moveDirection = 0;
	private float faceDirection = 0;
	private float acceleration;
	private float moveSpeed = 0;
	private float turnSpeed = 0;
	private float faceSpeed = 0;
	
	// Use this for initialization
	void Start () {
		motor = GetComponent(typeof(CharacterMotor)) as CharacterMotor;
	}
	
	// Update is called once per frame
	private void Update() {
		if (motor==null) return;
		
		// control move and facing turning
		turnSpeed += (Random.value-0.5f) * Time.deltaTime * 5;
		faceSpeed += (Random.value-0.5f) * Time.deltaTime * 5;
		turnSpeed = Mathf.Clamp(turnSpeed,-1,1)*Mathf.Pow(0.5f,Time.deltaTime);
		faceSpeed = Mathf.Clamp(faceSpeed,-1,1)*Mathf.Pow(0.5f,Time.deltaTime);
		moveDirection += turnSpeed * Time.deltaTime;
		faceDirection += faceSpeed * Time.deltaTime;
		moveDirection = Util.Mod(moveDirection);
		faceDirection = Util.Mod(faceDirection);
		
		// control speed
		acceleration += (Random.value-0.5f) * Time.deltaTime / 10;
		acceleration = Mathf.Clamp(acceleration,-1,1);
		moveSpeed += acceleration;
		moveSpeed = Mathf.Clamp(moveSpeed,0,1);
		if (acceleration<0 && moveSpeed==0) acceleration = 0;
		if (acceleration>0 && moveSpeed==1) acceleration = 0;
		
		// Just run right in the beginning
		if (Time.time<5) { moveDirection = 0; moveSpeed = 1; }
		
		// calculate move and facing vectors
		Vector3 moveVector = Quaternion.AngleAxis(moveDirection*360,Vector3.up) * Vector3.forward * moveSpeed;
		Vector3 faceVector = Quaternion.AngleAxis(faceDirection*360,Vector3.up) * Vector3.forward;
		faceVector += moveVector*0.5f;
		faceVector = faceVector.normalized;
		
		if (onlyWalkForward) faceVector = moveVector.normalized;
		
		// apply vectors
		float moveVectorMag = moveVector.magnitude;
		motor.desiredFacingDirection = faceVector;
		if (moveVectorMag<idleThreshold) {
			motor.desiredMovementDirection = Vector3.zero;
			if (onlyWalkForward) motor.desiredFacingDirection = Vector3.zero;
		}
		else {
			motor.desiredMovementDirection =
			Quaternion.Inverse(transform.rotation)
				* (moveVector/moveVectorMag)
				* ((moveVectorMag-idleThreshold)/(1-idleThreshold));
		}
	}
}
