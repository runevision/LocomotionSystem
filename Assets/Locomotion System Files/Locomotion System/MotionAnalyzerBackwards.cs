/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

[System.Serializable]
public class MotionAnalyzerBackwards : IMotionAnalyzer {
	
	public MotionAnalyzer orig;
	
	public LegCycleData[] m_cycles;
	public override LegCycleData[] cycles { get { return m_cycles; } }
	
	public override int samples { get { return orig.samples; } }
	
	public override Vector3 cycleDirection { get { return -orig.cycleDirection; } }
	
	public override float cycleDistance { get { return orig.cycleDistance; } }
	
	public override Vector3 cycleVector { get { return -orig.cycleVector; } }
	
	public override float cycleDuration { get { return orig.cycleDuration; } }
	
	public override float cycleSpeed { get { return orig.cycleSpeed; } }
	
	public override Vector3 cycleVelocity { get { return -orig.cycleVelocity; } }
	
	public float m_cycleOffset;
	public override float cycleOffset { get { return m_cycleOffset; } set { m_cycleOffset = value; } }
	
	public override Vector3 GetFlightFootPosition(int leg, float flightTime, int phase) {
		Vector3 origVector = orig.GetFlightFootPosition(leg, 1-flightTime, 2-phase);
		return new Vector3(-origVector.x,origVector.y,1-origVector.z);
	}
	
	public override void Analyze(GameObject o) {
		GameObject gameObject = o;
		animation = orig.animation;
		name = animation.name + "_bk";
		motionType = orig.motionType;
		motionGroup = orig.motionGroup;
		
		// Initialize legs and cycle data
		LegController legC = gameObject.GetComponent(typeof(LegController)) as LegController;
		int legs = legC.legs.Length;
		m_cycles = new LegCycleData[legs];
		for (int leg=0; leg<legs; leg++) {
			cycles[leg] = new LegCycleData();
			cycles[leg].cycleCenter = orig.cycles[leg].cycleCenter;
			cycles[leg].cycleScaling = orig.cycles[leg].cycleScaling;
			cycles[leg].cycleDirection = -orig.cycles[leg].cycleDirection;
			cycles[leg].stanceTime = 1-orig.cycles[leg].stanceTime;
			cycles[leg].liftTime = 1-orig.cycles[leg].landTime;
			cycles[leg].liftoffTime = 1-orig.cycles[leg].strikeTime;
			cycles[leg].postliftTime = 1-orig.cycles[leg].prelandTime;
			cycles[leg].prelandTime = 1-orig.cycles[leg].postliftTime;
			cycles[leg].strikeTime = 1-orig.cycles[leg].liftoffTime;
			cycles[leg].landTime = 1-orig.cycles[leg].liftTime;
			cycles[leg].cycleDistance = orig.cycles[leg].cycleDistance;
			cycles[leg].stancePosition = orig.cycles[leg].stancePosition;
			cycles[leg].heelToetipVector = orig.cycles[leg].heelToetipVector;
		}
		
	}
	
}
