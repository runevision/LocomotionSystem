/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

[System.Serializable]
public class LegCycleSample {
	public Matrix4x4 ankleMatrix;
	public Vector3 heel;
	public Matrix4x4 toeMatrix;
	public Vector3 toetip;
	public Vector3 middle;
	public float balance;
	public Vector3 footBase;
	public Vector3 footBaseNormalized;
}

[System.Serializable]
public class TimeStampInfo {
	public string label;
	public float time;
}

[System.Serializable]
public class CycleDebugInfo {
	public float toeLiftTime;
	public float toeLandTime;
	public float ankleLiftTime;
	public float ankleLandTime;
	public float footLiftTime;
	public float footLandTime;
}

[System.Serializable]
public class LegCycleData {
	public Vector3 cycleCenter;
	public float cycleScaling;
	public Vector3 cycleDirection;
	public float stanceTime;
	public float liftTime;
	public float liftoffTime;
	public float postliftTime;
	public float prelandTime;
	public float strikeTime;
	public float landTime;
	public float cycleDistance;
	public Vector3 stancePosition;
	public Vector3 heelToetipVector;
	public LegCycleSample[] samples;
	public int stanceIndex;
	public CycleDebugInfo debugInfo;
}

[System.Serializable]
public enum MotionType {
	WalkCycle,
	Grounded,
}

[System.Serializable]
public class MotionAnalyzer : IMotionAnalyzer {
	
	public bool alsoUseBackwards = false;
	
	public bool fixFootSkating = false;
	
	[HideInInspector] public LegCycleData[] m_cycles;
	public override LegCycleData[] cycles { get { return m_cycles; } }
	
	[HideInInspector] public int m_samples;
	public override int samples { get { return m_samples; } }
	
	[HideInInspector] public Vector3 m_cycleDirection;
	public override Vector3 cycleDirection { get { return m_cycleDirection; } }
	
	[HideInInspector] public float m_cycleDistance;
	public override float cycleDistance { get { return m_cycleDistance; } }
	
	public override Vector3 cycleVector { get { return m_cycleDirection * m_cycleDistance; } }
	
	[HideInInspector] public float m_cycleDuration;
	public override float cycleDuration { get { return m_cycleDuration; } }
	
	[HideInInspector] public float m_cycleSpeed;
	public override float cycleSpeed { get { return m_cycleSpeed; } }
	public float nativeSpeed;
	
	public override Vector3 cycleVelocity { get { return m_cycleDirection * m_cycleSpeed; } }
	
	[HideInInspector] public float m_cycleOffset;
	public override float cycleOffset { get { return m_cycleOffset; } set { m_cycleOffset = value; } }
	
	[HideInInspector] public Vector3 graphMin;
	[HideInInspector] public Vector3 graphMax;
	
	// Convenience variables - do not show in inspector, even in debug mode
	[HideInInspector] public GameObject gameObject;
	[HideInInspector] public int legs;
	[HideInInspector] public LegController legC;
	
	private float FindContactTime(LegCycleData data, bool useToe, int searchDirection, float yRange, float threshold) {
		// Find the contact time on the height curve, where the (projected ankle or toe)
		// hits or leaves the ground (depending on search direction in time).
		int spread = 5; // FIXME magic number for spread value
		float curvatureMax = 0;
		int curvatureMaxIndex = data.stanceIndex;
		for (int i=0; i<samples && i>-samples; i+=searchDirection) {
			// Find second derived by sampling three positions on curve.
			// Spred samples a bit to ignore high frequency noise.
			// FIXME magic number for spread value
			int[] j = new int[3];
			float[] value = new float[3];
			for (int s=0; s<3; s++) {
				j[s] = Util.Mod(i+data.stanceIndex-spread+spread*s,samples);
				if (useToe) value[s] = data.samples[j[s]].toetip.y;
				else		value[s] = data.samples[j[s]].heel.y;
			}
			//float curvatureCurrent = value[0]+value[2]-2*value[1];
			float curvatureCurrent = Mathf.Atan((value[2]-value[1])*10/yRange)-Mathf.Atan((value[1]-value[0])*10/yRange);
			if (
				// Sample must be above the ground
				(value[1]>legC.groundPlaneHeight)
				// Sample must have highest curvature
				&& (curvatureCurrent>curvatureMax)
				// Slope at sample must go upwards (when going in search direction)
				&& (Mathf.Sign(value[2]-value[0])==Mathf.Sign(searchDirection))
			) {
				curvatureMax = curvatureCurrent;
				curvatureMaxIndex = j[1];
			}
			// Terminate search when foot height is above threshold height above ground
			if (value[1]>legC.groundPlaneHeight+yRange*threshold) {
				break;
			}
		}
		return GetTimeFromIndex(Util.Mod(curvatureMaxIndex-data.stanceIndex,samples));
	}
	
	private float FindSwingChangeTime(LegCycleData data, int searchDirection, float threshold) {
		// Find the contact time on the height curve, where the (projected ankle or toe)
		// hits or leaves the ground (depending on search direction in time).
		int spread = samples/5; // FIXME magic number for spread value
		float stanceSpeed = 0;
		for (int i=0; i<samples && i>-samples; i+=searchDirection) {
			// Find speed by sampling curve value ahead and behind.
			int[] j = new int[3];
			float[] value = new float[3];
			for (int s=0; s<3; s++) {
				j[s] = Util.Mod(i+data.stanceIndex-spread+spread*s,samples);
				value[s] = Vector3.Dot(data.samples[j[s]].footBase, data.cycleDirection);
			}
			float currentSpeed = value[2]-value[0];
			if (i==0) stanceSpeed = currentSpeed;
			// If speed is too different from speed at stance time,
			// the current time is determined as the swing change time
			if (Mathf.Abs((currentSpeed-stanceSpeed)/stanceSpeed)>threshold) {
				return GetTimeFromIndex(Util.Mod(j[1]-data.stanceIndex,samples));
			}
		}
		return Util.Mod(searchDirection*-0.01f);
	}
	
