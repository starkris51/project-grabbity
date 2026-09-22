using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Gameplay;

public class PlacementOption
{
    public int Rotation;
    public int Column;
    public Cell[,] ResultingBoard;
    public int ClearsTriggered;
    public int ChainDepth;
    public float Score;
}

public class AIController(PlayerBoard board) : BoardController(board)
{
    private enum State { Deciding, Rotating, MovingHorizontal, Dropping, Waiting }

    private const double SoftDropInterval = 0.03;

    public List<PlacementOption> GetAllPlacements(Cell[,] currentPiece, Cell[,] board)
    {
        List<PlacementOption> options = [];
        Grid grid = Board.Grid;

        Cell[,] shape = currentPiece;
        for (int rotation = 0; rotation < 4; rotation++)
        {

        }

        return options;
    }


    protected override void OnNoActivePiece() { }

    protected override void UpdatePiece(Piece piece, GameTime gameTime)
    {
        
    }
}