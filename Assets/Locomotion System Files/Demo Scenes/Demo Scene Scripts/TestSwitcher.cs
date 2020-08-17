using UnityEngine;
using System.Collections;

public class TestSwitcher : MonoBehaviour {
	
	public GameObject[] characters;

	[Space]
	public Material vertexColorMaterial;

	private GameObject currentCharacter;
	private AnimationClip currentAnimation;
	private LegController legC;
	private MotionAnalyzer analyzer;
	
	private int selectedCharacter;
	private int selectedAnimation;
	private string[] optionsCharacter;
	private string[] optionsAnimation;
	private string[] optionsLeg;
	
	// Use this for initialization
	void Start() {
		for (int c=0; c<characters.Length; c++) {
			characters[c].SetActive(false);
		}
		
		selectedCharacter = 0;
		selectedAnimation = 0;
		opt.currentLeg = 0;
		UpdateOptions();
	}
	
	private void UpdateOptions () {
		// Activate character
		currentCharacter = characters[selectedCharacter];
		legC = currentCharacter.GetComponent(typeof(LegController)) as LegController;
		currentCharacter.SetActive(true);
		
		// Activate animation
		MotionAnalyzer newAnalyzer = legC.motions[selectedAnimation] as MotionAnalyzer;
		if (newAnalyzer!=null) analyzer = newAnalyzer;//(MotionAnalyzer)legC.motions[selectedAnimation];
		else return;
		currentAnimation = analyzer.animation;
		StartAnimation();
		
		optionsCharacter = new string[characters.Length];
		for (int i=0; i<characters.Length; i++) {
			optionsCharacter[i] = characters[i].name;
		}
		optionsAnimation = new string[legC.motions.Length];
		for (int i=0; i<legC.motions.Length; i++) {
			MotionAnalyzer newMotion = legC.motions[i] as MotionAnalyzer;
			if (newMotion!=null) optionsAnimation[i] = legC.motions[i].animation.name;
			else optionsAnimation[i] = "N/A";
		}
		optionsLeg = new string[legC.legs.Length];
		for (int i=0; i<legC.legs.Length; i++) {
			optionsLeg[i] = "Leg "+(i+1);
		}
	}
	
	private void StartAnimation() {
		// Start the animation.
		currentCharacter.GetComponent<Animation>().CrossFade(currentAnimation.name,0.0f);
		currentCharacter.GetComponent<Animation>()[currentAnimation.name].enabled = true;
		currentCharacter.GetComponent<Animation>()[currentAnimation.name].wrapMode = WrapMode.Loop;
		currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed = 1;
	}
	
	// Update is called once per frame
	void Update () {
		// next / previous leg
		if (Input.GetKeyDown("down")) opt.currentLeg -= 1;
		if (Input.GetKeyDown("up")) opt.currentLeg += 1;
		if (opt.currentLeg<0) opt.currentLeg += legC.legs.Length;
		if (opt.currentLeg>=legC.legs.Length) opt.currentLeg -= legC.legs.Length;
		
		// start / stop playback
		if (Input.GetKeyDown("return")) {
			if (currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed == 0) currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed = 1;
			else currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed = 0;
		}
		// next / previous sample
		if (Input.GetKeyDown("left")) {
			currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed = 0;
			currentCharacter.GetComponent<Animation>()[currentAnimation.name].normalizedTime =
				Mathf.Floor(
					currentCharacter.GetComponent<Animation>()[currentAnimation.name].normalizedTime
					*analyzer.samples-0.5f
				)/(analyzer.samples);
		}
		if (Input.GetKeyDown("right")) {
			currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed = 0;
			currentCharacter.GetComponent<Animation>()[currentAnimation.name].normalizedTime =
				Mathf.Ceil(currentCharacter.GetComponent<Animation>()[currentAnimation.name].normalizedTime*analyzer.samples+0.5f)/(analyzer.samples);
		}
		// speed up / down
		if (Input.GetKeyDown("[+]")) currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed *= 2;
		if (Input.GetKeyDown("[-]")) currentCharacter.GetComponent<Animation>()[currentAnimation.name].speed /= 2;
		
		// Wrap the time value.
		currentCharacter.GetComponent<Animation>()[currentAnimation.name].normalizedTime = Util.Mod(currentCharacter.GetComponent<Animation>()[currentAnimation.name].normalizedTime,1);
		
		if (Input.GetKeyDown(KeyCode.V)) {
			opt.isolateVertical = !opt.isolateVertical;
			if (opt.isolateVertical) opt.isolateHorisontal = false;
		}
		if (Input.GetKeyDown(KeyCode.H)) {
			opt.isolateHorisontal = !opt.isolateHorisontal;
			if (opt.isolateHorisontal) opt.isolateVertical = false;
		}
		
		opt.graphScaleH += (opt.isolateVertical   ? -Time.deltaTime : Time.deltaTime);
		opt.graphScaleV += (opt.isolateHorisontal ? -Time.deltaTime : Time.deltaTime);
		opt.graphScaleH = Mathf.Clamp01(opt.graphScaleH);
		opt.graphScaleV = Mathf.Clamp01(opt.graphScaleV);
	}
	