	private float GetFootGrounding(int leg, float time) {
		if ( (time<=cycles[leg].liftTime) || (time>=cycles[leg].landTime)	) return 0;
		if ( (time>=cycles[leg].postliftTime) && (time<=cycles[leg].prelandTime) ) return 1;
		float val;
		if ( time<cycles[leg].postliftTime ) {
			val = (time-cycles[leg].liftTime)/(cycles[leg].postliftTime-cycles[leg].liftTime);
		}
		else {
			val = 1-(time-cycles[leg].prelandTime)/(cycles[leg].landTime-cycles[leg].prelandTime);
		}
		return val;
	}
	
	private float GetFootGroundingOrig(int leg, float time) {
		if ( (time<=cycles[leg].liftTime) || (time>=cycles[leg].landTime)	) return 0;
		if ( (time>=cycles[leg].liftoffTime) && (time<=cycles[leg].strikeTime) ) return 1;
		float val;
		if ( time<cycles[leg].liftoffTime ) {
			val = (time-cycles[leg].liftTime)/(cycles[leg].liftoffTime-cycles[leg].liftTime);
		}
		else {
			val = 1-(time-cycles[leg].strikeTime)/(cycles[leg].landTime-cycles[leg].strikeTime);
		}
		return val;
	}
	
	private int GetIndexFromTime(float time) {
		return Util.Mod((int)(time*samples+0.5),samples);
	}
	
	private float GetTimeFromIndex(int index) {
		return index*1.0f/samples;
	}
	
	private delegate Vector3 GetVector3Member(LegCycleSample s);
	private delegate float GetFloatMember(LegCycleSample s);
	private Vector3 FootPositionNormalized(LegCycleSample s) { return s.footBaseNormalized; }
	private Vector3 FootPosition(LegCycleSample s) { return s.footBase; }
	private float Balance(LegCycleSample s) { return s.balance; }
	
	private Vector3 GetVector3AtTime(int leg, float flightTime, GetVector3Member get) {
		flightTime = Mathf.Clamp01(flightTime);
		int index = (int)(flightTime*samples);
		float weight = flightTime*samples-index;
		if (index>=samples-1) {
			index = samples-1;
			weight = 0;
		}
		index = Util.Mod(index+cycles[leg].stanceIndex,samples);
		return (
			get(cycles[leg].samples[index])*(1-weight)
			+get(cycles[leg].samples[Util.Mod(index+1,samples)])*(weight)
		);
	}
	private float GetFloatAtTime(int leg, float flightTime, GetFloatMember get) {
		flightTime = Mathf.Clamp01(flightTime);
		int index = (int)(flightTime*samples);
		float weight = flightTime*samples-index;
		if (index>=samples-1) {
			index = samples-1;
			weight = 0;
		}
		index = Util.Mod(index+cycles[leg].stanceIndex,samples);
		return (
			get(cycles[leg].samples[index])*(1-weight)
			+get(cycles[leg].samples[Util.Mod(index+1,samples)])*(weight)
		);
	}
		
	public override Vector3 GetFlightFootPosition(int leg, float flightTime, int phase) {
		if (motionType != MotionType.WalkCycle) {
			if (phase==0) return Vector3.zero;
			if (phase==1) return (-Mathf.Cos(flightTime * Mathf.PI)/2 + 0.5f) * Vector3.forward;
			if (phase==2) return Vector3.forward;
		}
		
		float cycleTime = 0;
		if      (phase==0) cycleTime = Mathf.Lerp(0,                       cycles[leg].liftoffTime, flightTime);
		else if (phase==1) cycleTime = Mathf.Lerp(cycles[leg].liftoffTime, cycles[leg].strikeTime,  flightTime);
		else               cycleTime = Mathf.Lerp(cycles[leg].strikeTime,  1,                       flightTime);
		//return GetVector3AtTime(leg,cycleTime,FootPositionNormalized);
		//flightTime = Mathf.Clamp01(flightTime);
		int index = (int)(cycleTime*samples);
		float weight = cycleTime*samples-index;
		if (index>=samples-1) {
			index = samples-1;
			weight = 0;
		}
		index = Util.Mod(index+cycles[leg].stanceIndex,samples);
		return (
			cycles[leg].samples[index].footBaseNormalized * (1-weight)
			+ cycles[leg].samples[Util.Mod(index+1,samples)].footBaseNormalized * (weight)
		);
	}
	
	public static Vector3 GetHeelOffset(
		Transform ankleT, Vector3 ankleHeelVector,
		Transform toeT, Vector3 toeToetipVector,
		Vector3 stanceFootVector,
		Quaternion footBaseRotation
	) {
		// Given the ankle and toe transforms,
		// the heel and toetip positions are calculated.
		Vector3 heel = ankleT.localToWorldMatrix.MultiplyPoint(ankleHeelVector);
		Vector3 toetip = toeT.localToWorldMatrix.MultiplyPoint(toeToetipVector);
		
		// From this the balance is calculated,
		// relative to the current orientation of the foot base.
		float balance = MotionAnalyzer.GetFootBalance(
			(Quaternion.Inverse(footBaseRotation) * heel).y,
			(Quaternion.Inverse(footBaseRotation) * toetip).y,
			stanceFootVector.magnitude
		);
		
		// From the balance, the heel offset can be calculated.
		Vector3 heelOffset = balance*((footBaseRotation*stanceFootVector)+(heel-toetip));
		
		return heelOffset;
	}
	
	public static Vector3 GetAnklePosition(
		Transform ankleT, Vector3 ankleHeelVector,
		Transform toeT, Vector3 toeToetipVector,
		Vector3 stanceFootVector,
		Vector3 footBasePosition, Quaternion footBaseRotation
	) {
		// Get the heel offset
		Vector3 heelOffset = GetHeelOffset(
			ankleT, ankleHeelVector, toeT, toeToetipVector,
			stanceFootVector, footBaseRotation
		);
		
		// Then calculate the ankle position.
		Vector3 anklePosition = (
			footBasePosition
			+ heelOffset
			+ ankleT.localToWorldMatrix.MultiplyVector(ankleHeelVector*-1)
		);
		
		return anklePosition;
	}
	
