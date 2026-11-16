using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Core;

public class ScalingWindow
{
    public int VirtualWidth { get; }
    public int VirtualHeight { get; }

    private readonly GraphicsDeviceManager _graphics;
    private readonly GameWindow _window;
    private RenderTarget2D _renderTarget;
    // Intermediate target at an integer multiple of the virtual resolution, used for non-integer window scales.
    private RenderTarget2D _upscaleTarget;
    private int _upscaleFactor;
    private bool _isIntegerScale;
    private Rectangle _destinationRect;

    public ScalingWindow(GraphicsDeviceManager graphics, GameWindow window, int virtualWidth, int virtualHeight)
    {
        _graphics = graphics;
        _window = window;
        VirtualWidth = virtualWidth;
        VirtualHeight = virtualHeight;

        _window.AllowUserResizing = true;
        _window.ClientSizeChanged += OnClientSizeChanged;

        SetWindowedSize();
    }

    public void Initialize()
    {
        _renderTarget = new RenderTarget2D(_graphics.GraphicsDevice, VirtualWidth, VirtualHeight);
        CenterWindow();
        UpdateDestinationRect();
    }

    public void ToggleFullscreen()
    {
        if (_graphics.IsFullScreen)
        {
            _graphics.IsFullScreen = false;
            SetWindowedSize();
        }
        else
        {
            DisplayMode displayMode = _graphics.GraphicsDevice.Adapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = displayMode.Width;
            _graphics.PreferredBackBufferHeight = displayMode.Height;
            _graphics.IsFullScreen = true;
        }

        _graphics.ApplyChanges();
        if (!_graphics.IsFullScreen)
            CenterWindow();
        UpdateDestinationRect();
    }

    public void BeginDraw()
    {
        _graphics.GraphicsDevice.SetRenderTarget(_renderTarget);
    }

    public void EndDraw(SpriteBatch spriteBatch)
    {
        GraphicsDevice device = _graphics.GraphicsDevice;

        if (_isIntegerScale || _upscaleTarget == null)
        {
            device.SetRenderTarget(null);
            device.Clear(Color.Black);

            spriteBatch.Begin(samplerState: _isIntegerScale ? SamplerState.PointClamp : SamplerState.LinearClamp);
            spriteBatch.Draw(_renderTarget, _destinationRect, Color.White);
            spriteBatch.End();
            return;
        }

        // Non-integer scale: nearest-neighbour upscale by a whole factor first (every pixel stays square),
        // then a linear downscale to the final size so only pixel edges get slightly softened.
        device.SetRenderTarget(_upscaleTarget);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_renderTarget, _upscaleTarget.Bounds, Color.White);
        spriteBatch.End();

        device.SetRenderTarget(null);
        device.Clear(Color.Black);
        spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        spriteBatch.Draw(_upscaleTarget, _destinationRect, Color.White);
        spriteBatch.End();
    }

    public Vector2 ScreenToVirtual(Vector2 screenPosition)
    {
        float x = (screenPosition.X - _destinationRect.X) / _destinationRect.Width * VirtualWidth;
        float y = (screenPosition.Y - _destinationRect.Y) / _destinationRect.Height * VirtualHeight;
        return new Vector2(x, y);
    }

    private void OnClientSizeChanged(object sender, EventArgs e)
    {
        if (_graphics.IsFullScreen) return;

        int width = _window.ClientBounds.Width;
        int height = _window.ClientBounds.Height;

        if (width <= 0 || height <= 0) return;

        if (width != _graphics.PreferredBackBufferWidth || height != _graphics.PreferredBackBufferHeight)
        {
            _graphics.PreferredBackBufferWidth = width;
            _graphics.PreferredBackBufferHeight = height;
            _graphics.ApplyChanges();
        }

        UpdateDestinationRect();
    }

    // Fit the initial client area within the display, leaving room for window chrome.
    private void SetWindowedSize()
    {
        const int WindowWidthHeadroom = 80;
        const int WindowHeightHeadroom = 100;

        DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        int availableWidth = Math.Max(1, displayMode.Width - WindowWidthHeadroom);
        int availableHeight = Math.Max(1, displayMode.Height - WindowHeightHeadroom);
        float scale = Math.Min(
            (float)availableWidth / VirtualWidth,
            (float)availableHeight / VirtualHeight);

        _graphics.PreferredBackBufferWidth = Math.Max(1, (int)(VirtualWidth * scale));
        _graphics.PreferredBackBufferHeight = Math.Max(1, (int)(VirtualHeight * scale));
    }

    private void CenterWindow()
    {
        DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _window.Position = new Point(
            Math.Max(0, (displayMode.Width - _window.ClientBounds.Width) / 2),
            Math.Max(0, (displayMode.Height - _window.ClientBounds.Height) / 2));
    }

    private void UpdateDestinationRect()
    {
        int windowWidth = _graphics.GraphicsDevice.PresentationParameters.BackBufferWidth;
        int windowHeight = _graphics.GraphicsDevice.PresentationParameters.BackBufferHeight;

        float scale = Math.Min(
            (float)windowWidth / VirtualWidth,
            (float)windowHeight / VirtualHeight);

        _isIntegerScale = MathF.Abs(scale - MathF.Round(scale)) < 0.001f && scale >= 1f;
        if (_isIntegerScale) scale = MathF.Round(scale);

        int scaledWidth = (int)(VirtualWidth * scale);
        int scaledHeight = (int)(VirtualHeight * scale);

        _destinationRect = new Rectangle(
            (windowWidth - scaledWidth) / 2,
            (windowHeight - scaledHeight) / 2,
            scaledWidth,
            scaledHeight);

        UpdateUpscaleTarget(scale);
    }

    private void UpdateUpscaleTarget(float scale)
    {
        int factor = _isIntegerScale || scale < 1f ? 0 : (int)MathF.Ceiling(scale);
        if (factor == _upscaleFactor) return;

        _upscaleTarget?.Dispose();
        _upscaleTarget = factor > 0
            ? new RenderTarget2D(_graphics.GraphicsDevice, VirtualWidth * factor, VirtualHeight * factor)
            : null;
        _upscaleFactor = factor;
    }
}
