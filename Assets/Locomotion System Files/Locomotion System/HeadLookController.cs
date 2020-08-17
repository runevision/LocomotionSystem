/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AlignmentTracker))]
public class HeadLookController : MonoBehaviour {
	
	public Transform neck;
	public Transform head;
	public Vector3 headLookVector = Vector3.forward;
	public Vector3 headUpVector = Vector3.up;
	public float rotationMultiplier = 0.5f;
	private Vector3 referenceLookDir;
	private Vector3 referenceUpDir;
	
	private AlignmentTracker tr;
	private Vector3 lookDir;
	private Vector3 upDir;
	
	void Start () {
		tr = GetComponent(typeof(AlignmentTracker)) as AlignmentTracker;
		
		Quaternion parentRot = neck.parent.rotation;
		Quaternion parentRotInv = Quaternion.Inverse(parentRot);
		referenceLookDir = parentRotInv * transform.rotation * headLookVector.normalized;
		referenceUpDir = parentRotInv * transform.rotation * headUpVector.normalized;
		lookDir = referenceLookDir;
		upDir = referenceUpDir;
	}
	
	void LateUpdate () {
		if (Time.deltaTime==0) return;
		
		Quaternion parentRot = neck.parent.rotation;
		Quaternion parentRotInv = Quaternion.Inverse(parentRot);
		
		// Desired look direction in world space
		Vector3 lookDirWorld = transform.rotation * headLookVector*0.01f;
		lookDirWorld += Util.ProjectOntoPlane(tr.velocity,transform.up);
		lookDirWorld = Quaternion.AngleAxis(
			Mathf.Clamp(tr.angularVelocitySmoothed.magnitude/2,-120,120),
			tr.angularVelocitySmoothed
		) * lookDirWorld;
		lookDirWorld = lookDirWorld.normalized;
		
		// Desired look and up directions in neck parent space
		Vector3 lookDirGoal = parentRotInv * lookDirWorld;
		
		// Handle things that are behind
		lookDirGoal = lookDirGoal.normalized;
		float behind = Vector3.Dot(lookDirGoal,referenceLookDir);
		if (behind<0) {
			if (Vector3.Dot(lookDirGoal,referenceUpDir)<0) {
				lookDirGoal -= Vector3.Project(lookDirGoal,referenceUpDir);
			}
			else {
				lookDirGoal += Vector3.Project(lookDirGoal,referenceUpDir)*behind;
			}
		}
		
		// Reduce effort - only rotate head some percentage toward direction
		float lookAngle = Vector3.Angle(referenceLookDir,lookDirGoal);
		Vector3 lookAxis = Vector3.Cross(referenceLookDir,lookDirGoal);
		if (lookAngle>180) lookAngle -= 360;
		lookAngle = lookAngle*rotationMultiplier; // multiplier here
		lookDirGoal = Quaternion.AngleAxis(lookAngle, lookAxis) * referenceLookDir;
		
		// Restrain look direction
		lookAngle = Vector3.Angle(referenceLookDir,lookDirGoal);
		lookAxis = Vector3.Cross(referenceLookDir,lookDirGoal);
		if (lookAngle>180) lookAngle -= 360;
		lookAngle = Mathf.Clamp(lookAngle, -80, 80); // max angle here
		lookDirGoal = Quaternion.AngleAxis(lookAngle, lookAxis) * referenceLookDir;
		
		// Make look and up perpendicular
		Vector3 upDirGoal = referenceUpDir;
		Vector3.OrthoNormalize(ref lookDirGoal, ref upDirGoal);
		
		// Interpolated look and up directions in neck parent space
		lookDir = Vector3.Slerp(lookDir, lookDirGoal, Time.deltaTime*5);
		upDir = Vector3.Slerp(upDir, upDirGoal, Time.deltaTime*5);
		Vector3.OrthoNormalize(ref lookDir, ref upDir);
		
		// Look rotation in world space
		Quaternion lookRot = (
			(parentRot * Quaternion.LookRotation(lookDir, upDir))
			* Quaternion.Inverse(parentRot * Quaternion.LookRotation(referenceLookDir, referenceUpDir))
		);
		
		Quaternion dividedRotation = Quaternion.Slerp(Quaternion.identity,lookRot,1f/2f);
		neck.rotation = dividedRotation * neck.rotation;
		head.rotation = dividedRotation * head.rotation;
		
	}
}
