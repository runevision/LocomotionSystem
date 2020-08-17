/*
Copyright (c) 2008-2020, Rune Skovbo Johansen

See the document "LICENSE" included in the project folder for licencing details.
*/
using UnityEngine;
using System.Collections;

public abstract class IKSolver {
	
	public float positionAccuracy = 0.001f;
	
	public abstract void Solve(Transform[] bones, Vector3 target);
	
}
