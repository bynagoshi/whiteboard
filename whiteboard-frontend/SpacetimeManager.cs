using Godot;
using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using SpacetimeDB.ClientApi;

public partial class SpacetimeManager : Node
{
	public static SpacetimeManager Instance { get; private set; }
	public DbConnection Client;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Instance = this;
		ConnectToDb();
		
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		Client?.FrameTick();
	}
	
	private void ConnectToDb()
	{
		const string HOST = "wss://maincloud.spacetimedb.com";
		const string DB_NAME = "whiteboard";
		
		Client = DbConnection
			.Builder()
			.WithUri(HOST)
			.WithModuleName(DB_NAME)
			.OnConnect((conn, identity, token) =>
			{
				conn.SubscriptionBuilder()
					.OnApplied(ctx => 
					{ 
						GD.Print("Subscribed");
						LoadAllStrokes(ctx);
					})
					.SubscribeToAllTables();
				
				conn.Db.Stroke.OnInsert += OnStrokeInserted;
				conn.Db.Stroke.OnUpdate += OnStrokeUpdated;
				conn.Db.Stroke.OnDelete += OnStrokeDeleted;
			})
			.Build();
			
	}
	
	private void LoadAllStrokes(SubscriptionEventContext ctx)
	{

		foreach (var stroke in ctx.Db.Stroke.Iter())
		{
			if (stroke.BoardId == 1)
			{
				Whiteboard.Instance?.DisplayStroke(stroke);
			}
		}
	}
	
	private void OnStrokeInserted(EventContext ctx, Stroke insertedStroke)
	{
		if (insertedStroke.BoardId == 1)
		{
			Whiteboard.Instance?.DisplayStroke(insertedStroke);
		}
	}
	
	private void OnStrokeUpdated(EventContext ctx, Stroke oldStroke, Stroke newStroke)
	{
		if (oldStroke.BoardId == 1)
		{
			Whiteboard.Instance?.RemoveStroke(oldStroke.Id);
		}
		if (newStroke.BoardId == 1)
		{
			Whiteboard.Instance?.DisplayStroke(newStroke);
		}
	}
	
	private void OnStrokeDeleted(EventContext ctx, Stroke deletedStroke)
	{
		if (deletedStroke.BoardId == 1)
		{
			Whiteboard.Instance?.RemoveStroke(deletedStroke.Id);
		}
	}
	
	public void AddStroke(ulong boardId, string color, float thickness, List<Point> points)
	{
		if (Client?.Reducers != null)
		{
			Client.Reducers.AddStroke(boardId, color, thickness, points);
		}
	}
}