	public static float GetFootBalance(float heelElevation, float toeElevation, float footLength) {
		// For any moment in time we want to know if the heel or toe is closer to the ground.
		// Rather than a binary value, we need a smooth curve with 0 = heel is closer and 1 = toe is closer.
		// We use the inverse tangens for this as it maps arbritarily large positive or negative values into a -1 to 1 range.
		return Mathf.Atan((
			// Difference in height between heel and toe.
			heelElevation - toeElevation
		)/footLength*20)/Mathf.PI+0.5f;
		// The 20 multiplier is found by trial and error. A rapid but still slightly smooth change of weight is wanted.
	}
	
	private void FindCycleAxis(int leg) {
		// Find axis that feet are moving back and forth along
		// (i.e. Z for characters facing Z, that are walking forward, but could be any direction)
		// FIXME
		// First find the average point of all the points in the foot motion curve
		// (projeted onto the ground plane). This gives us a center.
		cycles[leg].cycleCenter = Vector3.zero;
		for (int i=0; i<samples; i++) {
			LegCycleSample s = cycles[leg].samples[i];
			// FIXME: Assumes horizontal ground plane
			cycles[leg].cycleCenter += Util.ProjectOntoPlane(s.middle, Vector3.up);
		}
		cycles[leg].cycleCenter /= samples;
		
		float maxlength;
		// Then find the point furthest away from this center point
		Vector3 footCurvePointA = cycles[leg].cycleCenter;
		maxlength = 0.0f;
		for (int i=0; i<samples; i++) {
			LegCycleSample s = cycles[leg].samples[i];
			// TODO: Assumes horizontal ground plane
			Vector3 curvePoint = Util.ProjectOntoPlane(s.middle, Vector3.up);
			float curLength = (curvePoint - cycles[leg].cycleCenter).magnitude;
			if (curLength > maxlength) {
				footCurvePointA = curvePoint;
				maxlength = curLength;
			}
		}
		
		// Lastly find the point furthest away from the point we found before
		Vector3 footCurvePointB = footCurvePointA;
		maxlength = 0.0f;
		for (int i=0; i<samples; i++) {
			LegCycleSample s = cycles[leg].samples[i];
			// TODO: Assumes horizontal ground plane
			Vector3 curvePoint = Util.ProjectOntoPlane(s.middle, Vector3.up);
			float curLength = (curvePoint - footCurvePointA).magnitude;
			if (curLength > maxlength) {
				footCurvePointB = curvePoint;
				maxlength = curLength;
			}
		}
		
		cycles[leg].cycleDirection = (footCurvePointB - footCurvePointA).normalized;
		cycles[leg].cycleScaling = (footCurvePointB - footCurvePointA).magnitude;
	}
	
