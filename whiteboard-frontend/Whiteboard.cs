using Godot;
using System;
using System.Collections.Generic;
using SpacetimeDB.Types;

public enum UndoType
{
	Add,
	Delete,
}

public struct UndoAction
{
	public UndoType Type;
	public Stroke Stroke;
	public bool NeedsUndo;
	public UndoAction(Stroke stroke, UndoType type, bool needsUndo)
	{
		Stroke = stroke;
		Type = type;
		NeedsUndo = needsUndo;
	}
}

public partial class Whiteboard : Node2D
{
	public static Whiteboard Instance { get; private set; }
	
	[Export] public Node2D StrokesLayer;

	private Line2D _currentLine;
	private bool _isDrawing = false;
	private bool _isDeleting = false;
	private bool _waitingForStrokeFromDb = false;
	private Dictionary<ulong, Line2D> _displayedStrokes = new Dictionary<ulong, Line2D>();	
	private Dictionary<ulong, Dot> _displayedDots = new Dictionary<ulong, Dot>();
	public Stack<UndoAction> undoStack = new Stack<UndoAction>();
	public Stack<UndoAction> redoStack = new Stack<UndoAction>();
	public bool _isPerformingUndoRedo = false;
	public UndoAction? _pendingUndoRedoAction = null;

	
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

		if (@event is InputEventKey key && key.Pressed && key.CtrlPressed)
		{
			if (key.Keycode == Key.Z)
			{
				GD.Print("Undo");
				Undo();
			}
			else if (key.Keycode == Key.Y)
			{
				GD.Print("Redo");
				Redo();
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
		_currentLine.Width = LineWidthSelector.CurrentLineWidth;
		_currentLine.Antialiased = true;
		_currentLine.DefaultColor = ColorPicker.CurrentColor;
		
		StrokesLayer.CallDeferred("add_child", _currentLine);
		
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
			_waitingForStrokeFromDb = true;
		}
		_isDrawing = false;
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
		Color lineColor = ColorPicker.CurrentColor;
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

		if (_isPerformingUndoRedo && _pendingUndoRedoAction.HasValue)
		{
			var pendingAction = _pendingUndoRedoAction.Value;
			bool cameFromUndo = pendingAction.NeedsUndo;

			UpdateStoredStrokeReferences(pendingAction.Stroke.Id, stroke);
			
			var updatedAction = new UndoAction(stroke, pendingAction.Type, !cameFromUndo);
			
			if (cameFromUndo)
			{
				redoStack.Push(updatedAction);
			}
			else 
			{
				undoStack.Push(updatedAction);
			}
			
			_pendingUndoRedoAction = null;
			_isPerformingUndoRedo = false; 
		}

		if (stroke.Points.Count == 1)
		{
			var dot = new Dot();
			dot.Radius = stroke.Thickness / 2f;
			dot.Color = Color.FromHtml(stroke.Color);
			dot.Position = new Vector2(stroke.Points[0].X, stroke.Points[0].Y);
			StrokesLayer.AddChild(dot);
			_displayedDots[stroke.Id] = dot;

			if (_waitingForStrokeFromDb)
			{
				_waitingForStrokeFromDb = false;
				_currentLine = null;

				RegisterUserAction(stroke, UndoType.Add);
			}
			return;
		}

		if (_waitingForStrokeFromDb && _currentLine != null)
		{

			_currentLine.Width = stroke.Thickness;
			if (Color.HtmlIsValid(stroke.Color))
				_currentLine.DefaultColor = Color.FromHtml(stroke.Color);

			_displayedStrokes[stroke.Id] = _currentLine;
			_currentLine = null;
			_waitingForStrokeFromDb = false;

			RegisterUserAction(stroke, UndoType.Add);
			return;
		}
		
		
		// This might be useless? Above should be enough since there should always be a current line
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
				RemoveStrokeFromDisplay(kvp.Key);
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
					RemoveStrokeFromDisplay(kvp.Key);
					return;
				}
			}
		}
	}

	
	private void Undo()
	{
		
		if (undoStack.Count == 0 || _isPerformingUndoRedo || _waitingForStrokeFromDb)
		{
			return;
		}

		_isPerformingUndoRedo = true;
		UndoAction action = undoStack.Pop();
		if (action.Type == UndoType.Add)
		{
			SpacetimeManager.Instance?.DeleteStroke(action.Stroke.Id);
			_pendingUndoRedoAction = action;

			// redoStack.Push(new UndoAction(action.Stroke, UndoType.Delete, false));
		}
		else
		{
			_pendingUndoRedoAction = action;
			SpacetimeManager.Instance?.AddStroke(action.Stroke.BoardId, action.Stroke.Color, action.Stroke.Thickness, action.Stroke.Points);
		}
	}

	private void Redo()
	{
		if (redoStack.Count == 0 || _isPerformingUndoRedo || _waitingForStrokeFromDb)
		{
			return;
		}

		_isPerformingUndoRedo = true;
		UndoAction action = redoStack.Pop();
		if (action.Type == UndoType.Add)
		{
			_pendingUndoRedoAction = action;
			SpacetimeManager.Instance?.AddStroke(action.Stroke.BoardId, action.Stroke.Color, action.Stroke.Thickness, action.Stroke.Points);
		}
		else
		{
			SpacetimeManager.Instance?.DeleteStroke(action.Stroke.Id);
			// undoStack.Push(new UndoAction(action.Stroke, UndoType.Add, true));
			_pendingUndoRedoAction = action;

		}
	}

	public void RegisterUserAction(Stroke stroke, UndoType type)
	{
		if (_isPerformingUndoRedo)
		{
			return;
		}

		undoStack.Push(new UndoAction(stroke, type, true));
		redoStack.Clear();
	}

	private void UpdateStoredStrokeReferences(ulong oldId, Stroke replacement)
	{
		if (oldId == replacement.Id)
		{
			return;
		}

		void UpdateStack(Stack<UndoAction> stack)
		{
			if (stack.Count == 0)
			{
				return;
			}

			var actions = stack.ToArray();
			stack.Clear();

			for (int i = actions.Length - 1; i >= 0; i--)
			{
				var action = actions[i];
				if (action.Stroke.Id == oldId)
				{
					action = new UndoAction(replacement, action.Type, action.NeedsUndo);
				}
				stack.Push(action);
			}
		}

		UpdateStack(undoStack);
		UpdateStack(redoStack);

		if (_pendingUndoRedoAction.HasValue && _pendingUndoRedoAction.Value.Stroke.Id == oldId)
		{
			var pending = _pendingUndoRedoAction.Value;
			_pendingUndoRedoAction = new UndoAction(replacement, pending.Type, pending.NeedsUndo);
		}
	}

}
