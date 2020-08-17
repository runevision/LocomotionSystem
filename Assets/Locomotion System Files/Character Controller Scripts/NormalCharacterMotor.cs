using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class NormalCharacterMotor : CharacterMotor {
	
	public float maxRotationSpeed = 270;
	
	private bool firstframe = true;
	
	private void OnEnable () {
		firstframe = true;
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
		
		if (combinedFacingDirection.sqrMagnitude > 0.01f) {
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
		CharacterController controller = GetComponent(typeof(CharacterController)) as CharacterController;
		Vector3 velocity = controller.velocity;
		if (firstframe) {
			velocity = Vector3.zero;
			firstframe = false;
		}
		if (grounded) velocity = Util.ProjectOntoPlane(velocity, transform.up);
		
		// Calculate how fast we should be moving
		Vector3 movement = velocity;
		//bool hasJumped = false;
		jumping = false;
		if (grounded) {
			// Apply a force that attempts to reach our target velocity
			Vector3 velocityChange = (desiredVelocity - velocity);
			if (velocityChange.magnitude > maxVelocityChange) {
				velocityChange = velocityChange.normalized * maxVelocityChange;
			}
			movement += velocityChange;
		
			// Jump
			if (canJump && Input.GetButton("Jump")) {
				movement += transform.up * Mathf.Sqrt(2 * jumpHeight * gravity);
				//hasJumped = true;
				jumping = true;
			}
		}
		
		// Apply downwards gravity
		movement += transform.up * -gravity * Time.deltaTime;
		
		if (jumping) {
			movement -= transform.up * -gravity * Time.deltaTime / 2;
			
		}
		
		// Apply movement
		CollisionFlags flags = controller.Move(movement * Time.deltaTime);
		grounded = (flags & CollisionFlags.CollidedBelow) != 0;
	}
	
	// Update is called once per frame
	void Update () {
		UpdateFacingDirection();
		
		UpdateVelocity();
	}
}
