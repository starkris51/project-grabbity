
using System;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Gameplay;

public enum PieceTypes
{
    Mono,
    Dual,
    Chaos,
}

public static class PieceShapes
{
    public static readonly int[][,] BaseShapes =
    [
        new int[,] {{1, 1, 0},
                    {0, 0, 1}},
        new int[,] {{0, 0, 1,},
                    {1, 1, 0}},
        new int[,] {
            {1, 1},
            {1, 0},
        },
        new int[,] {
            {1, 1},
            {0, 1},
        },
        new int[,] {
            {1, 1},
            {1, 1},
        },
        new int[,] {
            {1, 0, 1},
            {0, 1, 0},
        },
        new int[,] {
            {0, 1, 0},
            {0, 1, 0},
        },
    ];

    public static Cell[,] GetNewPiece()
    {
        Random random = new();

        var randomBaseShape = BaseShapes[random.Next(BaseShapes.Length)];
        var pieceType = (PieceTypes)random.Next(Enum.GetValues<PieceTypes>().Length);

        Cell[,] pieceMatrix = new Cell[randomBaseShape.GetLength(0), randomBaseShape.GetLength(1)];

        CellState[] cellStates = [];

        if (pieceType == PieceTypes.Mono)
        {
            var randomSymbol = (CellState)random.Next((int)CellState.Symbol1, (int)CellState.Symbol3 + 1);

            cellStates = [randomSymbol];
        }
        else if (pieceType == PieceTypes.Dual)
        {
            var symbol1 = (CellState)random.Next((int)CellState.Symbol1, (int)CellState.Symbol3 + 1);
            var symbol2 = (CellState)(((int)symbol1 - (int)CellState.Symbol1 + 1) % 3 + (int)CellState.Symbol1);

            cellStates = [symbol1, symbol2];
        }
        else if (pieceType == PieceTypes.Chaos)
        {
            cellStates = [CellState.Symbol1, CellState.Symbol2, CellState.Symbol3];
        }

        for (int i = 0; i < randomBaseShape.GetLength(0); i++)
        {
            for (int j = 0; j < randomBaseShape.GetLength(1); j++)
            {
                if (randomBaseShape[i, j] == 1)
                {
                    var randomCellStateIndex = random.Next(cellStates.Length);
                    var newCellState = cellStates[randomCellStateIndex];
                    pieceMatrix[i, j] = new Cell(newCellState);
                }
                else
                {
                    pieceMatrix[i, j] = new Cell();
                }
            }
        }
        return pieceMatrix;
    }
}

public class Piece
{
    public Piece(Texture2D texture, Grid grid)
    {
        _texture = texture;
        _grid = grid;
        x = (grid.Width / 2) - 1;
        y = 0;
        matrix = PieceShapes.GetNewPiece();

        OnSpawned?.Invoke();
    }
    private readonly Texture2D _texture;
    private readonly Grid _grid;

    private Cell[,] matrix;

    private int x;
    private int y;

    private double fallTimer = 0;
    private readonly double fallInterval = 0.5; // seconds between drops

    // Set once the piece has handed its cells over to the grid. The scene drops locked
    // pieces from its update/draw list.
    public bool IsLocked { get; private set; }

    public event Action OnSpawned;
    public event Action OnMoved;
    public event Action OnRotated;
    public event Action OnLocked;

    private bool TryMove(int dx, int dy)
    {
        int newX = x + dx;
        int newY = y + dy;

        if (dx == 1 || dx == -1) SoundManager.Play(Sounds.MovePiece);

        if (!_grid.IsValidPosition(newX, newY, matrix)) return false;

        x = newX;
        y = newY;
        OnMoved?.Invoke();
        return true;
    }

