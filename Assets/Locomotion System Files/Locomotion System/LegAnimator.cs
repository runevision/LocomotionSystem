/*
Copyright (c) 2010, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
//#define DEBUG
//#define VISUALIZE

using UnityEngine;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;

public enum LegCyclePhase {
	Stance, Lift, Flight, Land
}

public class LegState {
	// Past and future step
	public Vector3 stepFromPosition;
	public Vector3 stepToPosition;
	public Vector3 stepToPositionGoal;
	public Matrix4x4 stepFromMatrix;
	public Matrix4x4 stepToMatrix;
	public float stepFromTime;
	public float stepToTime;
	
	public int stepNr = 0;
	
	// Continiously changing foot state
	public float cycleTime = 1;
	public float designatedCycleTimePrev = 0.9f;
	public Vector3 hipReference;
	public Vector3 ankleReference;
	public Vector3 footBase;
	public Quaternion footBaseRotation;
	public Vector3 ankle;
	
	// Foot cycle event time stamps
	public float stanceTime   = 0;
	public float liftTime	  = 0.1f;
	public float liftoffTime  = 0.2f;
	public float postliftTime = 0.3f;
	public float prelandTime  = 0.7f;
	public float strikeTime   = 0.8f;
	public float landTime	  = 0.9f;
	
	public LegCyclePhase phase = LegCyclePhase.Stance;
	
	// Standing logic
	public bool parked;
	
	// Cycle properties
	public Vector3 stancePosition;
	public Vector3 heelToetipVector;
	
	public List<string> debugHistory = new List<string>();
	
	public float GetFootGrounding(float time) {
		if ( (time<=liftTime) || (time>=landTime) ) return 0;
		if ( (time>=postliftTime) && (time<=prelandTime) ) return 1;
		if ( time<postliftTime ) {
			return (time-liftTime)/(postliftTime-liftTime);
		}
		else {
			return 1-(time-prelandTime)/(landTime-prelandTime);
		}
	}
}

public class MotionGroupState {
	public AnimationState controller;
	public float weight;
	public AnimationState[] motionStates;
	public float[] relativeWeights;
	public float[] relativeWeightsBlended;
	public int primaryMotionIndex;
}

[RequireComponent(typeof(LegController))]
[RequireComponent(typeof(AlignmentTracker))]
public class LegAnimator : MonoBehaviour {
	
	public bool startAutomatically = true;
	public bool useIK = true;

	[Space]

	public float maxFootRotationAngle = 45.0f;
	public float maxIKAdjustmentDistance = 0.5f;
	
	// Step behavior settings
	public float minStepDistance = 0.2f; // Model dependent, thus no better default
	public float maxStepDuration = 1.5f; // Sensible for most models
	public float maxStepRotation = 160; // Sensible for most models, must be less than 180
	public float maxStepAcceleration = 5.0f; // Model dependent, thus no better default
	public float maxStepHeight   = 1.0f;
	public float maxSlopeAngle = 60; // Sensible for most models, must be less than 90
	
	// Transition behavior settings
	public bool enableLegParking = true;
	public float blendSmoothing = 0.2f;
	public LayerMask groundLayers = 1; // Default layer per default
	
	// Tilting settings
	public float groundHugX = 0; // Sensible for humanoids
	public float groundHugZ = 0; // Sensible for humanoids
	public float climbTiltAmount = 0.5f; // Sensible default value
	public float climbTiltSensitivity = 0.0f; // None as default
	public float accelerateTiltAmount = 0.02f; // Sensible default value
	public float accelerateTiltSensitivity = 0.0f; // None as default;

	[Header ("Debug")]

	// Debug settings
	public bool renderFootMarkers = false;
	public bool renderBlendingGraph = false;
	public bool renderCycleGraph = false;
	public bool renderAnimationStates = false;

	public Material vertexColorMaterial;
	
	private bool isActive;
	private float currentTime;
	
	private LegController legC;
	private AlignmentTracker tr;
	private LegInfo[] legs;
	private LegState[] legStates;
	
	private Vector3 position;
	private float speed;
	private float hSpeedSmoothed;
	private Vector3 objectVelocity;
	private Vector3 usedObjectVelocity;
	private Quaternion rotation;
	private Vector3 up;
	private Vector3 forward;
	private float scale;
	private Vector3 baseUpGround;
	private Vector3 bodyUp;
	private Vector3 legsUp;
	private float accelerationTiltX;
	private float accelerationTiltZ;
	
	private AnimationState controlMotionState;
	private MotionGroupState[] motionGroupStates;
	private AnimationState[] nonGroupMotionStates;
	private float[] nonGroupMotionWeights;
	
	private AnimationState[] motionStates;
	private AnimationState[] cycleMotionStates;
	private float[] motionWeights;
	private float[] cycleMotionWeights;
	private float summedMotionWeight;
	private float summedCycleMotionWeight;
	private float locomotionWeight;
	
	private float cycleDuration;
	private float cycleDistance;
	private float normalizedTime;
	
	private bool updateStates = true;
	
	[System.NonSerialized]
	public GameObject ghost;
	
	private Dictionary<string,TrajectoryVisualizer> trajectories
		= new Dictionary<string,TrajectoryVisualizer>();
	
	[Conditional("VISUALIZE")]
	void AddTrajectoryPoint(string name, Vector3 point) {
		trajectories[name].AddPoint(Time.time,point);
	}
	
	[Conditional("DEBUG")]
	void Assert(bool condition, string text) {
		if (!condition) UnityEngine.Debug.LogError(text);
	}
	
	[Conditional("DEBUG")]
	void AssertSane(float f, string text) {
		if (!Util.IsSaneNumber(f)) UnityEngine.Debug.LogError(text+"="+f);
	}
	
	[Conditional("DEBUG")]
	void AssertSane(Vector3 vect, string text) {
		if (!Util.IsSaneNumber(vect.x)
			|| !Util.IsSaneNumber(vect.y)
			|| !Util.IsSaneNumber(vect.z)
		) UnityEngine.Debug.LogError(text+"="+vect);
	}
	
	[Conditional("DEBUG")]
	void AssertSane(Quaternion q, string text) {
		if (!Util.IsSaneNumber(q.x)
			|| !Util.IsSaneNumber(q.y)
			|| !Util.IsSaneNumber(q.z)
			|| !Util.IsSaneNumber(q.w)
		) UnityEngine.Debug.LogError(text+"="+q);
	}
	
	void Start () {
		tr = GetComponent(typeof(AlignmentTracker)) as AlignmentTracker;
		legC = GetComponent(typeof(LegController)) as LegController;
		legs = legC.legs;
		if (!legC.initialized) {
			UnityEngine.Debug.LogError(name+": Locomotion System has not been initialized.",this);
			enabled = false;
		}
		
		legStates = new LegState[legs.Length];
		
		updateStates = true;
		ResetMotionStates();
		ResetSteps();
		
		isActive = false;
		
		for (int leg=0; leg<legs.Length; leg++) {
			trajectories.Add(
				"leg"+leg+"heel",
				new TrajectoryVisualizer(legs[leg].debugColor, 3)
			);
			trajectories.Add(
				"leg"+leg+"toetip",
				new TrajectoryVisualizer(legs[leg].debugColor, 3)
			);
			trajectories.Add(
				"leg"+leg+"footbase",
				new TrajectoryVisualizer(legs[leg].debugColor, 3)
			);
		}
	}
	
	void OnEnable() {
		updateStates = true;
		if (legC==null) return;
		ResetMotionStates();
		ResetSteps();
		if (!legC.initialized) {
			UnityEngine.Debug.LogError(name+": Locomotion System has not been initialized.",this);
			enabled = false;
		}
	}
	
	private void ResetMotionStates() {
		motionStates = new AnimationState[legC.motions.Length];
		cycleMotionStates = new AnimationState[legC.cycleMotions.Length];
		motionWeights = new float[legC.motions.Length];
		cycleMotionWeights = new float[legC.cycleMotions.Length];
		nonGroupMotionWeights = new float[legC.nonGroupMotions.Length];
		
		// Create control motion state
		controlMotionState = GetComponent<Animation>()["LocomotionSystem"];
		if (controlMotionState==null) {
			// Create dummy animation state with control motion name
			AnimationClip clip = new AnimationClip();
			clip.legacy = true;
			GetComponent<Animation>().AddClip(clip, "LocomotionSystem");
			controlMotionState = GetComponent<Animation>()["LocomotionSystem"];
		}
		controlMotionState.enabled = true;
		controlMotionState.wrapMode = WrapMode.Loop;
		controlMotionState.weight = 1;
		controlMotionState.layer = 10000;
		
		// Create motion states
		motionGroupStates = new MotionGroupState[legC.motionGroups.Length];
		int cm = 0;
		for (int m=0; m<legC.motions.Length; m++) {
			motionStates[m] = GetComponent<Animation>()[legC.motions[m].name];
			if (motionStates[m]==null) {
				GetComponent<Animation>().AddClip(legC.motions[m].animation, legC.motions[m].name);
				motionStates[m] = GetComponent<Animation>()[legC.motions[m].name];
			}
			motionStates[m].wrapMode = WrapMode.Loop;
			if (legC.motions[m].motionType==MotionType.WalkCycle) {
				cycleMotionStates[cm] = motionStates[m];
				cycleMotionStates[cm].speed = 0;
				cm++;
			}
		}
		
		// Create motion group states
		for (int g=0; g<motionGroupStates.Length; g++) {
			AnimationState controller = GetComponent<Animation>()[legC.motionGroups[g].name];
			if (controller==null) {
				// Create dummy animation state with motion group name
				AnimationClip clip = new AnimationClip();
				clip.legacy = true;
				GetComponent<Animation>().AddClip(clip, legC.motionGroups[g].name);
				controller = GetComponent<Animation>()[legC.motionGroups[g].name];
			}
			controller.enabled = true;
			controller.wrapMode = WrapMode.Loop;
			if (startAutomatically && g==0) controller.weight = 1;
			
			// Create state for this motion group
			motionGroupStates[g] = new MotionGroupState();
			motionGroupStates[g].controller = controller;
			motionGroupStates[g].motionStates = new AnimationState[legC.motionGroups[g].motions.Length];
			motionGroupStates[g].relativeWeights = new float[legC.motionGroups[g].motions.Length];
			for (int m=0; m<motionGroupStates[g].motionStates.Length; m++) {
				motionGroupStates[g].motionStates[m] =
					GetComponent<Animation>()[legC.motionGroups[g].motions[m].name];
			}
			motionGroupStates[g].primaryMotionIndex = 0;
		}
		
		// Create list of motions states that are not in motions groups
		nonGroupMotionStates = new AnimationState[legC.nonGroupMotions.Length];
		for (int m=0; m<legC.nonGroupMotions.Length; m++) {
			nonGroupMotionStates[m] = GetComponent<Animation>()[legC.nonGroupMotions[m].name];
			if (nonGroupMotionStates[m]==null) {
				GetComponent<Animation>().AddClip(legC.nonGroupMotions[m].animation, legC.nonGroupMotions[m].name);
				nonGroupMotionStates[m] = GetComponent<Animation>()[legC.nonGroupMotions[m].name];
				nonGroupMotionWeights[m] = nonGroupMotionStates[m].weight;
			}
		}
		
		for (int leg=0; leg<legs.Length; leg++) {
			legStates[leg] = new LegState();
		}
	}
	
	private void ResetSteps() {
		up = transform.up;
		forward = transform.forward;
		baseUpGround = up;
		legsUp = up;
		accelerationTiltX = 0;
		accelerationTiltZ = 0;
		bodyUp = up;
		
		tr.Reset();
		
		for (int leg=0; leg<legs.Length; leg++) {
			legStates[leg].stepFromTime = Time.time-0.01f;
			legStates[leg].stepToTime = Time.time;
			
			legStates[leg].stepFromMatrix = FindGroundedBase(
				transform.TransformPoint(legStates[leg].stancePosition/scale),
				transform.rotation,
				legStates[leg].heelToetipVector,
				false
			);
			legStates[leg].stepFromPosition = legStates[leg].stepFromMatrix.GetColumn(3);
			
			legStates[leg].stepToPosition = legStates[leg].stepFromPosition;
			legStates[leg].stepToMatrix = legStates[leg].stepFromMatrix;
		}
		normalizedTime = 0;
		
		cycleDuration = maxStepDuration;
		cycleDistance = 0;
	}
	
	void Update() {
		
		if (Time.deltaTime == 0 || Time.timeScale == 0) return;
		
		scale = transform.lossyScale.z;
		
		AssertSane(tr.velocity,"tr.velocity");
		AssertSane(up,"up");
		
		// When calculating speed, clamp vertical speed to be no longer than horizontal speed
		// to avoid sudden spikes when CharacterController walks up a step.
		Vector3 velocityVClamped = Util.ProjectOntoPlane(tr.velocity, up);
		speed = velocityVClamped.magnitude;
		velocityVClamped = velocityVClamped + up * Mathf.Clamp(Vector3.Dot(tr.velocity, up), -speed, speed);
		speed = velocityVClamped.magnitude;
		
		hSpeedSmoothed = Util.ProjectOntoPlane(tr.velocitySmoothed, up).magnitude;
		
		objectVelocity = (
			transform.InverseTransformPoint(tr.velocitySmoothed)
			-transform.InverseTransformPoint(Vector3.zero)
		);
		
		// Check if velocity (and turning - not implemented yet) have changed significantly
		bool newVelocity = false;
		if (
			(objectVelocity-usedObjectVelocity).magnitude
			>
			0.002f * Mathf.Min(objectVelocity.magnitude, usedObjectVelocity.magnitude)
			||
			updateStates
		) {
			newVelocity = true;
			usedObjectVelocity = objectVelocity;
		}
		
		bool newWeights = false;
		float smallWeightDifference = 0.001f;
		
		// Handle weights in motions groups
		for (int g=0; g<legC.motionGroups.Length; g++) {
			MotionGroupState group = motionGroupStates[g];
			
			// Check if motion group weight have changed significantly
			bool changedGroupWeight = false;
			bool justEnabled = false;
			float newGroupWeight = group.controller.weight;
			AssertSane(newGroupWeight,"newGroupWeight");
			if (group.controller.enabled==false || newGroupWeight < smallWeightDifference) newGroupWeight = 0;
			else if (newGroupWeight > 1-smallWeightDifference) newGroupWeight = 1;
			if (Mathf.Abs(newGroupWeight-group.weight) > smallWeightDifference) {
				changedGroupWeight = true;
				newWeights = true;
				if (group.weight==0 && newGroupWeight>0) justEnabled = true;
				group.weight = newGroupWeight;
			}
			
			// Check if primary weight in motion group have changed significantly
			// by external factors, for example a CrossFade that fades down these weights.
			// We must then enforce that the weights are again set according to the group dictate
			else if (
				Mathf.Abs(
					group.motionStates[group.primaryMotionIndex].weight
					- group.relativeWeights[group.primaryMotionIndex] * group.weight
				) > smallWeightDifference
				||
				group.motionStates[group.primaryMotionIndex].layer
				!= group.controller.layer
			) {
				changedGroupWeight = true;
			}
			
			if ( newVelocity || changedGroupWeight ) {	
				
				// Update weights in motion group if necessary
				if ( (newVelocity || justEnabled) && group.weight > 0 ) {
					newWeights = true;
					
					// Calculate motion weights - heavy call! :(
					MotionGroupInfo groupInfo = legC.motionGroups[g];
					group.relativeWeights = groupInfo.GetMotionWeights(new Vector3(objectVelocity.x, 0, objectVelocity.z));

				}
			}
			
			if (group.weight > 0) {
				if (group.relativeWeightsBlended == null) {
					group.relativeWeightsBlended = new float[group.relativeWeights.Length];
					for (int m=0; m<group.motionStates.Length; m++) {
						group.relativeWeightsBlended[m] = group.relativeWeights[m];
					}
				}
				
				float highestWeight = 0;
				int controllerLayer = group.controller.layer;
				for (int m=0; m<group.motionStates.Length; m++) {
					if (blendSmoothing > 0)
						group.relativeWeightsBlended[m] = Mathf.Lerp(group.relativeWeightsBlended[m], group.relativeWeights[m], Time.deltaTime / blendSmoothing);
					else
						group.relativeWeightsBlended[m] = group.relativeWeights[m];
					AssertSane(group.relativeWeights[m],"group.relativeWeights[m]");
					AssertSane(group.relativeWeightsBlended[m],"group.relativeWeightsBlended[m]");
					AssertSane(Time.deltaTime / blendSmoothing,"Time.deltaTime / blendSmoothing ( "+Time.deltaTime+" / "+blendSmoothing+" )");
					float weight = group.relativeWeightsBlended[m] * group.weight;
					group.motionStates[m].weight = weight;
					if (weight>0) group.motionStates[m].enabled = true;
					else group.motionStates[m].enabled = false;
					group.motionStates[m].layer = controllerLayer;
					// Remember which motion has the highest weight
					// This will be used for checking that the weights
					// are not changed by external factors.
					if (weight>highestWeight) {
						group.primaryMotionIndex = m;
						highestWeight = weight;
					}
				}
			}
			else {
				for (int m=0; m<group.motionStates.Length; m++) {
					group.motionStates[m].weight = 0;
					group.motionStates[m].enabled = false;
				}
				group.relativeWeightsBlended = null;
			}
		}
		
		// Handle weights of motions that are not in motions groups
		for (int m=0; m<nonGroupMotionStates.Length; m++) {
			float newWeight = nonGroupMotionStates[m].weight;
			if (nonGroupMotionStates[m].enabled==false) newWeight = 0;
			if (
				Mathf.Abs(newWeight-nonGroupMotionWeights[m]) > smallWeightDifference
				|| (newWeight==0 && nonGroupMotionWeights[m]!=0)
			) {
				newWeights = true;
				nonGroupMotionWeights[m] = newWeight;
			}
		}
		
		bool justActivated = updateStates;
		if (newWeights || updateStates) {
			// Get summed weights
			summedMotionWeight = 0;
			summedCycleMotionWeight = 0;
			int cm = 0;
			for (int m=0; m<legC.motions.Length; m++) {
				motionWeights[m] = motionStates[m].weight;
				summedMotionWeight += motionWeights[m];
				if (legC.motions[m].motionType==MotionType.WalkCycle) {
					cycleMotionWeights[cm] = motionWeights[m];
					summedCycleMotionWeight += motionWeights[m];
					cm++;
				}
			}
			if (summedMotionWeight==0) {
				isActive = false;
				
				if (ghost!=null) {
					GhostOriginal go = ghost.GetComponent(typeof(GhostOriginal)) as GhostOriginal;
					go.Synch();
				}
				return;
			}
			else {
				if (isActive==false) justActivated = true;
				isActive = true;
			}
			
			// Make weights sum to 1
			for (int m=0; m<legC.motions.Length; m++) {
				motionWeights[m] /= summedMotionWeight;
			}
			if (summedCycleMotionWeight>0) {
				for (int m=0; m<legC.cycleMotions.Length; m++) {
					cycleMotionWeights[m] /= summedCycleMotionWeight;
				}
			}
			
			// Get blended cycle data (based on all animations)
			for (int leg=0; leg<legs.Length; leg++) {
				legStates[leg].stancePosition = Vector3.zero;
				legStates[leg].heelToetipVector = Vector3.zero;
			}
			for (int m=0; m<legC.motions.Length; m++) {
				IMotionAnalyzer motion = legC.motions[m];
				float weight = motionWeights[m];
				if (weight>0) {
					for (int leg=0; leg<legs.Length; leg++) {
						legStates[leg].stancePosition += (
							motion.cycles[leg].stancePosition * scale * weight
						);
						legStates[leg].heelToetipVector += (
							motion.cycles[leg].heelToetipVector * scale * weight
						);
					}
				}
			}
			
			// Ensure legs won't intersect WIP
			/*if (objectVelocity.x != 0 || objectVelocity.z != 0) {
				Vector3 perpObjectVelocity = new Vector3(-objectVelocity.z, objectVelocity.y, objectVelocity.x).normalized;
				Vector3[] stanceOffsets = new Vector3[legs.Length];
				// Compare every leg with every other leg
				for (int leg=0; leg<legs.Length; leg++) {
					for (int other=0; other<leg; other++) {
						Vector3 interStanceDir =
							(legStates[leg].stancePosition + legStates[leg].heelToetipVector / 2)
							- (legStates[other].stancePosition + legStates[other].heelToetipVector / 2);
						
						
						//UnityEngine.Debug.Log(interStanceDir.x+", "+interStanceDir.z);
						
						float stanceSpacing = Vector3.Dot(interStanceDir, perpObjectVelocity);
						
						float spacing = 0.2f;
						if (stanceSpacing < spacing) {
							stanceOffsets[leg] = (spacing - stanceSpacing) * 0.5f * perpObjectVelocity;
							stanceOffsets[other] = -(spacing - stanceSpacing) * 0.5f * perpObjectVelocity;
						}
						
						UnityEngine.Debug.Log("Leg "+other+"-"+leg+" stance spacing: "+stanceSpacing);
					}
				}
				
				for (int leg=0; leg<legs.Length; leg++) {
					legStates[leg].stancePosition += stanceOffsets[leg];
				}
			}*/
			
			// Get blended cycle data (based on cycle animations only)
			if (summedCycleMotionWeight>0) {
				for (int leg=0; leg<legs.Length; leg++) {
					legStates[leg].liftTime = 0;
					legStates[leg].liftoffTime = 0;
					legStates[leg].postliftTime = 0;
					legStates[leg].prelandTime = 0;
					legStates[leg].strikeTime = 0;
					legStates[leg].landTime = 0;
				}
				for (int m=0; m<legC.cycleMotions.Length; m++) {
					IMotionAnalyzer motion = legC.cycleMotions[m];
					float weight = cycleMotionWeights[m];
					if (weight>0) {
						for (int leg=0; leg<legs.Length; leg++) {
							legStates[leg].liftTime += motion.cycles[leg].liftTime * weight;
							legStates[leg].liftoffTime += motion.cycles[leg].liftoffTime * weight;
							legStates[leg].postliftTime += motion.cycles[leg].postliftTime * weight;
							legStates[leg].prelandTime += motion.cycles[leg].prelandTime * weight;
							legStates[leg].strikeTime += motion.cycles[leg].strikeTime * weight;
							legStates[leg].landTime += motion.cycles[leg].landTime * weight;
						}
					}
				}
			}
			
			// Get blended stance time (based on cycle animations only)
			// - getting the average is tricky becuase stance time is cyclic!
			if (summedCycleMotionWeight>0) {
				for (int leg=0; leg<legs.Length; leg++) {
					Vector2 stanceTimeVector = Vector2.zero;
					for (int m=0; m<legC.cycleMotions.Length; m++) {
						IMotionAnalyzer motion = legC.cycleMotions[m];
						float weight = cycleMotionWeights[m];
						if (weight>0) {
							stanceTimeVector += new Vector2(
								Mathf.Cos(motion.cycles[leg].stanceTime*2*Mathf.PI),
								Mathf.Sin(motion.cycles[leg].stanceTime*2*Mathf.PI)
							)*weight;
						}
					}
					legStates[leg].stanceTime = Util.Mod(
						Mathf.Atan2(stanceTimeVector.y,stanceTimeVector.x)/2/Mathf.PI
					);
				}
			}
		}
		
		float controlMotionStateWeight = controlMotionState.weight;
		if (!controlMotionState.enabled) controlMotionStateWeight = 0;
		locomotionWeight = Mathf.Clamp01(summedMotionWeight * controlMotionStateWeight);
		if (updateStates || justActivated) ResetSteps();
		
		// Calculate cycle distance and duration
		
		// TODO
		// Calculate exponent and multiplier
		
		/*float distanceExponent;
		if (legC.motions.Length>=2) {
			distanceExponent = (
				Mathf.Log(legC.motions[1].cycleDistance / legC.motions[0].cycleDistance)
				/
				Mathf.Log(legC.motions[1].cycleSpeed / legC.motions[0].cycleSpeed)
			);
		}
		else { distanceExponent = 0.5f; }
		float distanceMultiplier = (
			legC.motions[0].cycleDistance
			* scale / Mathf.Pow(legC.motions[0].cycleSpeed * scale, distanceExponent)
		);
		
		// Find distance based on speed
		cycleDistance = distanceMultiplier * Mathf.Pow(speed, distanceExponent);*/
		
		float cycleFrequency = 0;
		float animatedCycleSpeed = 0;
		for (int m=0; m<legC.motions.Length; m++) {
			IMotionAnalyzer motion = legC.motions[m];
			float weight = motionWeights[m];
			if (weight>0) {
				if (motion.motionType==MotionType.WalkCycle) {
					cycleFrequency += (1/motion.cycleDuration) * weight;
				}
				animatedCycleSpeed += motion.cycleSpeed * weight;
			}
		}
		float desiredCycleDuration = maxStepDuration;
		if (cycleFrequency>0) desiredCycleDuration = 1/cycleFrequency;
		
		// Make the step duration / step length relation follow a sqrt curve
		float speedMultiplier = 1;
		if (speed != 0)  speedMultiplier = animatedCycleSpeed*scale / speed;
		if (speedMultiplier>0) desiredCycleDuration *= /*Mathf.Sqrt(*/speedMultiplier/*)*/;
		
		// Enforce short enough step duration while rotating
		float verticalAngularVelocity = Vector3.Project(tr.rotation * tr.angularVelocitySmoothed, up).magnitude;
		if (verticalAngularVelocity>0) {
		 	desiredCycleDuration = Mathf.Min(
				maxStepRotation / verticalAngularVelocity,
				desiredCycleDuration
			);
		}
		
		// Enforce short enough step duration while accelerating
		float groundAccelerationMagnitude = Util.ProjectOntoPlane(tr.accelerationSmoothed,up).magnitude;
		if (groundAccelerationMagnitude>0) {
			desiredCycleDuration = Mathf.Clamp(
				maxStepAcceleration / groundAccelerationMagnitude,
				desiredCycleDuration/2,
				desiredCycleDuration
			);
		}
		
		// Enforce short enough step duration in general
		desiredCycleDuration = Mathf.Min(desiredCycleDuration, maxStepDuration);
		
		cycleDuration = desiredCycleDuration;
		
		// Set cycle distance
		AssertSane(cycleDuration,"cycleDuration");
		AssertSane(speed,"speed");
		cycleDistance = cycleDuration * speed;
				
		// Set time of all animations used in blending
		
		// Check if all legs are "parked" i.e. standing still
		bool allParked = false;
		if (enableLegParking) {
			allParked = true;
			for (int leg=0; leg<legs.Length; leg++) {
				if (legStates[leg].parked == false) allParked = false;
			}
		}
		
		// Synchronize animations
		if (!allParked) {
			normalizedTime = Util.Mod(normalizedTime + (1/cycleDuration)*Time.deltaTime);
			for (int m=0; m<legC.cycleMotions.Length; m++) {
				if (legC.cycleMotions[m].GetType()==typeof(MotionAnalyzerBackwards)) {
					cycleMotionStates[m].normalizedTime = (
						1 - (normalizedTime - legC.cycleMotions[m].cycleOffset)
					);
				} else {
					cycleMotionStates[m].normalizedTime = normalizedTime - legC.cycleMotions[m].cycleOffset;
				}
			}
		}
		
		updateStates = false;
		
		currentTime = Time.time;
		
		if (ghost!=null) {
			GhostOriginal go = ghost.GetComponent(typeof(GhostOriginal)) as GhostOriginal;
			go.Synch();
		}
	}
	
	void FixedUpdate() {
		if (Time.deltaTime == 0 || Time.timeScale == 0) return;
		tr.ControlledFixedUpdate();
	}
	
	// Update is called once per frame
	void LateUpdate () {
		if (Time.deltaTime == 0 || Time.timeScale == 0) return;
		
		MonitorFootsteps();
		
		tr.ControlledLateUpdate();
		position = tr.position;
		rotation = tr.rotation;
		
		AssertSane(tr.accelerationSmoothed, "acceleration");
		
		up = rotation * Vector3.up;
		forward = rotation * Vector3.forward;
		Vector3 right = rotation * Vector3.right;
		
		// Do not run locomotion system in this frame if locomotion weights are all zero
		if (!isActive) return;
		if (currentTime!=Time.time) return;
		if (!useIK) return;
		
		int origLayer = gameObject.layer;
		gameObject.layer = 2;
		
		for (int leg=0; leg<legs.Length; leg++) {
			
			// Calculate current time in foot cycle
			float designatedCycleTime = Util.CyclicDiff(normalizedTime, legStates[leg].stanceTime);
			
			// See if this time is beginning of a new step
			bool newStep = false;
			if (designatedCycleTime < legStates[leg].designatedCycleTimePrev-0.5f) {
				newStep = true;
				legStates[leg].stepNr++;
				if (!legStates[leg].parked) {
					legStates[leg].stepFromTime = legStates[leg].stepToTime;
					legStates[leg].stepFromPosition = legStates[leg].stepToPosition;
					legStates[leg].stepFromMatrix = legStates[leg].stepToMatrix;
					legStates[leg].debugHistory.Clear();
					legStates[leg].cycleTime = designatedCycleTime;
				}
				legStates[leg].parked = false;
				
			}
			legStates[leg].designatedCycleTimePrev = designatedCycleTime;
			
			// Find future step time	
			legStates[leg].stepToTime = (
				Time.time
				+ (1-designatedCycleTime) * cycleDuration
			);
			
			float predictedStrikeTime = (legStates[leg].strikeTime-designatedCycleTime) * cycleDuration;
			//float predictedStanceTime = (1-designatedCycleTime) * cycleDuration;
			
			if (
				(designatedCycleTime >= legStates[leg].strikeTime)
				//|| (legStates[leg].cycleTime >= cycleTimeNew)
			) legStates[leg].cycleTime = designatedCycleTime;
			else {
				// Calculate how fast cycle must go to catch up from a possible parked state
				legStates[leg].cycleTime += (
					(legStates[leg].strikeTime-legStates[leg].cycleTime)
					 * Time.deltaTime/predictedStrikeTime // * 2
					//(1-legStates[leg].cycleTime)
					// * Time.deltaTime/predictedStanceTime
				);
			}
			if (
				(legStates[leg].cycleTime >= designatedCycleTime)
			) legStates[leg].cycleTime = designatedCycleTime;
			
			// Find future step position and alignment
			if (legStates[leg].cycleTime < legStates[leg].strikeTime) {
				
				// Value from 0.0 at liftoff time to 1.0 at strike time
				float flightTime = Mathf.InverseLerp(
					legStates[leg].liftoffTime, legStates[leg].strikeTime, legStates[leg].cycleTime);
				
				// Find future step alignment
				Quaternion newPredictedRotation = Quaternion.AngleAxis(
					tr.angularVelocitySmoothed.magnitude*(legStates[leg].stepToTime-Time.time),
					tr.angularVelocitySmoothed
				) * tr.rotation;
				
				// Apply smoothing of predicted step rotation
				Quaternion predictedRotation;
				if (legStates[leg].cycleTime <= legStates[leg].liftoffTime) {
					// No smoothing if foot hasn't lifted off the ground yet
					predictedRotation = newPredictedRotation;
				}
				else {
					Quaternion oldPredictedRotation = Util.QuaternionFromMatrix(legStates[leg].stepToMatrix);
					oldPredictedRotation =
						Quaternion.FromToRotation(oldPredictedRotation*Vector3.up,up)
						* oldPredictedRotation;
					
					float rotationSeekSpeed = Mathf.Max(
						tr.angularVelocitySmoothed.magnitude*3,
						maxStepRotation / maxStepDuration
					);
					float maxRotateAngle = rotationSeekSpeed / flightTime * Time.deltaTime;
					predictedRotation = Util.ConstantSlerp(
						oldPredictedRotation, newPredictedRotation, maxRotateAngle);
				}
				
				// Find future step position (prior to raycast)
				Vector3 newStepPosition;
				
				// Find out how much the character is turning
				float turnSpeed = Vector3.Dot(tr.angularVelocitySmoothed,up);
				
				if (turnSpeed*cycleDuration<5) {
					// Linear prediction if no turning
					newStepPosition = (
						tr.position
						+ predictedRotation * legStates[leg].stancePosition
						+ tr.velocity * (legStates[leg].stepToTime-Time.time)
					);
				}
				else {
					// If character is turning, assume constant turning
					// and do circle-based prediction
					Vector3 turnCenter = Vector3.Cross(up, tr.velocity) / (turnSpeed*Mathf.PI/180);
					Vector3 predPos = turnCenter + Quaternion.AngleAxis(
						turnSpeed*(legStates[leg].stepToTime-Time.time),
						up
					) * -turnCenter;
					
					newStepPosition = (
						tr.position
						+ predictedRotation * legStates[leg].stancePosition
						+ predPos
					);
				}
				
				newStepPosition = Util.SetHeight(
					newStepPosition, position+legC.groundPlaneHeight*up*scale, up
				);
				
				// Get position and orientation projected onto the ground
				Matrix4x4 groundedBase = FindGroundedBase(
					newStepPosition,
					predictedRotation,
					legStates[leg].heelToetipVector,
					true
				);
				newStepPosition = groundedBase.GetColumn(3);
				
				// Apply smoothing of predicted step position
				if (newStep) {
					// No smoothing if foot hasn't lifted off the ground yet
					legStates[leg].stepToPosition = newStepPosition;
					legStates[leg].stepToPositionGoal = newStepPosition;
				}
				else {
					float stepSeekSpeed = Mathf.Max(
						speed*3 + tr.accelerationSmoothed.magnitude/10,
						legs[leg].footLength*scale*3
					);
					
					float towardStrike = legStates[leg].cycleTime/legStates[leg].strikeTime;
					
					// Evaluate if new potential goal is within reach
					if (
						(newStepPosition-legStates[leg].stepToPosition).sqrMagnitude
						< Mathf.Pow(stepSeekSpeed*((1/towardStrike)-1),2)
					) {
						legStates[leg].stepToPositionGoal = newStepPosition;
					}
					
					// Move towards goal - faster initially, then slower
					Vector3 moveVector = legStates[leg].stepToPositionGoal-legStates[leg].stepToPosition;
					if (moveVector!=Vector3.zero && predictedStrikeTime>0) {
						float moveVectorMag = moveVector.magnitude;
						float moveDist = Mathf.Min(
							moveVectorMag,
							Mathf.Max(
								stepSeekSpeed / Mathf.Max(0.1f,flightTime) * Time.deltaTime,
								(1+2*Mathf.Pow(towardStrike-1,2))
									* (Time.deltaTime/predictedStrikeTime)
									* moveVectorMag
							)
						);
						legStates[leg].stepToPosition += (
							(legStates[leg].stepToPositionGoal-legStates[leg].stepToPosition)
							/ moveVectorMag * moveDist
						);
					}
				}
				
				groundedBase.SetColumn(3,legStates[leg].stepToPosition);
				groundedBase[3,3] = 1;
				legStates[leg].stepToMatrix = groundedBase;
			}
			
			if (enableLegParking) {
				
				// Check if old and new footstep has
				// significant difference in position or rotation
				float distToNextStep = Util.ProjectOntoPlane(
					legStates[leg].stepToPosition - legStates[leg].stepFromPosition, up
				).magnitude;
				
				bool significantStepDifference = (
					distToNextStep > minStepDistance
					||
					Vector3.Angle(
						legStates[leg].stepToMatrix.GetColumn(2), 
						legStates[leg].stepFromMatrix.GetColumn(2)
					) > maxStepRotation/2
				);
				
				// Park foot's cycle if the step length/rotation is below threshold
				if (newStep	&& !significantStepDifference) {
					legStates[leg].parked = true;
				}
				
				// Allow unparking during first part of cycle if the
				// step length/rotation is now above threshold
				if (
					legStates[leg].parked
					//&& ( legStates[leg].cycleTime < 0.5f )
					&& ( designatedCycleTime < 0.67f )
					&& significantStepDifference
				) {
					legStates[leg].parked = false;
				}
				
				if (legStates[leg].parked) legStates[leg].cycleTime = 0;
			}
		}
		
		// Calculate base point
		Vector3 tangentDir = Quaternion.Inverse(tr.rotation) * tr.velocity;
		// This is in object space, so OK to set y to 0
		tangentDir.y = 0;
		if (tangentDir.sqrMagnitude>0) tangentDir = tangentDir.normalized;
		
		AssertSane(cycleDistance, "cycleDistance");
		
		Vector3[] basePointFoot = new Vector3[legs.Length];
		Vector3 basePoint = Vector3.zero;
		Vector3 baseVel = Vector3.zero;
		Vector3 avgFootPoint = Vector3.zero;
		float baseSummedWeight = 0.0f;
		for (int leg=0; leg<legs.Length; leg++) {
			// Calculate base position (starts and ends in tangent to surface)
			
			// weight goes 1 -> 0 -> 1 as cycleTime goes from 0 to 1
			float weight = Mathf.Cos(legStates[leg].cycleTime*2*Mathf.PI)/2.0f+0.5f;
			baseSummedWeight += weight+0.001f;
			
			// Value from 0.0 at lift time to 1.0 at land time
			float strideTime = Mathf.InverseLerp(
				legStates[leg].liftTime, legStates[leg].landTime, legStates[leg].cycleTime);
			float strideSCurve = -Mathf.Cos(strideTime*Mathf.PI)/2f+0.5f;
			
			Vector3 stepBodyPoint = transform.TransformDirection(-legStates[leg].stancePosition)*scale;
			
			AssertSane(legStates[leg].cycleTime, "legStates[leg].cycleTime");
			AssertSane(strideSCurve, "strideSCurve");
			AssertSane(tangentDir, "tangentDir");
			AssertSane(cycleDistance, "cycleDistance");
			AssertSane(legStates[leg].stepFromPosition, "legStates[leg].stepFromPosition");
			AssertSane(legStates[leg].stepToPosition, "legStates[leg].stepToPosition");
			AssertSane(legStates[leg].stepToMatrix.MultiplyVector(tangentDir), "stepToMatrix");
			AssertSane(legStates[leg].stepFromMatrix.MultiplyVector(tangentDir), "stepToMatrix");
			basePointFoot[leg] = (
				(
					legStates[leg].stepFromPosition
					+legStates[leg].stepFromMatrix.MultiplyVector(tangentDir)
						*cycleDistance*legStates[leg].cycleTime
				)*(1-strideSCurve)
				+ (
					legStates[leg].stepToPosition
					+legStates[leg].stepToMatrix.MultiplyVector(tangentDir)
						*cycleDistance*(legStates[leg].cycleTime-1)
				)*strideSCurve
			);
			AssertSane(basePointFoot[leg], "basePointFoot[leg]");
			if (System.Single.IsNaN(basePointFoot[leg].x) || System.Single.IsNaN(basePointFoot[leg].y) || System.Single.IsNaN(basePointFoot[leg].z)) {
				UnityEngine.Debug.LogError("legStates[leg].cycleTime="+legStates[leg].cycleTime+", strideSCurve="+strideSCurve+", tangentDir="+tangentDir+", cycleDistance="+cycleDistance+", legStates[leg].stepFromPosition="+legStates[leg].stepFromPosition+", legStates[leg].stepToPosition="+legStates[leg].stepToPosition+", legStates[leg].stepToMatrix.MultiplyVector(tangentDir)="+legStates[leg].stepToMatrix.MultiplyVector(tangentDir)+", legStates[leg].stepFromMatrix.MultiplyVector(tangentDir)="+legStates[leg].stepFromMatrix.MultiplyVector(tangentDir));
			}
			
			basePoint += (basePointFoot[leg]+stepBodyPoint)*(weight+0.001f);
			avgFootPoint += basePointFoot[leg];
			
			baseVel += (legStates[leg].stepToPosition-legStates[leg].stepFromPosition) * (1f-weight+0.001f);
		}
		Assert(baseSummedWeight!=0, "baseSummedWeight is zero");
		avgFootPoint /= legs.Length;
		basePoint /= baseSummedWeight;
		if (
			System.Single.IsNaN(basePoint.x)
			|| System.Single.IsNaN(basePoint.y)
			|| System.Single.IsNaN(basePoint.z)
		) basePoint = position;
		AssertSane(basePoint, "basePoint");
		Vector3 groundBasePoint = basePoint + up*legC.groundPlaneHeight;
		
		// Calculate base up vector
		Vector3 baseUp = up;
		if (groundHugX>=0 || groundHugZ>=0) {
			
			// Ground-based Base Up Vector
			Vector3 baseUpGroundNew = up*0.1f;
			for (int leg=0; leg<legs.Length; leg++) {
				Vector3 vec = (basePointFoot[leg]-avgFootPoint);
				baseUpGroundNew += Vector3.Cross(Vector3.Cross(vec,baseUpGround),vec);
				UnityEngine.Debug.DrawLine(basePointFoot[leg],avgFootPoint);
			}
			
			//Assert(up.magnitude>0, "up has zero length");
			//Assert(baseUpGroundNew.magnitude>0, "baseUpGroundNew has zero length");
			//Assert(Vector3.Dot(baseUpGroundNew,up)!=0, "baseUpGroundNew and up are perpendicular");
			float baseUpGroundNewUpPart = Vector3.Dot(baseUpGroundNew,up);
			if (baseUpGroundNewUpPart>0) {
				// Scale vector such that vertical element has length of 1
				baseUpGroundNew /= baseUpGroundNewUpPart;
				AssertSane(baseUpGroundNew, "baseUpGroundNew");
				baseUpGround = baseUpGroundNew;
			}
		
			if (groundHugX>=1 && groundHugZ>=1) {
				baseUp = baseUpGround.normalized;
			}
			else {
				baseUp = (
					up
					+ groundHugX*Vector3.Project(baseUpGround,right)
					+ groundHugZ*Vector3.Project(baseUpGround,forward)
				).normalized;
			}
		}
		
		// Velocity-based Base Up Vector
		Vector3 baseUpVel = up;
		if (baseVel!=Vector3.zero) baseUpVel = Vector3.Cross(baseVel,Vector3.Cross(up,baseVel));
		// Scale vector such that vertical element has length of 1
		baseUpVel /= Vector3.Dot(baseUpVel,up);
		
		// Calculate acceleration direction in local XZ plane
		Vector3 accelerationDir = Vector3.zero;
		if (accelerateTiltAmount*accelerateTiltSensitivity != 0) {
			float accelX = Vector3.Dot(
				tr.accelerationSmoothed*accelerateTiltSensitivity*accelerateTiltAmount,
				right
			) * (1-groundHugX);
			float accelZ = Vector3.Dot(
				tr.accelerationSmoothed*accelerateTiltSensitivity*accelerateTiltAmount,
				forward
			) * (1-groundHugZ);
			accelerationTiltX = Mathf.Lerp(accelerationTiltX, accelX, Time.deltaTime*10);
			accelerationTiltZ = Mathf.Lerp(accelerationTiltZ, accelZ, Time.deltaTime*10);
			accelerationDir = (
				(accelerationTiltX * right + accelerationTiltZ * forward)
				// a curve that goes towards 1 as speed goes towards infinity:
				* (1 - 1/(hSpeedSmoothed*accelerateTiltSensitivity + 1))
			);
		}
		
		// Calculate tilting direction in local XZ plane
		Vector3 tiltDir = Vector3.zero;
		if (climbTiltAmount*climbTiltAmount != 0) {
			tiltDir = (
				(
					Vector3.Project(baseUpVel,right) * (1-groundHugX)
					+ Vector3.Project(baseUpVel,forward) * (1-groundHugZ)
				) * -climbTiltAmount
				// a curve that goes towards 1 as speed goes towards infinity:
				* (1 - 1/(hSpeedSmoothed*climbTiltSensitivity + 1))
			);
		}
		
		// Up vector and rotations for the torso
		bodyUp = (baseUp + accelerationDir + tiltDir).normalized;
		Quaternion bodyRotation = Quaternion.AngleAxis(
			Vector3.Angle(up,bodyUp),
			Vector3.Cross(up,bodyUp)
		);
		
		// Up vector and rotation for the legs
		legsUp = (up + accelerationDir).normalized;
		Quaternion legsRotation = Quaternion.AngleAxis(
			Vector3.Angle(up,legsUp),
			Vector3.Cross(up,legsUp)
		);
		
		for (int leg=0; leg<legs.Length; leg++) {
			// Value from 0.0 at liftoff time to 1.0 at strike time
			float flightTime = Mathf.InverseLerp(
				legStates[leg].liftoffTime, legStates[leg].strikeTime, legStates[leg].cycleTime);
			
			// Value from 0.0 at lift time to 1.0 at land time
			float strideTime = Mathf.InverseLerp(
				legStates[leg].liftTime, legStates[leg].landTime, legStates[leg].cycleTime);
			
			int phase;
			float phaseTime = 0;
			if (legStates[leg].cycleTime < legStates[leg].liftoffTime) {
				phase = 0; phaseTime = Mathf.InverseLerp(
					0, legStates[leg].liftoffTime, legStates[leg].cycleTime
				);
			}
			else if (legStates[leg].cycleTime > legStates[leg].strikeTime) {
				phase = 2; phaseTime = Mathf.InverseLerp(
					legStates[leg].strikeTime, 1, legStates[leg].cycleTime
				);
			}
			else {
				phase = 1; phaseTime = flightTime;
			}
			
			// Calculate foot position on foot flight path from old to new step
			Vector3 flightPos = Vector3.zero;
			for (int m=0; m<legC.motions.Length; m++) {
				IMotionAnalyzer motion = legC.motions[m];
				float weight = motionWeights[m];
				if (weight>0) {
					flightPos += motion.GetFlightFootPosition(leg, phaseTime, phase)*weight;
				}
			}
			
			AssertSane(flightPos, "flightPos");
			
			// Start and end point at step from and step to positions
			Vector3 pointFrom = legStates[leg].stepFromPosition;
			Vector3 pointTo = legStates[leg].stepToPosition;
			Vector3 normalFrom = legStates[leg].stepFromMatrix.MultiplyVector(Vector3.up);
			Vector3 normalTo = legStates[leg].stepToMatrix.MultiplyVector(Vector3.up);
			Assert(Vector3.Dot(normalFrom, legsUp)>0, "normalFrom and legsUp are perpendicular");
			Assert(Vector3.Dot(normalFrom, legsUp)>0, "normalTo and legsUp are perpendicular");
			
			AssertSane(groundBasePoint, "groundBasePoint");
			AssertSane(baseUp, "baseUp");
			
			float flightProgressionLift = Mathf.Sin(flightPos.z*Mathf.PI);
			float flightTimeLift = Mathf.Sin(flightTime*Mathf.PI);
			
			// Calculate horizontal part of flight paths
			legStates[leg].footBase = pointFrom * (1-flightPos.z) + pointTo * flightPos.z;
			
			Vector3 offset =
				tr.position + tr.rotation * legStates[leg].stancePosition
				-Vector3.Lerp(pointFrom,pointTo,legStates[leg].cycleTime);
			
			legStates[leg].footBase += Util.ProjectOntoPlane(offset*flightProgressionLift, legsUp);
			
			//for (int leg=0; leg<legs.Length; leg++) {
			//	AddTrajectoryPoint("leg"+leg+"footbase",legStates[leg].footBase+up*0.01f+tr.rotation*legStates[leg].heelToetipVector*0);
			//}
			
			AssertSane(legStates[leg].footBase, "legStates[leg].footBase");
			
			// Calculate vertical part of flight paths
			Vector3 midPoint = (pointFrom + pointTo) / 2;
			float tangentHeightFrom = (
				Vector3.Dot(normalFrom, pointFrom - midPoint)
				/ Vector3.Dot(normalFrom, legsUp)
			);
			float tangentHeightTo = (
				Vector3.Dot(normalTo, pointTo - midPoint)
				/ Vector3.Dot(normalTo, legsUp)
			);
			float heightMidOffset = Mathf.Max(tangentHeightFrom, tangentHeightTo) * 2 / Mathf.PI;
			AssertSane(heightMidOffset, "heightMidOffset");
			
			legStates[leg].footBase += Mathf.Max(0, heightMidOffset * flightProgressionLift - flightPos.y * scale) * legsUp;
			AssertSane(legStates[leg].footBase, "legStates[leg].footBase");
			
			// Footbase rotation
			
			Quaternion footBaseRotationFromSteps = Quaternion.Slerp(
				Util.QuaternionFromMatrix(legStates[leg].stepFromMatrix),
				Util.QuaternionFromMatrix(legStates[leg].stepToMatrix),
				flightTime
			);
			
			if (strideTime < 0.5) {
				legStates[leg].footBaseRotation = Quaternion.Slerp(
					Util.QuaternionFromMatrix(legStates[leg].stepFromMatrix),
					rotation,
					strideTime*2
				);
			}
			else {
				legStates[leg].footBaseRotation = Quaternion.Slerp(
					rotation,
					Util.QuaternionFromMatrix(legStates[leg].stepToMatrix),
					strideTime*2-1
				);
			}
			
			float footRotationAngle = Quaternion.Angle(rotation, legStates[leg].footBaseRotation);
			if (footRotationAngle > maxFootRotationAngle) {
				legStates[leg].footBaseRotation = Quaternion.Slerp(
					rotation,
					legStates[leg].footBaseRotation,
					maxFootRotationAngle / footRotationAngle
				);
			}
			
			legStates[leg].footBaseRotation = Quaternion.FromToRotation(
				legStates[leg].footBaseRotation * Vector3.up,
				footBaseRotationFromSteps * Vector3.up
			) * legStates[leg].footBaseRotation;
			
			// Elevate feet according to flight pas from keyframed animation
			legStates[leg].footBase += flightPos.y*legsUp*scale;
			AssertSane(legStates[leg].footBase, "legStates[leg].footBase");
			
			// Offset feet sideways according to flight pas from keyframed animation
			Vector3 stepRight = Vector3.Cross(legsUp,pointTo-pointFrom).normalized;
			legStates[leg].footBase += flightPos.x*stepRight*scale;
			
			// Smooth lift that elevates feet in the air based on height of feet on the ground.
			Vector3 footBaseElevated = Vector3.Lerp(
				legStates[leg].footBase,
				Util.SetHeight(legStates[leg].footBase, groundBasePoint, legsUp),
				flightTimeLift
			);
			
			if (Vector3.Dot(footBaseElevated,legsUp) > Vector3.Dot(legStates[leg].footBase,legsUp)) {
				legStates[leg].footBase = footBaseElevated;
			}
			
			UnityEngine.Debug.DrawLine(
				legStates[leg].footBase,
				legStates[leg].footBase+legStates[leg].footBaseRotation*legStates[leg].heelToetipVector,
				legs[leg].debugColor
			);
		}
		
		// Blend locomotion system effect in and out according to its weight
		
		for (int leg=0; leg<legs.Length; leg++) {
			Vector3 footBaseReference = (
				-MotionAnalyzer.GetHeelOffset(
					legs[leg].ankle, legs[leg].ankleHeelVector,
					legs[leg].toe, legs[leg].toeToetipVector,
					legStates[leg].heelToetipVector,
					legStates[leg].footBaseRotation
				)
				+legs[leg].ankle.TransformPoint(legs[leg].ankleHeelVector)
			);
			AssertSane(footBaseReference, "footBaseReference");
			
			if (locomotionWeight<1) {
				legStates[leg].footBase = Vector3.Lerp(
					footBaseReference,
					legStates[leg].footBase,
					locomotionWeight
				);
				legStates[leg].footBaseRotation = Quaternion.Slerp(
					rotation,
					legStates[leg].footBaseRotation,
					locomotionWeight
				);
			}
			
			legStates[leg].footBase = Vector3.MoveTowards(
				footBaseReference,
				legStates[leg].footBase,
				maxIKAdjustmentDistance
			);
		}
		
		// Apply body rotation
		legC.root.transform.rotation = (
			tr.rotation * Quaternion.Inverse(transform.rotation)
			* bodyRotation
			* legC.root.transform.rotation
		);
		for (int leg=0; leg<legs.Length; leg++) {
			legs[leg].hip.rotation = legsRotation * Quaternion.Inverse(bodyRotation) * legs[leg].hip.rotation;
		}
		
		// Apply root offset based on body rotation
		Vector3 rootPoint = legC.root.transform.position;
		Vector3 hipAverage = transform.TransformPoint(legC.hipAverage);
		Vector3 hipAverageGround = transform.TransformPoint(legC.hipAverageGround);
		Vector3 rootPointAdjusted = rootPoint;
		rootPointAdjusted += bodyRotation * (rootPoint-hipAverage) - (rootPoint-hipAverage);
		rootPointAdjusted += legsRotation * (hipAverage-hipAverageGround) - (hipAverage-hipAverageGround);
		legC.root.transform.position = rootPointAdjusted + position-transform.position;
		
		for (int leg=0; leg<legs.Length; leg++) {
			legStates[leg].hipReference = legs[leg].hip.position;
			legStates[leg].ankleReference = legs[leg].ankle.position;
		}
		
		// Adjust legs in two passes
		// First pass is to find approximate place of hips and ankles
		// Second pass is to adjust ankles based on local angles found in first pass
		for (int pass=1; pass<=2; pass++) {
			// Find the ankle position for each leg
			for (int leg=0; leg<legs.Length; leg++) {
				legStates[leg].ankle = MotionAnalyzer.GetAnklePosition(
					legs[leg].ankle, legs[leg].ankleHeelVector,
					legs[leg].toe, legs[leg].toeToetipVector,
					legStates[leg].heelToetipVector,
					legStates[leg].footBase, legStates[leg].footBaseRotation
				);
			}
			
			// Find and apply the hip offset
			FindHipOffset();
			
			// Adjust the legs according to the found ankle and hip positions
			for (int leg=0; leg<legs.Length; leg++) { AdjustLeg(leg, legStates[leg].ankle, pass==2); }
		}
		
		for (int leg=0; leg<legs.Length; leg++) {
			// Draw desired bone alignment lines
			for (int i=0; i<legs[leg].legChain.Length-1; i++) {
				UnityEngine.Debug.DrawLine(
					legs[leg].legChain[i].position,
					legs[leg].legChain[i+1].position,
					legs[leg].debugColor
				);
			}
		}
		
		Vector3 temp = position;
		UnityEngine.Debug.DrawRay(temp,up/10,Color.white);
		UnityEngine.Debug.DrawRay(temp-forward/20,forward/10,Color.white);
		UnityEngine.Debug.DrawLine(hipAverage, hipAverageGround, Color.white);
		
		UnityEngine.Debug.DrawRay(temp,baseUp*2,Color.blue);
		UnityEngine.Debug.DrawRay(hipAverage,bodyUp*2,Color.yellow);
		
		gameObject.layer = origLayer;
	}
	
	public Matrix4x4 FindGroundedBase(
		Vector3 pos, Quaternion rot, Vector3 heelToToetipVector, bool avoidLedges
	) {
		RaycastHit hit;
		
		// Trace rays
		Vector3 hitAPoint = new Vector3();
		Vector3 hitBPoint = new Vector3();
		Vector3 hitANormal = new Vector3();
		Vector3 hitBNormal = new Vector3();
		bool hitA = false;
		bool hitB = false;
		bool valid = false;
		
		if (Physics.Raycast(
			pos + up*maxStepHeight,
			-up, out hit, maxStepHeight*2, groundLayers)
		) {
			valid = true;
			hitAPoint = hit.point;
			// Ignore surface normal if it deviates too much
			if (Vector3.Angle(hit.normal,up) < maxSlopeAngle) {
				hitANormal = hit.normal; hitA = true;
			}
		}
		
		Vector3 heelToToetip = rot * heelToToetipVector;
		float footLength = heelToToetip.magnitude;
		
		if (Physics.Raycast(
			pos + up*maxStepHeight + heelToToetip,
			-up, out hit, maxStepHeight*2, groundLayers)
		) {
			valid = true;
			hitBPoint = hit.point;
			// Ignore surface normal if it deviates too much
			if (Vector3.Angle(hit.normal,up) < maxSlopeAngle) {
				hitBNormal = hit.normal; hitB = true;
			}
		}
		
		if (!valid) {
			Matrix4x4 m = Matrix4x4.identity;
			m.SetTRS(pos,rot,Vector3.one);
			return m;
		}
		
		// Choose which raycast result to use
		bool exclusive = false;
		if (avoidLedges) {
			if (!hitA && !hitB) hitA = true;
			else if (hitA && hitB) {
				Vector3 avgNormal = (hitANormal+hitBNormal).normalized;
				float hA = Vector3.Dot(hitAPoint,avgNormal);
				float hB = Vector3.Dot(hitBPoint,avgNormal);
				if (hA >= hB) hitB = false;
				else hitA = false;
				if (Mathf.Abs(hA-hB) > footLength / 4) exclusive = true;
			}
			else exclusive = true;
		}
		
		Vector3 newStepPosition;
		
		Vector3 stepUp = rot*Vector3.up;
		
		// Apply result of raycast
		if (hitA) {
			if (hitANormal!=Vector3.zero) {
				rot = Quaternion.FromToRotation(stepUp, hitANormal) * rot;
			}
			newStepPosition = hitAPoint;
			if (exclusive) {
				heelToToetip = rot * heelToToetipVector;
				newStepPosition -= heelToToetip * 0.5f;
			}
		}
		else {
			if (hitBNormal!=Vector3.zero) {
				rot = Quaternion.FromToRotation(stepUp, hitBNormal) * rot;
			}
			heelToToetip = rot * heelToToetipVector;
			newStepPosition = hitBPoint - heelToToetip;
			if (exclusive) { newStepPosition += heelToToetip * 0.5f; }
		}
		
		return Util.MatrixFromQuaternionPosition(rot, newStepPosition);
	}
	
	public void FindHipOffset() {
		float lowestDesiredHeight = Mathf.Infinity;
		float lowestMaxHeight = Mathf.Infinity;
		float averageDesiredHeight = 0;
		AssertSane(legsUp, "legsUp");
		for (int leg=0; leg<legs.Length; leg++) {
			float[] intersections;
			
			// Calculate desired distance between original foot base position and original hip position
			Vector3 desiredVector = (legStates[leg].ankleReference-legStates[leg].hipReference);
			float desiredDistance = desiredVector.magnitude;
			float desiredDistanceGround = Util.ProjectOntoPlane(desiredVector, legsUp).magnitude;
			
			// Move closer if too far away
			Vector3 ankleVectorGround = Util.ProjectOntoPlane(
				legStates[leg].ankle - legs[leg].hip.position, legsUp
			);
			float excess = ankleVectorGround.magnitude - desiredDistanceGround;
			if (excess>0) {
				float bufferDistance = (legs[leg].legLength*scale*0.999f)-desiredDistanceGround;
				legStates[leg].ankle = (
					legStates[leg].ankle - ankleVectorGround
					+ ankleVectorGround.normalized
					* (
						desiredDistanceGround
						+ (1-(1/(excess/bufferDistance+1)))*bufferDistance
					)
				);
			}
			
			// Find the desired hip height (relative to the current hip height)
			// such that the original distance between ankle and hip is preserved.
			// (Move line start and sphere center by minus line start to avoid precision errors)
			intersections = Util.GetLineSphereIntersections(
				Vector3.zero, legsUp,
				legStates[leg].ankle - legs[leg].hip.position,
				desiredDistance
			);
			float hipDesiredHeight;
			if (intersections!=null) {
				hipDesiredHeight = intersections[1];
				AssertSane(hipDesiredHeight, "hipDesiredHeight (intersection)");
			}
			else {
				hipDesiredHeight = Vector3.Dot(legStates[leg].footBase - legs[leg].hip.position, legsUp);
				/*UnityEngine.Debug.Log(
					gameObject.name
					+": Line-sphere intersection failed for leg "+leg+", hipDesiredHeight."
				);*/
			}
			
			// Find the maximum hip height (relative to the current hip height) such that the
			// distance between the ankle and hip is no longer than the length of the leg bones combined.
			// (Move line start and sphere center by minus line start to avoid precision errors)
			intersections = Util.GetLineSphereIntersections(
				Vector3.zero, legsUp,
				legStates[leg].ankle - legs[leg].hip.position,
				(legs[leg].legLength*scale*0.999f)
			);
			float hipMaxHeight;
			if (intersections!=null) hipMaxHeight = intersections[1];
			else {
				hipMaxHeight = Vector3.Dot(legStates[leg].ankle - legs[leg].hip.position, legsUp);
				UnityEngine.Debug.Log(
					gameObject.name
					+": Line-sphere intersection failed for leg "+leg+", hipMaxHeight."
				);
			}
			
			// Find the lowest (and average) heights
			if (hipDesiredHeight<lowestDesiredHeight) { lowestDesiredHeight = hipDesiredHeight; }
			if (hipMaxHeight<lowestMaxHeight) { lowestMaxHeight = hipMaxHeight; }
			averageDesiredHeight += hipDesiredHeight/legs.Length;
		}
		
		Assert(lowestDesiredHeight!=Mathf.Infinity, "lowestDesiredHeight is infinite");
		Assert(lowestMaxHeight!=Mathf.Infinity, "lowestMaxHeight is infinite");
		AssertSane(averageDesiredHeight, "averageDesiredHeight");
		AssertSane(lowestDesiredHeight, "lowestDesiredHeight");
		AssertSane(lowestMaxHeight, "lowestMaxHeight");
		
		// Find offset that is in between lowest desired, average desired, and lowest max
		if (lowestDesiredHeight>lowestMaxHeight) lowestDesiredHeight = lowestMaxHeight;
		float minToAvg = averageDesiredHeight-lowestDesiredHeight;
		float minToMax = lowestMaxHeight-lowestDesiredHeight;
		
		float hipHeight = lowestDesiredHeight;
		if (minToAvg+minToMax > 0) { // make sure we don't divide by zero
			hipHeight += minToAvg*minToMax/(minToAvg+minToMax);
		}
		
		// Translate the root by this offset
		AssertSane(hipHeight, "hipHeight");
		legC.root.position += hipHeight*legsUp;
	}
	
	public void AdjustLeg(int leg, Vector3 desiredAnklePosition, bool secondPass) {
		LegInfo legInfo = legs[leg];
		LegState legState = legStates[leg];
		
		// Store original foot alignment
		Quaternion qAnkleOrigRotation;
		if (!secondPass) {
			// Footbase rotation in character space
			Quaternion objectToFootBaseRotation = legStates[leg].footBaseRotation * Quaternion.Inverse(rotation);
			qAnkleOrigRotation = objectToFootBaseRotation * legInfo.ankle.rotation;
		}
		else {
			qAnkleOrigRotation = legInfo.ankle.rotation;
		}
		
		// Choose IK solver
		IKSolver ikSolver;
		if (legInfo.legChain.Length==3) ikSolver = new IK1JointAnalytic();
		else ikSolver = new IKSimple();
		
		// Solve the inverse kinematics
		ikSolver.Solve( legInfo.legChain, desiredAnklePosition );
		
		// Calculate the desired new joint positions
		Vector3 pHip = legInfo.hip.position;
		Vector3 pAnkle = legInfo.ankle.position;
		
		if (!secondPass) {
			// Find alignment that is only rotates in horizontal plane
			// and keeps local ankle angle
			Quaternion horizontalRotation = Quaternion.FromToRotation(
				forward,
				Util.ProjectOntoPlane(legStates[leg].footBaseRotation*Vector3.forward,up)
			) * legInfo.ankle.rotation;
			
			// Apply original foot alignment when foot is grounded
			legInfo.ankle.rotation = Quaternion.Slerp(
				horizontalRotation, // only horizontal rotation (keep local angle)
				qAnkleOrigRotation, // rotates to slope of ground
				1-legState.GetFootGrounding(legState.cycleTime)
			);
		}
		else {
			// Rotate leg around hip-ankle axis by half amount of what the foot is rotated
			Vector3 hipAnkleVector = pAnkle-pHip;
			Quaternion legAxisRotate = Quaternion.Slerp(
				Quaternion.identity,
				Quaternion.FromToRotation(
					Util.ProjectOntoPlane(forward,hipAnkleVector),
					Util.ProjectOntoPlane(legStates[leg].footBaseRotation*Vector3.forward,hipAnkleVector)
				),
				0.5f
			);
			legInfo.hip.rotation = legAxisRotate * legInfo.hip.rotation;
			
			// Apply foot alignment found in first pass
			legInfo.ankle.rotation = qAnkleOrigRotation;
		}
	}
	
	void OnRenderObject() {
		vertexColorMaterial.SetPass( 0 );
		
		if (!isActive) return;
		if (renderFootMarkers) RenderFootMarkers();
		if (renderBlendingGraph) RenderBlendingGraph();
		if (renderCycleGraph) RenderCycleGraph();
		//RenderMotionCycles();
		/*foreach (KeyValuePair<string,TrajectoryVisualizer> kvp in trajectories) {
			kvp.Value.Render();
		}*/
	}
	
	private void DrawLine(Vector3 a, Vector3 b, Color color) {
		GL.Color(color);
		GL.Vertex(a);
		GL.Vertex(b);
	}
	
	public void RenderBlendingGraph() {
		Matrix4x4 matrix = Util.CreateMatrix(
			transform.right,transform.forward,transform.up,
			transform.TransformPoint(legC.hipAverage)
		);
		float size = (Camera.main.transform.position-transform.TransformPoint(legC.hipAverage)).magnitude/2;
		DrawArea graph = new DrawArea3D(new Vector3(-size,-size,0),new Vector3(size,size,0), matrix);
		
		GL.Begin( GL.QUADS );
		graph.DrawRect(new Vector3(0,0,0), new Vector3(1,1,0), new Color(0,0,0,0.2f));
		GL.End();
		
		//Color strongColor = new Color(0, 0, 0, 1);
		Color weakColor = new Color(0.7f, 0.7f, 0.7f, 1);
		
		float range = 0;
		for (int i=0; i<legC.motions.Length; i++) {
			IMotionAnalyzer m = legC.motions[i];
			range = Mathf.Max(range, Mathf.Abs(m.cycleVelocity.x));
			range = Mathf.Max(range, Mathf.Abs(m.cycleVelocity.z));
		}
		if (range==0) range = 1;
		else range *= 1.2f;
		
		GL.Begin( GL.LINES );
			graph.DrawLine (new Vector3(0.5f, 0, 0), new Vector3(0.5f, 1, 0), weakColor);
			graph.DrawLine (new Vector3(0, 0.5f, 0), new Vector3(1, 0.5f, 0), weakColor);
			graph.DrawLine (new Vector3(0, 0, 0), new Vector3(1, 0, 0), weakColor);
			graph.DrawLine (new Vector3(1, 0, 0), new Vector3(1, 1, 0), weakColor);
			graph.DrawLine (new Vector3(1, 1, 0), new Vector3(0, 1, 0), weakColor);
			graph.DrawLine (new Vector3(0, 1, 0), new Vector3(0, 0, 0), weakColor);
		GL.End();
		
		float mX, mY;
		for (int g=0; g<motionGroupStates.Length; g++) {
			Vector3 colorVect = Quaternion.AngleAxis(
				(g+0.5f)*360.0f/motionGroupStates.Length, Vector3.one
			) * Vector3.right;
			Color color = new Color(colorVect.x, colorVect.y, colorVect.z);
			IMotionAnalyzer[] motions = legC.motionGroups[g].motions;
			
			// Draw weights
			GL.Begin( GL.QUADS );
				Color colorTemp = color*0.4f;
				colorTemp.a = 0.8f;
				for (int i=0; i<motions.Length; i++) {
					IMotionAnalyzer m = motions[i];
					mX = (m.cycleVelocity.x) / range/2 +0.5f;
					mY = (m.cycleVelocity.z) / range/2 +0.5f;
					float s = 0.02f;
					graph.DrawDiamond(
						new Vector3(mX-s,mY-s,0),
						new Vector3(mX+s,mY+s,0),
						colorTemp
					);
				}
			GL.End();
			if (motionGroupStates[g].weight==0) continue;
			float[] weights = motionGroupStates[g].relativeWeights;
			GL.Begin( GL.QUADS );
				color.a = motionGroupStates[g].weight;
				for (int i=0; i<motions.Length; i++) {
					IMotionAnalyzer m = motions[i];
					mX = (m.cycleVelocity.x) / range/2 +0.5f;
					mY = (m.cycleVelocity.z) / range/2 +0.5f;
					float s = Mathf.Pow(weights[i],0.5f)*0.05f;
					graph.DrawRect(
						new Vector3(mX-s,mY-s,0),
						new Vector3(mX+s,mY+s,0),
						color
					);
				}
			GL.End();
		}
		GL.Begin( GL.QUADS );
			// Draw marker
			mX = (objectVelocity.x) / range/2 +0.5f;
			mY = (objectVelocity.z) / range/2 +0.5f;
			float t = 0.02f;
			graph.DrawRect(new Vector3(mX-t,mY-t,0), new Vector3(mX+t,mY+t,0), new Color(0,0,0,1));
			t /= 2;
			graph.DrawRect(new Vector3(mX-t,mY-t,0), new Vector3(mX+t,mY+t,0), new Color(1,1,1,1));
		GL.End();
	}
	
	public void RenderCycleGraph() {
		float w = Camera.main.pixelWidth;
		float h = Camera.main.pixelHeight;
		//Color strongColor = new Color(0, 0, 0, 1);
		Color weakColor = new Color(0.7f, 0.7f, 0.7f, 1);
		DrawArea graph = new DrawArea(new Vector3(w-0.49f*h,0.01f*h,0),new Vector3(w-0.01f*h,0.49f*h,0));
		graph.canvasMin = new Vector3(-1.1f,-1.1f,0);
		graph.canvasMax = new Vector3( 1.1f, 1.1f,0);
		
		GL.Begin( GL.QUADS );
		graph.DrawRect(new Vector3(-1,-1,0), new Vector3(1,1,0), new Color(0,0,0,0.2f));
		GL.End();
		
		GL.Begin( GL.LINES );
			graph.DrawLine (-0.9f*Vector3.up, -1.1f*Vector3.up, weakColor);
			for (int i=0; i<90; i++) {
				graph.DrawLine (
					Quaternion.AngleAxis( i   /90.0f*360, Vector3.forward) * Vector3.up,
					Quaternion.AngleAxis((i+1)/90.0f*360, Vector3.forward) * Vector3.up, weakColor);
			}
			for (int leg=0; leg<legs.Length; leg++) {
				Color c = legs[leg].debugColor;
				Vector3 marker;
				marker = Quaternion.AngleAxis(
					360.0f*legStates[leg].liftoffTime, -Vector3.forward
				) * -Vector3.up;
				graph.DrawLine(marker*0.9f, marker*1.1f, c);
				marker = Quaternion.AngleAxis(
					360.0f*legStates[leg].strikeTime, -Vector3.forward
				) * -Vector3.up;
				graph.DrawLine(marker*0.9f, marker*1.1f, c);
			}
		GL.End();
		GL.Begin( GL.QUADS );
			for (int leg=0; leg<legs.Length; leg++) {
				Color c = legs[leg].debugColor;
				Vector3 cycleVect;
				cycleVect = Quaternion.AngleAxis(
					360.0f*legStates[leg].cycleTime, -Vector3.forward
				) * -Vector3.up;
				graph.DrawRect(cycleVect-Vector3.one*0.1f, cycleVect+Vector3.one*0.1f, c);
				float dictatedCycle = Util.CyclicDiff(normalizedTime, legStates[leg].stanceTime);
				cycleVect = Quaternion.AngleAxis(360.0f*dictatedCycle, -Vector3.forward) * -Vector3.up * 0.8f;
				graph.DrawRect(cycleVect-Vector3.one*0.05f, cycleVect+Vector3.one*0.05f, c);
			}
		GL.End();
	}
	
	public void RenderMotionCycles() {
		//float w = Camera.main.pixelWidth;
		float h = Camera.main.pixelHeight;
		//Color strongColor = new Color(0, 0, 0, 1);
		Color weakColor = new Color(0.7f, 0.7f, 0.7f, 1);
		
		for (int m=0; m<legC.cycleMotions.Length; m++) {
			DrawArea graph = new DrawArea(
				new Vector3(0.01f*h+0.2f*h*m,0.31f*h,0),
				new Vector3(0.19f*h+0.2f*h*m,0.49f*h,0)
			);
			graph.canvasMin = new Vector3(-1.1f,-1.1f,0);
			graph.canvasMax = new Vector3( 1.1f, 1.1f,0);
			
			GL.Begin( GL.LINES );
				graph.DrawLine (-0.9f*Vector3.up, -1.1f*Vector3.up, weakColor);
				for (int i=0; i<90; i++) {
					graph.DrawLine (
						Quaternion.AngleAxis( i   /90.0f*360, Vector3.forward) * Vector3.up,
						Quaternion.AngleAxis((i+1)/90.0f*360, Vector3.forward) * Vector3.up, weakColor);
				}
			GL.End();
			GL.Begin( GL.QUADS );
				for (int leg=0; leg<legs.Length; leg++) {
					Color c = legs[leg].debugColor;
					Vector3 cycleVect;
					float t = (
						legC.cycleMotions[m].cycles[leg].stanceTime
						- legC.cycleMotions[m].cycleOffset*(0.5f+0.5f*Mathf.Sin(Time.time*2))
					);
					cycleVect = Quaternion.AngleAxis(360.0f*t, -Vector3.forward) * -Vector3.up;
					graph.DrawRect(cycleVect-Vector3.one*0.1f, cycleVect+Vector3.one*0.1f, c);
				}
			GL.End();
		}
	}
	
	public void RenderFootMarkers() {
		GL.Begin( GL.LINES );
			
		GL.End();
		GL.Begin( GL.QUADS );
			Vector3 heel, forward, up, right;
			Matrix4x4 m;
			for (int leg=0; leg<legs.Length; leg++) {
				for (int step=0; step<3; step++) {
					if (legStates[leg]==null) continue;
					if (step==0) {
						m = legStates[leg].stepFromMatrix;
						GL.Color(legs[leg].debugColor * 0.8f);
					}
					else if (step==1) {
						m = legStates[leg].stepToMatrix;
						GL.Color(legs[leg].debugColor);
					}
					else {
						m = legStates[leg].stepToMatrix;
						GL.Color(legs[leg].debugColor * 0.4f);
					}
					
					// Draw foot marker
					heel = m.MultiplyPoint3x4(Vector3.zero);
					forward = m.MultiplyVector(legStates[leg].heelToetipVector);
					up = m.MultiplyVector(Vector3.up);
					right = (Quaternion.AngleAxis(90,up) * forward).normalized * legs[leg].footWidth * scale;
					heel += up.normalized * right.magnitude/20;
					if (step==2) { heel += legStates[leg].stepToPositionGoal-legStates[leg].stepToPosition; }
					GL.Vertex(heel+right/2);
					GL.Vertex(heel-right/2);
					GL.Vertex(heel-right/4+forward);
					GL.Vertex(heel+right/4+forward);
				}
			}
		GL.End();
	}
	
	void OnGUI () {
		if (renderAnimationStates) RenderAnimationStates();
	}
	
	public void RenderAnimationStates() {
		int i = 0;
		foreach (AnimationState state in GetComponent<Animation>()) {
			string str = state.name;
			float v = 0.5f+0.5f*state.weight;
			GUI.color = new Color(0,0,v,1);
			if (state.enabled) {
				GUI.color = new Color(v,v,v,1);
			}
			str += " "+state.weight.ToString("0.000");
			GUI.Label (new Rect (Screen.width-200, 10+20*i, 200, 30), str);
			i++;
		}
	}
	
	private void MonitorFootsteps () {
		for (int legNr=0; legNr<legStates.Length; legNr++) {
			LegState legState = legStates[legNr];
			switch (legState.phase) {
			case LegCyclePhase.Stance:
				if (legState.cycleTime >= legState.liftTime && legState.cycleTime < legState.landTime) {
					legState.phase = LegCyclePhase.Lift;
					SendMessage("OnFootLift", SendMessageOptions.DontRequireReceiver);
				}
				break;
			case LegCyclePhase.Lift:
				if (legState.cycleTime >= legState.liftoffTime || legState.cycleTime < legState.liftTime) {
					legState.phase = LegCyclePhase.Flight;
					SendMessage("OnFootLiftoff", SendMessageOptions.DontRequireReceiver);
				}
				break;
			case LegCyclePhase.Flight:
				if (legState.cycleTime >= legState.strikeTime || legState.cycleTime < legState.liftoffTime) {
					legState.phase = LegCyclePhase.Land;
					SendMessage("OnFootStrike", SendMessageOptions.DontRequireReceiver);
				}
				break;
			case LegCyclePhase.Land:
				if (legState.cycleTime >= legState.landTime || legState.cycleTime < legState.strikeTime) {
					legState.phase = LegCyclePhase.Stance;
					SendMessage("OnFootLand", SendMessageOptions.DontRequireReceiver);
				}
				break;
			}
		}
	}
	
}
