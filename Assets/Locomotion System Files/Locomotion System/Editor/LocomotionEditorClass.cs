/*
Copyright (c) 2008, Rune Skovbo Johansen & Unity Technologies ApS

See the document "TERMS OF USE" included in the project folder for licencing details.
*/
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

class InspectorAnimationGroup {
	public string name;
	public bool expanded = true;
	public List<MotionAnalyzer> motions = new List<MotionAnalyzer>();
	
	public InspectorAnimationGroup (string name) {
		this.name = name;
	}
}

[CustomEditor(typeof(LegController))]
class LocomotionEditorClass : Editor {
	
	List<bool> legFoldouts = new List<bool>();
	bool legsFoldout = false;
	bool animsFoldout = false;
	List<MotionAnalyzer> selectedMotions = new List<MotionAnalyzer>();
	List<MotionAnalyzer> expandedMotions = new List<MotionAnalyzer>();
	List<InspectorAnimationGroup> groups = null;
	bool rebuildGroups = true;
	LegController lc;
	
	public void OnEnable () {
		lc = target as LegController;
	}
	
	public override void OnInspectorGUI () {
		if (!lc)
			return;
		
		EditorGUIUtility.labelWidth = 100;
		
		GUI.changed = false;
		lc.groundPlaneHeight = EditorGUILayout.FloatField("Ground Height", lc.groundPlaneHeight);
		lc.groundedPose = EditorGUILayout.ObjectField("Grounded Pose", lc.groundedPose, typeof(AnimationClip), false) as AnimationClip;
		lc.rootBone = EditorGUILayout.ObjectField("Root Bone", lc.rootBone, typeof(Transform), false) as Transform;
		if (GUI.changed)
			lc.initialized = false;
		
		EditorGUILayout.Space();
		
		LegSectionGUI();
		
		AnimationSectionGUI();
		
		EditorGUILayout.BeginHorizontal();
		GUILayout.Label("Status: "+(lc.initialized ? "Initialized" : "Not initialized"), GUILayout.ExpandWidth(true));
		if (GUILayout.Button("Initialize")) {
			lc.Init();
			
			bool success = true;
			foreach (MotionAnalyzer analyzer in lc.sourceAnimations) {
				if (!SanityCheckAnimationCurves(lc,analyzer.animation)) success = false;
			}
			if (success)
				lc.Init2();
		}
		EditorGUILayout.EndHorizontal();
	}
	
	void LegSectionGUI () {
		// Handle legs array
		
		if (lc.legs == null)
			lc.legs = new LegInfo[0];
		List<LegInfo> legs = new List<LegInfo>(lc.legs);
		if (legs.Count != legFoldouts.Count)
			legFoldouts = new List<bool>(new bool[legs.Count]);
		
		legsFoldout = EditorGUILayout.Foldout(legsFoldout, "Legs");
		if (legsFoldout) {
			int removeIndex = -1;
			for (int l=0; l<legs.Count; l++) {
				EditorGUIUtility.labelWidth = 50;
				GUILayout.BeginHorizontal();
				string str = "Leg " + (l+1) + (legs[l].hip != null ? " (" + legs[l].hip.name + ")" : "");
				legFoldouts[l] = EditorGUILayout.Foldout(legFoldouts[l], str);
				if (GUILayout.Button("Remove", GUILayout.Width(80)))
					removeIndex = l;
				GUILayout.EndHorizontal();
				
				if (legFoldouts[l]) {
					GUI.changed = false;
					
					EditorGUI.indentLevel++;
					
					LegInfo li = legs[l];
					li.hip = EditorGUILayout.ObjectField("Hip", li.hip, typeof(Transform), true) as Transform;
					li.ankle = EditorGUILayout.ObjectField("Ankle", li.ankle, typeof(Transform), true) as Transform;
					li.toe = EditorGUILayout.ObjectField("Toe", li.toe, typeof(Transform), true) as Transform;
					
					GUILayout.BeginHorizontal();
					EditorGUILayout.PrefixLabel("Foot");
					EditorGUI.indentLevel--;
					EditorGUIUtility.labelWidth = 45;
					EditorGUIUtility.fieldWidth = 0;
					GUILayout.BeginVertical();
					li.footWidth = EditorGUILayout.FloatField("Width", li.footWidth);
					li.footLength = EditorGUILayout.FloatField("Length", li.footLength);
					GUILayout.EndVertical();
					GUILayout.BeginVertical();
					li.footOffset.x = EditorGUILayout.FloatField("Offset", li.footOffset.x);
					li.footOffset.y = EditorGUILayout.FloatField("Offset", li.footOffset.y);
					GUILayout.EndVertical();
					GUILayout.EndHorizontal();
					
					if (GUI.changed) {
						SceneView.RepaintAll();
						lc.initialized = false;
					}
					
					EditorGUILayout.Space();
				}
			}
			
			// Not Used a leg?
			if (removeIndex >= 0) {
				legs.RemoveAt(removeIndex);
				lc.initialized = false;
				legFoldouts.RemoveAt(removeIndex);
			}
			
			// Add a leg?
			GUILayout.BeginHorizontal();
			GUILayout.Label("");
			if (GUILayout.Button("Add Leg", GUILayout.Width(80))) {
				LegInfo li = new LegInfo();
				if (legs.Count > 0) {
					li.footWidth = legs[legs.Count-1].footWidth;
					li.footLength = legs[legs.Count-1].footLength;
					li.footOffset = legs[legs.Count-1].footOffset;
				}
				legs.Add(li);
				lc.initialized = false;
				legFoldouts.Add(true);
			}
			GUILayout.EndHorizontal();
			
			lc.legs = legs.ToArray();
			
			EditorGUIUtility.labelWidth = 100;
		}
		
		EditorGUILayout.Space();
	}
	
