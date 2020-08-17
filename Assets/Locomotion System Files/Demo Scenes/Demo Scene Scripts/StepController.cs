using UnityEngine;
using System.Collections;

public class StepController : MonoBehaviour {
	
	public GameObject character;
	public GameObject box;
	public int stepAmount = 10;
	public float stepLength = 32;
	public float slope = 0.0f;
	public float slopeJitter = 0.0f;
	public float step = 0.0f;
	public float stepJitter = 0.0f;
	public bool alternate = true;
	public float alternateFrequency = 3.0f;
	private float timer = 2.0f;
	private GameObject[] steps;
	private int leftmost;
	private int rightmost;
	private GameObject testStepA;
	private GameObject testStepB;
	
	// Use this for initialization
	void Start () {
		steps = new GameObject[stepAmount];
		Random.InitState (123);
		Vector3 pos = character.transform.position;
		for (int i=0; i<stepAmount; i++) {
			steps[i] = Instantiate(box, new Vector3(pos.x,-0.5f,-stepAmount/2+i+pos.z), transform.rotation) as GameObject;
			steps[i].transform.localScale = new Vector3(stepLength,1,1);
			steps[i].transform.parent = transform;
			steps[i].GetComponent<Renderer>().material.mainTextureScale = new Vector2(stepLength,steps[i].GetComponent<Renderer>().material.mainTextureScale.y);
		}
		leftmost = 0;
		rightmost = stepAmount-1;
	}
	
	// Update is called once per frame
	void Update () {
		Vector3 pos = character.transform.position;
		
		for (int dir=-1; dir<=1; dir+=2) {
			int frontmost = rightmost;
			int backmost = leftmost;
			if (dir==-1) {
				frontmost = leftmost;
				backmost = rightmost;
			}
			if (
				Mathf.Abs(steps[frontmost].transform.position.z-pos.z)
				< Mathf.Abs(steps[Util.Mod(backmost+dir,stepAmount)].transform.position.z-pos.z)
			) {
				float stress;
				
				float currentStep = (step+(Random.value-0.5f)*stepJitter);
				if (Mathf.Abs(currentStep)<0.05f) currentStep = 0;
				stress = (Mathf.Abs(step)+stepJitter*0.5f);
				if (stress>0.5f) currentStep /= (stress/0.5f);
				currentStep *= 0.50f;
				
				float currentSlope = (slope+(Random.value-0.5f)*slopeJitter);
				stress = (Mathf.Abs(slope)+slopeJitter*0.5f);
				if (stress>0.5f) currentSlope /= (stress/0.5f);
				currentSlope *= 0.60f;
				
				// Create offset
				Vector3 stepOffset = new Vector3(0,currentStep,0);
				
				// Create slope
				Vector3 stepSlope = new Vector3(0,currentSlope,1);
				
				steps[backmost].transform.position = Vector3.zero;
				
				// Set scale
				steps[backmost].transform.localScale = new Vector3(stepLength,1,stepSlope.magnitude);
				
				// Set rotation
				steps[backmost].transform.rotation = new Quaternion();
				steps[backmost].transform.Rotate(Vector3.right,Mathf.Atan2(-stepSlope.y,stepSlope.z)*Mathf.Rad2Deg);
				
				// Set position
				steps[backmost].transform.position = (
					-steps[backmost].transform.TransformPoint(new Vector3(0,0.5f,-dir*0.5f))
					+steps[frontmost].transform.TransformPoint(new Vector3(0,0.5f,dir*0.5f))
					+stepOffset*dir
				);
				
				rightmost = Util.Mod(rightmost+dir,stepAmount);
				leftmost = Util.Mod(leftmost+dir,stepAmount);
			}
		}
		float offset = pos.x;
		for (int i=0; i<steps.Length; i++) {
			steps[i].transform.position = new Vector3(offset,steps[i].transform.position.y,steps[i].transform.position.z);
			steps[i].GetComponent<Renderer>().material.mainTextureOffset =
				new Vector2(-offset/steps[i].transform.lossyScale.x*steps[i].GetComponent<Renderer>().material.mainTextureScale.x,0);
		}
		
		if (alternate) {
			timer -= Time.deltaTime;
			if (timer<0) {
				timer = alternateFrequency;
				if (Random.value>0.5f) { step = Mathf.Clamp(step*0.7f + (Random.value-0.5f)*0.5f, -0.5f, 0.5f); }
				if (Random.value>0.5f) { stepJitter = Mathf.Clamp01 (stepJitter*0.6f + Random.value-0.5f); }
				if (Random.value>0.5f) { slope = Mathf.Clamp(slope*0.7f + (Random.value-0.5f)*0.5f, -0.5f, 0.5f); }
				if (Random.value>0.5f) { slopeJitter = Mathf.Clamp01 (slopeJitter*0.6f + Random.value-0.5f); }
			}
		}
	}
	
}
