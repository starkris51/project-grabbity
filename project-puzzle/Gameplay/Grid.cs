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
    DroppingGarbage,
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

    private readonly Texture2D _pixel;

    private static readonly Color GarbageFill = new(210, 210, 210);

    private GridPhase _currentPhase = GridPhase.Playing;
    private double _phaseTimer = 0;
    private const double GravityStepDelay = 0.1;
    private const double ClearDelay = 0.4;

    private readonly List<Cell> _pendingGarbage = [];
    public int PendingGarbageCount => _pendingGarbage.Count;

    public bool IsGameOver => _currentPhase == GridPhase.GameOver;

    public event Action OnGameOver;
    public event Action RequestNewPiece;

    public const int MaxSymbolBar = 10;

    public int symbol1Bar = 0;
    public int symbol2Bar = 0;
    public int symbol3Bar = 0;
    public event Action<CellState, int> RequestSendGarbage;

    public int GetSymbolBar(CellState type) => type switch
    {
        CellState.Symbol1 => symbol1Bar,
        CellState.Symbol2 => symbol2Bar,
        CellState.Symbol3 => symbol3Bar,
        _ => 0
    };

    private readonly Cell[,] cells;

    public Cell[,] Cells { get; set; }

    public Grid(Texture2D texture, Rectangle viewport, GridMargins margins = default)
    {
        _texture = texture;
        _pixel = new Texture2D(texture.GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        cells = new Cell[Width, Height];
        SetViewport(viewport, margins);
        Reset();
    }

    public void FillSymbolBar(CellState state)
    {
        if (state == CellState.Symbol1)
        {
            symbol1Bar = Math.Min(symbol1Bar + 1, MaxSymbolBar);
        } else if (state == CellState.Symbol2)
        {
            symbol2Bar = Math.Min(symbol2Bar + 1, MaxSymbolBar);
        } else if (state == CellState.Symbol3)
        {
            symbol3Bar = Math.Min(symbol3Bar + 1, MaxSymbolBar);
        }
    }

    // Each attack spends this much of a symbol bar and sends that many garbage blocks.
    public const int GarbagePerAttack = 3;

    public bool CanSendGarbage(CellState type) => !IsGameOver && GetSymbolBar(type) >= GarbagePerAttack;

    public bool TrySendGarbage(CellState type)
    {
        if (!CanSendGarbage(type)) return false;

        switch (type)
        {
            case CellState.Symbol1: symbol1Bar -= GarbagePerAttack; break;
            case CellState.Symbol2: symbol2Bar -= GarbagePerAttack; break;
            case CellState.Symbol3: symbol3Bar -= GarbagePerAttack; break;
        }

        RequestSendGarbage?.Invoke(type, GarbagePerAttack);
        return true;
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

    private static bool IsMatchable(Cell cell)
    {
        CellState state = cell.State;
        return !cell.IsGarbage && state != CellState.Empty && state != CellState.Placeholder && state != CellState.Invisible;
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
                if (!IsMatchable(board[runStart, y]))
                {
                    runStart++;
                    continue;
                }

                int runEnd = runStart + 1;
                while (runEnd < Width && IsMatchable(board[runEnd, y]) && board[runEnd, y].State == state)
                    runEnd++;

                if (runEnd - runStart >= AmountToClear)
                {
                    found = true;
                    for (int x = runStart; x < runEnd; x++) {
                        board[x, y].IsClearing = true;
                    }
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
                if (!IsMatchable(board[x, runStart]))
                {
                    runStart++;
                    continue;
                }

                int runEnd = runStart + 1;
                while (runEnd < Height && IsMatchable(board[x, runEnd]) && board[x, runEnd].State == state)
                    runEnd++;

                if (runEnd - runStart >= AmountToClear)
                {
                    found = true;
                    for (int y = runStart; y < runEnd; y++) {
                        board[x, y].IsClearing = true;
                    }
                }

                runStart = runEnd;
            }
        }

        if (found) MarkGarbage(board);

        return found;
    }

    private void MarkGarbage(Cell[,] board)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Cell cell = board[x, y];
                if (!cell.IsGarbage || cell.IsClearing) continue;

                if (IsClearingOfType(board, x, y - 1, cell.State) ||
                    IsClearingOfType(board, x, y + 1, cell.State) ||
                    IsClearingOfType(board, x - 1, y, cell.State) ||
                    IsClearingOfType(board, x + 1, y, cell.State))
                {
                    cell.IsClearing = true;
                }
            }
        }
    }

    private bool IsClearingOfType(Cell[,] board, int x, int y, CellState state)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
        Cell cell = board[x, y];
        return cell.IsClearing && !cell.IsGarbage && cell.State == state;
    }

    public Cell[,] Clone() => CloneBoard(cells);

    public Cell[,] CloneBoard(Cell[,] board)
    {
        Cell[,] clone = new Cell[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                clone[x, y] = new Cell(board[x, y].State, board[x, y].IsGarbage);
            }
        }
        return clone;
    }

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

    public void QueueGarbage(CellState type, int count = 1)
    {
        for (int i = 0; i < count; i++)
            _pendingGarbage.Add(new Cell(type, isGarbage: true));
    }

    public void QueueRandomGarbage(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var type = (CellState)Random.Shared.Next((int)CellState.Symbol1, (int)CellState.Symbol3 + 1);
            QueueGarbage(type);
        }
    }

    private bool SpawnGarbageRow()
    {
        List<int> freeColumns = [];
        for (int x = 0; x < Width; x++)
        {
            if (cells[x, 0].State == CellState.Empty) freeColumns.Add(x);
        }

        if (freeColumns.Count == 0) return false;

        int spawnCount = Math.Min(freeColumns.Count, _pendingGarbage.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            int pick = Random.Shared.Next(freeColumns.Count);
            int column = freeColumns[pick];
            freeColumns.RemoveAt(pick);

            SetCell(column, 0, _pendingGarbage[0]);
            _pendingGarbage.RemoveAt(0);
        }

        return true;
    }

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

    private int RemoveMarkedCells(Cell[,] board, bool isSimulated = true)
    {
        int cleared = 0;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (board[x, y].IsClearing)
                {
                    if (!isSimulated)
                    {
                        FillSymbolBar(board[x, y].State);
                    }
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
                else
                {
                    EndResolution();
                }
                break;

            case GridPhase.WaitingAfterClear:
                if (_phaseTimer >= ClearDelay)
                {

                    RemoveMarkedCells(cells, false);
                    _currentPhase = GridPhase.Gravity;
                    _phaseTimer = 0;
                }
                break;

            case GridPhase.DroppingGarbage:
                if (_phaseTimer >= GravityStepDelay)
                {
                    _phaseTimer = 0;
                    if (ApplyGravityOneStep(cells))
                    {
                        SoundManager.Play(Sounds.Fall);
                        break;
                    }

                    if (_pendingGarbage.Count > 0 && SpawnGarbageRow()) break;

                    _pendingGarbage.Clear();
                    EndResolution();
                }
                break;
        }
    }

    private void EndResolution()
    {
        if (CheckGameOver())
        {
            _currentPhase = GridPhase.GameOver;
            OnGameOver?.Invoke();
        }
        else if (_pendingGarbage.Count > 0)
        {
            _currentPhase = GridPhase.DroppingGarbage;
            _phaseTimer = 0;
        }
        else
        {
            _currentPhase = GridPhase.Playing;
            RequestNewPiece?.Invoke();
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

        _pendingGarbage.Clear();
        symbol1Bar = 0;
        symbol2Bar = 0;
        symbol3Bar = 0;
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
                if (cell.IsGarbage)
                {
                    Rectangle dest = new(OffsetX + x * CellSize, OffsetY + y * CellSize, CellSize, CellSize);
                    DrawGarbageCell(spriteBatch, dest, cell.SourceRect, cell.Background, 3, cell.IsClearing ? tint : GarbageFill);
                    continue;
                }
                // Cell background
                spriteBatch.Draw(_texture, new Rectangle(OffsetX + x * CellSize + CellSize / 2, OffsetY + y * CellSize + CellSize / 2, CellSize, CellSize), cell.Background, tint, 0f, origin, SpriteEffects.None, 0f);
                // Cell block
                spriteBatch.Draw(_texture, new Rectangle(OffsetX + x * CellSize + CellSize / 2, OffsetY + y * CellSize + CellSize / 2, CellSize, CellSize), cell.SourceRect, tint, 0f, origin, SpriteEffects.None, 0f);
            }
        }

        DrawPendingGarbage(spriteBatch);

        // Draw da bars

        DrawSymbolBar(spriteBatch, 16, symbol1Bar, CellTexture.MiniSymbol1);
        DrawSymbolBar(spriteBatch, 48, symbol2Bar, CellTexture.MiniSymbol2);
        DrawSymbolBar(spriteBatch, 80, symbol3Bar, CellTexture.MiniSymbol3);
    }

    private void DrawSymbolBar(SpriteBatch spriteBatch, int xOffset, int value, Rectangle symbolRect)
    {
        const int barWidth = 16;
        int maxHeight = CellSize * Height / 2 ;
        int barHeight = Math.Min(value, MaxSymbolBar) * maxHeight / MaxSymbolBar;
        int barBottom = OffsetY + maxHeight * 2;
        int barX = OffsetX + CellSize * Width + xOffset;

        spriteBatch.Draw(_pixel, new Rectangle(barX, barBottom - barHeight, barWidth, barHeight), Color.White);

        // Symbol icon under the bar
        Rectangle iconDest = new(barX, barBottom + 4, barWidth, barWidth);
        spriteBatch.Draw(_texture, iconDest, CellTexture.MiniBackgroundBlock1, Color.White);
        spriteBatch.Draw(_texture, iconDest, symbolRect, Color.White);
    }

    private void DrawPendingGarbage(SpriteBatch spriteBatch)
    {
        const int iconSize = 16;
        int maxIcons = Width * CellSize / iconSize;
        int count = Math.Min(_pendingGarbage.Count, maxIcons);
        int y = OffsetY - iconSize - 4;

        for (int i = 0; i < count; i++)
        {
            Cell cell = _pendingGarbage[i];
            Rectangle dest = new(OffsetX + i * iconSize, y, iconSize, iconSize);
            DrawGarbageCell(spriteBatch, dest, cell.MiniSourceRect, cell.MiniBackground, 2, GarbageFill);
        }
    }

    private void DrawGarbageCell(SpriteBatch spriteBatch, Rectangle dest, Rectangle symbol, Rectangle frame, int inset, Color fill)
    {
        Rectangle fillRect = dest;
        fillRect.Inflate(-inset, -inset);

        spriteBatch.Draw(_pixel, fillRect, fill);
        spriteBatch.Draw(_texture, dest, symbol, Color.Black);
        spriteBatch.Draw(_texture, dest, frame, Color.White);
    }
}