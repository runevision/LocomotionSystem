/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class LegInfo {
	public Transform hip;
	public Transform ankle;
	public Transform toe;
	public float footWidth;
	public float footLength;
	public Vector2 footOffset;
	[HideInInspector] public Transform[] legChain;
	[HideInInspector] public Transform[] footChain;
	[HideInInspector] public float legLength;
	[HideInInspector] public Vector3 ankleHeelVector;
	[HideInInspector] public Vector3 toeToetipVector;
	[HideInInspector] public Color debugColor;
}

[System.Serializable]
public class MotionGroupInfo {
	public string name;
	
	public IMotionAnalyzer[] motions;
	
	public Interpolator interpolator;
	public float[] GetMotionWeights(Vector3 velocity) {
		return interpolator.Interpolate(new float[] {velocity.x, velocity.y, velocity.z});
	}
}

[System.Serializable]
public class LegController : MonoBehaviour {
	
	public float groundPlaneHeight;
	
	public AnimationClip groundedPose;
	
	/*[HideInInspector]*/ public Transform rootBone;
	public Transform root { get { return rootBone; } }
	
	public LegInfo[] legs;
		
	public MotionAnalyzer[] sourceAnimations;
	
	[HideInInspector] public bool initialized = false;
	
	[HideInInspector] public MotionAnalyzerBackwards[] sourceAnimationsBackwards;
	
	[HideInInspector] public Vector3 m_HipAverage;
	public Vector3 hipAverage { get { return m_HipAverage; } }
	[HideInInspector] public Vector3 m_HipAverageGround;
	public Vector3 hipAverageGround { get { return m_HipAverageGround; } }
	
	[HideInInspector] public IMotionAnalyzer[] m_Motions;
	public IMotionAnalyzer[] motions { get { return m_Motions; } }
	
	[HideInInspector] public IMotionAnalyzer[] m_CycleMotions;
	public IMotionAnalyzer[] cycleMotions { get { return m_CycleMotions; } }
	
	[HideInInspector] public MotionGroupInfo[] m_MotionGroups;
	public MotionGroupInfo[] motionGroups { get { return m_MotionGroups; } }
	
	[HideInInspector] public IMotionAnalyzer[] m_NonGroupMotions;
	public IMotionAnalyzer[] nonGroupMotions { get { return m_NonGroupMotions; }}
	
	public void InitFootData(int leg) {
		// Make sure character is in grounded pose before analyzing
		groundedPose.SampleAnimation(gameObject,0);
		
		// Give each leg a color
		Vector3 colorVect = Quaternion.AngleAxis(leg*360.0f/legs.Length, Vector3.one) * Vector3.right;
		legs[leg].debugColor = new Color(colorVect.x, colorVect.y, colorVect.z);
		
		// Calculate heel and toetip positions and alignments
		
		// (The vector from the ankle to the ankle projected onto the ground at the stance pose
		// in local coordinates relative to the ankle transform.
		// This essentially is the ankle moved to the bottom of the foot, approximating the heel.)
		
		// Get ankle position projected down onto the ground
		Matrix4x4 ankleMatrix = Util.RelativeMatrix(legs[leg].ankle,gameObject.transform);
		Vector3 anklePosition = ankleMatrix.MultiplyPoint(Vector3.zero);
		Vector3 heelPosition = anklePosition;
		heelPosition.y = groundPlaneHeight;
		
		// Get toe position projected down onto the ground
		Matrix4x4 toeMatrix = Util.RelativeMatrix(legs[leg].toe,gameObject.transform);
		Vector3 toePosition = toeMatrix.MultiplyPoint(Vector3.zero);
		Vector3 toetipPosition = toePosition;
		toetipPosition.y = groundPlaneHeight;
		
		// Calculate foot middle and vector
		Vector3 footMiddle = (heelPosition + toetipPosition)/2;
		Vector3 footVector;
		if (toePosition==anklePosition) {
			footVector = ankleMatrix.MultiplyVector(legs[leg].ankle.localPosition);
			footVector.y = 0;
			footVector = footVector.normalized;
		}
		else {
			footVector = (toetipPosition - heelPosition).normalized;
		}
		Vector3 footSideVector = Vector3.Cross(Vector3.up, footVector);
		
		legs[leg].ankleHeelVector = (
			footMiddle
			+ (-legs[leg].footLength/2 + legs[leg].footOffset.y) * footVector
			+ legs[leg].footOffset.x * footSideVector
		);
		legs[leg].ankleHeelVector = ankleMatrix.inverse.MultiplyVector(legs[leg].ankleHeelVector - anklePosition);
		
		legs[leg].toeToetipVector = (
			footMiddle
			+ (legs[leg].footLength/2 + legs[leg].footOffset.y) * footVector
			+ legs[leg].footOffset.x * footSideVector
		);
		legs[leg].toeToetipVector = toeMatrix.inverse.MultiplyVector(legs[leg].toeToetipVector - toePosition);
	}
	
