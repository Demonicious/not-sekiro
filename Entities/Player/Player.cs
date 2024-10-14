using Godot;
using System;
using System.Diagnostics;

public partial class Player : CharacterBody2D
{
	[ExportGroup("Basic Stats")]
	[Export]
	public int MaxHP = 1000;
	[Export]
	public int HP = 500;
	[Export] // Amount of Parry Frames
	public int ParryFrames = 8;
	[Export] // Default Attack Animation Speed.
	public float AttackSpeedScale = 1.0f;
	[Export] // Default Running Animation Speed.
	public float RunSpeedScale = 1.0f;
	[Export]
	public float GuardDamage = 0.45f;	
	[Export]
	public float GuardPushback = 0.25f;

	[ExportGroup("Posture System")]
	[Export]
	public int MaxPosture = 125;
	[Export]
	public float Posture = 124;
	[Export]
	public float ParryPostureDamage = 0.75f;
	[Export]
	public float ParryPushback = 0.0f;
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
	public float RollSpeed = 100.0f;
	[Export]
	public float MotionLerpWeight = 12.0f;

	Node2D pivot;
	Hitbox hitbox;
	Area2D guardbox;
	Area2D hurtbox;

	Node2D vfx_hit_location;
	PackedScene vfx_hit;

	// Death Scene
	PackedScene scene_death;

	AnimationTree animationTree;
	AnimationNodeStateMachinePlayback animationState;

	AudioStreamPlayer2D sfx_footsteps;
	AudioStreamPlayer2D sfx_guard_up;
	AudioStreamPlayer2D sfx_block;
	OverlappingAudio sfx_parry;

	AnimatedProgressBar hud_hp;
	TextureProgressBar hud_posture;
	TextureProgressBar hud_parry_timing;

	Timer postureRegenDelayTimer;

	bool can_regenerate_posture = true;
	int guard_frames = 0;

	bool dead = false;

    public override void _Ready()
    {
		// Get references to some nodes in the scene and set up defaults;

		// Facing Direction and Hitboxes
		pivot = GetNode<Node2D>("Pivot");
		hitbox = GetNode<Hitbox>("Pivot/Hitbox");
		guardbox = GetNode<Area2D>("Pivot/Guardbox");
		hurtbox = GetNode<Area2D>("Pivot/Hurtbox");

		// Effects and their Spawn Points
		vfx_hit_location = GetNode<Node2D>("Pivot/EffectLocations/Hit");
		vfx_hit = ResourceLoader.Load<PackedScene>("res://Entities/Player/HitEffect.tscn");

		// Death Scene
		scene_death = ResourceLoader.Load<PackedScene>("res://Entities/Player/PlayerDeath.tscn");

		// AnimationTree and State Machine
		animationTree = GetNode<AnimationTree>("AnimationTree");
        animationState = (AnimationNodeStateMachinePlayback)GetNode<AnimationTree>("AnimationTree").Get("parameters/playback");

		// SFX Nodes
		sfx_footsteps = GetNode<AudioStreamPlayer2D>("Audio/Footsteps");
		sfx_guard_up = GetNode<AudioStreamPlayer2D>("Audio/Guardup");
		sfx_block = GetNode<AudioStreamPlayer2D>("Audio/Block");
		sfx_parry = GetNode<OverlappingAudio>("Audio/Parry");

		postureRegenDelayTimer = GetNode<Timer>("PostureRegenDelay");

		// HUD Nodes
		hud_hp = GetNode<AnimatedProgressBar>("HUD/HP");
		hud_posture = GetNode<TextureProgressBar>("HUD/Posture");
		hud_parry_timing = GetNode<TextureProgressBar>("HUD/ParryTiming");

		// Initialize HUD Elements
		hud_hp.MaxValue = MaxHP;
		hud_hp.Value = HP;
		hud_hp.InitializeAnimatedBar();

		hud_parry_timing.MaxValue = ParryFrames + 1;
		hud_parry_timing.Value = 0;

		hud_posture.MaxValue = MaxPosture;
		hud_posture.Value = Posture;

		// Initialize Posture Regen Delay after Attack timer; Connect Signal to ensure posture regeneration is allowed after timeout.
		postureRegenDelayTimer.WaitTime = AttackRegenDelay;
		postureRegenDelayTimer.Connect("timeout", Callable.From(() => can_regenerate_posture = true));

		// Hurtbox and Guardbox Signals
		hurtbox.AreaEntered += (Area2D area) => HandleHurtboxInteraction(area);
		guardbox.AreaEntered += (Area2D area) => HandleGuardboxInteraction(area);

		// Reset Defaults;
		ResetAnimationSpeedScale();
		DeactivateHitbox();
		guardbox.Monitoring = false;
    }


