using System.Collections.Generic;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Gameplay;

public class PlayerBoard
{
    public required Grid Grid;
    public required PieceManager PieceManager;
    public int Score { get; set; } = 0;
}

public class GameScene(ContentManager content, int screenWidth, int screenHeight) : IScene
{

    // Top == bottom so the vertical centering in Grid.OffsetY places the board dead
    // center on screen regardless of the margin value.
    private static readonly GridMargins SingleBoardMargins = new(top: 40, bottom: 40, left: 40, right: 40);
    private static readonly GridMargins MultiBoardMargins = new(top: 450, bottom: 60, left: 60, right: 60);

    private const int UpcomingPieceCount = 3;

    private readonly ContentManager _content = content;
    private Texture2D tileset = null!;

    private readonly List<PlayerBoard> boards = [];

    public int BoardCount { get; private set; } = 1;

    public void Load()
    {
        tileset = _content.Load<Texture2D>("wip-tileset");

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
            var grid = new Grid(tileset, viewport, margins);
            var pieceManager = new PieceManager(UpcomingPieceCount);
            pieceManager.Initialize(grid, tileset);

            var board = new PlayerBoard { Grid = grid, PieceManager = pieceManager };
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
        board.PieceManager.SpawnNext();
    }

    private void Restart(PlayerBoard board)
    {
        board.Grid.Reset();
        board.PieceManager.Initialize(board.Grid, tileset);
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

            board.PieceManager.Update(gameTime);
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (PlayerBoard board in boards)
        {
            board.Grid.Draw(spriteBatch);
            board.PieceManager.Draw(spriteBatch);
        }
    }
}
