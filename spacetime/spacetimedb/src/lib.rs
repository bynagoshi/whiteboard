use spacetimedb::{ReducerContext, Table, SpacetimeType};

#[spacetimedb::table(name = board, public)]
pub struct Board {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub name: String,
}

#[derive(SpacetimeType, Clone)]
pub struct Point {
    pub x: f32,
    pub y: f32,
}

#[spacetimedb::table(name = stroke, public)]
pub struct Stroke {
    #[primary_key]
    #[auto_inc]
    pub id: u64,
    pub board_id: u64,
    pub user: String,
    pub color: String,
    pub thickness: f32,
    pub points: Vec<Point>,
    pub timestamp: u64, // Unix timestamp in milliseconds
}

#[spacetimedb::reducer(init)]
pub fn init(_ctx: &ReducerContext) {
    // Called when the module is initially published
}

#[spacetimedb::reducer(client_connected)]
pub fn identity_connected(ctx: &ReducerContext) {
    log::info!("Client connected: {}", ctx.sender);
}

#[spacetimedb::reducer(client_disconnected)]
pub fn identity_disconnected(ctx: &ReducerContext) {
    log::info!("Client disconnected: {}", ctx.sender);
}

#[spacetimedb::reducer]
pub fn create_board(ctx: &ReducerContext, name: String) {
    ctx.db.board().insert(Board {
        id: 0,
        name,
    });
    log::info!("Board created by: {}", ctx.sender);
}

#[spacetimedb::reducer]
pub fn add_stroke(
    ctx: &ReducerContext, 
    board_id: u64,
    color: String,
    thickness: f32,
    points: Vec<Point>,
) {
    let user = ctx.sender;
    let timestamp = ctx.timestamp;
    
    ctx.db.stroke().insert(Stroke { 
        id: 0,
        board_id,
        user,
        color,
        thickness,
        points,
        timestamp,
     });
}

#[spacetimedb::reducer]
pub fn delete_stroke(ctx: &ReducerContext, stroke_id: u64) {
    if let Some(stroke) = ctx.db.stroke().filter_by_id(&stroke_id).next() {
        if stroke.user == ctx.sender {
            ctx.db.stroke().delete_by_id(&stroke_id);
            log::info!("Stroke {} deleted by user {}", stroke_id, ctx.sender);
        } else {
            log::warn!("User {} attempted to delete stroke {} owned by {}", ctx.sender, stroke_id, stroke.user);
        }
    }
}

#[spacetimedb::reducer]
pub fn delete_stroke_anyone(ctx: &ReducerContext, stroke_id: u64) {
    if let Some(stroke) = ctx.db.stroke().filter_by_id(&stroke_id).next() {
        ctx.db.stroke().delete_by_id(&stroke_id);
        log::info!("Stroke {} deleted by user {}", stroke_id, ctx.sender);
    }
}

#[spacetimedb::reducer]
pub fn clear_board(ctx: &ReducerContext, board_id: u64) {
    if ctx.db.board().filter_by_id(&board_id).next().is_none() {
        log::warn!("Attempted to clear non-existent board {} by user {}", board_id, ctx.sender);
        return;
    }

    let strokes_to_delete: Vec<u64> = ctx.db.stroke()
        .iter()
        .filter(|stroke| stroke.board_id == board_id)
        .map(|stroke| stroke.id)
        .collect();
    
    for stroke_id in strokes_to_delete {
        ctx.db.stroke().delete_by_id(&stroke_id);
    }
    
    log::info!("Board {} cleared by user {}", board_id, ctx.sender);
}

