using Godot;
using System;

public partial class Toolbar : HBoxContainer
{
	[Export] public LineWidthSelector LineWidthSelector;
	[Export] public ColorPicker ColorPicker;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