	void AnimationSectionGUI () {
		// Handle animations array
		
		bool changed = false;
		
		animsFoldout = EditorGUILayout.Foldout(animsFoldout, "Source Animations");
		if (animsFoldout) {
			EditorGUIUtility.labelWidth = 160;
			
			if (rebuildGroups || groups == null) {
				rebuildGroups = false;
				
				bool noGroupsAreUsed = true;
				
				if (groups == null) {
					groups = new List<InspectorAnimationGroup>();
					groups.Add(new InspectorAnimationGroup("No Change"));
					groups.Add(new InspectorAnimationGroup(""));
					groups.Add(new InspectorAnimationGroup("Not Used"));
				}
				else {
					for (int i=0; i<groups.Count; i++)
						groups[i].motions.Clear();
				}
				
				InspectorAnimationGroup unusedGroup = groups[groups.Count-1];
				List<AnimationClip> unusedClips = new List<AnimationClip>(AnimationUtility.GetAnimationClips(lc.gameObject));
				
				if (lc.sourceAnimations == null)
					lc.sourceAnimations = new MotionAnalyzer[0];
				for (int m=0; m<lc.sourceAnimations.Length; m++) {
					MotionAnalyzer ma = lc.sourceAnimations[m];
					noGroupsAreUsed = false;
					
					if (unusedClips.Contains(ma.animation))
						unusedClips.Remove(ma.animation);
					
					if (ma.motionGroup == null)
						ma.motionGroup = "";
					
					bool found = false;
					for (int g=0; g<groups.Count; g++) {
						if (ma.motionGroup == groups[g].name) {
							groups[g].motions.Add(ma);
							found = true;
						}
					}
					if (!found) {
						InspectorAnimationGroup group = new InspectorAnimationGroup(ma.motionGroup);
						group.motions.Add(ma);
						groups.Insert(groups.Count-1, group);
					}
				}
				
				// Populate group of unused motions
				if (unusedClips.Count != unusedGroup.motions.Count) {
					selectedMotions.Clear();
					for (int i=0; i<unusedClips.Count; i++) {
						MotionAnalyzer ma = new MotionAnalyzer();
						ma.animation = unusedClips[i];
						unusedGroup.motions.Add(ma);
					}
				}
				
				// Check for empty groups
				for (int i=2; i<groups.Count-1; i++) {
					if (groups[i].motions.Count == 0 || groups[i].name == "") {
						groups.Remove(groups[i]);
						i--;
					}
				}
				
				// Only have unused group expanded by default if no other groups have any animations in them
				if (!noGroupsAreUsed) {
					groups[groups.Count-1].expanded = false;
				}
			}
			
			string[] groupNames = new string[groups.Count+1];
			for (int g=0; g<groups.Count; g++) {
				groupNames[g] = groups[g].name;
			}
			groupNames[1] = "Ungrouped";
			groupNames[groupNames.Length-2] = "New Group";
			groupNames[groupNames.Length-1] = "Not Used";
			
			for (int g=1; g<groups.Count; g++) {
				InspectorAnimationGroup group = groups[g];
				bool used = true;
				
				if (g==1 && group.motions.Count == 0)
					continue;
				
				GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
				group.expanded = GUILayout.Toggle(group.expanded, "", EditorStyles.foldout, GUILayout.ExpandWidth(false));
				if (g==1) {
					GUILayout.Label("Ungrouped Animations", GUILayout.ExpandWidth(true));
				}
				else if (group.name == "Not Used") {
					GUILayout.Label("Not Used (by Locomotion System)", GUILayout.ExpandWidth(true));
					used = false;
				}
				else {
					GUILayout.Label("Motion Group:", GUILayout.ExpandWidth(false));
					string newName = GUILayout.TextField(group.name, GUILayout.ExpandWidth(true));
					if (newName != group.name) {
						group.name = newName;
						changed = true;
					}
				}
				if (GUILayout.Button("", "toggle", GUILayout.ExpandWidth(false))) {
					bool allWasSelected = true;
					for (int m=0; m<group.motions.Count; m++) {
						if (!selectedMotions.Contains(group.motions[m])) {
							selectedMotions.Add(group.motions[m]);
							allWasSelected = false;
						}
					}
					if (allWasSelected) {
						for (int m=0; m<group.motions.Count; m++) {
							if (selectedMotions.Contains(group.motions[m])) {
								selectedMotions.Remove(group.motions[m]);
							}
						}
					}
				}
				GUILayout.EndHorizontal();
				
				if (group.expanded) {
					EditorGUI.indentLevel++;
					
					for (int m=0; m<group.motions.Count; m++) {
						MotionAnalyzer ma = group.motions[m];
						
						GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
						
						GUILayout.Space(15);
						
						// Foldout
						bool expanded = false;
						if (used) {
							bool expandedOld = expandedMotions.Contains(ma);
							expanded = GUILayout.Toggle(expandedOld, "", EditorStyles.foldout, GUILayout.ExpandWidth(false));
							if (expanded != expandedOld) {
								if (expanded)
									expandedMotions.Add(ma);
								else
									expandedMotions.Remove(ma);
							}
							
							GUI.changed = false;
							ma.animation = EditorGUILayout.ObjectField(ma.animation, typeof(AnimationClip), false, GUILayout.ExpandWidth(true)) as AnimationClip;
							ma.motionType = (MotionType)EditorGUILayout.EnumPopup(ma.motionType, GUILayout.Width(70));
							if (GUI.changed)
								changed = true;
						}
						else {
							GUI.enabled = false;
							GUILayout.Toggle(false, "", EditorStyles.foldout, GUILayout.ExpandWidth(false));
							EditorGUILayout.ObjectField(ma.animation, typeof(AnimationClip), false, GUILayout.ExpandWidth(true));
							GUI.enabled = true;
							GUILayout.Space(70+4);
						}
						
						// Selection
						bool selectedOld = selectedMotions.Contains(ma);
						bool selected = GUILayout.Toggle(selectedOld, "", GUILayout.ExpandWidth(false));
						if (selected != selectedOld) {
							if (selected)
								selectedMotions.Add(ma);
							else
								selectedMotions.Remove(ma);
						}
						
						GUILayout.EndHorizontal();
						
						if (expanded) {
							GUI.changed = false;
							EditorGUI.indentLevel += 2;
							ma.alsoUseBackwards = EditorGUILayout.Toggle("Also Use Backwards", ma.alsoUseBackwards);
							ma.fixFootSkating = EditorGUILayout.Toggle("Fix Foot Skating", ma.fixFootSkating);
							EditorGUILayout.LabelField("Native Speed", ""+ma.nativeSpeed);
							EditorGUI.indentLevel -= 2;
							if (GUI.changed)
								changed = true;
						}
					}
					EditorGUI.indentLevel--;
					
					EditorGUILayout.Space();
				}
			}
			
			EditorGUIUtility.labelWidth = 120;
			
			// Apply changes to selections
			bool clearInspectorGroups = false;
			GUI.enabled = (selectedMotions.Count > 0);
			int selectedGroup = EditorGUILayout.Popup("Move Selected To", 0, groupNames);
			if (selectedGroup != 0) {
				for (int i=0; i<selectedMotions.Count; i++) {
					MotionAnalyzer ma = selectedMotions[i];
					for (int j=0; j<groups.Count; j++) {
						if (groups[j].motions.Contains(ma)) {
							groups[j].motions.Remove(ma);
						}
					}
					
					// Add to unused
					if (selectedGroup == groupNames.Length-1) {
						groups[selectedGroup-1].motions.Add(ma);
					}
					// Add to new group
					else if (selectedGroup == groupNames.Length-2) {
						string newName = "MotionGroup";
						bool exists = true;
						int c = 1;
						while (exists) {
							newName = "MotionGroup" + c;
							exists = false;
							for (int j=0; j<groups.Count; j++) {
								if (groups[j].name == newName)
									exists = true;
							}
							c++;
						}
						groups.Insert(groups.Count-1, new InspectorAnimationGroup(newName));
						groups[selectedGroup].motions.Add(ma);
					}
					// Add to selected group
					else {
						groups[selectedGroup].motions.Add(ma);
					}
				}
				selectedMotions.Clear();
				clearInspectorGroups = true;
				lc.initialized = false;
				changed = true;
			}
			GUI.enabled = true;
			
			if (changed) {
				List<MotionAnalyzer> motions = new List<MotionAnalyzer>();
				for (int g=0; g<groups.Count-1; g++) {
					for (int m=0; m<groups[g].motions.Count; m++) {
						groups[g].motions[m].motionGroup = groups[g].name;
						motions.Add(groups[g].motions[m]);
					}
				}
				lc.sourceAnimations = motions.ToArray();
				lc.initialized = false;
			}
			
			if (clearInspectorGroups)
				rebuildGroups = true;
		}
		
		EditorGUILayout.Space();
	}
	
