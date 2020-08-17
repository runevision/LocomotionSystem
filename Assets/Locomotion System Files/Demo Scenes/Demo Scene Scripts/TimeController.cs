using UnityEngine;
using System.Collections;

public class TimeController : MonoBehaviour {
	
	private bool paused = false;
	private float timeScale = 1;
	
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		// Control playback speed
		bool adjusted = false;
		if (Input.GetKeyDown("[+]")) { Time.timeScale *= 2; adjusted = true; }
		if (Input.GetKeyDown("[-]")) { Time.timeScale /= 2; adjusted = true; }
		if (adjusted && Time.timeScale>0.75f && Time.timeScale<1.5f) {
			Time.timeScale = 1;
		}
		if (Input.GetKeyDown("p")) {
			paused = !paused;
			if (paused) {
				timeScale = Time.timeScale;
				Time.timeScale = 0.001f;
			}
			else Time.timeScale = timeScale;
		}
	}
	
}
