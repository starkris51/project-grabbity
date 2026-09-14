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

    // Applied on top of the fit-to-viewport scale so the board doesn't fill its content
    // area edge-to-edge — purely a visual sizing knob, tweak to taste.
    private const float BoardScale = 0.7f;

    private int ContentWidth => _viewport.Width - _margins.Left - _margins.Right;
    private int ContentHeight => _viewport.Height - _margins.Top - _margins.Bottom;

    public int OffsetX => _viewport.X + _margins.Left + (ContentWidth - Width * CellSize) / 2;
    public int OffsetY => _viewport.Y + _margins.Top;

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

        int nativeSize = CellTexture.CellSize;
        float scaleByWidth = (float)ContentWidth / (Width * nativeSize);
        float scaleByHeight = (float)ContentHeight / (Height * nativeSize);

        float scale = Math.Min(scaleByWidth, scaleByHeight) * BoardScale;

        CellSize = Math.Max(1, (int)MathF.Round(nativeSize * scale));
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

    private bool ApplyGravityOneStep()
    {
        bool moved = false;
        for (int x = 0; x < Width; x++)
        {
            for (int y = Height - 2; y >= 0; y--)
            {
                if (cells[x, y].State == CellState.Empty || cells[x, y].State == CellState.Invisible || cells[x, y].IsClearing) continue;
                if (cells[x, y + 1].State == CellState.Empty)
                {
                    cells[x, y + 1] = cells[x, y];
                    cells[x, y] = new Cell();
                    moved = true;
                }
            }
        }
        return moved;
    }

    private bool MarkMatches()
    {
        bool found = false;

        // Horizontal runs
        for (int y = 0; y < Height; y++)
        {
            int runStart = 0;
            while (runStart < Width)
            {
                CellState state = cells[runStart, y].State;
                if (state == CellState.Empty || state == CellState.Placeholder || state == CellState.Invisible)
                {
                    runStart++;
                    continue;
                }

                int runEnd = runStart + 1;
                while (runEnd < Width && cells[runEnd, y].State == state)
                    runEnd++;

                if (runEnd - runStart >= AmountToClear)
                {
                    found = true;
                    for (int x = runStart; x < runEnd; x++)
                        cells[x, y].IsClearing = true;
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
                CellState state = cells[x, runStart].State;
                if (state == CellState.Empty || state == CellState.Placeholder || state == CellState.Invisible)
                {
                    runStart++;
                    continue;
                }

                int runEnd = runStart + 1;
                while (runEnd < Height && cells[x, runEnd].State == state)
                    runEnd++;

                if (runEnd - runStart >= AmountToClear)
                {
                    found = true;
                    for (int y = runStart; y < runEnd; y++)
                        cells[x, y].IsClearing = true;
                }

                runStart = runEnd;
            }
        }

        // Diagonal runs (top-left to bottom-right)
        // for (int startX = 0; startX < Width; startX++)
        // {
        //     for (int startY = 0; startY < Height; startY++)
        //     {
        //         CellState state = cells[startX, startY].State;
        //         if (state == CellState.Empty || state == CellState.Placeholder || state == CellState.Invisible)
        //             continue;

        //         int count = 1;
        //         while (startX + count < Width && startY + count < Height && cells[startX + count, startY + count].State == state)
        //             count++;

        //         if (count >= AmountToClear)
        //         {
        //             found = true;
        //             for (int i = 0; i < count; i++)
        //                 cells[startX + i, startY + i].IsClearing = true;
        //         }
        //     }
        // }

        // // Diagonal runs (top-right to bottom-left)
        // for (int startX = 0; startX < Width; startX++)
        // {
        //     for (int startY = 0; startY < Height; startY++)
        //     {
        //         CellState state = cells[startX, startY].State;
        //         if (state == CellState.Empty || state == CellState.Placeholder || state == CellState.Invisible)
        //             continue;

        //         int count = 1;
        //         while (startX - count >= 0 && startY + count < Height && cells[startX - count, startY + count].State == state)
        //             count++;

        //         if (count >= AmountToClear)
        //         {
        //             found = true;
        //             for (int i = 0; i < count; i++)
        //                 cells[startX - i, startY + i].IsClearing = true;
        //         }
        //     }
        // }

        // Square runs
        // for (int x = 0; x < Width - 1; x++)
        // {
        //     for (int y = 0; y < Height - 1; y++)
        //     {
        //         CellState state = cells[x, y].State;
        //         if (state == CellState.Empty || state == CellState.Placeholder || state == CellState.Invisible)
        //             continue;

        //         if (cells[x + 1, y].State == state &&
        //             cells[x, y + 1].State == state &&
        //             cells[x + 1, y + 1].State == state)
        //         {
        //             found = true;
        //             cells[x, y].IsClearing = true;
        //             cells[x + 1, y].IsClearing = true;
        //             cells[x, y + 1].IsClearing = true;
        //             cells[x + 1, y + 1].IsClearing = true;
        //         }
        //     }
        // }

        return found;
    }

    private void RemoveMarkedCells()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (cells[x, y].IsClearing)
                {
                    cells[x, y] = new Cell();
                }
            }
        }
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
                    bool moved = ApplyGravityOneStep();
                    SoundManager.Play(Sounds.Fall);
                    if (!moved)
                    {
                        _currentPhase = GridPhase.Clearing;
                    }
                }
                break;

            case GridPhase.Clearing:
                bool hadMatches = MarkMatches();
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
                    RemoveMarkedCells();
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

        cells[0, 0] = new Cell(CellState.Invisible);
        cells[Width - 1, 0] = new Cell(CellState.Invisible);

        _currentPhase = GridPhase.Playing;
        _phaseTimer = 0;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
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
                if (cells[x, y].State == CellState.Empty) continue;

                Cell cell = cells[x, y];
                Vector2 origin = new(CellSize / 2, CellSize / 2);
                Color tint = cell.IsClearing ? Color.Blue * 1.2f : Color.White;
                spriteBatch.Draw(_texture, new Rectangle(OffsetX + x * CellSize + CellSize / 2, OffsetY + y * CellSize + CellSize / 2, CellSize, CellSize), cell.SourceRect, tint, 0f, origin, SpriteEffects.None, 0f);
            }
        }
    }
}