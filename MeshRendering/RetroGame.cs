using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Globalization;

namespace MeshRendering
{
    // What the retro-look games share: a window (F11 for borderless full screen, Escape to exit), drawn to a
    // small render target and scaled up with hard pixels, the largest whole number of times that fits (L turns
    // that off), colours on or off for the wireframe look (see MeshInstance), and a MeshCache for the game's
    // meshes. On a controller, Back exits and Y toggles the colours. A game gives its own LoadWorld, UpdateWorld and DrawWorld, and, if it wants, DrawOverlay: drawn over the
    // scaled-up picture at the window's own resolution (a tool's panels, say).
    //
    // For development, BASIC_WORLD_SHOT="file.png;seconds;keys" saves one low-resolution frame to the file after
    // that many seconds (default 3) - and whatever else WriteShotReport writes, beside it - then exits, with no
    // need of the screen. What the keys mean is the game's business (see Shot), but for c: colours off, the wireframe
    // look. With ShotOfWholeWindow, the file is the whole window instead, overlay and all.
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
        private GamePadState _previousPad;

        protected GraphicsDeviceManager Graphics { get; }
        protected MeshCache MeshCache { get; } = new MeshCache();
        protected ScreenShot? Shot { get; } = ReadShot();
        protected bool ColorsOn { get; private set; }            // off: faces drawn in the background colour (wireframe look)
        protected bool LowResOn { get; private set; } = true;
        protected float Clock { get; private set; }   // seconds drawn so far

        // The first controller, this frame (a circular dead zone, for sticks that steer). Back exits, Y toggles the
        // colours; the rest is the game's. PadPressed says what's just gone down.
        protected GamePadState Pad { get; private set; }

        // Where the picture is in the window (scaled up, it's centred with a border), as of the last frame: to turn a
        // mouse position into a point on the picture.
        protected Rectangle PictureArea { get; private set; }

        // `colorsKey` toggles the colours (not Tab, which Alt+Tab would press on the way out).
        protected RetroGame(int windowWidth, int windowHeight, int lowResWidth, int lowResHeight, Keys colorsKey)
        {
            _lowResWidth = lowResWidth;
            _lowResHeight = lowResHeight;
            _colorsKey = colorsKey;
            ColorsOn = Shot?.Keys.Contains('c') != true;
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

        // While true, the keyboard is someone else's (a tool's text box): Escape, F11 and the toggles are left alone.
        protected virtual bool KeyboardCaptured => false;

        // Whether a screenshot (see Shot) is of the whole window, overlay and all, rather than the low-resolution picture.
        protected virtual bool ShotOfWholeWindow => false;

        protected sealed override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            Pad = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);

            if (Pad.Buttons.Back == ButtonState.Pressed)
                Exit();
            if (IsActive && PadPressed(Buttons.Y))
                ColorsOn = !ColorsOn;
            if (!KeyboardCaptured)
            {
                if (keyboard.IsKeyDown(Keys.Escape))
                    Exit();
                if (Pressed(keyboard, Keys.F11))
                    Graphics.ToggleFullScreen();
                if (Pressed(keyboard, _colorsKey))
                    ColorsOn = !ColorsOn;
                if (Pressed(keyboard, Keys.L))
                    LowResOn = !LowResOn;
            }

            UpdateWorld(gameTime, keyboard);

            _previousKeyboard = keyboard;
            _previousPad = Pad;
            base.Update(gameTime);
        }

        // Its own keys and the world moving on. `keyboard` is this frame's; Pressed says what's just gone down.
        protected abstract void UpdateWorld(GameTime gameTime, KeyboardState keyboard);

        // Down now, and up last frame.
        protected bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

        // Down now on the controller, and up last frame.
        protected bool PadPressed(Buttons button) => Pad.IsButtonDown(button) && _previousPad.IsButtonUp(button);

        // +1, -1 or 0 (both or neither), for a pair of keys that push opposite ways.
        protected static float Axis(KeyboardState keyboard, Keys positive, Keys negative) =>
            (keyboard.IsKeyDown(positive) ? 1f : 0f) - (keyboard.IsKeyDown(negative) ? 1f : 0f);

        // The same, for a pair of controller buttons (the d-pad, the shoulders).
        protected static float Axis(GamePadState pad, Buttons positive, Buttons negative) =>
            (pad.IsButtonDown(positive) ? 1f : 0f) - (pad.IsButtonDown(negative) ? 1f : 0f);

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
            var shotDue = Shot is { } due && Clock >= due.After;
            if (shotDue && !ShotOfWholeWindow)
            {
                var shot = Shot!.Value;
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
                PictureArea = destination;
            }
            else
                PictureArea = GraphicsDevice.Viewport.Bounds;

            DrawOverlay(gameTime);

            if (shotDue)
            {
                SaveWindow(Shot!.Value.File);
                WriteShotReport(Path.ChangeExtension(Shot.Value.File, ".txt"));
                Exit();
                return;
            }

            base.Draw(gameTime);
        }

        // Over the picture, at the window's resolution, into the back buffer.
        protected virtual void DrawOverlay(GameTime gameTime)
        {
        }

        private void SaveWindow(string path)
        {
            var back = GraphicsDevice.PresentationParameters;
            var pixels = new Color[back.BackBufferWidth * back.BackBufferHeight];
            GraphicsDevice.GetBackBufferData(pixels);
            using var picture = new Texture2D(GraphicsDevice, back.BackBufferWidth, back.BackBufferHeight);
            picture.SetData(pixels);
            using var file = File.Create(path);
            picture.SaveAsPng(file, back.BackBufferWidth, back.BackBufferHeight);
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