	public override void Analyze(GameObject o) {
		Debug.Log("Starting analysis");
		gameObject = o;
		name = animation.name;
		m_samples = 50;
		
		// Initialize legs and cycle data
		legC = gameObject.GetComponent(typeof(LegController)) as LegController;
		legs = legC.legs.Length;
		m_cycles = new LegCycleData[legs];
		for (int leg=0; leg<legs; leg++) {
			cycles[leg] = new LegCycleData();
			cycles[leg].samples = new LegCycleSample[samples+1];
			for (int i=0; i<samples+1; i++) {
				cycles[leg].samples[i] = new LegCycleSample();
			}
			cycles[leg].debugInfo = new CycleDebugInfo();
		}
		
		graphMin = new Vector3(0, 1000, 1000);
		graphMax = new Vector3(0,-1000,-1000);
		
		for (int leg=0; leg<legs; leg++) {
			// Sample ankle, heel, toe, and toetip positions over the length of the animation.
			Transform ankleT = legC.legs[leg].ankle;
			Transform toeT = legC.legs[leg].toe;
			
			float rangeMax = 0;
			float ankleMin; float ankleMax; float toeMin; float toeMax;
			ankleMin = 1000;
			ankleMax = -1000;
			toeMin = 1000;
			toeMax = -1000;
			for (int i=0; i<samples+1; i++) {
				LegCycleSample s = cycles[leg].samples[i];
				animation.SampleAnimation(gameObject,i*1.0f/samples*animation.length);
				s.ankleMatrix = Util.RelativeMatrix(ankleT,gameObject.transform);
				s.toeMatrix = Util.RelativeMatrix(toeT,gameObject.transform);
				s.heel = s.ankleMatrix.MultiplyPoint(legC.legs[leg].ankleHeelVector);
				s.toetip = s.toeMatrix.MultiplyPoint(legC.legs[leg].toeToetipVector);
				s.middle = (s.heel+s.toetip)/2;
				
				// For each sample in time we want to know if the heel or toetip is closer to the ground.
				// We need a smooth curve with 0 = ankle is closer and 1 = toe is closer.
				s.balance = MotionAnalyzer.GetFootBalance(s.heel.y, s.toetip.y, legC.legs[leg].footLength);
				
				// Find the minimum and maximum extends on all axes of the ankle and toe positions.
				ankleMin = Mathf.Min(ankleMin,s.heel.y);
				toeMin = Mathf.Min(toeMin,s.toetip.y);
				ankleMax = Mathf.Max(ankleMax,s.heel.y);
				toeMax = Mathf.Max(toeMax,s.toetip.y);
			}
			rangeMax = Mathf.Max(ankleMax-ankleMin,toeMax-toeMin);
			
			// Determine motion type
			/*if (motionType==MotionType.AutoDetect) {
				motionType = MotionType.WalkCycle;
			}*/
			
			if (motionType==MotionType.WalkCycle) {
				FindCycleAxis(leg);
				
				// Foot stance time
				// Find the time when the foot stands most firmly on the ground.
				float stanceValue = Mathf.Infinity;
				for (int i=0; i<samples+1; i++) {
					LegCycleSample s = cycles[leg].samples[i];
					
					float sampleValue =
					// We want the point in time when the max of the heel height and the toe height is lowest
					Mathf.Max(s.heel.y, s.toetip.y)/rangeMax
					// Add some bias to poses where the leg is in the middle of the swing
					// i.e. the foot position is close to the middle of the foot curve
					+Mathf.Abs(
						Util.ProjectOntoPlane(s.middle-cycles[leg].cycleCenter, Vector3.up).magnitude
					)/cycles[leg].cycleScaling;
					
					// Use the new value if it is lower (better).
					if (sampleValue<stanceValue) {
						cycles[leg].stanceIndex = i;
						stanceValue = sampleValue;
					}
				}
			}
			else {
				cycles[leg].cycleDirection = Vector3.forward;
				cycles[leg].cycleScaling = 0;
				cycles[leg].stanceIndex = 0;
			}
			// The stance time
			cycles[leg].stanceTime = GetTimeFromIndex(cycles[leg].stanceIndex);
			
			// The stance index sample
			LegCycleSample ss = cycles[leg].samples[cycles[leg].stanceIndex];
			// Sample the animation at stance time
			animation.SampleAnimation(gameObject,cycles[leg].stanceTime*animation.length);
			
			// Using the stance sample as reference we can now determine:
			
			// The vector from heel to toetip at the stance pose 
			cycles[leg].heelToetipVector = (
				ss.toeMatrix.MultiplyPoint(legC.legs[leg].toeToetipVector)
				- ss.ankleMatrix.MultiplyPoint(legC.legs[leg].ankleHeelVector)
			);
			cycles[leg].heelToetipVector = Util.ProjectOntoPlane(cycles[leg].heelToetipVector, Vector3.up);
			cycles[leg].heelToetipVector = cycles[leg].heelToetipVector.normalized * legC.legs[leg].footLength;
			
			// Calculate foot flight path based on weighted average between ankle flight path and toe flight path,
			// using foot balance as weight.
			// The distance between ankle and toe is accounted for, using the stance pose for reference.
			for (int i=0; i<samples+1; i++) {
				LegCycleSample s = cycles[leg].samples[i];
				s.footBase = (
					(s.heel)*(1-s.balance)
					+(s.toetip-cycles[leg].heelToetipVector)*(s.balance)
				);
			}
			
			// The position of the footbase in the stance pose
			cycles[leg].stancePosition = ss.footBase;
			cycles[leg].stancePosition.y = legC.groundPlaneHeight;
			
			if (motionType==MotionType.WalkCycle) {
				// Find contact times:
				// Strike time: foot first touches the ground (0% grounding)
				// Down time: all of the foot touches the ground (100% grounding)
				// Lift time: all of the foot still touches the ground but begins to lift (100% grounding)
				// Liftoff time: last part of the foot leaves the ground (0% grounding)
				float timeA;
				float timeB;
				
				// Find upwards contact times for projected ankle and toe
				// Use the first occurance as lift time and the second as liftoff time
				timeA = FindContactTime(cycles[leg], false, +1, rangeMax, 0.1f);
				cycles[leg].debugInfo.ankleLiftTime = timeA;
				timeB = FindContactTime(cycles[leg], true,  +1, rangeMax, 0.1f);
				cycles[leg].debugInfo.toeLiftTime = timeB;
				if (timeA<timeB) {
					cycles[leg].liftTime = timeA;
					cycles[leg].liftoffTime = timeB;
				}
				else {
					cycles[leg].liftTime = timeB;
					cycles[leg].liftoffTime = timeA;
				}
				
				// Find time where swing direction and speed changes significantly.
				// If this happens sooner than the found liftoff time,
				// then the liftoff time must be overwritten with this value.
				timeA = FindSwingChangeTime(cycles[leg], +1, 0.5f);
				cycles[leg].debugInfo.footLiftTime = timeA;
				if (cycles[leg].liftoffTime > timeA) {
					cycles[leg].liftoffTime = timeA;
					if (cycles[leg].liftTime > cycles[leg].liftoffTime) {
						cycles[leg].liftTime = cycles[leg].liftoffTime;
					}
				}
				
				// Find downwards contact times for projected ankle and toe
				// Use the first occurance as strike time and the second as down time
				timeA = FindContactTime(cycles[leg], false, -1, rangeMax, 0.1f);
				timeB = FindContactTime(cycles[leg], true,  -1, rangeMax, 0.1f);
				if (timeA<timeB) {
					cycles[leg].strikeTime = timeA;
					cycles[leg].landTime = timeB;
				}
				else {
					cycles[leg].strikeTime = timeB;
					cycles[leg].landTime = timeA;
				}
				
				// Find time where swing direction and speed changes significantly.
				// If this happens later than the found strike time,
				// then the strike time must be overwritten with this value.
				timeA = FindSwingChangeTime(cycles[leg], -1, 0.5f);
				cycles[leg].debugInfo.footLandTime = timeA;
				if (cycles[leg].strikeTime < timeA) {
					cycles[leg].strikeTime = timeA;
					if (cycles[leg].landTime < cycles[leg].strikeTime) {
						cycles[leg].landTime = cycles[leg].strikeTime;
					}
				}
				
				// Set postliftTime and prelandTime
				float softening = 0.2f;
				
				cycles[leg].postliftTime = cycles[leg].liftoffTime;
				if (cycles[leg].postliftTime < cycles[leg].liftTime+softening) {
					cycles[leg].postliftTime = cycles[leg].liftTime+softening;
				}
				
				cycles[leg].prelandTime = cycles[leg].strikeTime;
				if (cycles[leg].prelandTime > cycles[leg].landTime-softening) {
					cycles[leg].prelandTime = cycles[leg].landTime-softening;
				}
				
				// Calculate the distance traveled during one cycle (for this foot).
				Vector3 stanceSlideVector = (
					cycles[leg].samples[GetIndexFromTime(Util.Mod(cycles[leg].liftoffTime+cycles[leg].stanceTime))].footBase
					-cycles[leg].samples[GetIndexFromTime(Util.Mod(cycles[leg].strikeTime+cycles[leg].stanceTime))].footBase
				);
				// FIXME: Assumes horizontal ground plane
				stanceSlideVector.y = 0;
				cycles[leg].cycleDistance = stanceSlideVector.magnitude/(cycles[leg].liftoffTime-cycles[leg].strikeTime+1);
				cycles[leg].cycleDirection = -(stanceSlideVector.normalized);
			}
			else {
				cycles[leg].cycleDirection = Vector3.zero;
				cycles[leg].cycleDistance = 0;
			}
			
			graphMax.y = Mathf.Max(graphMax.y,Mathf.Max(ankleMax,toeMax));
		}
		
		// Find the overall speed and direction traveled during one cycle,
		// based on average of speed values for each individual foot.
		// (They should be very close, but animations are often imperfect,
		// leading to different speeds for different legs.)
		m_cycleDistance = 0;
		m_cycleDirection = Vector3.zero;
		for (int leg=0; leg<legs; leg++) {
			m_cycleDistance += cycles[leg].cycleDistance;
			m_cycleDirection += cycles[leg].cycleDirection;
			Debug.Log("Cycle direction of leg "+leg+" is "+cycles[leg].cycleDirection+" with step distance "+cycles[leg].cycleDistance);
		}
		m_cycleDistance /= legs;
		m_cycleDirection /= legs;
		m_cycleDuration = animation.length;
		m_cycleSpeed = cycleDistance/cycleDuration;
		Debug.Log("Overall cycle direction is "+m_cycleDirection+" with step distance "+m_cycleDistance+" and speed "+m_cycleSpeed);
		nativeSpeed = m_cycleSpeed * gameObject.transform.localScale.x;
		
		// Calculate normalized foot flight path
		for (int leg=0; leg<legs; leg++) {
			if (motionType==MotionType.WalkCycle) {
				for (int j=0; j<samples; j++) {
					int i = Util.Mod(j+cycles[leg].stanceIndex,samples);
					LegCycleSample s = cycles[leg].samples[i];
					float time = GetTimeFromIndex(j);
					s.footBaseNormalized = s.footBase;
					
					if (fixFootSkating) {
						// Calculate normalized foot flight path
						// based on the calculated cycle distance of each individual foot
						Vector3 reference = (
							-cycles[leg].cycleDistance * cycles[leg].cycleDirection * (time-cycles[leg].liftoffTime)
							+cycles[leg].samples[
								GetIndexFromTime(cycles[leg].liftoffTime+cycles[leg].stanceTime)
							].footBase
						);
						
						s.footBaseNormalized = (s.footBaseNormalized-reference);
						if (cycles[leg].cycleDirection!=Vector3.zero) {
							s.footBaseNormalized = Quaternion.Inverse(
								Quaternion.LookRotation(cycles[leg].cycleDirection)
							) * s.footBaseNormalized;
						}
						
						s.footBaseNormalized.z /= cycles[leg].cycleDistance;
						if (time<=cycles[leg].liftoffTime) { s.footBaseNormalized.z = 0; }
						if (time>=cycles[leg].strikeTime) { s.footBaseNormalized.z = 1; }
						
						s.footBaseNormalized.y = s.footBase.y - legC.groundPlaneHeight;
					}
					else {
						// Calculate normalized foot flight path
						// based on the cycle distance of the whole motion
						// (the calculated average cycle distance)
						Vector3 reference = (
							-m_cycleDistance * m_cycleDirection * (time-cycles[leg].liftoffTime*0)
							// FIXME: Is same as stance position:
							+cycles[leg].samples[
								GetIndexFromTime(cycles[leg].liftoffTime*0+cycles[leg].stanceTime)
							].footBase
						);
						
						s.footBaseNormalized = (s.footBaseNormalized-reference);
						if (cycles[leg].cycleDirection!=Vector3.zero) {
							s.footBaseNormalized = Quaternion.Inverse(
								Quaternion.LookRotation(m_cycleDirection)
							) * s.footBaseNormalized;
						}
						
						s.footBaseNormalized.z /= m_cycleDistance;
						
						s.footBaseNormalized.y = s.footBase.y - legC.groundPlaneHeight;
					}
				}
				//cycles[leg].samples[cycles[leg].stanceIndex].footBaseNormalized.z = 0;
				cycles[leg].samples[samples] = cycles[leg].samples[0];
			}
			else {
				for (int j=0; j<samples; j++) {
					int i = Util.Mod(j+cycles[leg].stanceIndex,samples);
					LegCycleSample s = cycles[leg].samples[i];
					s.footBaseNormalized = s.footBase - cycles[leg].stancePosition;
				}
			}
		}
		
		for (int leg=0; leg<legs; leg++) {
			float heelToeZDist = Vector3.Dot(cycles[leg].heelToetipVector,cycleDirection);
			for (int i=0; i<samples; i++) {
				LegCycleSample s = cycles[leg].samples[i];
				float zVal = Vector3.Dot(s.footBase, cycleDirection);
				if (zVal < graphMin.z) graphMin.z = zVal;
				if (zVal > graphMax.z) graphMax.z = zVal;
				if (zVal+heelToeZDist < graphMin.z) graphMin.z = zVal+heelToeZDist;
				if (zVal+heelToeZDist > graphMax.z) graphMax.z = zVal+heelToeZDist;
			}
		}
		graphMin.y = legC.groundPlaneHeight;
	}
	
