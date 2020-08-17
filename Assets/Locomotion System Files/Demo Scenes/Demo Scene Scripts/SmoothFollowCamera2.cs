using UnityEngine;
using System.Collections;

public class SmoothFollowCamera2 : MonoBehaviour {
	
	public GameObject target;
	public Transform cam;
	public float smoothingTime = 0.5f;
	public Vector3 offset = Vector3.zero;
	public bool useBounds = false;
	private SmoothFollower follower;
	public int cameraMode = 0;
	public float cameraRotSide = 0;
	public float cameraRotUp = 0;
	private float cameraRotSideCur = 0;
	private float cameraRotUpCur = 0;
	public float distance = 4;
	
	// Use this for initialization
	void Start () {
		follower = new SmoothFollower(smoothingTime);
	}
	
	// Update is called once per frame
	void LateUpdate () {
		if (Time.deltaTime==0) return;
		
		if (target==null) return;
		
		bool altFunc = false;
		if (Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftShift)) altFunc = true;
		if (Input.GetMouseButton(0) && !altFunc) {
			cameraRotSide += Input.GetAxis("Mouse X")*5;
			cameraRotUp -= Input.GetAxis("Mouse Y")*5;
		}
		cameraRotSideCur = Mathf.LerpAngle(cameraRotSideCur,cameraRotSide,Time.deltaTime*5);
		cameraRotUpCur = Mathf.Lerp(cameraRotUpCur,cameraRotUp,Time.deltaTime*5);
		
		if (Input.GetMouseButton(1) || (Input.GetMouseButton(0) && altFunc) ) {
			distance *= (1-0.1f*Input.GetAxis("Mouse Y"));
		}
		distance *= (1-0.1f*Input.GetAxis("Mouse ScrollWheel"));
		
		Vector3 targetPoint = target.transform.position;
		if (useBounds) {
			Renderer r = target.GetComponentInChildren(typeof(Renderer)) as Renderer;
			Vector3 targetPointCenter = r.bounds.center;
			if (!float.IsNaN(targetPointCenter.x)
				&&targetPointCenter.magnitude < 10000) targetPoint = targetPointCenter;
		}
		transform.position = follower.Update(targetPoint + offset, Time.deltaTime);
		transform.rotation = Quaternion.Euler(0,cameraRotSideCur,cameraRotUpCur);
		
		float usedDistance = distance;
		if (Camera.main.orthographic) { usedDistance *= 4; }
		float dist = Mathf.Lerp(cam.transform.localPosition.x,usedDistance,Time.deltaTime*5);
		cam.transform.localPosition = Vector3.right * dist;
		if (Camera.main.orthographic) {
			Camera.main.orthographicSize = dist/8;
		}
	}
}
