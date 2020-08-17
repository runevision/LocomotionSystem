using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AlignmentTracker))]
public class JumpAndIdle : MonoBehaviour {
	
	public AnimationClip jumpingAnimation;
	public float jumpTimeStart = 0.0f;
	public float fallTimeThreshold = 0.2f;
	public AnimationClip waitingAnimation;
	
	private bool doJumping = false;
	private bool doWaiting = false;
	
	//private LegAnimator legA;
	private AlignmentTracker align;
	private CharacterMotor cm;
	
	private bool grounded;
	private bool waiting = false;
	private float idleTimer = 0.0f;
	private float fallingTimer = 0.0f;
	
	// Use this for initialization
	void Start () {
		//legA = GetComponent(typeof(LegAnimator)) as LegAnimator;
		align = GetComponent(typeof(AlignmentTracker)) as AlignmentTracker;
		cm = GetComponent(typeof(CharacterMotor)) as CharacterMotor;
		grounded = false;
		
		// Only use jumping if the jumping animation has ben set
		if (jumpingAnimation!=null) {
			GetComponent<Animation>()[jumpingAnimation.name].wrapMode = WrapMode.ClampForever;
			doJumping = true;
		}
		
		// Only use idle animation if it has been set
		if (waitingAnimation!=null) {
			GetComponent<Animation>()[waitingAnimation.name].wrapMode = WrapMode.ClampForever;
			doWaiting = true;
		}
		
		// Start with locomotion
		//animation.Play("locomotion");
	}
	
	void OnEnable () {
		if (GetComponent<Animation>()["locomotion"]!=null) GetComponent<Animation>()["locomotion"].weight = 1;
	}
	
	// Update is called once per frame
	void Update () {
		float speed = align.velocity.magnitude;
		
		// CrossFade quick to jumping animation while not grounded
		if (doJumping) {
			// If the jump button has been pressed
			if (cm.jumping) {
				grounded = false;
				waiting = false;
				// Fade to jumping animation quickly
				GetComponent<Animation>().CrossFade(jumpingAnimation.name, 0.1f);
				GetComponent<Animation>()[jumpingAnimation.name].time = jumpTimeStart;
				GetComponent<Animation>()[jumpingAnimation.name].wrapMode = WrapMode.ClampForever;
			}
			// If the character has walked over a ledge and is now in air
			else if (grounded && !cm.grounded) {
				grounded = false;
				waiting = false;
			}
			// If the character has landed on the ground again
			else if (!grounded && cm.grounded) {
				grounded = true;
				waiting = false;
				fallingTimer = 0;
				// Fade to locomotion motion group quickly
				GetComponent<Animation>().CrossFade("locomotion", 0.1f);
			}
			// If the character is falling
			else if (!grounded && fallingTimer<fallTimeThreshold) {
				fallingTimer += Time.deltaTime;
				if (fallingTimer>=fallTimeThreshold) {
					// Fade to jumping motion group slowly
					GetComponent<Animation>().CrossFade(jumpingAnimation.name, 0.2f);
					GetComponent<Animation>()[jumpingAnimation.name].time = jumpTimeStart;
					GetComponent<Animation>()[jumpingAnimation.name].wrapMode = WrapMode.ClampForever;
				}
			}
		}
		
		// CrossFade to waiting animation when inactive for a little while
		if (doWaiting) {
			if (speed==0) {
				idleTimer += Time.deltaTime;
				if (idleTimer>3) {
					// if the idle animation is not in the middle of playing
					if (
						GetComponent<Animation>()[waitingAnimation.name].time==0
						|| GetComponent<Animation>()[waitingAnimation.name].time>=GetComponent<Animation>()[waitingAnimation.name].length
					) {
						// Then rewind and play it
						GetComponent<Animation>()[waitingAnimation.name].time = 0;
						GetComponent<Animation>().CrossFade(waitingAnimation.name);
						GetComponent<Animation>()[waitingAnimation.name].wrapMode = WrapMode.ClampForever;
						waiting = true;
					}
					// Don't play again for a little random while
					idleTimer = -(2+4*Random.value);
				}
			}
			// If we have started to move again
			else if (speed>0 && waiting) {
				// Crossfade to locomotion
				GetComponent<Animation>().CrossFade("locomotion");
				waiting = false;
				idleTimer = 0;
			}
		}
	}
	
}
