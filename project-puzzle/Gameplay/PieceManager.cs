using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Gameplay;

public class PieceManager(int maxPieces)
{
    // Upcoming pieces are drawn at the small 16px tileset scale, regardless of the
    // grid's own cell size.
    private const int PreviewCellSize = 16;
    private const int QueueGapCells = 2;
    private const int QueueSlotHeightCells = 4;

    private readonly int _maxPieces = maxPieces;

    private readonly List<Piece> _pieces = [];
    private Piece _activePiece = null!;

    private Grid _grid;
    private Texture2D _texture;

    public void Initialize(Grid grid, Texture2D texture)
    {
        _grid = grid;
        _texture = texture;

        _pieces.Clear();
        for (int i = 0; i < _maxPieces; i++)
        {
            _pieces.Add(new Piece(_texture, _grid, isInBag: true));
        }
    }

    // Pops the next queued piece, refills the queue behind it, and spawns a live
    // falling piece built from that exact shape.
    public void SpawnNext()
    {
        Piece queued = _pieces[0];
        _pieces.RemoveAt(0);
        _pieces.Add(new Piece(_texture, _grid, isInBag: true));

        _activePiece = new Piece(_texture, _grid, queued.Matrix, isInBag: false);
    }

    public void Update(GameTime gameTime)
    {
        if (_activePiece != null)
        {
            _activePiece.Update(gameTime);
            if (_activePiece.IsLocked) _activePiece = null;
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        int queueOffsetXCells = _grid.Width * _grid.CellSize / PreviewCellSize + QueueGapCells;

        for (int i = 0; i < _pieces.Count; i++)
        {
            _pieces[i].Draw(spriteBatch, queueOffsetXCells, i * QueueSlotHeightCells);
        }

        _activePiece?.Draw(spriteBatch);
    }
}
