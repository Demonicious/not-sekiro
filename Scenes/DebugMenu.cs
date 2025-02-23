using Godot;
using System;

public partial class DebugMenu : Control
{
	Label framerate;
	Label frametime;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		framerate = GetNode<Label>("Framerate");
		frametime = GetNode<Label>("Frametime");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		framerate.Text = "FPS: " + Engine.GetFramesPerSecond().ToString();
		frametime.Text = delta.ToString("0.000000") + " MS";
	}
}
