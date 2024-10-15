using Godot;
using System;

public partial class OverlappingAudio : AudioStreamPlayer2D
{
	public async void OverlappingPlay() {
		var node = new AudioStreamPlayer2D();

		AddChild(node);
		node.Stream = Stream;
		node.PitchScale = PitchScale;
		node.Play();
		await ToSignal(node, "finished");
		node.QueueFree();
	}
}