	// Physics process, Runs 60 ticks per second (Defined in Project settings). Ideal for physics calculations.
	// Also ideal for Frame Based timing calculations as it runs at a constant tickrate and can be used as a timing scale.
    public override void _PhysicsProcess(double delta)
	{
		Movement(delta);
		PostureHandler(delta);
		Attacks();
		Guarding();
		Rolling(delta);
		SimulateStuff();
	}

	private void SimulateStuff() {
		if(Input.IsActionJustPressed("jump")) {
			// Simulate a Hit;
			// HandleHurtboxInteraction(hitbox);
			
			// Simulate a Parry;
			HandleGuardboxInteraction(hitbox);

			// Simulate a Guard;
			// guard_frames = ParryFrames + 1;
			// HandleGuardboxInteraction(hitbox);
		}
	}

	private void Movement(double delta)
	{
		// Create a copy of the global Velocity vector.
		Vector2 velocity = Velocity;

		// Get the movement direction by subtracting the opposite axis. 1 = Right. -1 = Left. 0 = Idle. In-between values exist for smaller motion.
		float direction = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");

		if(direction != 0 && HitboxesDisabled() && !IsGuarding() && !IsHurt() && !IsRolling() && !dead) {
			// Flip the Sprite based on Facing Direction
			pivot.Scale = new Vector2(direction >= 0 ? 1 : -1, 1);

			// Lerp Velocity using the MotionLerpWeight and Delta. This will smoothly increase / decrease the velocity and imitate acceleration without actually using acceleration.
			velocity.X = Mathf.Lerp(velocity.X, MaxSpeed * direction, MotionLerpWeight * (float)delta);
		} else {
			velocity.X = Mathf.Lerp(velocity.X, 0, MotionLerpWeight * (float)delta);
		}

		// Only play movement animations if Attack animations are not playing.
		if(!IsAttacking()) {
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

	// Controls the Guarding State. Responsible for activating and deactivating the guard animation and state.
	private void Guarding() {
		// Activate Guarding Hitbox and Start counting frames when initially started guarding.
		if(Input.IsActionPressed("action_guard") && !IsAttacking()) {
			guardbox.Monitoring = true;
			animationState.Travel("Guard");

			// Just started guarding;
			if(guard_frames == 0) {
				sfx_guard_up.PitchScale = GD.Randf() % 1.05f + 0.95f;
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
		if(Input.IsActionJustReleased("action_guard")) {
			guard_frames = 0;
			guardbox.Monitoring = false;
			hud_parry_timing.Value = 0;
		}
	}

	// Controls the Rolling State.
	private void Rolling(double delta) {
		if(Input.IsActionJustPressed("action_roll") && CanRoll()) {
			animationState.Travel("Roll");
		}

		// 
		if(IsRolling()) {
			// Create a copy of the global Velocity vector.
			Vector2 velocity = Velocity;
			// Lerp Velocity using the MotionLerpWeight and Delta. This will smoothly increase / decrease the velocity and imitate acceleration without actually using acceleration.
			velocity.X = Mathf.Lerp(velocity.X, RollSpeed * pivot.Scale.X, MotionLerpWeight * (float)delta);
			Velocity = velocity;
			MoveAndSlide();
		}
	}

	// Controls the regeneration of posture. As-well as determines how fast or slow posture should generate depending on the current HP.
	private void PostureHandler(double delta) {
		// Regenerate posture if allowed.
		if(can_regenerate_posture && Posture > 0) {
			// Decrease posture steadily based on Current HP.
			var hp_percentage = (HP * 1.0f / MaxHP) * 100f;
			var seconds = hp_percentage >= 75 ? PostureRegenHigh : (hp_percentage >= 25 ? PostureRegenMedium : PostureRegenCritical);

			// Apply a buff to posture regeneration if currently holding guard.
			if(IsGuarding())
				seconds *= GuardRegenBuff;

			var regenPerTick = (MaxPosture / seconds) * delta;

			Posture -= (float)regenPerTick;
		}

		if(Posture >= MaxPosture) {
			// Break Stance.
			// Activate Timer.
			// Reset Posture to 0 after Timer is Up.
		}

		hud_posture.Value = Posture;
	}

	private void HandleHurtboxInteraction(Area2D area) {
		if(area is not Hitbox)
			return;

		var damage = (int)area.Get("damage");

		animationState.Travel("Hurt");

		ApplyPushback((float)area.Get("pushback"));

		SetHP(HP - damage);

		// Todo;
		// 1. Hurt Animation
		// 2. Screen Shake Effect
		// 3. Hurt Particle Effects
		// 4. Hurt Sounds
		// 5. Add Posture
	}

	private void HandleGuardboxInteraction(Area2D area) {
		if(area is not Hitbox)
			return;

		var damage = (float)area.Get("damage") * GuardDamage;

		can_regenerate_posture = false;
		postureRegenDelayTimer.Start();

		// Check if it is a Parry or a Block.
		if(guard_frames <= ParryFrames) {
			// Add Posture but Prevent Stagger
			Posture += (float)area.Get("posture_damage") * ParryPostureDamage;
			if(Posture >= MaxPosture)
				Posture = MaxPosture - 1;

			ApplyPushback((float)area.Get("pushback") * ParryPushback);

			animationState.Travel("Parry");

			// Todo;
			// 1. Parry Sound
			// 2. Parry Particles
			// 3. Add Posture but Prevent Stagger

		} else {
			// Add posture.
			Posture += (float)area.Get("posture_damage") * ParryPostureDamage;

			ApplyPushback((float)area.Get("pushback") * GuardPushback);

			// Reduce HP but prevent death.
			var new_hp = HP - (int)Mathf.Round(damage);
			if(new_hp < 0)
				SetHP(1);
			else
				SetHP(new_hp);
			
			// Spawn Hit Effect.
			// SimpleEffect effect = vfx_hit.Instantiate<SimpleEffect>();
			// effect.Position = vfx_hit_location.Position;
			// AddChild(effect);

			animationState.Travel("Block");

			// Todo;
			// 1. Block Sound
			// 2. Block Particles
			// 3. Add Posture
		}
	}

	private void ApplyPushback(float pushback) {
		// Pushback
		Vector2 velocity = Vector2.Zero;
		// Lerp Velocity using the MotionLerpWeight and Delta. This will smoothly increase / decrease the velocity and imitate acceleration without actually using acceleration.
		velocity.X = Mathf.Lerp(velocity.X,  -pivot.Scale.X * pushback, MotionLerpWeight * (float)GetPhysicsProcessDeltaTime()); 
		Velocity = velocity;
		MoveAndSlide();
	}

	private async void SetHP(int hp) {
		if(dead)
			return;

		HP = hp;
		hud_hp.Value = HP;

		// Die
		if(HP <= 0) {
			dead = true;
			Sprite2D current_sprite = pivot.GetNode<Sprite2D>("Sprite2D");
			AnimatedSprite2D death_sprite = scene_death.Instantiate<AnimatedSprite2D>();

			current_sprite.Modulate = Colors.Transparent;
			death_sprite.Position = current_sprite.Position;
			pivot.AddChild(death_sprite);
			death_sprite.Play();

			SimpleEffect effect = vfx_hit.Instantiate<SimpleEffect>();
			effect.Position = vfx_hit_location.Position;
			AddChild(effect);

			await ToSignal(death_sprite, "animation_finished");
			GetTree().ReloadCurrentScene();
			return;
		}
	}

	// Check if Hitboxes are disabled.
	bool HitboxesDisabled() {
		return !hitbox.Monitoring;
	}

	// Check if Guarding.
	bool IsGuarding() {
		return guardbox.Monitoring;
	}

	// Check if Attacking.
	bool IsAttacking() {
		return animationState.GetCurrentNode() == "Attack_1" || animationState.GetCurrentNode() == "Attack_2";
	}

	// Check if the hurt animation is playing.
	bool IsHurt() {
		return animationState.GetCurrentNode() == "Hurt";
	}

	bool IsRolling() {
		return animationState.GetCurrentNode() == "Roll";
	}

	// Method to check whether a roll should be allowed or not.
	bool CanRoll() {
		return !IsHurt() && HitboxesDisabled() && !IsRolling() && !dead;
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

	// Deactivate Hurtbox
	public void DeactivateHurtbox() {
		hurtbox.Monitoring = true;
	}

	// Activate Hurtbox
	public void ActivateHurtbox() {
		hurtbox.Monitoring = false;
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