	public void Init() {
		
		// Only set initialized to true in the end, when we know that no errors have occurred.
		initialized = false;
		Debug.Log("Initializing "+name+" Locomotion System...");
		
		// Find the skeleton root (child of the GameObject) if none has been set already
		if (rootBone==null) {
			if (legs[0].hip==null) { Debug.LogError(name+": Leg Transforms are null.",this); return; }
			rootBone = legs[0].hip;
			while (root.parent != transform) rootBone = root.parent;
		}
		
		// Calculate data for LegInfo objects
		m_HipAverage = Vector3.zero;
		for (int leg=0; leg<legs.Length; leg++) {
			// Calculate leg bone chains
			if (legs[leg].toe==null) legs[leg].toe = legs[leg].ankle;
			legs[leg].legChain = GetTransformChain(legs[leg].hip, legs[leg].ankle);
			legs[leg].footChain = GetTransformChain(legs[leg].ankle, legs[leg].toe);
			
			// Calculate length of leg
			legs[leg].legLength = 0;
			for (int i=0; i<legs[leg].legChain.Length-1; i++) {
				legs[leg].legLength += (
					transform.InverseTransformPoint(legs[leg].legChain[i+1].position)
					-transform.InverseTransformPoint(legs[leg].legChain[i].position)
				).magnitude;
			}
			
			m_HipAverage += transform.InverseTransformPoint(legs[leg].legChain[0].position);
			
			InitFootData(leg);
		}
		m_HipAverage /= legs.Length;
		m_HipAverageGround = m_HipAverage;
		m_HipAverageGround.y = groundPlaneHeight;
	}
	
	public void Init2() {
		// Analyze motions
		List<MotionAnalyzerBackwards> sourceAnimationBackwardsList = new List<MotionAnalyzerBackwards>();
		for (int i=0; i<sourceAnimations.Length; i++) {
			
			// Initialize motion objects
			Debug.Log("Analysing sourceAnimations["+i+"]: "+sourceAnimations[i].name);
			sourceAnimations[i].Analyze(gameObject);
			
			// Also initialize backwards motion, if specified
			if (sourceAnimations[i].alsoUseBackwards) {
				MotionAnalyzerBackwards backwards = new MotionAnalyzerBackwards();
				backwards.orig = sourceAnimations[i];
				backwards.Analyze(gameObject);
				sourceAnimationBackwardsList.Add(backwards);
			}
		}
		sourceAnimationsBackwards = sourceAnimationBackwardsList.ToArray();
		
		// Motion sampling have put bones in random pose...
		// Reset to grounded pose, time 0
		groundedPose.SampleAnimation(gameObject,0);
		
		initialized = true;
		
		Debug.Log("Initializing "+name+" Locomotion System... Done!");
	}
	
	void Awake() {
		
		if (!initialized) { Debug.LogError(name+": Locomotion System has not been initialized.",this); return; }
		
		// Put regular and backwards motions into one array
		m_Motions = new IMotionAnalyzer[sourceAnimations.Length + sourceAnimationsBackwards.Length];
		for (int i=0; i<sourceAnimations.Length; i++) {
			motions[i] = sourceAnimations[i];
		}
		for (int i=0; i<sourceAnimationsBackwards.Length; i++) {
			motions[sourceAnimations.Length+i] = sourceAnimationsBackwards[i];
		}
		
		// Get number of walk cycle motions and put them in an array
		int cycleMotionAmount = 0;
		for (int i=0; i<motions.Length; i++) {
			if (motions[i].motionType == MotionType.WalkCycle) cycleMotionAmount++;
		}
		m_CycleMotions = new IMotionAnalyzer[cycleMotionAmount];
		int index = 0;
		for (int i=0; i<motions.Length; i++) {
			if (motions[i].motionType == MotionType.WalkCycle) {
				cycleMotions[index] = motions[i];
				index++;
			}
		}
		
		// Setup motion groups
		List<string> motionGroupNameList = new List<string>();
		List<MotionGroupInfo> motionGroupList = new List<MotionGroupInfo>();
		List<List<IMotionAnalyzer>> motionGroupMotionLists = new List<List<IMotionAnalyzer>>();
		List<IMotionAnalyzer> nonGroupMotionList = new List<IMotionAnalyzer>();
		for (int i=0; i<motions.Length; i++) {
			if (motions[i].motionGroup == "") {
				nonGroupMotionList.Add(motions[i]);
			}
			else {
				string groupName = motions[i].motionGroup;
				if ( !motionGroupNameList.Contains(groupName) ) {
					// Name is new so create a new motion group
					MotionGroupInfo m = new MotionGroupInfo();
					
					// Use it as controller for our new motion group
					m.name = groupName;
					motionGroupList.Add(m);
					motionGroupNameList.Add(groupName);
					motionGroupMotionLists.Add(new List<IMotionAnalyzer>());
				}
				motionGroupMotionLists[motionGroupNameList.IndexOf(groupName)].Add(motions[i]);
			}
		}
		m_NonGroupMotions = nonGroupMotionList.ToArray();
		m_MotionGroups = motionGroupList.ToArray();
		for (int g=0; g<motionGroups.Length; g++) {
			motionGroups[g].motions = motionGroupMotionLists[g].ToArray();
		}
				
		// Set up parameter space (for each motion group) used for automatic blending
		for (int g=0; g<motionGroups.Length; g++) {
			MotionGroupInfo group = motionGroups[g];
			Vector3[] motionVelocities = new Vector3[group.motions.Length];
			float[][] motionParameters = new float[group.motions.Length][];
			for (int i=0; i<group.motions.Length; i++) {
				motionVelocities[i] = group.motions[i].cycleVelocity;
				motionParameters[i] = new float[] {motionVelocities[i].x, motionVelocities[i].y, motionVelocities[i].z};
			}
			group.interpolator = new PolarGradientBandInterpolator(motionParameters);
		}
		
		// Calculate offset time values for each walk cycle motion
		CalculateTimeOffsets();
	}
	
