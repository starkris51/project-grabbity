using System;
using System.Collections.Generic;
using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gameplay;

public enum GridPhase
{
    Playing,
    Gravity,
    Clearing,
    WaitingAfterClear,
    GameOver
}

public readonly struct GridMargins(int top, int bottom, int left, int right)
{
    public int Top { get; } = top;
    public int Bottom { get; } = bottom;
    public int Left { get; } = left;
    public int Right { get; } = right;

    public static readonly GridMargins None = new(0, 0, 0, 0);
}

public class Grid
{
    public int Width = 8;
    public int Height = 14;

    public int CellSize { get; private set; } = 32;

    private Rectangle _viewport;
    private GridMargins _margins;

    public int AmountToClear { get; private set; } = 3;

    private int ContentWidth => _viewport.Width - _margins.Left - _margins.Right;
    private int ContentHeight => _viewport.Height - _margins.Top - _margins.Bottom;

    public int OffsetX => _viewport.X + _margins.Left + (ContentWidth - Width * CellSize) / 2;
    public int OffsetY => _viewport.Y + _margins.Top + (ContentHeight - Height * CellSize) / 2;

    private readonly Texture2D _texture;

    private GridPhase _currentPhase = GridPhase.Playing;
    private double _phaseTimer = 0;
    private const double GravityStepDelay = 0.1;
    private const double ClearDelay = 0.4;

    public bool IsGameOver => _currentPhase == GridPhase.GameOver;

    public event Action OnGameOver;
    public event Action RequestNewPiece;

    private readonly Cell[,] cells;

    public Cell[,] Cells { get; set; }

    public Grid(Texture2D texture, Rectangle viewport, GridMargins margins = default)
    {
        _texture = texture;

        cells = new Cell[Width, Height];
        SetViewport(viewport, margins);
        Reset();
    }

    public void SetViewport(Rectangle viewport, GridMargins margins = default)
    {
        _viewport = viewport;
        _margins = margins;
    }

    public bool IsCellEmpty(int x, int y)
    {
        return cells[x, y].State == CellState.Empty;
    }

    public CellState GetCellState(int x, int y)
    {
        return cells[x, y].State;
    }

