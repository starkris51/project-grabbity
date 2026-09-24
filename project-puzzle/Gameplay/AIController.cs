using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace Gameplay;

public class PlacementOption(int rotation, int column, Cell[,] resultingBoard, int clearsTriggered, int chainDepth, int maxHeight)
{
    public int Rotation = rotation;
    public int Column = column;
    public Cell[,] ResultingBoard = resultingBoard;
    public int ClearsTriggered = clearsTriggered;
    public int ChainDepth = chainDepth;
    public int MaxHeight = maxHeight;
}

public enum AIMode
{
    // Stack pieces as low as possible without triggering clears.
    Build,
    // Take the placement with the most cleared cells, then the longest chain.
    Clear,
}

public class AIController(PlayerBoard board) : BoardController(board)
{
    private enum State { Deciding, Rotating, MovingHorizontal, Dropping }
    private State currentState = State.Deciding;
    private PlacementOption target;
    private Piece trackedPiece;

    private const double ActionInterval = 0.3;
    private double actionTimer;

    // Switch to Clear once the board is either this tall or holds this many blocks.
    private const int ClearAtHeight = 9;
    private const int ClearAtBlockCount = 25;

    public AIMode Mode { get; private set; } = AIMode.Build;

    public List<PlacementOption> GetAllPlacements(Piece currentPiece, Cell[,] board)
    {
        List<PlacementOption> options = [];
        Grid grid = Board.Grid;
        Cell[,] matrix = currentPiece.Matrix;
        for (int rotation = 0; rotation < 4; rotation++)
        {
            if (rotation > 0) matrix = Piece.RotateMatrix(matrix);

            for (int column = 0; column < grid.Width; column++)
            {
                int dropY = grid.GetDropY(board, matrix, column);
                if (dropY < 0) continue;

                Grid.SimulationResult simulation = grid.SimulatePlacement(board, matrix, column, dropY);
                int maxHeight = MaxHeight(simulation.ResultBoard);

                options.Add(new PlacementOption(rotation, column, simulation.ResultBoard, simulation.CellsCleared, simulation.ChainDepth, maxHeight));
            }
        }

        return options;
    }

    private void UpdateMode(Cell[,] board)
    {
        bool enoughBlocks = MaxHeight(board) >= ClearAtHeight || BlockCount(board) >= ClearAtBlockCount;
        Mode = enoughBlocks ? AIMode.Clear : AIMode.Build;
    }

    private PlacementOption PickBest(List<PlacementOption> options)
    {
        if (options.Count == 0) return null;

        return Mode switch
        {
            // No clears first, then the lowest stack.
            AIMode.Build => options
                .OrderBy(o => o.ClearsTriggered > 0)
                .ThenBy(o => o.MaxHeight)
                .First(),

            // Most cleared cells, then longest chain, then the lowest stack.
            AIMode.Clear => options
                .OrderByDescending(o => o.ClearsTriggered)
                .ThenByDescending(o => o.ChainDepth)
                .ThenBy(o => o.MaxHeight)
                .First(),

            _ => options[0],
        };
    }

    private int MaxHeight(Cell[,] board)
    {
        int max = 0;
        for (int x = 0; x < Board.Grid.Width; x++)
        {
            int height = ColumnHeight(board, x);
            if (height > max) max = height;
        }
        return max;
    }

    private int ColumnHeight(Cell[,] board, int x)
    {
        int height = Board.Grid.Height;
        for (int y = 0; y < height; y++)
        {
            if (IsBlock(board[x, y])) return height - y;
        }
        return 0;
    }

    private static int BlockCount(Cell[,] board)
    {
        int count = 0;
        foreach (Cell cell in board)
        {
            if (IsBlock(cell)) count++;
        }
        return count;
    }

    private static bool IsBlock(Cell cell) => cell.State != CellState.Empty && cell.State != CellState.Invisible;

    protected override void OnNoActivePiece()
    {
        currentState = State.Deciding;
        trackedPiece = null;
        target = null;
        actionTimer = 0;
    }

    protected override void UpdatePiece(Piece piece, GameTime gameTime)
    {
        // A new piece spawned without a gap frame in between — re-plan.
        if (!ReferenceEquals(piece, trackedPiece))
        {
            OnNoActivePiece();
            trackedPiece = piece;
        }

        if (currentState == State.Deciding)
        {
            Cell[,] board = Board.Grid.Clone();
            UpdateMode(board);

            List<PlacementOption> options = GetAllPlacements(piece, board);
            target = PickBest(options);

            // Nowhere fits; drop it and let game over trigger naturally.
            currentState = target is null ? State.Dropping : State.Rotating;
            return;
        }

        actionTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (actionTimer < ActionInterval) return;
        actionTimer = 0;

        if (currentState == State.Rotating)
        {
            if (piece.Rotation == target.Rotation)
            {
                currentState = State.MovingHorizontal;
            }
            else if (!piece.Rotate())
            {
                // Blocked (wall/stack). Drop where we are rather than stalling.
                currentState = State.Dropping;
            }
        }
        else if (currentState == State.MovingHorizontal)
        {
            bool moved = true;
            if (piece.Column < target.Column) moved = piece.MoveRight();
            else if (piece.Column > target.Column) moved = piece.MoveLeft();
            else currentState = State.Dropping;

            if (!moved) currentState = State.Dropping;
        }
        else if (currentState == State.Dropping)
        {
            piece.HardDrop();
        }
    }
}
