using Core;
using Gameplay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace project_puzzle;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private readonly ScalingWindow _scalingWindow;
    private SceneManager _sceneManager;

    private GameScene _gameScene;
    private KeyboardState _previousKeyboardState;
    private int ScreenWidth = 1920;
    private int ScreenHeight = 1080;
    private Effect _colorChangeEffect;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _scalingWindow = new ScalingWindow(_graphics, Window, ScreenWidth, ScreenHeight);
    }

    protected override void Initialize()
    {
        base.Initialize();
        _scalingWindow.Initialize();

        _sceneManager = new SceneManager();

        _gameScene = new GameScene(Content, ScreenWidth, ScreenHeight);
        _sceneManager.Push(_gameScene);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _colorChangeEffect = Content.Load<Effect>("colorEffect");
        _colorChangeEffect.Parameters["ScreenSize"].SetValue(new Vector2(ScreenWidth, ScreenHeight));
        _colorChangeEffect.Parameters["TopColor"].SetValue(Color.Green.ToVector4());
        _colorChangeEffect.Parameters["MiddleColor"].SetValue(Color.Green.ToVector4());
        _colorChangeEffect.Parameters["BottomColor"].SetValue(Color.Green.ToVector4());
        SoundManager.Load(Content);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboardState = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboardState.IsKeyDown(Keys.Escape))
            Exit();

        if (keyboardState.IsKeyDown(Keys.F11) && !_previousKeyboardState.IsKeyDown(Keys.F11))
            _scalingWindow.ToggleFullscreen();

        _previousKeyboardState = keyboardState;

        _sceneManager.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _scalingWindow.BeginDraw();

        _graphics.GraphicsDevice.Clear(Color.Black); // Black background

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, effect: _colorChangeEffect);

        _sceneManager.Draw(gameTime, _spriteBatch); // Draw the current scene

        _spriteBatch.End();

        _scalingWindow.EndDraw(_spriteBatch);

        base.Draw(gameTime);
    }
}
