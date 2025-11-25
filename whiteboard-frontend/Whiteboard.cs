using Godot;
using System;
using System.Collections.Generic;
using SpacetimeDB.Types;

public partial class Whiteboard : Node2D
{
	[Export] public Node2D StrokesLayer;

	private Line2D _currentLine;
	private bool _isDrawing = false;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (StrokesLayer == null)
		{
			StrokesLayer = GetNode<Node2D>("StrokesLayer");
		}
	}
	
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
			{
				StartStroke(mb.Position);
			}
			else
			{
				EndStroke();
			}
		}
		else if (@event is InputEventMouseMotion motion)
		{
			if (_isDrawing && _currentLine != null)
			{
				AddPointToStroke(motion.Position);
			}
		}
	}
	
	private void StartStroke(Vector2 screenPos)
	{
		_currentLine = new Line2D();
		_currentLine.Width = 4f;
		_currentLine.Antialiased = true;
		
		StrokesLayer.AddChild(_currentLine);
		
		var localPos = StrokesLayer.ToLocal(screenPos);
		_currentLine.AddPoint(localPos);
		_isDrawing = true;
	}
	
	private void AddPointToStroke(Vector2 screenPos)
	{
		var localPos = StrokesLayer.ToLocal(screenPos);
		
		var points = _currentLine.Points;
		if (points.Length == 0 || points[^1].DistanceTo(localPos)>2f)
		{
			_currentLine.AddPoint(localPos);
		}
	}
	
	private void EndStroke()
	{
		if (_currentLine != null && _currentLine.Points.Length > 0)
		{
			OnAddStroke();
		}
		_isDrawing = false;
		_currentLine = null;
	}

	private void OnAddStroke()
	{
		if (_currentLine == null || SpacetimeManager.Instance?.Client == null)
		{
			return;
		}

		var points = new List<Point>();
		foreach (var point in _currentLine.Points)
		{
			points.Add(new Point(point.X, point.Y));
		}
		
		ulong boardId = 1;
		Color lineColor = _currentLine.DefaultColor;
		string color = "#" + lineColor.ToHtml(false); 
		float thickness = _currentLine.Width;
		
		SpacetimeManager.Instance.AddStroke(boardId, color, thickness, points);
	}


	
	// // Called every frame. 'delta' is the elapsed time since the previous frame.
	// public override void _Process(double delta)
	// {
	// }
}
