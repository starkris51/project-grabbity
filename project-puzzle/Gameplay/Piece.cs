
using System;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
    public Piece(Texture2D texture, Grid grid, bool isInBag = true)
        : this(texture, grid, PieceShapes.GetNewPiece(), isInBag)
    {
    }

    // Builds a piece from an already-rolled shape, so a queued preview piece can be
    // promoted into the falling piece without re-rolling a different shape.
    public Piece(Texture2D texture, Grid grid, Cell[,] matrix, bool isInBag)
    {
        _texture = texture;
        _grid = grid;
        _isInBag = isInBag;
        if (isInBag)
        {
            x = 0;
            y = 0;
        }
        else
        {
            x = (grid.Width / 2) - 1;
            y = 0;
        }
        this.matrix = matrix;


        if (!isInBag)
        {
            OnSpawned?.Invoke();
        }
    }
    private readonly Texture2D _texture;
    private readonly Grid _grid;
    private readonly bool _isInBag;

    private Cell[,] matrix;

    public Cell[,] Matrix => matrix;

    private int x;
    private int y;

    public int Column => x;

    // Number of successful clockwise rotations since spawn, mod 4.
    public int Rotation { get; private set; }

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

    // Clockwise 90° rotation of a piece matrix. Static so the AI can simulate the exact
    // same rotation on a copy without touching the live piece.
    public static Cell[,] RotateMatrix(Cell[,] matrix)
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

        return rotated;
    }

    private bool TryRotate()
    {
        var rotated = RotateMatrix(matrix);

        if (!_grid.IsValidPosition(x, y, rotated)) return false;

        matrix = rotated;
        Rotation = (Rotation + 1) % 4;
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

    public bool MoveLeft() => TryMove(-1, 0);
    public bool MoveRight() => TryMove(1, 0);

    public bool SoftDrop()
    {
        if (TryMove(0, 1))
        {
            fallTimer = 0;
            return true;
        }
        Lock();
        return false;
    }

    public void HardDrop() => Lock();
    public bool Rotate() => TryRotate();

    public void Update(GameTime gameTime)
    {
        if (IsLocked || _grid.IsGameOver) return;

        fallTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (fallTimer >= fallInterval)
        {
            if (!TryMove(0, 1)) Lock();
            fallTimer = 0;
        }
    }

    public void Draw(SpriteBatch spriteBatch, int offsetX = 0, int offsetY = 0)
    {
        if (IsLocked) return;

        int cellSize = _isInBag ? 16 : _grid.CellSize;

        int pixelX = _grid.OffsetX + x * cellSize + offsetX * cellSize;
        int pixelY = _grid.OffsetY + y * cellSize + offsetY * cellSize;

        // Draw Piece
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if (matrix[i, j].State != CellState.Empty)
                {
                    Cell cell = matrix[i, j];
                    Rectangle sourceRect = _isInBag ? cell.MiniSourceRect : cell.SourceRect;
                    Rectangle backgroundRect = _isInBag ? cell.MiniBackground : cell.Background;
                    int drawX = pixelX + i * cellSize;
                    int drawY = pixelY + j * cellSize;
                    Vector2 origin = new(cellSize / 2, cellSize / 2);
                    spriteBatch.Draw(_texture, new Rectangle(drawX + cellSize / 2, drawY + cellSize / 2, cellSize, cellSize), backgroundRect, Color.White, 0f, origin, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_texture, new Rectangle(drawX + cellSize / 2, drawY + cellSize / 2, cellSize, cellSize), sourceRect, Color.White, 0f, origin, SpriteEffects.None, 0f);
                }
            }
        }

        if (_isInBag) return;

        // Draw Ghost at the piece's immediate landing spot against the grid as it stands
        // right now. Cells still mid-fall haven't reached their final resting place yet,
        // so the ghost must sit on top of them where they currently are, not on the
        // board's eventual settled state.
        int ghostY = y;
        while (_grid.IsValidPosition(x, ghostY + 1, matrix))
            ghostY++;

        if (ghostY != y)
        {
            int pixelGhostX = _grid.OffsetX + x * cellSize;
            int pixelGhostY = _grid.OffsetY + ghostY * cellSize;

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (matrix[i, j].State == CellState.Empty) continue;

                    Cell cell = matrix[i, j];
                    int drawX = pixelGhostX + i * cellSize;
                    int drawY = pixelGhostY + j * cellSize;
                    Vector2 origin = new(cellSize / 2, cellSize / 2);
                    spriteBatch.Draw(_texture, new Rectangle(drawX + cellSize / 2, drawY + cellSize / 2, cellSize, cellSize), cell.SourceRect, Color.White * 0.3f, 0f, origin, SpriteEffects.None, 0f);
                    spriteBatch.Draw(_texture, new Rectangle(drawX + cellSize / 2, drawY + cellSize / 2, cellSize, cellSize), CellTexture.Select, Color.White, 0f, origin, SpriteEffects.None, 0f);
                }
            }
        }
    }
}