	void OnSceneGUI () {
		if (Application.isPlaying || AnimationMode.InAnimationMode())
			return;
		
		Vector3 up = lc.transform.up;
		Vector3 forward = lc.transform.forward;
		Vector3 right = lc.transform.right;
		
		// Draw cross signifying the Ground Plane Height
		Vector3 groundCenter = (
			lc.transform.position
				+ lc.groundPlaneHeight * up * lc.transform.lossyScale.y
		);
		Handles.color = (Color.green+Color.white)/2;
		Handles.DrawLine(groundCenter-forward, groundCenter+forward);
		Handles.DrawLine(groundCenter-right, groundCenter+right);
		
		// Draw rect showing foot boundaries
		if (lc.groundedPose==null) return;
		float scale = lc.transform.lossyScale.z;
		for (int leg=0; leg<lc.legs.Length; leg++) {
			if (lc.legs[leg].ankle==null) continue;
			if (lc.legs[leg].toe==null) continue;
			if (lc.legs[leg].footLength+lc.legs[leg].footWidth==0) continue;
			lc.InitFootData(leg); // Note: Samples animation
			Vector3 heel = lc.legs[leg].ankle.TransformPoint(lc.legs[leg].ankleHeelVector);
			Vector3 toetip = lc.legs[leg].toe.TransformPoint(lc.legs[leg].toeToetipVector);
			Vector3 side = (Quaternion.AngleAxis(90,up) * (toetip-heel)).normalized * lc.legs[leg].footWidth * scale;
			Handles.DrawLine(heel+side/2, toetip+side/2);
			Handles.DrawLine(heel-side/2, toetip-side/2);
			Handles.DrawLine(heel-side/2, heel+side/2);
			Handles.DrawLine(toetip-side/2, toetip+side/2);
		}
	}
	
