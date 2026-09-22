using Microsoft.Xna.Framework;

namespace Gameplay;

// Shared entry point for anything that drives a board's active piece — human input via
// PlayerController today, an AIController later. Owns fetching the active piece and
// short-circuiting when there's nothing to control, so subclasses only implement the
// actual decision-making.
public abstract class BoardController(PlayerBoard board)
{
    protected readonly PlayerBoard Board = board;

    public void Update(GameTime gameTime)
    {
        Piece piece = Board.PieceManager.ActivePiece;
        if (piece is null || piece.IsLocked || Board.Grid.IsGameOver)
        {
            OnNoActivePiece();
            return;
        }

        UpdatePiece(piece, gameTime);
    }

    // Called once per frame while there's a live, unlocked piece to control.
    protected abstract void UpdatePiece(Piece piece, GameTime gameTime);

    // Called instead of UpdatePiece when there's no piece to act on, e.g. the gap
    // between a piece locking and the next one spawning, or game over. Override to
    // clear any held-state timers (DAS charge, held direction, etc).
    protected virtual void OnNoActivePiece() { }
}