    public void SetCell(int x, int y, Cell cell)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        cell.X = x;
        cell.Y = y;
        cells[x, y] = cell;
    }

    public bool IsValidPosition(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        return IsCellEmpty(x, y);
    }

    public bool IsValidPosition(int x, int y, Cell[,] matrix)
    {
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if (matrix[i, j].State == CellState.Empty) continue;
                if (!IsValidPosition(x + i, y + j)) return false;
            }
        }
        return true;
    }

    public void PlacePiece(int x, int y, Cell[,] matrix)
    {
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if (matrix[i, j].State != CellState.Empty)
                {
                    Cell src = matrix[i, j];
                    SetCell(x + i, y + j, new Cell(src.State));
                }
            }
        }

        _currentPhase = GridPhase.Gravity;
        _phaseTimer = 0;
    }

    private bool ApplyGravityOneStep(Cell[,] board)
    {
        bool moved = false;
        for (int x = 0; x < Width; x++)
        {
            for (int y = Height - 2; y >= 0; y--)
            {
                if (board[x, y].State == CellState.Empty || board[x, y].State == CellState.Invisible || board[x, y].IsClearing) continue;
                if (board[x, y + 1].State == CellState.Empty)
                {
                    board[x, y + 1] = board[x, y];
                    board[x, y] = new Cell();
                    moved = true;
                }
            }
        }
        return moved;
    }

    private bool MarkMatches(Cell[,] board)
    {
        bool found = false;

        // Horizontal runs
        for (int y = 0; y < Height; y++)
        {
            int runStart = 0;
            while (runStart < Width)
            {
                CellState state = board[runStart, y].State;
                if (state == CellState.Empty || state == CellState.Placeholder || state == CellState.Invisible)
                {
                    runStart++;
                    continue;
                }

                int runEnd = runStart + 1;
                while (runEnd < Width && board[runEnd, y].State == state)
                    runEnd++;

                if (runEnd - runStart >= AmountToClear)
                {
                    found = true;
                    for (int x = runStart; x < runEnd; x++)
                        board[x, y].IsClearing = true;
                }

                runStart = runEnd;
            }
        }

        // Vertical runs
        for (int x = 0; x < Width; x++)
        {
            int runStart = 0;
            while (runStart < Height)
            {
                CellState state = board[x, runStart].State;
                if (state == CellState.Empty || state == CellState.Placeholder || state == CellState.Invisible)
                {
                    runStart++;
                    continue;
                }

                int runEnd = runStart + 1;
                while (runEnd < Height && board[x, runEnd].State == state)
                    runEnd++;

                if (runEnd - runStart >= AmountToClear)
                {
                    found = true;
                    for (int y = runStart; y < runEnd; y++)
                        board[x, y].IsClearing = true;
                }

                runStart = runEnd;
            }
        }
        return found;
    }

    public Cell[,] Clone() => CloneBoard(cells);

    public Cell[,] CloneBoard(Cell[,] board)
    {
        Cell[,] clone = new Cell[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                clone[x, y] = new Cell(board[x, y].State);
            }
        }
        return clone;
    }

    // Bounds/occupancy check for dropping a piece onto an arbitrary board snapshot,
    // independent of this grid's live state. Used by AI placement simulation.
    public bool CanPlaceOnBoard(Cell[,] board, Cell[,] matrix, int x, int y)
    {
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if (matrix[i, j].State == CellState.Empty) continue;

                int bx = x + i;
                int by = y + j;
                if (bx < 0 || bx >= Width || by < 0 || by >= Height) return false;
                if (board[bx, by].State != CellState.Empty) return false;
            }
        }
        return true;
    }

    // Returns the resting Y for a hard drop of matrix at column x on the given board,
    // or -1 if the piece can't even fit at its spawn row there.
    public int GetDropY(Cell[,] board, Cell[,] matrix, int x, int startY = 0)
    {
        if (!CanPlaceOnBoard(board, matrix, x, startY)) return -1;

        int y = startY;
        while (CanPlaceOnBoard(board, matrix, x, y + 1))
            y++;
        return y;
    }

    public Cell[,] PlacePieceOnBoard(Cell[,] board, Cell[,] matrix, int x, int y)
    {
        Cell[,] result = CloneBoard(board);
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                if (matrix[i, j].State != CellState.Empty)
                {
                    result[x + i, y + j] = new Cell(matrix[i, j].State);
                }
            }
        }
        return result;
    }

    public readonly struct SimulationResult(Cell[,] resultBoard, int cellsCleared, int chainDepth)
    {
        public Cell[,] ResultBoard { get; } = resultBoard;
        public int CellsCleared { get; } = cellsCleared;
        public int ChainDepth { get; } = chainDepth;
    }

    // Resolves gravity, then repeatedly marks/clears/re-settles until the board is
    // stable, mirroring the real Gravity -> Clearing -> WaitingAfterClear loop but
    // instantly and on a throwaway copy, so it's safe to call from AI lookahead.
    public SimulationResult SimulateClearsAndGravity(Cell[,] board)
    {
        Cell[,] sim = CloneBoard(board);

        while (ApplyGravityOneStep(sim)) { }

        int cellsCleared = 0;
        int chainDepth = 0;

        while (MarkMatches(sim))
        {
            chainDepth++;
            cellsCleared += RemoveMarkedCells(sim);
            while (ApplyGravityOneStep(sim)) { }
        }

        return new SimulationResult(sim, cellsCleared, chainDepth);
    }

    public SimulationResult SimulatePlacement(Cell[,] board, Cell[,] matrix, int x, int y)
    {
        Cell[,] placed = PlacePieceOnBoard(board, matrix, x, y);
        return SimulateClearsAndGravity(placed);
    }

    private int RemoveMarkedCells(Cell[,] board)
    {
        int cleared = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (board[x, y].IsClearing)
                {
                    board[x, y] = new Cell();
                    cleared++;
                }
            }
        }
        return cleared;
    }

    public void Update(GameTime gameTime)
    {
        if (_currentPhase == GridPhase.Playing) return;

        _phaseTimer += gameTime.ElapsedGameTime.TotalSeconds;

        switch (_currentPhase)
        {
            case GridPhase.Gravity:
                if (_phaseTimer >= GravityStepDelay)
                {
                    _phaseTimer = 0;
                    bool moved = ApplyGravityOneStep(cells);
                    SoundManager.Play(Sounds.Fall);
                    if (!moved)
                    {
                        _currentPhase = GridPhase.Clearing;
                    }
                }
                break;

            case GridPhase.Clearing:
                bool hadMatches = MarkMatches(cells);
                if (hadMatches)
                {
                    SoundManager.Play(Sounds.ClearMatch);
                    _currentPhase = GridPhase.WaitingAfterClear;
                    _phaseTimer = 0;
                }
                else if (CheckGameOver())
                {
                    _currentPhase = GridPhase.GameOver;
                    OnGameOver?.Invoke();
                }
                else
                {
                    _currentPhase = GridPhase.Playing;
                    RequestNewPiece?.Invoke();
                }
                break;

            case GridPhase.WaitingAfterClear:
                if (_phaseTimer >= ClearDelay)
                {
                    RemoveMarkedCells(cells);
                    _currentPhase = GridPhase.Gravity;
                    _phaseTimer = 0;
                }
                break;
        }
    }

    private bool CheckGameOver()
    {
        int threshold = 1;
        for (int x = 0; x < Width; x++)
        {
            if (cells[x, threshold].State != CellState.Empty)
                return true;
        }
        return false;
    }

    public void Reset()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                cells[x, y] = new Cell();
            }
        }

        cells[0, Height - 1] = new Cell(CellState.Invisible);
        cells[0, Height - 2] = new Cell(CellState.Invisible);
        cells[1, Height - 1] = new Cell(CellState.Invisible);
        cells[Width - 1, Height - 1] = new Cell(CellState.Invisible);
        cells[Width - 1, Height - 2] = new Cell(CellState.Invisible);
        cells[Width - 2, Height - 1] = new Cell(CellState.Invisible);

        _currentPhase = GridPhase.Playing;
        _phaseTimer = 0;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        // Draw player icon background
        spriteBatch.Draw(_texture, new Rectangle(OffsetX - 64, OffsetY + 48, 96, 96), new Rectangle(128, 64, 64, 64), Color.White, 0f, new Vector2(32, 32), SpriteEffects.None, 0f);

        // Draw grid background
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (cells[x, y].State == CellState.Invisible) continue;

                Vector2 origin = new(CellSize / 2, CellSize / 2);
                spriteBatch.Draw(_texture, new Rectangle(OffsetX + x * CellSize + CellSize / 2, OffsetY + y * CellSize + CellSize / 2, CellSize, CellSize), CellTexture.Empty, Color.White, 0f, origin, SpriteEffects.None, 0f);
            }
        }

        // Draw cells

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (cells[x, y].State == CellState.Empty || cells[x, y].State == CellState.Invisible) continue;

                Cell cell = cells[x, y];
                Vector2 origin = new(CellSize / 2, CellSize / 2);
                Color tint = cell.IsClearing ? Color.Blue * 1.2f : Color.White;
                // Cell background
                spriteBatch.Draw(_texture, new Rectangle(OffsetX + x * CellSize + CellSize / 2, OffsetY + y * CellSize + CellSize / 2, CellSize, CellSize), cell.Background, tint, 0f, origin, SpriteEffects.None, 0f);
                // Cell block
                spriteBatch.Draw(_texture, new Rectangle(OffsetX + x * CellSize + CellSize / 2, OffsetY + y * CellSize + CellSize / 2, CellSize, CellSize), cell.SourceRect, tint, 0f, origin, SpriteEffects.None, 0f);
            }
        }
    }
}