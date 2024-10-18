using Godot;
using System;

public partial class AnimatedProgressBar : Control
{
	[Export]
	public int MaxValue = 100;
	[Export]
	public int MinValue = 0;
	[Export]
	public int Value {
		get => _value;
		set {
			if(_value != value) {
				_value = value;

				if(_value > MaxValue)
					_value = MaxValue;

				if(initialized)
					Set(_value);
			}
		}
	}

	private int _value = 100;

	[Export]
	public float Duration = 0.05f;
	[Export]
	public float ShadowDelay = 1f;

	[Export]
	Texture2D FillTexture;

	TextureProgressBar primary;
	TextureProgressBar shadow;
	Timer timer;

	bool initialized = false;
	
	// Called when the node enters the scene tree for the first time.
	public void InitializeAnimatedBar()
	{
		primary = GetNode<TextureProgressBar>("Primary");
		shadow = GetNode<TextureProgressBar>("Shadow");
		timer = GetNode<Timer>("Timer");

		primary.MaxValue = MaxValue;
		primary.MinValue = MinValue;

		shadow.MaxValue = MaxValue;
		shadow.MinValue = MinValue;

		primary.Value = Value;
		shadow.Value = Value;

		primary.TextureProgress = FillTexture;

		initialized = true;
	}

	private async void Set(int value) {
		CreateTween().TweenProperty(primary, "value", value, Duration);

		timer.WaitTime = ShadowDelay;
		timer.Start();
		await ToSignal(timer, "timeout");

		CreateTween().TweenProperty(shadow, "value", value, Duration);
	}
}
