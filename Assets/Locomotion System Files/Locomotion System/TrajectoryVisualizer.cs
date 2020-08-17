using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public struct TimePoint {
	public float time;
	public Vector3 point;
	public TimePoint(float time, Vector3 point) {
		this.time = time;
		this.point = point;
	}
}

public class TrajectoryVisualizer {
	
	private Color color;
	private float length;
	private bool dotted;
	private List<TimePoint> trajectory = new List<TimePoint>();
	
	public TrajectoryVisualizer(Color color, float length) {
		this.color = color;
		this.length = length;
	}
	
	public void AddPoint(float time, Vector3 point) {
		trajectory.Add(new TimePoint(time,point));
		while (trajectory[0].time<time-length) {
			trajectory.RemoveAt(0);
		}
	}
	
	public void Render() {
		//Debug.Log("Point count: "+trajectory.Count);
		if (trajectory.Count==0) return;
		DrawArea draw = new DrawArea3D(Vector3.zero,Vector3.one,Matrix4x4.identity);
		float curTime = trajectory[trajectory.Count-1].time;
		GL.Begin(GL.LINES);
		for (int i=0; i<trajectory.Count-1; i++) {
			Color col = color;
			col.a = (curTime-trajectory[i].time)/length;
			col.a = 1-col.a*col.a;
			draw.DrawLine(trajectory[i].point, trajectory[i+1].point, col);
		}
		GL.End();
	}
}
