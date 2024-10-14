using Godot;
using System;

public partial class Hitbox : Area2D
{
	[Export]
	public int damage = 225;

	[Export]
	public float posture_damage = 25;

	[Export]
	public float pushback = 500f;
}
