/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

[System.Serializable]
public abstract class IMotionAnalyzer {
	
	[HideInInspector] public string name;
	
	public AnimationClip animation;
	
	public MotionType motionType = MotionType.WalkCycle;
	
	public string motionGroup = "locomotion";
	
	public abstract int samples { get; }
	
	public abstract LegCycleData[] cycles { get; }
	
	public abstract Vector3 cycleDirection { get; }
	
	public abstract float cycleDistance { get; }
	
	public abstract Vector3 cycleVector { get; }
	
	public abstract float cycleDuration { get; }
	
	public abstract float cycleSpeed { get; }
	
	public abstract Vector3 cycleVelocity { get; }
	
	public abstract Vector3 GetFlightFootPosition(int leg, float flightTime, int phase);
	
	public abstract float cycleOffset { get; set; }
	
	public abstract void Analyze(GameObject o);
}
