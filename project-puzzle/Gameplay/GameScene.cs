using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Gameplay;

public sealed class BattleBoard
{
    public required Grid Grid { get; init; }
    public required PieceManager PieceManager { get; init; }
    public required ICombatant Combatant { get; init; }
    public BoardController Controller = null!;

}

public class GameScene(ContentManager content, int screenWidth, int screenHeight) : IScene
{
    private static readonly GridMargins SingleBoardMargins = new(top: 40, bottom: 40, left: 40, right: 40);

    // 450 in the future
    private static readonly GridMargins MultiBoardMargins = new(top: 450, bottom: 40, left: 60, right: 60);

    private const int UpcomingPieceCount = 3;

    private readonly ContentManager _content = content;
    private Texture2D tileset = null!;

    private readonly List<BattleBoard> boards = [];

    public int BoardCount { get; private set; } = 2;

    // Swap this out to try a different combat system.
    public ICombatRule Combat { get; set; } = new NoCombat();

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

            ICombatant combatant = item.index == 0
                ? new PlayerCharacter("Player", maxHealth: 100, attack: 10)
                : CreateTestEnemy();
            var board = new BattleBoard
            {
                Grid = grid,
                PieceManager = pieceManager,
                Combatant = combatant
            };
            // The first board is the player; remaining boards are enemies.
            if (item.index > 0)
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
            board.Grid.RequestSendGarbage += (type, amount) => SendGarbage(board, type, amount);
            board.Grid.CellsCleared += step => Combat.OnCellsCleared(board, step);
            board.Grid.ChainEnded += chain => Combat.OnChainEnded(board, chain);

            SpawnPiece(board);
        }
    }

    private static EnemyCharacter CreateTestEnemy()
    {
        var definition = new EnemyDefinition(
            id: "training-enemy",
            name: "Training Enemy",
            maxHealth: 80,
            attack: 8,
            fullBodyAsset: null,
            targets:
            [
                new EnemyTargetDefinition(
                    id: "head",
                    name: "Head",
                    maxHealth: 20,
                    hitChanceMultiplier: 0.65f,
                    destroyedEffect: EnemyTargetDestroyedEffect.ExposeWeakSpot),
                new EnemyTargetDefinition(
                    id: "torso",
                    name: "Torso",
                    maxHealth: 40,
                    hitChanceMultiplier: 1.0f,
                    destroyedEffect: EnemyTargetDestroyedEffect.DisableAttack)
            ]);

        return new EnemyCharacter(definition);
    }

    private void SendGarbage(BattleBoard sender, CellState type, int amount)
    {
        foreach (BattleBoard target in boards)
        {
            if (target == sender || target.Grid.IsGameOver) continue;
            target.Grid.QueueGarbage(type, amount);
        }
    }


    public void SetBoardCount(int count)
    {
        BoardCount = count;
        if (tileset != null) CreateBoards();
    }

    private void SpawnPiece(BattleBoard board)
    {
        board.PieceManager.SpawnNext();
    }

    private void Restart(BattleBoard board)
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

        // // Temporary dev toggle for exercising the layout system before real UI/menu
        // // flow exists to pick a mode.
        // if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D1)) SetBoardCount(1);
        // if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D2)) SetBoardCount(2);
        // if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D3)) SetBoardCount(3);
        // if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.D4)) SetBoardCount(4);

        // Debug: queue 5 random-typed garbage cells on the player's board. They drop in
        // once the current piece locks and its chain resolves.
        if (KeyboardInfo.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.G) && boards.Count > 0)
            boards[0].Grid.QueueRandomGarbage(5);

        foreach (BattleBoard board in boards)
        {
            board.Grid.Update(gameTime);

            if (board.Grid.IsGameOver) continue;

            board.PieceManager.Update(gameTime);
            board.Controller.Update(gameTime);
            Combat.Update(board, gameTime);
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (BattleBoard board in boards)
        {
            board.Grid.Draw(spriteBatch);
            board.PieceManager.Draw(spriteBatch);
        }
    }
}
