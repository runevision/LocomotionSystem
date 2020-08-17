using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class StepWalkSwitcher : MonoBehaviour {
	
	public GameObject[] characters;
	public GameObject cameraController;
	public bool showGhost = false;

	private int showClones = 0;
	private float lastCloneTime = 0;
	private Vector3[] characterBounds;
	private bool showOptions = false;
	private StepController sc;
	private int oldCharacter = 0;
	private int activeCharacter = 0;
	private float[] initHeights;
	private bool switchCharacter = false;
	private GameObject ghost = null;
	private List<GameObject> clones;
	
	public bool autoWalk = true;
	public bool autoSwitchCharacter = true;

	private float autoSwitchCharacterTime = 29;
	private float autoSwitchCharacterTimer = 29;
	private float autoSwitchCameraTime = 9;
	private float autoSwitchCameraTimer = 9;
	private bool renderFootMarkers = false;
	private bool renderBlendingGraph = false;
	private bool renderCycleGraph = false;
	private bool renderAnimationStates = false;
	private bool disableLocomotion = false;
	
	void Awake () {
		sc = GetComponent(typeof(StepController)) as StepController;
		sc.character = characters[0];
		clones = new List<GameObject>();
		
		SmoothFollowCamera2 sf = cameraController.GetComponent(typeof(SmoothFollowCamera2)) as SmoothFollowCamera2;
		sf.target = characters[0];
		
		characterBounds = new Vector3[characters.Length];
	}
	
	// Use this for initialization
	void Start () {
		initHeights = new float[characters.Length];
		for (int i=0; i<characters.Length; i++) {
			initHeights[i] = characters[i].transform.position.y;
			
			Renderer r = characters[i].GetComponentInChildren(typeof(Renderer)) as Renderer;
			characterBounds[i] = r.bounds.size;
			if (
				float.IsNaN(characterBounds[i].x)
				|| characterBounds[i].magnitude > 10000
			) {
				characterBounds[i] = Vector3.zero;
			}
		}
		UseCharacter(activeCharacter);
		
		ToggleAutoWalk(autoWalk);
		ToggleRenderFootMarkers(renderFootMarkers);
		ToggleRenderBlendingGraph(renderBlendingGraph);
		ToggleRenderCycleGraph(renderCycleGraph);
		ToggleRenderAnimationStates(renderAnimationStates);
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKeyDown("return")) { switchCharacter = true; activeCharacter++; }
		if (Input.GetKeyDown(KeyCode.Backspace)) { showOptions = !showOptions; }
		
		if (autoSwitchCharacter) {
			autoSwitchCharacterTimer -= Time.deltaTime;
			if (autoSwitchCharacterTimer<=0) {
				autoSwitchCharacterTimer = autoSwitchCharacterTime;
				activeCharacter++;
				switchCharacter = true;
			}
			autoSwitchCameraTimer -= Time.deltaTime;
			if (autoSwitchCameraTimer<=0) {
				autoSwitchCameraTimer = autoSwitchCameraTime;
				SmoothFollowCamera2 sf = cameraController.GetComponent(typeof(SmoothFollowCamera2)) as SmoothFollowCamera2;
				sf.cameraRotSide = UnityEngine.Random.Range(-90,90);
				sf.cameraRotUp = Mathf.Pow(UnityEngine.Random.value,2)*50;
			}
		}
		
		if (switchCharacter) UseCharacter( activeCharacter % characters.Length );
		switchCharacter = false;
		oldCharacter = activeCharacter;
		
		float interDist = 2.5f*Mathf.Max(
			Mathf.Max(characterBounds[activeCharacter].x,
			characterBounds[activeCharacter].y*0.75f),
			characterBounds[activeCharacter].z
		);
		
		if (Input.GetKeyDown(KeyCode.C)) showClones++;
		if (Input.GetKeyDown(KeyCode.X)) showClones = Mathf.Max(0,showClones-1);
		if (showClones>clones.Count-1 && lastCloneTime<Time.time-1) {
			lastCloneTime = Time.time;
			Debug.Log("Adding Clone");
			// Add clone for character
			GameObject clone = (GameObject)Instantiate(characters[activeCharacter]);
			clones.Add(clone);
			
			// Remove all unneccasary components from clone
			Component[] components;
			components = clone.GetComponents(typeof(Component));
			foreach (Component component in components) {
				Type type = component.GetType();
				if (
					type==typeof(PlatformCharacterController)
					|| type==typeof(AimLookCharacterController)
					|| type==typeof(AimLookCharacterController)
					|| type==typeof(WanderingAICharacterController)
				) Destroy(component);
			}
			
			// Move clone
			clone.transform.position = characters[activeCharacter].transform.position+new Vector3(-2*interDist,2,0);
		}
		if (showClones<clones.Count-1) {
			Debug.Log("Removing Clone");
			Destroy(clones[clones.Count-1]);
			clones.RemoveAt(clones.Count-1);
		}
		
		// Delete clones that have fallen down
		for (int c=1; c<clones.Count; c++) {
			if (clones[c].transform.position.y-clones[0].transform.position.y<-100) {
				Destroy(clones[c]);
				clones.RemoveAt(c);
				c--;
			}
		}
		
		// Move clones
		Vector3 avgPos = characters[activeCharacter].transform.position;
		if (clones.Count>1) {
			// Find desired direction for each clone
			for (int c=1; c<clones.Count; c++) {
				Vector3 groupDir = (avgPos-clones[c].transform.position).normalized;
				
				Vector3 avoidanceDir = Vector3.zero;
				for (int other=0; other<clones.Count; other++) {
					if (c==other) continue;
					Vector3 dir = clones[c].transform.position-clones[other].transform.position;
					float dirMag = dir.magnitude;
					avoidanceDir += (dir/dirMag) * Mathf.Pow(Mathf.Max(0, interDist-dirMag),2);
				}
				
				NormalCharacterMotor motor = clones[c].GetComponent(typeof(NormalCharacterMotor)) as NormalCharacterMotor;
				Vector3 moveDir = 
				Util.ProjectOntoPlane(
					clones[c].transform.InverseTransformDirection(groupDir*2+avoidanceDir),
					Vector3.up
				);
				float moveDirMag = moveDir.magnitude;
				if (moveDirMag==0) motor.desiredMovementDirection = Vector3.zero;
				else {
					motor.desiredMovementDirection = Vector3.Lerp(
						motor.desiredMovementDirection,
						moveDir/moveDirMag * Mathf.InverseLerp(0.2f,1.0f,moveDirMag),
						0.5f
					);
				}
			}
		}
	}
	
	private void UseCharacter(int nr) {
		
		activeCharacter = nr;
		
		// Switch relevant characters on and off
		for (int c=0; c<characters.Length; c++) {
			if (c==nr)
				characters[nr].SetActive(true);
			else
				characters[c].SetActive(false);
		}
		
		// Apply new character to relevant controllers
		SmoothFollowCamera2 sf = cameraController.GetComponent(typeof(SmoothFollowCamera2)) as SmoothFollowCamera2;
		sf.target = characters[activeCharacter];
		if (showGhost)
			sf.offset = Vector3.right*-characterBounds[activeCharacter].magnitude/2;
		else sf.offset = Vector3.zero; 
		sf.distance = characterBounds[activeCharacter].magnitude*(showGhost ? 1.8f : 1.2f );
		sc.character = characters[activeCharacter];
		
		// Apply position to character
		characters[activeCharacter].transform.position =
			characters[oldCharacter].transform.position
			+Vector3.up*(initHeights[activeCharacter]-initHeights[oldCharacter]);
		
		if (clones.Count>0) {
			for (int c=1; c<clones.Count; c++) {
				Destroy(clones[c]);
			}
		}
		clones.Clear();
		clones.Add(characters[activeCharacter]);
		
		if (ghost!=null) {
			Destroy(ghost);
			ghost = null;
		}
		
		if (showGhost) {
			// Add ghost for character
			ghost = (GameObject)Instantiate(characters[activeCharacter]);
			
			// Remove all unneccasary components from ghost
			Component[] components;
			int tries = 0;
			do {
				components = ghost.GetComponents(typeof(Component));
				foreach (Component component in components) {
					Type type = component.GetType();
					if (
						type!=typeof(Transform)
						&& type!=typeof(Animation)
						&& (
							(tries>0)
							|| (
								type!=typeof(LegController)
								&& type!=typeof(CharacterController)
								&& type!=typeof(AlignmentTracker)
							)
						)
					) Destroy(component);
				}
				tries++;
			} while (components.Length>2 && tries<2);
			
			// Add ghost scrip to ghost
			GhostOriginal ghostComponent = ghost.AddComponent(typeof(GhostOriginal)) as GhostOriginal;
			ghostComponent.character = characters[activeCharacter];
			ghostComponent.offset = new Vector3(-characterBounds[activeCharacter].magnitude,0,0);
			
			LegAnimator LegA = characters[activeCharacter].GetComponent(typeof(LegAnimator)) as LegAnimator;
			LegA.ghost = ghost;
		}
	}
	
	void WindowFunction (int id) {
		// Draw any Controls inside the window here
		
		// Options
		GUILayout.BeginHorizontal();
		
		GUILayout.BeginVertical();
			bool toggleOrig;
			
			GUILayout.Label ("Automatic Demo:");
			
			toggleOrig = autoWalk;
			autoWalk = GUILayout.Toggle (autoWalk, "Random Walking");
			if (toggleOrig != autoWalk) ToggleAutoWalk(autoWalk);
			
			toggleOrig = autoSwitchCharacter;
			autoSwitchCharacter = GUILayout.Toggle (autoSwitchCharacter, "Variate Character + Cam");
			if (toggleOrig != autoSwitchCharacter) ToggleAutoSwitch(autoSwitchCharacter);
			
			toggleOrig = sc.alternate;
			sc.alternate = GUILayout.Toggle (sc.alternate, "Variate Terrain");
			if (toggleOrig != sc.alternate) ToggleVariateTerrain(sc.alternate);
			
			GUILayout.Label ("Visualization:");
			
			toggleOrig = showGhost;
			showGhost = GUILayout.Toggle (showGhost, "Render Animation Ghost");
			if (toggleOrig != showGhost) {
				switchCharacter = true;
			}
			
			toggleOrig = renderFootMarkers;
			renderFootMarkers = GUILayout.Toggle (renderFootMarkers, "Render Foot Markers");
			if (toggleOrig != renderFootMarkers) ToggleRenderFootMarkers(renderFootMarkers);
			
			toggleOrig = renderCycleGraph;
			renderCycleGraph = GUILayout.Toggle (renderCycleGraph, "Render Cycle Graph");
			if (toggleOrig != renderCycleGraph) ToggleRenderCycleGraph(renderCycleGraph);
			
			toggleOrig = renderBlendingGraph;
			renderBlendingGraph = GUILayout.Toggle (renderBlendingGraph, "Render Blending Graph");
			if (toggleOrig != renderBlendingGraph) ToggleRenderBlendingGraph(renderBlendingGraph);
			
			toggleOrig = renderAnimationStates;
			renderAnimationStates = GUILayout.Toggle (renderAnimationStates, "Render Animation States");
			if (toggleOrig != renderAnimationStates) ToggleRenderAnimationStates(renderAnimationStates);
			
		GUILayout.EndVertical();
		
		GUILayout.BeginVertical();
			
			GUILayout.Label ("Characters: "+(showClones+1));
			showClones = (int)Mathf.Round(GUILayout.HorizontalSlider(showClones, 0, 19));
			
			GUILayout.Label ("Time Scale: "+Time.timeScale.ToString(".##"));
			float timeScale = Mathf.Log(Time.timeScale,2);
			timeScale = GUILayout.HorizontalSlider(timeScale, -3f, 3f);
			if (timeScale>-0.1f && timeScale<0.1f) timeScale = 0;
			Time.timeScale = Mathf.Pow(2,timeScale);
			
			GUILayout.Label ("Other Options:");
			
			toggleOrig = disableLocomotion;
			disableLocomotion = GUILayout.Toggle (disableLocomotion, "Disable Locomotion");
			if (toggleOrig != disableLocomotion) ToggleDisableLocomotion(disableLocomotion);
			
		GUILayout.EndVertical();
		
		GUILayout.EndHorizontal();
	}
	
	void OnGUI () {
		GUI.Label (new Rect(5, 5, 90, 20), "Step height:");
		sc.step = GUI.HorizontalSlider(new Rect(80, 10, 100, 15), sc.step, -0.5f, 0.5f);
		if (Mathf.Abs(sc.step)<0.05f) sc.step = 0.0f;
		GUI.Label (new Rect(195, 5, 90, 20), "Random:");
		sc.stepJitter = GUI.HorizontalSlider(new Rect(260, 10, 50, 15), sc.stepJitter, 0.0f, 1.0f);

		GUI.Label (new Rect(5, 25, 90, 20), "Step slope:");
		sc.slope = GUI.HorizontalSlider(new Rect(80, 30, 100, 15), sc.slope, -0.5f, 0.5f);
		if (Mathf.Abs(sc.slope)<0.05f) sc.slope = 0.0f;
		GUI.Label (new Rect(195, 25, 90, 20), "Random:");
		sc.slopeJitter = GUI.HorizontalSlider(new Rect(260, 30, 50, 15), sc.slopeJitter, 0.0f, 1.0f);
		
		
		bool temp;
		GUI.Label (new Rect(328, 5, 90, 20), "Auto-Demo:");
		
		bool demoOn = (autoSwitchCharacter && autoWalk && sc.alternate);
		temp = demoOn;
		demoOn = GUI.Toggle (new Rect(325, 25, 35, 15), demoOn, "On");
		if (demoOn && !temp) {
			ToggleAutoSwitch(true);
			ToggleAutoWalk(true);
			ToggleVariateTerrain(true);
		}
		
		bool demoOff = (!autoSwitchCharacter && !autoWalk && !sc.alternate);
		temp = demoOff;
		demoOff = GUI.Toggle(new Rect(360, 25, 40, 15), demoOff, "Off");
		if (demoOff && !temp) {
			ToggleAutoSwitch(false);
			ToggleAutoWalk(false);
			ToggleVariateTerrain(false);
		}
		
		// OPTION button and character change buttons
		GUILayout.BeginArea (new Rect (5, Screen.height-25, 500, 25));
		GUILayout.BeginHorizontal();
			if (GUILayout.Button("OPTIONS", GUILayout.Width(80))) {
				showOptions = !showOptions;
			}
			if (showOptions) {
				for (int c=0; c<characters.Length; c++) {
					if (GUILayout.Button(characters[c].name, GUILayout.Width(60))) {
						switchCharacter = true;
						activeCharacter = c;
						showOptions = false;
					}
				}
			}
		GUILayout.EndHorizontal();
		GUILayout.EndArea();
		
		if (showOptions) {
			GUI.Window (0, new Rect (5, Screen.height-280, 180*2, 280-30), WindowFunction, "Options");
		}
	}
	
	private void ToggleAutoWalk(bool state) {
		autoWalk = state;
		for (int c=0; c<characters.Length; c++) {
			WanderingAICharacterController comp = characters[c].GetComponent(typeof(WanderingAICharacterController)) as WanderingAICharacterController;
			comp.enabled = state;
			PlatformCharacterController comp2 = characters[c].GetComponent(typeof(PlatformCharacterController)) as PlatformCharacterController;
			comp2.enabled = !state;
		}
		if (state==false) ToggleAutoSwitch(false);
	}
	
	private void ToggleRenderFootMarkers(bool state) {
		for (int c=0; c<characters.Length; c++) {
			LegAnimator comp = characters[c].GetComponent(typeof(LegAnimator)) as LegAnimator;
			comp.renderFootMarkers = state;
		}
	}
	
	private void ToggleRenderBlendingGraph(bool state) {
		for (int c=0; c<characters.Length; c++) {
			LegAnimator comp = characters[c].GetComponent(typeof(LegAnimator)) as LegAnimator;
			comp.renderBlendingGraph = state;
		}
	}
	
	private void ToggleRenderCycleGraph(bool state) {
		for (int c=0; c<characters.Length; c++) {
			LegAnimator comp = characters[c].GetComponent(typeof(LegAnimator)) as LegAnimator;
			comp.renderCycleGraph = state;
		}
	}
	
	private void ToggleRenderAnimationStates(bool state) {
		for (int c=0; c<characters.Length; c++) {
			LegAnimator comp = characters[c].GetComponent(typeof(LegAnimator)) as LegAnimator;
			comp.renderAnimationStates = state;
		}
	}
	
	private void ToggleVariateTerrain(bool state) {
		sc.alternate = state;
		if (sc.alternate==false) {
			sc.slope = 0;
			sc.slopeJitter = 0;
			sc.step = 0;
			sc.stepJitter = 0;
		}
	}
	
	private void ToggleAutoSwitch(bool state) {
		autoSwitchCharacter = state;
		autoSwitchCharacterTimer = autoSwitchCharacterTime;
	}
	
	private void ToggleDisableLocomotion(bool state) {
		disableLocomotion = state;
		for (int c=0; c<clones.Count; c++) {
			LegAnimator legA = clones[c].GetComponent(typeof(LegAnimator)) as LegAnimator;
			legA.enabled = !state;
			HeadLookController head = clones[c].GetComponent(typeof(HeadLookController)) as HeadLookController;
			head.enabled = !state;
		}
	}
}