	private void DrawLine(Vector3 a, Vector3 b, Color color) {
		GL.Color(color);
		GL.Vertex(a);
		GL.Vertex(b);
	}
	private void DrawRay(Vector3 a, Vector3 b, Color color) {
		DrawLine(a,a+b,color);
	}
	private void DrawDiamond(Vector3 a, float size, Color color) {
		Vector3 up = Camera.main.transform.up;
		Vector3 right = Camera.main.transform.right;
		GL.Color(color);
		GL.Vertex(a+up*size);
		GL.Vertex(a+right*size);
		GL.Vertex(a-up*size);
		GL.Vertex(a-right*size);
	}
	
	public void RenderGraph(MotionAnalyzerDrawOptions opt, Material vertexColorMaterial) {
		vertexColorMaterial.SetPass( 0 );
		
		if (opt.drawAllFeet) {
			for (int leg=0; leg<legs; leg++) {
				RenderGraphAll(opt,leg);
			}
		}
		else {
			RenderGraphAll(opt,opt.currentLeg);
		}
	}
	
	// toggles
	/*[System.NonSerialized] public bool drawAllFeet;
	[System.NonSerialized] public bool drawHeelToe;
	[System.NonSerialized] public bool drawFootBase;
		
	// children only true if parent is
	[System.NonSerialized] public bool drawTrajectories;
	[System.NonSerialized] public bool drawTrajectoriesProjected;
	[System.NonSerialized] public bool drawThreePoints;
		
	// at most one of these true
	[System.NonSerialized] public bool drawGraph;
	[System.NonSerialized] public bool normalizeGraph;
		
	// graph toggles
	[System.NonSerialized] public bool drawStanceMarkers;
	[System.NonSerialized] public bool drawBalanceCurve;
	[System.NonSerialized] public bool drawLiftedCurve;
	
	[System.NonSerialized] public float graphScaleH;
	[System.NonSerialized] public float graphScaleV;
		
	// toggles
	[System.NonSerialized] public bool drawFootPrints = true;*/
	