	private static bool SanityCheckAnimationCurves(LegController legC, AnimationClip animation) {
		EditorCurveBinding[] curveData = AnimationUtility.GetCurveBindings (animation);
		
		bool hasRootPosition = false;
		bool hasRootRotation = false;
		
		// Check each joint from hip to ankle in each leg
		bool[][] hasJointRotation = new bool[legC.legs.Length][];
		for (int i=0; i<legC.legs.Length; i++) {
			hasJointRotation[i] = new bool[legC.legs[i].legChain.Length];
		}

		foreach (EditorCurveBinding data in curveData) {
			Transform bone = legC.transform.Find(data.path);
			if (bone==legC.root && data.propertyName=="m_LocalPosition.x") hasRootPosition = true;
			if (bone==legC.root && data.propertyName=="m_LocalRotation.x") hasRootRotation = true;
			for (int i=0; i<legC.legs.Length; i++) {
				for (int j=0; j<legC.legs[i].legChain.Length; j++) {
					if (bone==legC.legs[i].legChain[j] &&  data.propertyName=="m_LocalRotation.x") {
						hasJointRotation[i][j] = true;
					}
				}
			}
		}
		
		bool success = true;
		
		if (!hasRootPosition) {
			Debug.LogError("AnimationClip \""+animation.name+"\" is missing animation curve for the position of the root bone \""+legC.root.name+"\".");
			success = false;
		}
		if (!hasRootRotation) {
			Debug.LogError("AnimationClip \""+animation.name+"\" is missing animation curve for the rotation of the root bone \""+legC.root.name+"\".");
			success = false;
		}
		for (int i=0; i<legC.legs.Length; i++) {
			for (int j=0; j<legC.legs[i].legChain.Length; j++) {
				if (!hasJointRotation[i][j]) {
					Debug.LogError("AnimationClip \""+animation.name+"\" is missing animation curve for the rotation of the joint \""+legC.legs[i].legChain[j].name+"\" in leg "+i+".");
					success = false;
				}
			}
		}
		
		return success;
	}
}
