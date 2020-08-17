using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PhysicsCharacterMotor : CharacterMotor {
	
	public float maxRotationSpeed = 270;
	public bool useCentricGravity = false;
	public LayerMask groundLayers;
	public Vector3 gravityCenter = Vector3.zero;
	
	void Awake () {
		GetComponent<Rigidbody>().freezeRotation = true;
		GetComponent<Rigidbody>().useGravity = false;
	}
	
	private void AdjustToGravity() {
		int origLayer = gameObject.layer;
		gameObject.layer = 2;
		
		Vector3 currentUp = transform.up;
		//Vector3 gravityUp = (transform.position-gravityCenter).normalized;
		
		float damping = Mathf.Clamp01(Time.deltaTime*5);
		
		RaycastHit hit;
		
		Vector3 desiredUp = Vector3.zero;
		for (int i=0; i<8; i++) {
			Vector3 rayStart =
				transform.position
					+ transform.up
					+ Quaternion.AngleAxis(360*i/8.0f, transform.up)
						* (transform.right*0.5f)
					+ desiredVelocity*0.2f;
			if ( Physics.Raycast(rayStart, transform.up*-2, out hit, 3.0f, groundLayers.value) ) {
				desiredUp += hit.normal;
			}
		}
		desiredUp = (currentUp+desiredUp).normalized;
		Vector3 newUp = (currentUp+desiredUp*damping).normalized;
		
		float angle = Vector3.Angle(currentUp,newUp);
		if (angle>0.01) {
			Vector3 axis = Vector3.Cross(currentUp,newUp).normalized;
			Quaternion rot = Quaternion.AngleAxis(angle,axis);
			transform.rotation = rot * transform.rotation;
		}
		
		gameObject.layer = origLayer;
	}
	
	private void UpdateFacingDirection() {
		// Calculate which way character should be facing
		float facingWeight = desiredFacingDirection.magnitude;
		Vector3 combinedFacingDirection = (
			transform.rotation * desiredMovementDirection * (1-facingWeight)
			+ desiredFacingDirection * facingWeight
		);
		combinedFacingDirection = Util.ProjectOntoPlane(combinedFacingDirection, transform.up);
		combinedFacingDirection = alignCorrection * combinedFacingDirection;
		
		if (combinedFacingDirection.sqrMagnitude > 0.1f) {
			Vector3 newForward = Util.ConstantSlerp(
				transform.forward,
				combinedFacingDirection,
				maxRotationSpeed*Time.deltaTime
			);
			newForward = Util.ProjectOntoPlane(newForward, transform.up);
			//Debug.DrawLine(transform.position, transform.position+newForward, Color.yellow);
			Quaternion q = new Quaternion();
			q.SetLookRotation(newForward, transform.up);
			transform.rotation = q;
		}
	}
	
	private void UpdateVelocity() {
		Vector3 velocity = GetComponent<Rigidbody>().velocity;
		if (grounded) velocity = Util.ProjectOntoPlane(velocity, transform.up);
		
		// Calculate how fast we should be moving
		jumping = false;
		if (grounded) {
			// Apply a force that attempts to reach our target velocity
			Vector3 velocityChange = (desiredVelocity - velocity);
			if (velocityChange.magnitude > maxVelocityChange) {
				velocityChange = velocityChange.normalized * maxVelocityChange;
			}
			GetComponent<Rigidbody>().AddForce(velocityChange, ForceMode.VelocityChange);
		
			// Jump
			if (canJump && Input.GetButton("Jump")) {
				GetComponent<Rigidbody>().velocity = velocity + transform.up * Mathf.Sqrt(2 * jumpHeight * gravity);
				jumping = true;
			}
		}
		
		// Apply downwards gravity
		GetComponent<Rigidbody>().AddForce(transform.up * -gravity * GetComponent<Rigidbody>().mass);
		
		grounded = false;
	}
	void OnCollisionStay () {
		grounded = true;
	}
	
	void FixedUpdate () {
		if (useCentricGravity) AdjustToGravity();
		
		UpdateFacingDirection();
		
		UpdateVelocity();
	}
	
}
