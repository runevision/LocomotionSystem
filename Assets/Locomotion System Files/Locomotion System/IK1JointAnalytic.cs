/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

public class IK1JointAnalytic : IKSolver {
	
	public override void Solve(Transform[] bones, Vector3 target) {
		
		Transform hip = bones[0];
		Transform knee = bones[1];
		Transform ankle = bones[2];
		
		// Calculate the direction in which the knee should be pointing
		Vector3 vKneeDir = Vector3.Cross(
			ankle.position - hip.position,
			Vector3.Cross(
				ankle.position-hip.position,
				ankle.position-knee.position
			)
		);
		
		// Get lengths of leg bones
		float fThighLength = (knee.position-hip.position).magnitude;
		float fShinLength = (ankle.position-knee.position).magnitude;
		
		// Calculate the desired new joint positions
		Vector3 pHip = hip.position;
		Vector3 pAnkle = target;
		Vector3 pKnee = findKnee(pHip,pAnkle,fThighLength,fShinLength,vKneeDir);
		
		// Rotate the bone transformations to align correctly
		Quaternion hipRot = Quaternion.FromToRotation(knee.position-hip.position, pKnee-pHip) * hip.rotation;
		if (System.Single.IsNaN(hipRot.x)) {
			Debug.LogWarning("hipRot="+hipRot+" pHip="+pHip+" pAnkle="+pAnkle+" fThighLength="+fThighLength+" fShinLength="+fShinLength+" vKneeDir="+vKneeDir);
		}
		else {
			hip.rotation = hipRot;
			knee.rotation = Quaternion.FromToRotation(ankle.position-knee.position, pAnkle-pKnee) * knee.rotation;
		}
		
	}
	
	public Vector3 findKnee(Vector3 pHip, Vector3 pAnkle, float fThigh, float fShin, Vector3 vKneeDir) {
		Vector3 vB = pAnkle-pHip;
		float LB = vB.magnitude;
		
		float maxDist = (fThigh+fShin)*0.999f;
		if (LB>maxDist) {
			// ankle is too far away from hip - adjust ankle position
			pAnkle = pHip+(vB.normalized*maxDist);
			vB = pAnkle-pHip;
			LB = maxDist;
		}
		
		float minDist = Mathf.Abs(fThigh-fShin)*1.001f;
		if (LB<minDist) {
			// ankle is too close to hip - adjust ankle position
			pAnkle = pHip+(vB.normalized*minDist);
			vB = pAnkle-pHip;
			LB = minDist;
		}
		
		float aa = (LB*LB+fThigh*fThigh-fShin*fShin)/2/LB;
		float bb = Mathf.Sqrt(fThigh*fThigh-aa*aa);
		Vector3 vF = Vector3.Cross(vB,Vector3.Cross(vKneeDir,vB));
		return pHip+(aa*vB.normalized)+(bb*vF.normalized);
	}
	
}
