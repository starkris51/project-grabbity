using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Gameplay;

public sealed class BattleBoard
{
    public required Rectangle Viewport { get; init; }
    public required Grid Grid { get; init; }
    public required PieceManager PieceManager { get; init; }
    public required ICombatant Combatant { get; init; }
    public Texture2D Portrait { get; init; } = null!;
    public BoardController Controller = null!;

}

public class GameScene(ContentManager content, int screenWidth, int screenHeight) : IScene
{
    private static readonly GridMargins SingleBoardMargins = new(top: 40, bottom: 40, left: 40, right: 40);

    // 450 in the future
    private static readonly GridMargins MultiBoardMargins = new(top: 450, bottom: 40, left: 60, right: 60);

    private const int UpcomingPieceCount = 3;
    private const int PortraitSize = 240;
    private const int HealthBarHeight = 18;
    private const int PlayerHealthTextScale = 2;

    private readonly ContentManager _content = content;
    private Texture2D tileset = null!;
    private Texture2D uiPixel = null!;
    private PixelFont pixelFont = null!;

    private readonly List<BattleBoard> boards = [];

    public int BoardCount { get; private set; } = 2;

    // Swap this out to try a different combat system.
    public ICombatRule Combat { get; set; } = new WIPCombat();

    public void Load()
    {
        tileset = _content.Load<Texture2D>("wip-tileset");
        uiPixel = new Texture2D(tileset.GraphicsDevice, 1, 1);
        uiPixel.SetData([Color.White]);
        pixelFont = new PixelFont(tileset.GraphicsDevice);

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
                Viewport = item.viewport,
                Grid = grid,
                PieceManager = pieceManager,
                Combatant = combatant,
                Portrait = combatant.FullBodyAsset is string asset
                    ? _content.Load<Texture2D>(asset)
                    : null
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
            board.Grid.RequestSendDamage += amount => SendGarbage(board, amount);
            board.Grid.CellsCleared += step => Combat.OnCellsCleared(board, step);
            board.Grid.ChainEnded += chain => Combat.OnChainEnded(board, chain);

            SpawnPiece(board);
        }
    }

    private static Enemy CreateTestEnemy() => new(
        name: "Test",
        maxHealth: 80,
        attack: 8,
        fullBodyAsset: "ghust",
        targets:
        [
            new Enemy.Target(
                id: "head",
                name: "Head",
                maxHealth: 20,
                hitChanceMultiplier: 0.65f,
                destroyedEffect: Enemy.TargetDestroyedEffect.ExposeWeakSpot),
            new Enemy.Target(
                id: "torso",
                name: "Torso",
                hitChanceMultiplier: 1.0f,
                destroyedEffect: Enemy.TargetDestroyedEffect.None)
        ]);

    private void SendGarbage(BattleBoard sender, int amount)
    {
        foreach (BattleBoard target in boards)
        {
            if (target == sender || target.Grid.IsGameOver) continue;
            target.Grid.QueueGarbage(amount);
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
        uiPixel?.Dispose();
        pixelFont?.Dispose();
        _content.Unload();
    }

    public void Update(GameTime gameTime)
    {
        KeyboardInfo.Update();
        UpdateTargetSelection();

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

        for (int i = 0; i < boards.Count; i++)
        {
            BattleBoard board = boards[i];
            if (i == 0)
            {
                DrawPlayerHealth(spriteBatch, board);
            }
            else if (board.Combatant is Enemy enemy && board.Portrait is not null)
            {
                int selectedTarget = boards[0].Combatant is PlayerCharacter player
                    ? player.SelectedTarget
                    : -1;
                DrawEnemyHud(spriteBatch, board, enemy, selectedTarget);
            }
        }
    }

    private void UpdateTargetSelection()
    {
        if (!KeyboardInfo.WasKeyJustPressed(Keys.Tab) ||
            boards.Count < 2 ||
            boards[0].Combatant is not PlayerCharacter player ||
            boards[1].Combatant is not Enemy enemy ||
            enemy.Targets.Length == 0)
        {
            return;
        }

        player.SelectedTarget = (player.SelectedTarget + 1) % enemy.Targets.Length;
    }

    private void DrawEnemyHud(SpriteBatch spriteBatch, BattleBoard board, Enemy enemy, int selectedTarget)
    {
        int size = Math.Min(PortraitSize, Math.Max(0, screenWidth - 80));
        int centerX = screenWidth / 2;
        Rectangle portraitBounds = new(
            centerX - size / 2,
            board.Viewport.Y + 32,
            size,
            size);

        spriteBatch.Draw(board.Portrait, portraitBounds, Color.White);
        DrawEnemyTargets(spriteBatch, enemy, portraitBounds, selectedTarget);

        int healthWidth = Math.Min(PortraitSize, Math.Max(0, screenWidth - 80));
        Rectangle healthBounds = new(
            centerX - healthWidth / 2,
            portraitBounds.Bottom + 10,
            healthWidth,
            HealthBarHeight);
        DrawHealthBar(spriteBatch, healthBounds, enemy);
    }

    private void DrawEnemyTargets(SpriteBatch spriteBatch, Enemy enemy, Rectangle portraitBounds, int selectedTarget)
    {
        for (int i = 0; i < enemy.Targets.Length; i++)
        {
            Enemy.Target target = enemy.Targets[i];
            float verticalPosition = target.Id switch
            {
                "head" => 0.24f,
                "torso" => 0.58f,
                _ => -1f
            };
            if (verticalPosition < 0) continue;

            bool isSelected = i == selectedTarget;
            int markerSize = isSelected ? 40 : 32;
            Rectangle markerBounds = new(
                portraitBounds.Center.X - markerSize / 2,
                portraitBounds.Y + (int)(portraitBounds.Height * verticalPosition) - markerSize / 2,
                markerSize,
                markerSize);
            Color tint = isSelected ? Color.White : Color.White * 0.55f;
            Rectangle symbol = target.Id == "head" ? CellTexture.BackgroundBlock1 : CellTexture.BackgroundBlock1;

            spriteBatch.Draw(tileset, markerBounds, symbol, tint);
        }
    }

    private void DrawPlayerHealth(SpriteBatch spriteBatch, BattleBoard board)
    {
        int width = board.Grid.Width * board.Grid.CellSize;
        int boardBottom = board.Grid.OffsetY + board.Grid.Height * board.Grid.CellSize;
        int labelHeight = PixelFont.GlyphHeight * PlayerHealthTextScale;
        int y = Math.Min(boardBottom + 12, screenHeight - HealthBarHeight - labelHeight - 12);
        Rectangle bounds = new(board.Grid.OffsetX, y, width, HealthBarHeight);
        DrawHealthBar(spriteBatch, bounds, board.Combatant);
        pixelFont.DrawString(
            spriteBatch,
            $"HP {board.Combatant.Health}/{board.Combatant.MaxHealth}",
            new Vector2(bounds.X, bounds.Bottom + 4),
            Color.White,
            PlayerHealthTextScale);
    }

    private void DrawHealthBar(SpriteBatch spriteBatch, Rectangle bounds, ICombatant combatant)
    {
        const int border = 2;
        Rectangle innerBounds = new(
            bounds.X + border,
            bounds.Y + border,
            bounds.Width - border * 2,
            bounds.Height - border * 2);
        float healthFraction = Math.Clamp(combatant.Health / (float)combatant.MaxHealth, 0f, 1f);
        Rectangle fillBounds = innerBounds;
        fillBounds.Width = (int)(innerBounds.Width * healthFraction);

        if (fillBounds.Width > 0)
            spriteBatch.Draw(uiPixel, fillBounds, new Color(65, 190, 90));
    }
}
