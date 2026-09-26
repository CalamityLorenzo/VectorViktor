using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Globalization;

namespace MeshRendering
{
    // What the retro-look games share: a window (F11 for borderless full screen, Escape to exit), drawn to a
    // small render target and scaled up with hard pixels, the largest whole number of times that fits (L turns
    // that off), colours on or off for the wireframe look (see MeshInstance), and a MeshCache for the game's
    // meshes. A game gives its own LoadWorld, UpdateWorld and DrawWorld.
    //
    // For development, BASIC_WORLD_SHOT="file.png;seconds;keys" saves one low-resolution frame to the file after
    // that many seconds (default 3) - and whatever else WriteShotReport writes, beside it - then exits, with no
    // need of the screen. What the keys mean is the game's business (see Shot).
    public abstract class RetroGame : Game
    {
        protected static readonly Color BackgroundColor = RetroStyle.Background;

        // A screenshot to take: where, how long after starting, and the game's own keys for what to do first.
        protected readonly record struct ScreenShot(string File, float After, string Keys);

        private readonly int _lowResWidth, _lowResHeight;
        private readonly Keys _colorsKey;
        private RenderTarget2D? _lowRes;
        private SpriteBatch? _spriteBatch;
        private RasterizerState? _rasterizerState;
        private KeyboardState _previousKeyboard;

        protected GraphicsDeviceManager Graphics { get; }
        protected MeshCache MeshCache { get; } = new MeshCache();
        protected bool ColorsOn { get; private set; } = true;   // off: faces drawn in the background colour (wireframe look)
        protected bool LowResOn { get; private set; } = true;
        protected ScreenShot? Shot { get; } = ReadShot();
        protected float Clock { get; private set; }   // seconds drawn so far

        // `colorsKey` toggles the colours (not Tab, which Alt+Tab would press on the way out).
        protected RetroGame(int windowWidth, int windowHeight, int lowResWidth, int lowResHeight, Keys colorsKey)
        {
            _lowResWidth = lowResWidth;
            _lowResHeight = lowResHeight;
            _colorsKey = colorsKey;
            Graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = windowWidth,
                PreferredBackBufferHeight = windowHeight,
                // Exclusive fullscreen leaves the process running (window gone, exe alive) after exit; use borderless.
                HardwareModeSwitch = false,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        private static ScreenShot? ReadShot()
        {
            var setting = Environment.GetEnvironmentVariable("BASIC_WORLD_SHOT");
            if (string.IsNullOrEmpty(setting))
                return null;
            var parts = setting.Split(';');
            var after = parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 3f;
            return new ScreenShot(parts[0], after, parts.Length > 2 ? parts[2] : "");
        }

        protected sealed override void LoadContent()
        {
            _rasterizerState = new RasterizerState { CullMode = CullMode.None };
            _lowRes = new RenderTarget2D(GraphicsDevice, _lowResWidth, _lowResHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);   // the stencil for windows (see WindowPortals)
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            LoadWorld();
        }

        protected abstract void LoadWorld();

        protected sealed override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();
            if (Pressed(keyboard, Keys.F11))
                Graphics.ToggleFullScreen();
            if (Pressed(keyboard, _colorsKey))
                ColorsOn = !ColorsOn;
            if (Pressed(keyboard, Keys.L))
                LowResOn = !LowResOn;

            UpdateWorld(gameTime, keyboard);

            _previousKeyboard = keyboard;
            base.Update(gameTime);
        }

        // Its own keys and the world moving on. `keyboard` is this frame's; Pressed says what's just gone down.
        protected abstract void UpdateWorld(GameTime gameTime, KeyboardState keyboard);

        // Down now, and up last frame.
        protected bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

        // +1, -1 or 0 (both or neither), for a pair of keys that push opposite ways.
        protected static float Axis(KeyboardState keyboard, Keys positive, Keys negative) =>
            (keyboard.IsKeyDown(positive) ? 1f : 0f) - (keyboard.IsKeyDown(negative) ? 1f : 0f);

        protected sealed override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTarget(LowResOn ? _lowRes : null);
            GraphicsDevice.Clear(BackgroundColor);

            // SpriteBatch leaves these changed, so set them each frame
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = _rasterizerState;

            DrawWorld(gameTime);

            Clock += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (Shot is { } shot && Clock >= shot.After)
            {
                GraphicsDevice.SetRenderTarget(null);
                using (var file = File.Create(shot.File))
                    _lowRes!.SaveAsPng(file, _lowResWidth, _lowResHeight);
                WriteShotReport(Path.ChangeExtension(shot.File, ".txt"));
                Exit();
                return;
            }

            if (LowResOn)
            {
                GraphicsDevice.SetRenderTarget(null);
                GraphicsDevice.Clear(BackgroundColor);

                // Largest whole-number scale that fits, centred, so every low-res pixel is the same size
                var back = GraphicsDevice.PresentationParameters;
                var scale = Math.Max(1, Math.Min(back.BackBufferWidth / _lowResWidth, back.BackBufferHeight / _lowResHeight));
                var width = _lowResWidth * scale;
                var height = _lowResHeight * scale;
                var destination = new Rectangle((back.BackBufferWidth - width) / 2, (back.BackBufferHeight - height) / 2, width, height);

                _spriteBatch!.Begin(samplerState: SamplerState.PointClamp);
                _spriteBatch.Draw(_lowRes, destination, Color.White);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        // The world, into whatever's been set up to draw to (the render target is cleared, the states set).
        protected abstract void DrawWorld(GameTime gameTime);

        // Anything to save beside the screenshot, to `path`: where everything ended up, say.
        protected virtual void WriteShotReport(string path)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                MeshCache.Dispose();
                _rasterizerState?.Dispose();
                _lowRes?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
