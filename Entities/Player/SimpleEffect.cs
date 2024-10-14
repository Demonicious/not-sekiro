using Godot;
using System;

public partial class SimpleEffect : Node2D
{
	public override void _Ready()
	{
		/* Rotate(Mathf.DegToRad(
			GD.Randi() * 200 + 150
		)); */

		GetNode<AnimationPlayer>("AnimationPlayer").Play("Play");
	}
}
