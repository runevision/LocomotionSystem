/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

public class IKSimple : IKSolver {
	
	public int maxIterations = 100;
	
	public override void Solve(Transform[] bones, Vector3 target) {
		Transform endEffector = bones[bones.Length-1];
		//public Transform targetEffector = null; //the actual target end effector that we move around
		
		// Get the axis of rotation for each joint
		Vector3[] rotateAxes = new Vector3[bones.Length-2];
		float[] rotateAngles = new float[bones.Length-2];
		Quaternion[] rotations = new Quaternion[bones.Length-2];
		for (int i=0; i<bones.Length-2; i++) {
			rotateAxes[i] = Vector3.Cross(
				bones[i+1].position-bones[i].position,
				bones[i+2].position-bones[i+1].position
			);
			rotateAxes[i] = Quaternion.Inverse(bones[i].rotation) * rotateAxes[i];
			rotateAxes[i] = rotateAxes[i].normalized;
			rotateAngles[i] = Vector3.Angle(
				bones[i+1].position-bones[i].position,
				bones[i+1].position-bones[i+2].position
			);
			
			rotations[i] = bones[i+1].localRotation;
		}
		
		// Get the length of each bone
		float[] boneLengths = new float[bones.Length-1];
		float legLength = 0;
		for (int i=0; i<bones.Length-1; i++) {
			boneLengths[i] = (bones[i+1].position-bones[i].position).magnitude;
			legLength += boneLengths[i];
		}
		positionAccuracy = legLength*0.001f;
		
		float currentDistance = (endEffector.position-bones[0].position).magnitude;
		float targetDistance = (target-bones[0].position).magnitude;
		
		// Search for right joint bendings to get target distance between hip and foot
		float bendingLow, bendingHigh;
		bool minIsFound = false;
		bool bendMore = false;
		if (targetDistance > currentDistance) {
			minIsFound = true;
			bendingHigh = 1;
			bendingLow = 0;
		}
		else {
			bendMore = true;
			bendingHigh = 1;
			bendingLow = 0;
		}
		int tries = 0;
		while ( Mathf.Abs(currentDistance-targetDistance) > positionAccuracy && tries < maxIterations ) {
			tries++;
			float bendingNew;
			if (!minIsFound) bendingNew = bendingHigh;
			else bendingNew = (bendingLow+bendingHigh)/2;
			for (int i=0; i<bones.Length-2; i++) {
				float newAngle;
				if (!bendMore) newAngle = Mathf.Lerp(180, rotateAngles[i], bendingNew);
				else newAngle = rotateAngles[i]*(1-bendingNew) + (rotateAngles[i]-30)*bendingNew;
				float angleDiff = (rotateAngles[i]-newAngle);
				Quaternion addedRotation = Quaternion.AngleAxis(angleDiff,rotateAxes[i]);
				Quaternion newRotation = addedRotation * rotations[i];
				bones[i+1].localRotation = newRotation;
			}
			currentDistance = (endEffector.position-bones[0].position).magnitude;
			if (targetDistance > currentDistance) minIsFound = true;
			if (minIsFound) {
				if (targetDistance > currentDistance) bendingHigh = bendingNew;
				else bendingLow = bendingNew;
				if (bendingHigh < 0.01f) break;
			}
			else {
				bendingLow = bendingHigh;
				bendingHigh++;
			}
		}
		//Debug.Log("tries: "+tries);
		
		// Rotate hip bone such that foot is at desired position
		bones[0].rotation = (
			Quaternion.AngleAxis(
				Vector3.Angle(
					(endEffector.position-bones[0].position),
					(target-bones[0].position)
				),
				Vector3.Cross(
					(endEffector.position-bones[0].position),
					(target-bones[0].position)
				)
			) * bones[0].rotation
		);
		
	}
}