	// Get the chain of transforms from one transform to a descendent one
	public Transform[] GetTransformChain(Transform upper, Transform lower) {
		Transform t = lower;
		int chainLength = 1;
		while (t != upper) {
			t = t.parent;
			chainLength++;
		}
		Transform[] chain = new Transform[chainLength];
		t = lower;
		for (int j=0; j<chainLength; j++) {
			chain[chainLength-1-j] = t;
			t = t.parent;
		}
		return chain;
	}
	
	public void CalculateTimeOffsets() {
		float[] offsets = new float[cycleMotions.Length];
		float[] offsetChanges = new float[cycleMotions.Length];
		for (int i=0; i<cycleMotions.Length; i++) offsets[i] = 0;
		
		int springs = (cycleMotions.Length*cycleMotions.Length-cycleMotions.Length)/2;
		int iteration = 0;
		bool finished = false;
		while (iteration<100 && finished==false) {
			for (int i=0; i<cycleMotions.Length; i++) offsetChanges[i] = 0;
			
			// Calculate offset changes
			for (int i=1; i<cycleMotions.Length; i++) {
				for (int j=0; j<i; j++) {
					for (int leg=0; leg<legs.Length; leg++) {
						float ta = cycleMotions[i].cycles[leg].stanceTime + offsets[i];
						float tb = cycleMotions[j].cycles[leg].stanceTime + offsets[j];
						Vector2 va = new Vector2( Mathf.Cos(ta*2*Mathf.PI), Mathf.Sin(ta*2*Mathf.PI) );
						Vector2 vb = new Vector2( Mathf.Cos(tb*2*Mathf.PI), Mathf.Sin(tb*2*Mathf.PI) );
						Vector2 abVector = vb-va;
						Vector2 va2 = va + abVector*0.1f;
						Vector2 vb2 = vb - abVector*0.1f;
						float ta2 = Util.Mod(Mathf.Atan2(va2.y,va2.x)/2/Mathf.PI);
						float tb2 = Util.Mod(Mathf.Atan2(vb2.y,vb2.x)/2/Mathf.PI);
						float aChange = Util.Mod(ta2-ta);
						float bChange = Util.Mod(tb2-tb);
						if (aChange>0.5f) aChange = aChange-1;
						if (bChange>0.5f) bChange = bChange-1;
						offsetChanges[i] += aChange * 5.0f / springs;
						offsetChanges[j] += bChange * 5.0f / springs;
					}
				}
			}
			
			// Apply new offset changes
			float maxChange = 0;
			for (int i=0; i<cycleMotions.Length; i++) {
				offsets[i] += offsetChanges[i];
				maxChange = Mathf.Max(maxChange,Mathf.Abs(offsetChanges[i]));
			}
			
			iteration++;
			if (maxChange<0.0001) finished = true;
		}
		
		// Apply the offsets to the motions
		for (int m=0; m<cycleMotions.Length; m++) {
			cycleMotions[m].cycleOffset = offsets[m];
			for (int leg=0; leg<legs.Length; leg++) {
				cycleMotions[m].cycles[leg].stanceTime =
					Util.Mod(cycleMotions[m].cycles[leg].stanceTime + offsets[m]);
			}
		}
	}
			
}
