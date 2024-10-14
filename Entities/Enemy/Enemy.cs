/*
 * Derived from the Player Class
 * Basically a stripped down version of it.
 */

using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[ExportGroup("Basic Stats")]
	[Export]
	public int MaxHP = 1000;
	[Export]
	public int HP = 500;
	[Export] // Default Attack Animation Speed.
	public float AttackSpeedScale = 1.0f;
	[Export] // Default Running Animation Speed.
	public float RunSpeedScale = 1.0f;

	[ExportGroup("Posture System")]
	[Export]
	public int MaxPosture = 125;
	[Export]
	public float Posture = 124;
	[Export(PropertyHint.None, "Seconds in which the Posture will regenerate when at High HP (>= 65%).")]
	public float PostureRegenHigh = 5f;
	[Export(PropertyHint.None, "Seconds in which the Posture will regenerate when at Medium HP (>= 25%).")]
	public float PostureRegenMedium = 8f;
	[Export(PropertyHint.None, "Seconds in which the Posture will regenerate when at Critical HP (< 25%).")]
	public float PostureRegenCritical = 15f;
	[Export(PropertyHint.None, "Seconds will be multiplied with this value if Guard is held up.")]
	public float GuardRegenBuff = 0.5f;
	[Export(PropertyHint.None, "Time to wait before regenerating posture after an attack.")]
	public float AttackRegenDelay = 1.5f;

	[ExportGroup("Behavior")]
	[Export]
	public float MaxSpeed = 135.0f;
	[Export]
	public float MotionLerpWeight = 12.0f;

	Node2D pivot;
	Area2D hitbox;
	Area2D hurtbox;

	AnimationTree animationTree;
	AnimationNodeStateMachinePlayback animationState;

	AudioStreamPlayer2D sfx_footsteps;
	AudioStreamPlayer2D sfx_guard_up;
	AudioStreamPlayer2D sfx_block;
	OverlappingAudio sfx_parry;

	AnimatedProgressBar hud_hp;
	TextureProgressBar hud_posture;

	Timer postureRegenDelayTimer;

	bool can_regenerate_posture = true;

    public override void _Ready()
    {
		// Get references to some nodes in the scene and set up defaults;

		// Facing Direction and Hitboxes
		pivot = GetNode<Node2D>("Pivot");
		hitbox = GetNode<Area2D>("Pivot/Hitbox");
		hurtbox = GetNode<Area2D>("Pivot/Hurtbox");

		// AnimationTree and State Machine
		animationTree = GetNode<AnimationTree>("AnimationTree");
        animationState = (AnimationNodeStateMachinePlayback)GetNode<AnimationTree>("AnimationTree").Get("parameters/playback");

		// SFX Nodes
		sfx_footsteps = GetNode<AudioStreamPlayer2D>("Audio/Footsteps");

		postureRegenDelayTimer = GetNode<Timer>("PostureRegenDelay");

		// HUD Nodes
		hud_hp = GetNode<AnimatedProgressBar>("HUD/HP");
		hud_posture = GetNode<TextureProgressBar>("HUD/Posture");

		// Initialize HUD Elements
		hud_hp.MaxValue = MaxHP;
		hud_hp.Value = HP;

		hud_posture.MaxValue = MaxPosture;
		hud_posture.Value = Posture;

		// Initialize Posture Regen Delay after Attack timer; Connect Signal to ensure posture regeneration is allowed after timeout.
		postureRegenDelayTimer.WaitTime = AttackRegenDelay;
		postureRegenDelayTimer.Connect("timeout", Callable.From(() => can_regenerate_posture = true));

		// Hurtbox
		hurtbox.AreaEntered += (Area2D area) => HandleHurtboxInteraction(area);

		// Reset Defaults;
		ResetAnimationSpeedScale();
		DeactivateHitbox();
    }


	// Physics process, Runs 60 ticks per second (Defined in Project settings). Ideal for physics calculations.
	// Also ideal for Frame Based timing calculations as it runs at a constant tickrate and can be used as a timing scale.
    public override void _PhysicsProcess(double delta)
	{
		Movement(delta);
		PostureHandler(delta);
		Attacks();
	}

    private void Movement(double delta)
	{
		// Create a copy of the global Velocity vector.
		Vector2 velocity = Velocity;

		// Get the movement direction by subtracting the opposite axis. 1 = Right. -1 = Left. 0 = Idle. In-between values exist for smaller motion.
		float direction = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");

		if(direction != 0 && HitboxesDisabled()) {
			// Flip the Sprite based on Facing Direction
			pivot.Scale = new Vector2(direction >= 0 ? 1 : -1, 1);

			// Lerp Velocity using the MotionLerpWeight and Delta. This will smoothly increase / decrease the velocity and imitate acceleration without actually using acceleration.
			velocity.X = Mathf.Lerp(velocity.X, MaxSpeed * direction, MotionLerpWeight * (float)delta);
		} else {
			velocity.X = Mathf.Lerp(velocity.X, 0, MotionLerpWeight * (float)delta);
		}

		// Only play movement animations if Attack animations are not playing.
		if(NotAttacking()) {
			// Play running animation if inputs are held down. Else play Idle animation. Also change the speed of the animation depending on axis.
			if(direction != 0) {
				SetRunSpeedScale(Mathf.Abs(direction) * RunSpeedScale);
				animationState.Travel("Run");
			} else {
				animationState.Travel("Idle");
			}
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	// Simple Combo System. Only allows Attack #2 to be used if Attack #1 is used beforehand. Should be easy to extend with more attacks.
	private void Attacks() {
		if(Input.IsActionJustPressed("action_attack")) {
			// Change the Speed of the Attacking Animation (depending on exported variable)
			SetAttackSpeedScale();

			// Play Attack #1 if any animation is playing.
			if(animationState.GetCurrentNode() != "Attack_1") {
				animationState.Travel("Attack_1");

			// Play Attack #2 only if Attack #1 is playing.
			} else {
				animationState.Travel("Attack_2");
			}
		}
	}

	// Controls the regeneration of posture. As-well as determines how fast or slow posture should generate depending on the current HP.
	private void PostureHandler(double delta) {
		// Regenerate posture if allowed.
		if(can_regenerate_posture && Posture > 0) {
			// Decrease posture steadily based on Current HP.
			var hp_percentage = (HP * 1.0f / MaxHP) * 100f;
			var seconds = hp_percentage >= 75 ? PostureRegenHigh : (hp_percentage >= 25 ? PostureRegenMedium : PostureRegenCritical);

			var regenPerTick = (MaxPosture / seconds) * delta;

			Posture -= (float)regenPerTick;

			hud_posture.Value = Posture;
		}
	}

	private void HandleHurtboxInteraction(Area2D area) {
		var thing = area.GetParent();
	}

	// Check if Hitboxes are disabled.
	bool HitboxesDisabled() {
		return !hitbox.Monitoring;
	}

	// Check if Not Attacking.
	bool NotAttacking() {
		return animationState.GetCurrentNode() != "Attack_1" && animationState.GetCurrentNode() != "Attack_2";
	}

	// Play footstep sound with a random pitch.
	public void PlayFootstep() {
		sfx_footsteps.PitchScale = GD.Randf() % 1.1f + 0.9f;
		sfx_footsteps.Play();
	}

	// Activate the Attack Hitbox.
	public void ActivateHitbox() {
		hitbox.Monitoring = true;
		can_regenerate_posture = false;
	}

	// Deactivate the Attack Hitbox.
	public void DeactivateHitbox() {
		hitbox.Monitoring = false;
		ResetAnimationSpeedScale();
		postureRegenDelayTimer.Start();
	}

	// Allows the changing of attack speed. Potentially useful with an upgrade system that will improve the attack speed.
	public void SetAttackSpeedScale() {
		animationTree.Set("parameters/Attack_1/TimeScale/scale", AttackSpeedScale);
		animationTree.Set("parameters/Attack_2/TimeScale/scale", AttackSpeedScale);
	}

	// Allows the changing of running speed.
	public void SetRunSpeedScale(float scale) {
		animationTree.Set("parameters/Run/TimeScale/scale", scale);
	}

	// Reset Animation Speed Scales back to Normal.
	public void ResetAnimationSpeedScale() {
		animationTree.Set("parameters/Attack_1/TimeScale/scale", 1.0f);
		animationTree.Set("parameters/Attack_2/TimeScale/scale", 1.0f);
		animationTree.Set("parameters/Run/TimeScale/scale", RunSpeedScale);
	}
}
