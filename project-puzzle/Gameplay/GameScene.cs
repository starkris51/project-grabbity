using System.Collections.Generic;
using System.Linq;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Gameplay;

public class PlayerBoard
{
    public required Grid Grid;
    public required PieceManager PieceManager;
    public BoardController Controller = null!;
    public int Score { get; set; } = 0;
}

public class GameScene(ContentManager content, int screenWidth, int screenHeight) : IScene
{
    private static readonly GridMargins SingleBoardMargins = new(top: 40, bottom: 40, left: 40, right: 40);

    // 450 in the future
    private static readonly GridMargins MultiBoardMargins = new(top: 40, bottom: 60, left: 60, right: 60);

    private const int UpcomingPieceCount = 3;

    private readonly ContentManager _content = content;
    private Texture2D tileset = null!;

    private readonly List<PlayerBoard> boards = [];

    public int BoardCount { get; private set; } = 2;

    public void Load()
    {
        tileset = _content.Load<Texture2D>("wip-tileset");

        CreateBoards();
    }

    private void CreateBoards()
    {
        boards.Clear();

        GridMargins margins = BoardCount > 1 ? MultiBoardMargins : SingleBoardMargins;
        Rectangle[] viewports = BoardLayout.GetViewports(BoardCount, screenWidth, screenHeight);

        foreach (var item in viewports.Select((viewport, index) => (viewport, index)))
        {
            var grid = new Grid(tileset, item.viewport, margins);
            var pieceManager = new PieceManager(UpcomingPieceCount);
            pieceManager.Initialize(grid, tileset);

            var board = new PlayerBoard { Grid = grid, PieceManager = pieceManager };
            // Use AI Controller if board is second
            if (item.index == 1)
            {
                board.Controller = new AIController(board);
            }
            else
            {
                board.Controller = new PlayerController(board);
            }

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
            board.Controller.Update(gameTime);
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
