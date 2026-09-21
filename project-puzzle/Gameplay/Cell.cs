// List of rectangles for the tileset
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Gameplay;

public static class CellTexture
{
    public const int CellSize = 32;

    private static Rectangle FromCoords(int x, int y)
    {
        return new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
    }

    private static Rectangle FromCoordsSmall(int x, int y)
    {
        return new Rectangle(x * 16, y * 16, 16, 16);
    }

    public static readonly Rectangle Empty = FromCoords(0, 0);
    public static readonly Rectangle Invisible = FromCoords(8, 8);
    public static readonly Rectangle Blocker = FromCoords(0, 0);
    public static readonly Rectangle BackgroundBlock1 = FromCoords(1, 0);
    public static readonly Rectangle BackgroundBlock2 = FromCoords(2, 0);
    public static readonly Rectangle BackgroundBlock3 = FromCoords(3, 0);
    public static readonly Rectangle BackgroundBlock4 = FromCoords(4, 0);
    public static readonly Rectangle BackgroundBlock5 = FromCoords(5, 0);
    public static readonly Rectangle BackgroundBlock6 = FromCoords(6, 0);
    public static readonly Rectangle BackgroundBlock7 = FromCoords(7, 0);
    public static readonly Rectangle Symbol1 = FromCoords(3, 1);
    public static readonly Rectangle Symbol2 = FromCoords(1, 1);
    public static readonly Rectangle Symbol3 = FromCoords(2, 1);
    public static readonly Rectangle Select = FromCoords(6, 2);

    public static readonly Rectangle WallLeft = FromCoords(0, 2);
    public static readonly Rectangle WallRight = FromCoords(1, 2);
    public static readonly Rectangle WallTop = FromCoords(2, 2);
    public static readonly Rectangle WallBottom = FromCoords(3, 2);

    public static readonly Rectangle MiniBackgroundBlock1 = FromCoordsSmall(14, 2);
    public static readonly Rectangle MiniSymbol1 = FromCoordsSmall(12, 2);
    public static readonly Rectangle MiniSymbol2 = FromCoordsSmall(13, 2);
    public static readonly Rectangle MiniSymbol3 = FromCoordsSmall(12, 3);

}

public enum CellType
{
    Symbol1,
    Symbol2,
    Symbol3
}

public enum CellState
{
    Empty,
    Placeholder,
    Invisible,
    Blocker,
    Symbol1,
    Symbol2,
    Symbol3
}

public static class CellHelpers
{
    // Symbol1 beats symbol2, symbol2 beats symbol3, symbol3 beats symbol1
    public static bool Beats(this CellState a, CellState b)
    {
        return (a == CellState.Symbol1 && b == CellState.Symbol2) ||
               (a == CellState.Symbol2 && b == CellState.Symbol3) ||
               (a == CellState.Symbol3 && b == CellState.Symbol1);
    }
}

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public class Cell
{
    public bool IsClearing { get; set; }

    public Cell(CellState state = CellState.Empty)
    {
        State = state;
    }

    private Rectangle UpdateSourceRect()
    {
        return State switch
        {
            CellState.Empty => CellTexture.Empty,
            CellState.Invisible => CellTexture.Invisible,
            CellState.Blocker => CellTexture.Blocker,
            CellState.Symbol1 => CellTexture.Symbol1,
            CellState.Symbol2 => CellTexture.Symbol2,
            CellState.Symbol3 => CellTexture.Symbol3,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private Rectangle UpdateBackgroundRect()
    {
        return State switch
        {
            CellState.Symbol1 => CellTexture.BackgroundBlock3,
            CellState.Symbol2 => CellTexture.BackgroundBlock7,
            CellState.Symbol3 => CellTexture.BackgroundBlock6,
            _ => CellTexture.Empty
        };
    }

    // Small-scale variants used when drawing the upcoming-pieces queue, sourced from
    // the tileset's dedicated 16x16 art rather than downscaling the 32x32 sprites.
    private Rectangle UpdateMiniSourceRect()
    {
        return State switch
        {
            CellState.Symbol1 => CellTexture.MiniSymbol1,
            CellState.Symbol2 => CellTexture.MiniSymbol2,
            CellState.Symbol3 => CellTexture.MiniSymbol3,
            _ => CellTexture.Empty
        };
    }

    private Rectangle UpdateMiniBackgroundRect()
    {
        return State switch
        {
            CellState.Symbol1 or CellState.Symbol2 or CellState.Symbol3 => CellTexture.MiniBackgroundBlock1,
            _ => CellTexture.Empty
        };
    }

    private CellState _state;
    public CellState State
    {
        get => _state;
        set
        {
            _state = value;
            SourceRect = UpdateSourceRect();
            Background = UpdateBackgroundRect();
            MiniSourceRect = UpdateMiniSourceRect();
            MiniBackground = UpdateMiniBackgroundRect();
        }
    }

    public int X { get; set; }
    public int Y { get; set; }

    public Rectangle Background;

    public Rectangle SourceRect;

    public Rectangle MiniBackground;

    public Rectangle MiniSourceRect;

    public void Clear()
    {
        State = CellState.Empty;
    }

    public List<(Direction, CellState)> CheckNeighborStates(Cell[,] grid, int x, int y)
    {
        List<(Direction, CellState)> results = [];

        if (y > 0 && State.Beats(grid[x, y - 1].State)) results.Add((Direction.Up, grid[x, y - 1].State));
        if (y < grid.GetLength(1) - 1 && State.Beats(grid[x, y + 1].State)) results.Add((Direction.Down, grid[x, y + 1].State));
        if (x > 0 && State.Beats(grid[x - 1, y].State)) results.Add((Direction.Left, grid[x - 1, y].State));
        if (x < grid.GetLength(0) - 1 && State.Beats(grid[x + 1, y].State)) results.Add((Direction.Right, grid[x + 1, y].State));

        return results;
    }
}