    private bool TryRotate()
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var rotated = new Cell[cols, rows];

        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                Cell src = matrix[i, j];
                if (src.State != CellState.Empty)
                {
                    rotated[j, rows - 1 - i] = new Cell(src.State);
                }
                else
                {
                    rotated[j, rows - 1 - i] = new Cell();
                }
            }

        if (!_grid.IsValidPosition(x, y, rotated)) return false;

        matrix = rotated;
        OnRotated?.Invoke();
        SoundManager.Play(Sounds.Rotate);
        return true;
    }

    private void Lock()
    {
        if (IsLocked) return;
        if (!_grid.IsValidPosition(x, y, matrix)) return;

        // Lock to the bottom of the grid
        while (_grid.IsValidPosition(x, y + 1, matrix))
            y++;

        _grid.PlacePiece(x, y, matrix);
        IsLocked = true;
        OnLocked?.Invoke();
        // SoundManager.Play(Sounds.PlacePiece); --- IGNORE ---
    }

    public void Update(GameTime gameTime)
    {
        if (IsLocked || _grid.IsGameOver) return;

        fallTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (fallTimer >= fallInterval)
        {
            if (!TryMove(0, 1)) Lock();
            fallTimer = 0;
            if (IsLocked) return;
        }

        if (KeyboardInfo.WasKeyJustPressed(Keys.Left)) TryMove(-1, 0);
        if (KeyboardInfo.WasKeyJustPressed(Keys.Right)) TryMove(1, 0);
        if (KeyboardInfo.WasKeyJustPressed(Keys.Down))
        {
            if (!TryMove(0, 1)) Lock();
        }
        if (KeyboardInfo.WasKeyJustPressed(Keys.Space))
        {
            Lock();
        }
        if (IsLocked) return;
        if (KeyboardInfo.WasKeyJustPressed(Keys.R) || KeyboardInfo.WasKeyJustPressed(Keys.Up))
        {
            TryRotate();
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (IsLocked) return;

        int pixelX = _grid.OffsetX + x * _grid.CellSize;
        int pixelY = _grid.OffsetY + y * _grid.CellSize;

        // Draw Piece
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if (matrix[i, j].State != CellState.Empty)
                {
                    Cell cell = matrix[i, j];
                    Rectangle sourceRect = cell.SourceRect;
                    int drawX = pixelX + i * _grid.CellSize;
                    int drawY = pixelY + j * _grid.CellSize;
                    Vector2 origin = new(_grid.CellSize / 2, _grid.CellSize / 2);
                    spriteBatch.Draw(_texture, new Rectangle(drawX + _grid.CellSize / 2, drawY + _grid.CellSize / 2, _grid.CellSize, _grid.CellSize), sourceRect, Color.White, 0f, origin, SpriteEffects.None, 0f);
                }
            }
        }

        // Draw Ghost at the piece's immediate landing spot against the grid as it stands
        // right now. Cells still mid-fall haven't reached their final resting place yet,
        // so the ghost must sit on top of them where they currently are, not on the
        // board's eventual settled state.
        int ghostY = y;
        while (_grid.IsValidPosition(x, ghostY + 1, matrix))
            ghostY++;

        if (ghostY != y)
        {
            int pixelGhostX = _grid.OffsetX + x * _grid.CellSize;
            int pixelGhostY = _grid.OffsetY + ghostY * _grid.CellSize;

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (matrix[i, j].State == CellState.Empty) continue;

                    Cell cell = matrix[i, j];
                    int drawX = pixelGhostX + i * _grid.CellSize;
                    int drawY = pixelGhostY + j * _grid.CellSize;
                    Vector2 origin = new(_grid.CellSize / 2, _grid.CellSize / 2);
                    spriteBatch.Draw(_texture, new Rectangle(drawX + _grid.CellSize / 2, drawY + _grid.CellSize / 2, _grid.CellSize, _grid.CellSize), cell.SourceRect, Color.White * 0.3f, 0f, origin, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_texture, new Rectangle(drawX + _grid.CellSize / 2, drawY + _grid.CellSize / 2, _grid.CellSize, _grid.CellSize), CellTexture.Select, Color.White, 0f, origin, SpriteEffects.None, 0f);
                }
            }
        }
    }
}