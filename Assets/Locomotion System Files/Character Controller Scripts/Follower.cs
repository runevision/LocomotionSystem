using UnityEngine;
using System.Collections;

public class Follower : MonoBehaviour {
	
	public float desiredDistance;
	public GameObject target;
	
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		// get direction to target
		Vector3 targetVector = target.transform.position-transform.position;
		targetVector = Util.ProjectOntoPlane(targetVector, transform.up).normalized * targetVector.magnitude;
		float speed = (targetVector.magnitude-desiredDistance)*2;
		
		Vector3 directionVector = targetVector.normalized * speed;
		
		// Apply direction
		CharacterMotor motor = GetComponent(typeof(CharacterMotor)) as CharacterMotor;
		motor.desiredMovementDirection = directionVector;
		motor.desiredFacingDirection = targetVector;
	}
}
