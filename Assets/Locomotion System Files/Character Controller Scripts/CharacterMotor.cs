using UnityEngine;
using System.Collections;

public abstract class CharacterMotor : MonoBehaviour {
	
	public float maxForwardSpeed = 1.5f;
	public float maxBackwardsSpeed = 1.5f;
	public float maxSidewaysSpeed = 1.5f;
	public float maxVelocityChange = 0.2f;
	
	public float gravity = 10.0f;
	public bool canJump = true;
	public float jumpHeight = 1.0f;
	
	public Vector3 forwardVector = Vector3.forward;
	protected Quaternion alignCorrection;
	
	private bool m_Grounded = false;
	public bool grounded {
		get { return m_Grounded; }
		protected set { m_Grounded = value; }
	}
	
	private bool m_Jumping = false;
	public bool jumping	{
		get { return m_Jumping; }
		protected set { m_Jumping = value; }
	}
	
	private Vector3 m_desiredMovementDirection;
	private Vector3 m_desiredFacingDirection;
	
	void Start () {
		alignCorrection = new Quaternion();
		alignCorrection.SetLookRotation(forwardVector, Vector3.up);
		alignCorrection = Quaternion.Inverse(alignCorrection);
	}
	
	public Vector3 desiredMovementDirection {
		get { return m_desiredMovementDirection; }
		set {
			m_desiredMovementDirection = value;
			if (m_desiredMovementDirection.magnitude>1) m_desiredMovementDirection = m_desiredMovementDirection.normalized;
		}
	}
	public Vector3 desiredFacingDirection {
		get { return m_desiredFacingDirection; }
		set {
			m_desiredFacingDirection = value;
			if (m_desiredFacingDirection.magnitude>1) m_desiredFacingDirection = m_desiredFacingDirection.normalized;
		}
	}
	public Vector3 desiredVelocity {
		get {
			//return m_desiredVelocity;
			if (m_desiredMovementDirection==Vector3.zero) return Vector3.zero;
			else {
				float zAxisEllipseMultiplier = (m_desiredMovementDirection.z>0 ? maxForwardSpeed : maxBackwardsSpeed) / maxSidewaysSpeed;
				Vector3 temp = new Vector3(m_desiredMovementDirection.x, 0, m_desiredMovementDirection.z/zAxisEllipseMultiplier).normalized;
				float length = new Vector3(temp.x, 0, temp.z*zAxisEllipseMultiplier).magnitude * maxSidewaysSpeed;
				Vector3 velocity = m_desiredMovementDirection * length;
				return transform.rotation * velocity;
			}
		}
	}
}