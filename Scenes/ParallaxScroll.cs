using Godot;
using System;

public partial class ParallaxScroll : ParallaxBackground
{
	[Export]
	public float ScrollSpeed = 0.25f;

    public override void _Ready()
    {
        // Set Centered = False on the Sprite.
		GetNode<Sprite2D>("ParallaxLayer/Sky").Centered = false;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
		ScrollBaseScale = new Vector2(ScrollBaseScale.X - (ScrollSpeed * (float)delta), ScrollBaseScale.Y);
	}
}