	public void RenderGraphAll(MotionAnalyzerDrawOptions opt, int currentLeg) {
		//int currentLeg = opt.currentLeg;
		bool showAll = opt.drawAllFeet;
		
		bool drawFrames = (!showAll || currentLeg==0);
		
		// Helper variables
		int l = currentLeg;
		Transform ankleT = legC.legs[l].ankle;
		Transform toeT = legC.legs[l].toe;
		Vector3 graphSpan = graphMax-graphMin;
		Matrix4x4 objectMatrix = gameObject.transform.localToWorldMatrix;
		
		// Time variables
		float time = Util.Mod(gameObject.GetComponent<Animation>()[animation.name].normalizedTime);
		int cycleIndex = GetIndexFromTime(time);
		float timeRounded = GetTimeFromIndex(cycleIndex);
		
		// Standard sizes
		float scale = gameObject.transform.localScale.z;
		float unit = cycleDistance * scale;
		float diamondSize = graphSpan.z*scale*0.03f;
		
		// Colors
		Color frameColor = new Color(0.7f, 0.7f, 0.7f, 1);
		Color ankleColor = new Color(0.8f, 0, 0, 1);
		Color toeColor = new Color(0, 0.7f, 0, 1);
		Color strongColor = new Color(0, 0, 0, 1);
		Color weakColor = new Color(0.7f, 0.7f, 0.7f, 1);
		if (showAll) {
			ankleColor = legC.legs[l].debugColor;
			toeColor = legC.legs[l].debugColor;
			strongColor = legC.legs[l].debugColor*0.5f + Color.black*0.5f;
			weakColor = legC.legs[l].debugColor*0.5f + Color.white*0.5f;
		}
		Color strongClear = strongColor;
		strongClear.a = 0.5f;
		
		GL.Begin( GL.QUADS );
		
		// Key positions
		Vector3 footHeel = ankleT.position+Util.TransformVector(
			ankleT,legC.legs[l].ankleHeelVector
		);
		Vector3 footToetip = toeT.position+Util.TransformVector(
			toeT,legC.legs[l].toeToetipVector
		);
		Vector3 footbaseHeel = objectMatrix.MultiplyPoint3x4(
			cycles[l].samples[cycleIndex].footBase
		);
		Vector3 footbaseToetip = objectMatrix.MultiplyPoint3x4(
			cycles[l].samples[cycleIndex].footBase
			+cycles[l].heelToetipVector
		);
		
		// Draw foot heel and toetip
		if (opt.drawHeelToe) {
			DrawDiamond(footHeel, diamondSize, ankleColor);
			DrawDiamond(footToetip, diamondSize, toeColor);
		}
		
		// Draw foot balanced base
		if (opt.drawFootBase) {
			DrawDiamond(footbaseHeel, diamondSize, strongClear);
			DrawDiamond(footbaseToetip, diamondSize, strongClear);
			GL.End();
			GL.Begin( GL.LINES );
			DrawLine (footbaseHeel, footbaseToetip, strongColor);
		}
		
		GL.End();
		
		// Draw foot step markers
		if (opt.drawFootPrints) {
			float cycleTime = Util.Mod(time-cycles[l].stanceTime+cycleOffset);
			Color c = weakColor*GetFootGrounding(l,cycleTime)+strongColor*(1-GetFootGrounding(l,cycleTime));
			if (l==currentLeg) GL.Begin( GL.QUADS );
			else GL.Begin( GL.LINES );
			GL.Color(c);
			
			// Draw rectangles
			Vector3 pos = (
				cycles[l].stancePosition
				+(cycleDirection*-cycleDistance*(Util.Mod(cycleTime+0.5f,1)-0.5f))
			);
			Vector3 down = Vector3.up*-0.5f*cycles[l].heelToetipVector.magnitude;
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos+down) );
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos) );
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos+cycles[l].heelToetipVector) );
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos+cycles[l].heelToetipVector+down) );
			if (l!=currentLeg) {
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos) );
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos+cycles[l].heelToetipVector) );
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos+cycles[l].heelToetipVector+down) );
			GL.Vertex ( objectMatrix.MultiplyPoint3x4(pos+down) );
			}
			
			GL.End();
		}
		
		if (motionType!=MotionType.WalkCycle) { return; }
		
		if (opt.drawTrajectories) {
			GL.Begin( GL.LINES );
			// Draw foot trajectories
			for (int i=0; i<samples*2; i++) {
				int j = Util.Mod(cycleIndex-i,samples);
				LegCycleSample sA = cycles[l].samples[j];
				LegCycleSample sB = cycles[l].samples[Util.Mod(j-1,samples)];
				
				float t1 = GetTimeFromIndex(i  );
				float t2 = GetTimeFromIndex(i+1);
				
				float driftA = 0;
				float driftB = 0;
				if (opt.normalizeGraph) {
					driftA = -t1*cycleDistance;
					driftB = -t2*cycleDistance;
				}
				
				Vector3 heelA   = objectMatrix.MultiplyPoint3x4(sA.heel+driftA*cycleDirection);
				Vector3 heelB   = objectMatrix.MultiplyPoint3x4(sB.heel+driftB*cycleDirection);
				Vector3 toetipA = objectMatrix.MultiplyPoint3x4(sA.toetip+driftA*cycleDirection);
				Vector3 toetipB = objectMatrix.MultiplyPoint3x4(sB.toetip+driftB*cycleDirection);
				Vector3 baseA = objectMatrix.MultiplyPoint3x4(sA.footBase+driftA*cycleDirection);
				Vector3 baseB = objectMatrix.MultiplyPoint3x4(sB.footBase+driftB*cycleDirection);
				if (opt.drawHeelToe) {
					DrawLine (heelA, heelB, ankleColor);
					DrawLine (toetipA, toetipB, toeColor);
				}
				if (opt.drawFootBase) {
					DrawLine (baseA, baseB, ((j%2==0) ? strongColor : weakColor));
				}
				
				if (opt.drawTrajectoriesProjected) {
					// Draw foot center trajectory projected onto ground plane
					DrawLine (
						objectMatrix.MultiplyPoint3x4(
							Util.ProjectOntoPlane(sA.heel+sA.toetip,Vector3.up)/2
							+ (legC.groundPlaneHeight-graphSpan.y)*Vector3.up
						),
						objectMatrix.MultiplyPoint3x4(
							Util.ProjectOntoPlane(sB.heel+sB.toetip,Vector3.up)/2
							+ (legC.groundPlaneHeight-graphSpan.y)*Vector3.up
						),
						strongColor
					);
				}
			}
			GL.End();
			
			if (opt.drawTrajectoriesProjected) {
				GL.Begin( GL.QUADS );
				DrawDiamond(
					objectMatrix.MultiplyPoint3x4(
						Util.ProjectOntoPlane(
							cycles[l].samples[cycleIndex].heel
							+cycles[l].samples[cycleIndex].toetip,
							Vector3.up
						)/2
						+ (legC.groundPlaneHeight-graphSpan.y)*Vector3.up
					),
					diamondSize,
					strongClear
				);
				GL.End();
				
				// Draw three points used to derive cycle direction
				if (opt.drawThreePoints) {
					for (int i=-1; i<=1; i++) {
						GL.Begin( GL.QUADS );
						DrawDiamond(
							objectMatrix.MultiplyPoint3x4(
								cycles[l].cycleCenter
								+ (legC.groundPlaneHeight-graphSpan.y)*Vector3.up
								+ (i*cycles[l].cycleDirection*cycles[l].cycleScaling*0.5f)
							),
							diamondSize,
							strongClear
						);
						GL.End();
					}
					
					// Draw axis line for movement.
					GL.Begin( GL.LINES );
					DrawLine (
						objectMatrix.MultiplyPoint3x4(
							cycles[l].cycleCenter
							+(legC.groundPlaneHeight-graphSpan.y)*Vector3.up
							- (cycles[l].cycleDirection*cycles[l].cycleScaling)
						),
						objectMatrix.MultiplyPoint3x4(
							cycles[l].cycleCenter
							+(legC.groundPlaneHeight-graphSpan.y)*Vector3.up
							+ (cycles[l].cycleDirection*cycles[l].cycleScaling)
						),
						strongClear
					);
					GL.End();
				}
			}
		}
		
		if (opt.drawGraph) {
			GL.Begin( GL.LINES );
			
			// Define graph areas
			Vector3 groundPlaneDown = legC.groundPlaneHeight*gameObject.transform.up*scale;
			float normExtend = 2*cycleDistance;
			if (!opt.normalizeGraph) normExtend = 0;
			Quaternion rot = gameObject.transform.rotation;
			Vector3 sideDir = rot * Quaternion.Euler(0,90,0)*cycleDirection;
			Vector3 upDir = rot * gameObject.transform.up;
			Vector3 cycleDir = rot * cycleDirection;
			Vector3 groundPos = gameObject.transform.position + groundPlaneDown;
			DrawArea graphZ = new DrawArea3D(
				new Vector3(-unit*0.5f, 0, 0),
				new Vector3(-unit*1.5f, scale, scale),
				Util.CreateMatrix(sideDir, upDir*opt.graphScaleV, cycleDir*opt.graphScaleH, groundPos)
			);
			
			DrawArea graphBalance = new DrawArea3D(
				new Vector3(-unit*0.5f, graphSpan.y*scale*0.7f, 0),
				new Vector3(-unit*1.5f, graphSpan.y*scale*1.0f, 1),
				Util.CreateMatrix(
					sideDir, upDir+cycleDir, cycleDir-upDir,
					groundPos + cycleDir*graphMax.z*scale + upDir*graphSpan.y*scale
				)
			);
			DrawArea graphMarkers = new DrawArea3D(
				new Vector3(-unit*0.5f, graphSpan.y*scale*0.2f, 0),
				new Vector3(-unit*1.5f, graphSpan.y*scale*0.5f, 1),
				Util.CreateMatrix(
					sideDir, upDir+cycleDir, cycleDir-upDir,
					groundPos + cycleDir*graphMax.z*scale + upDir*graphSpan.y*scale
				)
			);
			
			// Draw frame
			if (drawFrames) {
				graphZ.DrawCube(
					new Vector3(0,0,graphMax.z),
					Vector3.right*2,
					Vector3.up*graphSpan.y,
					-Vector3.forward*(graphSpan.z+normExtend),
					frameColor
				);
			}
			
			// Draw twice
			for (int i=0; i<2; i++) {
				// Draw stance time marker
				if (opt.drawStanceMarkers) {
					graphZ.DrawRect(
						new Vector3(Util.Mod(timeRounded-cycles[l].stanceTime+cycleOffset,1)+i,0,graphMax.z),
						Vector3.up*graphSpan.y,
						-Vector3.forward*(graphSpan.z+normExtend),
						strongColor
					);
				}
			}
			
			if (opt.drawHeelToe) {
				DrawLine(
					ankleT.position+Util.TransformVector(ankleT,legC.legs[l].ankleHeelVector),
					graphZ.Point(new Vector3(
						0,
						cycles[l].samples[cycleIndex].heel.y-legC.groundPlaneHeight,
						Vector3.Dot(cycles[l].samples[cycleIndex].heel,cycleDirection)
					)),
					ankleColor
				);
				DrawLine(
					toeT.position+Util.TransformVector(toeT,legC.legs[l].toeToetipVector),
					graphZ.Point(new Vector3(
						0,
						cycles[l].samples[cycleIndex].toetip.y-legC.groundPlaneHeight,
						Vector3.Dot(cycles[l].samples[cycleIndex].toetip,cycleDirection)
					)),
					toeColor
				);
			}
			
			if (opt.drawFootBase) {
				DrawLine(
					footbaseHeel,
					graphZ.Point(new Vector3(
						0,
						cycles[l].samples[cycleIndex].footBase.y-legC.groundPlaneHeight,
						Vector3.Dot(cycles[l].samples[cycleIndex].footBase,cycleDirection)
					)),
					Color.black
				);
			}
			
			// Draw curves
			for (int i=0; i<samples*2; i++) {
				//int j = Util.Mod(cycles[l].stanceIndex+i,samples);
				int j = Util.Mod(cycleIndex-i,samples);
				LegCycleSample sA = cycles[l].samples[j];
				LegCycleSample sB = cycles[l].samples[Util.Mod(j-1,samples)];
				
				float t1 = GetTimeFromIndex(i  );
				float t2 = GetTimeFromIndex(i+1);
				float c1 = GetTimeFromIndex(Util.Mod(j  -cycles[l].stanceIndex,samples));
				float c2 = GetTimeFromIndex(Util.Mod(j-1-cycles[l].stanceIndex,samples));
				
				float driftA = 0;
				float driftB = 0;
				if (opt.normalizeGraph) {
					driftA = -t1*cycleDistance;
					driftB = -t2*cycleDistance;
				}
				
				// Draw ankle and toe curves
				if (opt.drawHeelToe) {
					graphZ.DrawLine (
						new Vector3(t1,
							sA.heel.y-legC.groundPlaneHeight,
							Vector3.Dot(sA.heel,cycleDirection)+driftA
						),
						new Vector3(t2,
							sB.heel.y-legC.groundPlaneHeight,
							Vector3.Dot(sB.heel,cycleDirection)+driftB
						),
						ankleColor
					);
					graphZ.DrawLine (
						new Vector3(t1,
							sA.toetip.y-legC.groundPlaneHeight,
							Vector3.Dot(sA.toetip,cycleDirection)+driftA
						),
						new Vector3(t2,
							sB.toetip.y-legC.groundPlaneHeight,
							Vector3.Dot(sB.toetip,cycleDirection)+driftB
						),
						toeColor
					);
				}
				// Draw foot base curve
				if (opt.drawFootBase && j%2==0) {
					graphZ.DrawLine (
						new Vector3(t1,
							sA.footBase.y-legC.groundPlaneHeight,
							Vector3.Dot(sA.footBase,cycleDirection)+driftA
						),
						new Vector3(t2,
							sB.footBase.y-legC.groundPlaneHeight,
							Vector3.Dot(sB.footBase,cycleDirection)+driftB
						),
						strongColor
					);
				}
				
				// Draw foot balance curve (0=ankle, 1=toe)
				if (opt.drawBalanceCurve) {
					float balance = (sA.balance+sB.balance)/2;
					graphBalance.DrawLine (
						new Vector3(t1,sA.balance,0),
						new Vector3(t2,sB.balance,0),
						toeColor*balance+ankleColor*(1-balance)
					);
				}
				
				// Draw lift/liftoff and strike/land times
				if (opt.drawLiftedCurve) {
					float lifted = (GetFootGroundingOrig(l,c1)+GetFootGroundingOrig(l,c2))/2;
					graphMarkers.DrawLine (
						new Vector3(t1,GetFootGroundingOrig(l,c1),0),
						new Vector3(t2,GetFootGroundingOrig(l,c2),0),
						strongColor*(1-lifted)+weakColor*lifted
					);
				}
			}
			GL.End();
		}
		
	}
}

public class MotionAnalyzerDrawOptions {
	// general
	[System.NonSerialized] public int currentLeg;
	
	// toggles
	[System.NonSerialized] public bool drawAllFeet = false;
	[System.NonSerialized] public bool drawHeelToe = true;
	[System.NonSerialized] public bool drawFootBase = false;
		
	// children only true if parent is
	[System.NonSerialized] public bool drawTrajectories = true;
	[System.NonSerialized] public bool drawTrajectoriesProjected;
	[System.NonSerialized] public bool drawThreePoints;
		
	// at most one of these true
	[System.NonSerialized] public bool drawGraph = true;
	[System.NonSerialized] public bool normalizeGraph = true;
		
	// graph toggles
	[System.NonSerialized] public bool drawStanceMarkers;
	[System.NonSerialized] public bool drawBalanceCurve;
	[System.NonSerialized] public bool drawLiftedCurve;
	
	[System.NonSerialized] public bool isolateVertical = false;
	[System.NonSerialized] public bool isolateHorisontal = true;
	[System.NonSerialized] public float graphScaleH;
	[System.NonSerialized] public float graphScaleV;
		
	// toggles
	[System.NonSerialized] public bool drawFootPrints = true;
}
