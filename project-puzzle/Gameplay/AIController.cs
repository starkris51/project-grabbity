using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Gameplay;

public class PlacementOption(int rotation, int column, Cell[,] resultingBoard, int clearsTriggered, int chainDepth)
{
    public int Rotation = rotation;
    public int Column = column;
    public Cell[,] ResultingBoard = resultingBoard;
    public int ClearsTriggered = clearsTriggered;
    public int ChainDepth = chainDepth;
    public double Score;
}

public class AIController(PlayerBoard board) : BoardController(board)
{
    private enum State { Deciding, Rotating, MovingHorizontal, Dropping }
    private State currentState = State.Deciding;
    private PlacementOption target;
    private Piece trackedPiece;

    // Delay between individual rotate/move inputs so the CPU is watchable rather than
    // snapping into place on the first frame.
    private const double ActionInterval = 0.08;
    private const double SoftDropInterval = 0.03;
    private double actionTimer;

    // Scoring weights for evaluating a resulting board.
    private const double ChainWeight = 100;
    private const double ClearWeight = 10;
    private const double AdjacencyWeight = 2;
    private const double MaxHeightWeight = 4;
    private const double TotalHeightWeight = 0.5;
    private const double DangerPenalty = 10000;

    public List<PlacementOption> GetAllPlacements(Piece currentPiece, Cell[,] board)
    {
        List<PlacementOption> options = [];
        Grid grid = Board.Grid;

        // Rotate a copy of the matrix so the live piece is untouched. Rotation index N
        // means N clockwise rotations from spawn, matching Piece.Rotation.
        Cell[,] matrix = currentPiece.Matrix;
        for (int rotation = 0; rotation < 4; rotation++)
        {
            if (rotation > 0) matrix = Piece.RotateMatrix(matrix);

            for (int column = 0; column < grid.Width; column++)
            {
                int dropY = grid.GetDropY(board, matrix, column);
                if (dropY < 0) continue;

                Grid.SimulationResult simulation = grid.SimulatePlacement(board, matrix, column, dropY);

                var option = new PlacementOption(rotation, column, simulation.ResultBoard, simulation.CellsCleared, simulation.ChainDepth);
                option.Score = Evaluate(option);
                options.Add(option);
            }
        }

        return options;
    }

    private double Evaluate(PlacementOption option)
    {
        Cell[,] board = option.ResultingBoard;
        Grid grid = Board.Grid;

        int maxHeight = 0;
        int totalHeight = 0;
        for (int x = 0; x < grid.Width; x++)
        {
            int height = ColumnHeight(board, x);
            totalHeight += height;
            if (height > maxHeight) maxHeight = height;
        }

        double score = option.ChainDepth * ChainWeight
                     + option.ClearsTriggered * ClearWeight
                     + CountAdjacentPairs(board) * AdjacencyWeight
                     - maxHeight * MaxHeightWeight
                     - totalHeight * TotalHeightWeight;

        // Grid.CheckGameOver trips when anything occupies row 1.
        if (maxHeight >= grid.Height - 1) score -= DangerPenalty;

        return score;
    }

    private int ColumnHeight(Cell[,] board, int x)
    {
        int height = Board.Grid.Height;
        for (int y = 0; y < height; y++)
        {
            CellState state = board[x, y].State;
            if (state != CellState.Empty && state != CellState.Invisible)
                return height - y;
        }
        return 0;
    }

    // Same-symbol orthogonal neighbours — rewards setting up future matches.
    private int CountAdjacentPairs(Cell[,] board)
    {
        Grid grid = Board.Grid;
        int pairs = 0;
        for (int x = 0; x < grid.Width; x++)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                CellState state = board[x, y].State;
                if (state == CellState.Empty || state == CellState.Invisible || state == CellState.Placeholder) continue;

                if (x + 1 < grid.Width && board[x + 1, y].State == state) pairs++;
                if (y + 1 < grid.Height && board[x, y + 1].State == state) pairs++;
            }
        }
        return pairs;
    }

    private static PlacementOption PickBest(List<PlacementOption> options)
    {
        PlacementOption best = null;
        foreach (PlacementOption option in options)
        {
            if (best is null || option.Score > best.Score) best = option;
        }
        return best;
    }

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
            List<PlacementOption> options = GetAllPlacements(piece, Board.Grid.Clone());
            target = PickBest(options);

            // Nowhere fits; just let it fall and trigger game over naturally.
            currentState = target is null ? State.Dropping : State.Rotating;
            return;
        }

        actionTimer += gameTime.ElapsedGameTime.TotalSeconds;

        if (currentState == State.Dropping)
        {
            while (actionTimer >= SoftDropInterval)
            {
                actionTimer -= SoftDropInterval;
                if (!piece.SoftDrop()) return;
            }
            return;
        }

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
    }
}
