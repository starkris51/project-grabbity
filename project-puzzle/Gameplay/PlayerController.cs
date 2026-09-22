using Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Gameplay;

public readonly struct PlayerControls(
    Keys moveLeft = Keys.Left,
    Keys moveRight = Keys.Right,
    Keys softDrop = Keys.Down,
    Keys hardDrop = Keys.Space,
    Keys rotate = Keys.Up,
    Keys rotateAlt = Keys.R)
{
    public Keys MoveLeft { get; } = moveLeft;
    public Keys MoveRight { get; } = moveRight;
    public Keys SoftDrop { get; } = softDrop;
    public Keys HardDrop { get; } = hardDrop;
    public Keys Rotate { get; } = rotate;
    public Keys RotateAlt { get; } = rotateAlt;

    public static readonly PlayerControls Default = new(Keys.Left, Keys.Right, Keys.Down, Keys.Space, Keys.Up, Keys.R);
}

public class PlayerController(PlayerBoard board, PlayerControls? controls = null) : BoardController(board)
{
    private const double DasDelay = 0.15;
    private const double ArrInterval = 0.03;

    private const double SoftDropInterval = 0.03;

    private readonly PlayerControls _controls = controls ?? PlayerControls.Default;

    // -1 = left held, 1 = right held, 0 = neither.
    private int _heldDirection;
    private double _dasTimer;
    private bool _dasCharged;
    private double _softDropTimer;

    protected override void UpdatePiece(Piece piece, GameTime gameTime)
    {
        double delta = gameTime.ElapsedGameTime.TotalSeconds;

        HandleHorizontal(piece, delta);
        if (piece.IsLocked) return;

        HandleSoftDrop(piece, delta);
        if (piece.IsLocked) return;

        HandleHardDrop(piece);
        if (piece.IsLocked) return;

        HandleRotate(piece);
    }

    protected override void OnNoActivePiece() => ResetState();

    private void HandleHorizontal(Piece piece, double delta)
    {
        bool leftDown = KeyboardInfo.IsKeyDown(_controls.MoveLeft);
        bool rightDown = KeyboardInfo.IsKeyDown(_controls.MoveRight);
        bool leftPressed = KeyboardInfo.WasKeyJustPressed(_controls.MoveLeft);
        bool rightPressed = KeyboardInfo.WasKeyJustPressed(_controls.MoveRight);

        int desiredDirection = _heldDirection;

        if (leftPressed) desiredDirection = -1;
        else if (rightPressed) desiredDirection = 1;

        else if (desiredDirection == -1 && !leftDown) desiredDirection = rightDown ? 1 : 0;
        else if (desiredDirection == 1 && !rightDown) desiredDirection = leftDown ? -1 : 0;

        if (desiredDirection != _heldDirection)
        {
            _heldDirection = desiredDirection;
            _dasTimer = 0;
            _dasCharged = false;

            if (_heldDirection == -1) piece.MoveLeft();
            else if (_heldDirection == 1) piece.MoveRight();
            return;
        }

        if (_heldDirection == 0) return;

        _dasTimer += delta;

        if (!_dasCharged)
        {
            if (_dasTimer < DasDelay) return;
            _dasCharged = true;
            _dasTimer -= DasDelay;
        }

        while (_dasTimer >= ArrInterval)
        {
            _dasTimer -= ArrInterval;
            bool moved = _heldDirection == -1 ? piece.MoveLeft() : piece.MoveRight();
            if (!moved)
            {
                _dasTimer = 0;
                break;
            }
        }
    }

    private void HandleSoftDrop(Piece piece, double delta)
    {
        if (!KeyboardInfo.IsKeyDown(_controls.SoftDrop))
        {
            _softDropTimer = 0;
            return;
        }

        if (KeyboardInfo.WasKeyJustPressed(_controls.SoftDrop))
        {
            piece.SoftDrop();
            _softDropTimer = 0;
            return;
        }

        _softDropTimer += delta;
        while (_softDropTimer >= SoftDropInterval)
        {
            _softDropTimer -= SoftDropInterval;
            if (!piece.SoftDrop()) break;
        }
    }

    private void HandleHardDrop(Piece piece)
    {
        if (KeyboardInfo.WasKeyJustPressed(_controls.HardDrop))
            piece.HardDrop();
    }

    private void HandleRotate(Piece piece)
    {
        if (KeyboardInfo.WasKeyJustPressed(_controls.Rotate) || KeyboardInfo.WasKeyJustPressed(_controls.RotateAlt))
            piece.Rotate();
    }

    private void ResetState()
    {
        _heldDirection = 0;
        _dasTimer = 0;
        _dasCharged = false;
        _softDropTimer = 0;
    }
}
