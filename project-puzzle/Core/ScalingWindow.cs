using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Core;

public class ScalingWindow
{
    private static extern void SDL_MaximizeWindow(IntPtr window);

    public int VirtualWidth { get; }
    public int VirtualHeight { get; }

    private readonly GraphicsDeviceManager _graphics;
    private readonly GameWindow _window;
    private RenderTarget2D _renderTarget;
    private Rectangle _destinationRect;

    public ScalingWindow(GraphicsDeviceManager graphics, GameWindow window, int virtualWidth, int virtualHeight)
    {
        _graphics = graphics;
        _window = window;
        VirtualWidth = virtualWidth;
        VirtualHeight = virtualHeight;

        _window.AllowUserResizing = true;
        _window.ClientSizeChanged += OnClientSizeChanged;

        _graphics.PreferredBackBufferWidth = virtualWidth;
        _graphics.PreferredBackBufferHeight = virtualHeight;
    }

    public void Initialize()
    {
        _renderTarget = new RenderTarget2D(_graphics.GraphicsDevice, VirtualWidth, VirtualHeight);
        UpdateDestinationRect();
    }

    public void ToggleFullscreen()
    {
        if (_graphics.IsFullScreen)
        {
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = VirtualWidth;
            _graphics.PreferredBackBufferHeight = VirtualHeight;
        }
        else
        {
            DisplayMode displayMode = _graphics.GraphicsDevice.Adapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = displayMode.Width;
            _graphics.PreferredBackBufferHeight = displayMode.Height;
            _graphics.IsFullScreen = true;
        }

        _graphics.ApplyChanges();
        UpdateDestinationRect();
    }

    public void BeginDraw()
    {
        _graphics.GraphicsDevice.SetRenderTarget(_renderTarget);
    }

    public void EndDraw(SpriteBatch spriteBatch)
    {
        _graphics.GraphicsDevice.SetRenderTarget(null);
        _graphics.GraphicsDevice.Clear(Color.Black);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(_renderTarget, _destinationRect, Color.White);
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
        if (width == _graphics.PreferredBackBufferWidth && height == _graphics.PreferredBackBufferHeight) return;

        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;
        _graphics.ApplyChanges();

        UpdateDestinationRect();
    }

    private void UpdateDestinationRect()
    {
        int windowWidth = _graphics.GraphicsDevice.PresentationParameters.BackBufferWidth;
        int windowHeight = _graphics.GraphicsDevice.PresentationParameters.BackBufferHeight;

        float scale = Math.Min(
            (float)windowWidth / VirtualWidth,
            (float)windowHeight / VirtualHeight);

        int scaledWidth = (int)(VirtualWidth * scale);
        int scaledHeight = (int)(VirtualHeight * scale);

        _destinationRect = new Rectangle(
            (windowWidth - scaledWidth) / 2,
            (windowHeight - scaledHeight) / 2,
            scaledWidth,
            scaledHeight);
    }
}
