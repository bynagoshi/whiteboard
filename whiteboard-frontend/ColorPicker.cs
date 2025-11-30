using Godot;
using System;

public partial class ColorPicker : ColorPickerButton
{
	public static Color CurrentColor { get; private set; } = Colors.White;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		CurrentColor = Color;
		Connect("color_changed", new Callable(this, nameof(OnColorChanged)));
	}
	
	private void OnColorChanged(Color color)
	{
		CurrentColor = color;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
