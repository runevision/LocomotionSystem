using UnityEngine;
using System.Collections;

public class SmoothFollowCamera : MonoBehaviour {
	
	public GameObject target;
	public float smoothingTime = 0.5f;
	public Vector3 offset = Vector3.zero;
	public float rotateAngle = 45;
	public float rotateTime = 25;
	public bool useBounds = false;
	private SmoothFollower follower;
	
	// Use this for initialization
	void Start () {
		follower = new SmoothFollower(smoothingTime);
	}
	
	// Update is called once per frame
	void LateUpdate () {
		if (target==null) return;
		
		Vector3 targetPoint = target.transform.position;
		if (useBounds) {
			Renderer r = target.GetComponentInChildren(typeof(Renderer)) as Renderer;
			Vector3 targetPointCenter = r.bounds.center;
			if (!float.IsNaN(targetPointCenter.x)
				&&targetPointCenter.magnitude < 10000) targetPoint = targetPointCenter;
		}
		transform.position = follower.Update(targetPoint + offset, Time.deltaTime);
		transform.rotation = Quaternion.AngleAxis(
			Mathf.Sin(Time.time*2*Mathf.PI/rotateTime)*rotateAngle,
			Vector3.up
		);
	}
}
