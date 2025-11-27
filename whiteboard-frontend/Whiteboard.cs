using Godot;
using System;
using System.Collections.Generic;
using SpacetimeDB.Types;

public partial class Whiteboard : Node2D
{
	public static Whiteboard Instance { get; private set; }
	
	[Export] public Node2D StrokesLayer;

	private Line2D _currentLine;
	private bool _isDrawing = false;
	private bool _isDeleting = false;
	private Dictionary<ulong, Line2D> _displayedStrokes = new Dictionary<ulong, Line2D>();	
	private Dictionary<ulong, Dot> _displayedDots = new Dictionary<ulong, Dot>();
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Instance = this;
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
		else if (@event is InputEventMouseButton mb2 && mb2.ButtonIndex == MouseButton.Right)
		{
			if (mb2.Pressed)
			{
				_isDeleting = true;
				DeleteStrokeAt(mb2.Position);
			}
			else
			{
				_isDeleting = false;
			}
		}
		else if (@event is InputEventMouseMotion motion)
		{
			if (_isDrawing && _currentLine != null)
			{
				AddPointToStroke(motion.Position);
			}
			if (_isDeleting)
			{
				DeleteStrokeAt(motion.Position);
			}
		}

		// if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Right)
		// {
		// 	if (mb.Pressed)
		// 	{
		// 		(mb.Position);
		// 	}
		// 	else
		// 	{
		// 		EndStroke();
		// 	}
			
		// }
		// else if (@event is InputEventMouseMotion motion)
		// {
		// 	if (!_isDeleting)
		// 	{
		// 		RemoveStroke();
		// 	}
		// }
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
			_currentLine.QueueFree();
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

	private partial class Dot : Node2D
	{
		public float Radius;
		public Color Color;

		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, Radius, Color);
		}
	}

	public void DisplayStroke(Stroke stroke)
	{
		if (_displayedStrokes.ContainsKey(stroke.Id) || _displayedDots.ContainsKey(stroke.Id))
		{
			return;
		}
		
		if (stroke.Points.Count == 1)
		{
			var dot = new Dot();
			dot.Radius = stroke.Thickness / 2f;
			dot.Color = Color.FromHtml(stroke.Color);
			dot.Position = new Vector2(stroke.Points[0].X, stroke.Points[0].Y);
			StrokesLayer.AddChild(dot);
			_displayedDots[stroke.Id] = dot;
			return;
		}
		
		var line = new Line2D();
		line.Width = stroke.Thickness;
		line.Antialiased = true;
		
		if (Color.HtmlIsValid(stroke.Color))
		{
			line.DefaultColor = Color.FromHtml(stroke.Color);
		}
		
		foreach (var point in stroke.Points)
		{
			line.AddPoint(new Vector2(point.X, point.Y));
		}

		
		
		StrokesLayer.AddChild(line);
		_displayedStrokes[stroke.Id] = line;
	}
	
	public void RemoveStrokeFromDisplay(ulong strokeId)
	{
		if (_displayedStrokes.TryGetValue(strokeId, out Line2D line))
		{
			line.QueueFree();
			_displayedStrokes.Remove(strokeId);
		}
		else if (_displayedDots.TryGetValue(strokeId, out Dot dot))
		{
			dot.QueueFree();
			_displayedDots.Remove(strokeId);
		}
	}

	private void DeleteStrokeAt(Vector2 screenPos)
	{
		var localPos = StrokesLayer.ToLocal(screenPos);
		foreach (var kvp in _displayedDots)
		{
			if (kvp.Value.Position.DistanceTo(localPos) <= kvp.Value.Radius)
			{
				SpacetimeManager.Instance?.DeleteStroke(kvp.Key);

				return;
			}
		}
		foreach (var kvp in _displayedStrokes)
		{
			var points = kvp.Value.Points;
			for (int i = 0; i < points.Length; i++)
			{
				if (points[i].DistanceTo(localPos) <= kvp.Value.Width / 2f)
				{
					SpacetimeManager.Instance?.DeleteStroke(kvp.Key);
					return;
				}
			}
		}
	}

	
	// // Called every frame. 'delta' is the elapsed time since the previous frame.
	// public override void _Process(double delta)
	// {
	// }
}