	void OnGUI () {
		GUI.Label (new Rect (10, 0, 260, 20), "Use enter, +/-, and arrows for time control");
		GUI.Label (new Rect (260, 0, 200, 20), string.Format("Time: {0:0.000}",currentCharacter.GetComponent<Animation>()[currentAnimation.name].normalizedTime));

		selectedCharacter = GUI.Toolbar (new Rect (10, 20, 220, 20), selectedCharacter, optionsCharacter);
		selectedAnimation = GUI.Toolbar (new Rect (10, 40, 220, 20), selectedAnimation, optionsAnimation);
		opt.currentLeg    = GUI.Toolbar (new Rect (10, 60, 220, 20), opt.currentLeg   , optionsLeg      );
		
		if (optionsCharacter[selectedCharacter]!=currentCharacter.name) {
			selectedAnimation = 0;
			opt.currentLeg = 0;
			currentCharacter.SetActive(false);
			UpdateOptions();
		}
		else if (optionsAnimation[selectedAnimation]!=currentAnimation.name) {
			opt.currentLeg = 0;
			UpdateOptions();
		}
		
		if (GUI.Button(new Rect(Screen.width-85,5,80,25), "OPTIONS")) {
			showOptions = !showOptions;
		}
		if (showOptions) {
			GUI.Window (0, new Rect (Screen.width-180-5, 30, 180, 375), WindowFunction, "Options");
		}
	}
	
	private bool showOptions = true;
	
	private MotionAnalyzerDrawOptions opt = new MotionAnalyzerDrawOptions();
	
	void WindowFunction (int id) {
		// Draw any Controls inside the window here
		
		// Options
		GUILayout.BeginVertical();
			
			bool tempValue = false;
			
			GUILayout.Label ("Visualization:");
			
			opt.drawAllFeet = GUILayout.Toggle(opt.drawAllFeet, "Show all feet");
			opt.drawHeelToe = GUILayout.Toggle(opt.drawHeelToe, "Show heel and toe");
			opt.drawFootBase = GUILayout.Toggle(opt.drawFootBase, "Show foot base");
			opt.drawFootPrints = GUILayout.Toggle(opt.drawFootPrints, "Show foot prints");
			
			// children only true if parent is
			GUILayout.Label ("Trajectories:");
			opt.drawTrajectories = GUILayout.Toggle(opt.drawTrajectories, "Show trajectories");
			opt.drawTrajectoriesProjected = GUILayout.Toggle(opt.drawTrajectoriesProjected, "   Show projected");
			opt.drawThreePoints = GUILayout.Toggle(opt.drawThreePoints, "      Show dots and axis");
			
			GUILayout.Label ("Graph:");
			
			opt.drawGraph = GUILayout.Toggle(opt.drawGraph, "Show graph");
			
			// graph toggles
			tempValue = opt.isolateHorisontal;
			opt.isolateHorisontal = GUILayout.Toggle(opt.isolateHorisontal, "   Isolate horisontal");
			if (tempValue==false && opt.isolateHorisontal==true) opt.isolateVertical = false;
			
			tempValue = opt.isolateVertical;
			opt.isolateVertical = GUILayout.Toggle(opt.isolateVertical, "   Isolate vertical");
			if (tempValue==false && opt.isolateVertical==true) opt.isolateHorisontal = false;
			
			opt.drawStanceMarkers = GUILayout.Toggle(opt.drawStanceMarkers, "   Show stance markers");
			
			opt.drawLiftedCurve = GUILayout.Toggle(opt.drawLiftedCurve, "   Show lifting curve");
			opt.drawBalanceCurve = GUILayout.Toggle(opt.drawBalanceCurve, "   Show balance curve");
			opt.normalizeGraph = GUILayout.Toggle(opt.normalizeGraph, "   Show normalized");
			
		GUILayout.EndVertical();
	}
	
	void OnRenderObject() {
		analyzer.RenderGraph(opt, vertexColorMaterial);
	}
	
}
