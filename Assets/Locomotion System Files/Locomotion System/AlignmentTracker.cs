/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;

public class AlignmentTracker : MonoBehaviour {
	
	public bool fixedUpdate = false;

	private float m_CurrentFixedTime;
	private float m_CurrentLateTime;
	
	public Vector3 position { get{ return m_Position; } }
	private Vector3 m_Position = Vector3.zero;
	private Vector3 m_PositionPrev = Vector3.zero;
	
	public Vector3 velocity { get{ return m_Velocity; } }
	private Vector3 m_Velocity = Vector3.zero;
	private Vector3 m_VelocityPrev = Vector3.zero;
	public Vector3 velocitySmoothed { get{ return m_VelocitySmoothed; } }
	private Vector3 m_VelocitySmoothed = Vector3.zero;
	
	public Vector3 acceleration { get{ return m_Acceleration; } }
	private Vector3 m_Acceleration = Vector3.zero;
	public Vector3 accelerationSmoothed { get{ return m_AccelerationSmoothed; } }
	private Vector3 m_AccelerationSmoothed = Vector3.zero;
	
	public Quaternion rotation { get{ return m_Rotation; } }
	private Quaternion m_Rotation = Quaternion.identity;
	private Quaternion m_RotationPrev = Quaternion.identity;
	
	public Vector3 angularVelocity { get{ return m_AngularVelocity; } }
	private Vector3 m_AngularVelocity = Vector3.zero;
	public Vector3 angularVelocitySmoothed { get{ return m_AngularVelocitySmoothed; } }
	private Vector3 m_AngularVelocitySmoothed = Vector3.zero;
	
	private Rigidbody m_Rigidbody;
	private Transform m_Transform;
	
	void Awake() {
		Reset();
	}
	
	void OnEnable () {
		Reset();
	}
	
	public void Reset () {
		m_Rigidbody = GetComponent<Rigidbody>();
		m_Transform = transform;
		m_CurrentLateTime = -1;
		m_CurrentFixedTime = -1;
		m_Position = m_PositionPrev = m_Transform.position;
		m_Rotation = m_RotationPrev = m_Transform.rotation;
		m_Velocity = Vector3.zero;
		m_VelocityPrev = Vector3.zero;
		m_VelocitySmoothed = Vector3.zero;
		m_Acceleration = Vector3.zero;
		m_AccelerationSmoothed = Vector3.zero;
		m_AngularVelocity = Vector3.zero;
		m_AngularVelocitySmoothed = Vector3.zero;
	}
	
	private Vector3 CalculateAngularVelocity(Quaternion prev, Quaternion current) {
		Quaternion deltaRotation = Quaternion.Inverse(prev) * current;
		float angle = 0.0f;
		Vector3 axis = Vector3.zero;
		deltaRotation.ToAngleAxis(out angle, out axis);
		if (axis == Vector3.zero || axis.x == Mathf.Infinity || axis.x == Mathf.NegativeInfinity)
			return Vector3.zero;
		if (angle>180) angle -= 360;
		angle = angle/Time.deltaTime;
		return axis.normalized*angle;
	}
	
	private void UpdateTracking() {
		m_Position = m_Transform.position;
		m_Rotation = m_Transform.rotation;
		
		if (m_Rigidbody!=null) {
			// Rigidbody velocity is not reliable, so we calculate our own
			m_Velocity = (m_Position-m_PositionPrev)/Time.deltaTime;
			
			// Rigidbody angularVelocity is not reliable, so we calculate out own
			m_AngularVelocity = CalculateAngularVelocity(m_RotationPrev, m_Rotation);
		}
		else {
			m_Velocity = (m_Position-m_PositionPrev)/Time.deltaTime;
			
			m_AngularVelocity = CalculateAngularVelocity(m_RotationPrev, m_Rotation);
		}
		
		m_Acceleration = (m_Velocity-m_VelocityPrev) / Time.deltaTime;
		
		m_PositionPrev = m_Position;
		m_RotationPrev = m_Rotation;
		m_VelocityPrev = m_Velocity;
	}
	
	public void ControlledFixedUpdate() {
		if (Time.deltaTime == 0 || Time.timeScale == 0) return;
		
		if (m_CurrentFixedTime==Time.time) return;
		m_CurrentFixedTime = Time.time;
		
		if (fixedUpdate) UpdateTracking();
	}
	
	public void ControlledLateUpdate () {
		if (Time.deltaTime == 0 || Time.timeScale == 0) return;
		
		if (m_CurrentLateTime==Time.time) return;
		m_CurrentLateTime = Time.time;
		
		if (!fixedUpdate) UpdateTracking();
		
		m_VelocitySmoothed = Vector3.Lerp(
			m_VelocitySmoothed, m_Velocity, Time.deltaTime*10
		);
		
		m_AccelerationSmoothed = Vector3.Lerp(
			m_AccelerationSmoothed, m_Acceleration, Time.deltaTime*3
		);
		
		m_AngularVelocitySmoothed = Vector3.Lerp(
			m_AngularVelocitySmoothed, m_AngularVelocity, Time.deltaTime*3
		);
		
		if (fixedUpdate) {
			m_Position += m_Velocity*Time.deltaTime;
		}
	}
}
