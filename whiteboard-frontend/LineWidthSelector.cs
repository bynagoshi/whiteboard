using Godot;
using System;

public partial class LineWidthSelector : OptionButton
{
	public static float CurrentLineWidth { get; private set; } = 4f;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		AddItem("2px");
		AddItem("4px");
		AddItem("8px");
		AddItem("12px");
		
		int[] sizes = { 2, 4, 8, 12 };
		for (int i = 0; i < sizes.Length; i++)
		{
			SetItemIcon(i, MakeThicknessIcon(sizes[i]));
		}

		Selected = 1;
		ItemSelected += (index) => {
			CurrentLineWidth = index switch {
				0 => 2f,
				1 => 4f,
				2 => 8f,
				3 => 12f,
				_ => CurrentLineWidth
			};
		};
	}
	
	private Texture2D MakeThicknessIcon(int thickness)
	{
		const int w = 40;
		const int h = 20;

		var img = Godot.Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
		img.Fill(new Color(0,0,0,0));

		int mid = h / 2;
		for (int y = mid - thickness / 2; y <= mid + thickness / 2; y++)
		{
			for (int x = 2; x < w - 2; x++)
				img.SetPixel(x, y, Colors.White);
		}

		return ImageTexture.CreateFromImage(img);
	}
	
	
}
