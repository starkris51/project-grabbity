using System;
using System.Collections.Generic;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Gameplay;

public class PlayerBoard
{
    public required Grid Grid;
    public readonly List<Piece> Pieces = [];
    public int Score { get; set; } = 0;
}

public class GameScene(ContentManager content, int screenWidth, int screenHeight) : IScene
{

    // Margins reserved around each board's viewport for that player's UI. A single
    // board gets a bit of breathing room; splitting the screen for multiple boards
    // reserves more (top especially) so each board's UI doesn't collide with its grid.
    private static readonly GridMargins SingleBoardMargins = new(top: 300, bottom: 120, left: 40, right: 40);
    private static readonly GridMargins MultiBoardMargins = new(top: 180, bottom: 60, left: 60, right: 60);

    private readonly ContentManager _content = content;
    private Texture2D tileset = null!;

    private readonly List<PlayerBoard> boards = [];

    // Number of boards to lay out on screen. This only controls layout (1 = single
    // board centered, 2 = boards placed left/right for a 1v1 layout) — it does not
    // wire up separate input or any actual multiplayer logic.
    public int BoardCount { get; private set; } = 1;

    public void Load()
    {
        tileset = _content.Load<Texture2D>("TilesetV5");

        CreateBoards();
    }

    // Rebuilds the boards for the current BoardCount. Call SetBoardCount first to change
    // the layout (e.g. switching from single-player to a 1v1 layout).
    private void CreateBoards()
    {
        boards.Clear();

        GridMargins margins = BoardCount > 1 ? MultiBoardMargins : SingleBoardMargins;
        Rectangle[] viewports = BoardLayout.GetViewports(BoardCount, screenWidth, screenHeight);

        foreach (Rectangle viewport in viewports)
        {
            var board = new PlayerBoard { Grid = new Grid(tileset, viewport, margins) };
            boards.Add(board);

            board.Grid.OnGameOver += () => Restart(board);
            board.Grid.RequestNewPiece += () => SpawnPiece(board);

            SpawnPiece(board);
        }
    }

    public void SetBoardCount(int count)
    {
        BoardCount = count;
        if (tileset != null) CreateBoards();
    }

    private void SpawnPiece(PlayerBoard board)
    {
        board.Pieces.Add(new Piece(tileset, board.Grid));
    }

    private void Restart(PlayerBoard board)
    {
        board.Grid.Reset();
        board.Pieces.Clear();
        SpawnPiece(board);
    }

    public void Unload()
    {
        // Unload game assets here
        _content.Unload();
    }

    public void Update(GameTime gameTime)
    {
        KeyboardInfo.Update();

        // Temporary dev toggle for exercising the layout system before real UI/menu
        // flow exists to pick a mode.
        if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D1)) SetBoardCount(1);
        if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D2)) SetBoardCount(2);
        if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D3)) SetBoardCount(3);
        if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D4)) SetBoardCount(4);

        foreach (PlayerBoard board in boards)
        {
            board.Grid.Update(gameTime);

            if (board.Grid.IsGameOver) continue;

            // Walk backwards over the pieces that existed at the start of the frame: locking a
            // piece spawns its replacement at the end of the list, which must not be updated
            // again this frame.
            for (int i = board.Pieces.Count - 1; i >= 0; i--)
            {
                Piece p = board.Pieces[i];
                p.Update(gameTime);
                if (p.IsLocked) board.Pieces.RemoveAt(i);
            }
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (PlayerBoard board in boards)
        {
            board.Grid.Draw(spriteBatch);

            foreach (Piece p in board.Pieces)
                p.Draw(spriteBatch);
        }
    }
}
