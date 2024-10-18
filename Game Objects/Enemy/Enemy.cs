using Godot;
using System;
using System.Diagnostics;

// A cut down version of the player class that includes a Detection Box for player detecting aswell as a ray-cast for range determination.
// Also replaces the player input with behavior methods that determine whether an action should be performed or not.
// Has all the features of the player class.

public partial class Enemy : Player
{
	public new int MaxHP = 450;
	public new int HP = 450;

	Player player_ref = null; // Player will be stored after detection.

	Area2D detection_box; // This box will be used to detect the player.
	RayCast2D proximity_detection; // This raycast is used to determine whether the player is in-range to start attacking.

	Timer attackCooldown;

	bool attack_cooldown_expired = true;

	public override void _Ready()
	{
		base._Ready();

		detection_box = GetNode<Area2D>("Pivot/DetectionBox");
		proximity_detection = GetNode<RayCast2D>("Pivot/ProximityDetection");

		attackCooldown = GetNode<Timer>("AttackCooldown");

		detection_box.BodyEntered += (Node2D player) => HandleDetection(player);

		attackCooldown.Timeout += () => attack_cooldown_expired = true;

		// Reset HP based on new values.
		
		// Initialize HUD Elements
		hud_hp.MaxValue = MaxHP;
		hud_hp.Value = HP;
		hud_hp.InitializeAnimatedBar();
	}

	protected override void SimulateStuff() {
		if(Input.IsActionJustPressed("jump")) {
			// Simulate a Hit;
			// HandleHurtboxInteraction(hitbox);
			
			// Simulate a Parry;
			// HandleGuardboxInteraction(hitbox);

			// Simulate a Guard;
			// guard_frames = ParryFrames + 1;
			// HandleGuardboxInteraction(hitbox);
		}
	}

	protected override void Movement(double delta)
	{
		// Create a copy of the global Velocity vector.
		Vector2 velocity = Velocity;

		// Get the movement direction by subtracting the opposite axis. 1 = Right. -1 = Left. 0 = Idle. In-between values exist for smaller motion.
		float direction = GetMovementDirection();

		if(direction != 0 && CanMove() && ShouldMove()) {
			// Flip the Sprite based on Facing Direction
			pivot.Scale = new Vector2(direction >= 0 ? 1 : -1, 1);

			// Lerp Velocity using the MotionLerpWeight and Delta. This will smoothly increase / decrease the velocity and imitate acceleration without actually using acceleration.
			velocity.X = Mathf.Lerp(velocity.X, MaxSpeed * direction, MotionLerpWeight * (float)delta);
		} else {
			velocity.X = Mathf.Lerp(velocity.X, 0, MotionLerpWeight * (float)delta);
		}

		// Only play movement animations if Attack animations are not playing.
		if(!IsAttacking() && !staggered) {
			// Play running animation if inputs are held down. Else play Idle animation. Also change the speed of the animation depending on axis.
			if(direction != 0 && ShouldMove()) {
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
	protected override void Attacks() {
		if(ShouldAttack() && CanAttack()) {
			// Change the Speed of the Attacking Animation (depending on exported variable)
			SetAttackSpeedScale();

			// Play Attack #1 if any animation is playing.
			if(animationState.GetCurrentNode() != "Attack_1") {
				animationState.Travel("Attack_1");

			// Play Attack #2 only if Attack #1 is playing.
			} else {
				animationState.Travel("Attack_2");
				attack_cooldown_expired = false;
				attackCooldown.Start();
			}
		}
	}

	// Controls the Guarding State. Responsible for activating and deactivating the guard animation and state.
	protected override void Guarding() {
		// Activate Guarding Hitbox and Start counting frames when initially started guarding.
		if(ShouldGuard() && !IsAttacking() && !staggered) {
			ActivateGuardbox();
			DeactivateHurtbox();
			animationState.Travel("Guard");

			// Just started guarding;
			if(guard_frames == 0) {
				sfx_guard_up.PitchScale = GD.Randf() % 1.1f + 0.9f;
				sfx_guard_up.Play();
			}

			// Count guarding frames up to ParryFrames + 1. This limit exists only so that the number doesn't count up indefinitely.
			// To differentiate between a Parry and a Block, we just need to test whether guard_frames is less than equal to ParryFrames.
			// Allowing the counting to go up to ParryFrames + 1 lets us determine it's a block as long as it's 1 frame higher than ParryFrames without the need for more counting.
			if(guard_frames <= ParryFrames)
				guard_frames += 1;

			if(guard_frames <= ParryFrames)
				hud_parry_timing.Value = guard_frames;
			else
				hud_parry_timing.Value = 0;
		}

		// Reset Guard Frames upon release and disable guard hitbox;
		if(guard_frames != 0 && !ShouldGuard()) {
			ActivateHurtbox();
			DeactivateGuardbox();
			guard_frames = 0;
			guardbox.Monitoring = false;
			hud_parry_timing.Value = 0;
		}
	}

	// Controls the Rolling State.
	protected override void Rolling(double delta) {
		if(ShouldRoll() && CanRoll()) {
			animationState.Travel("Roll");
		}

		// Move Automatically if Rolling.
		if(IsRolling()) {
			AutomaticMovement(RollSpeed, delta);
		}
	}

	void HandleDetection(Node2D player) {
		if(player is not Player)
			return;

		player_ref = (Player)player;
	}

	/*
	 * Behavior Methods
	 *
	 * These can be modified to allow the enemy to perform various actions based on conditions. They are essentially inputs through code.
	 */

	float GetMovementDirection() {
		if(player_ref == null || proximity_detection.IsColliding())
			return 0.0f;

		var player_pos = player_ref.GlobalPosition;

		if(player_pos.X > GlobalPosition.X) {
			return +1.0f;
		} else {
			return -1.0f;
		}
	}

	bool ShouldMove() {
		return player_ref != null && !proximity_detection.IsColliding();
	}

	bool ShouldAttack() {
		return proximity_detection.IsColliding() && attack_cooldown_expired;
	}

	bool ShouldRoll() {
		return false;
	}

	bool ShouldGuard() {
		return false;
	}
}
