using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Basic.Models
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private BasicEffect _basicEffect;
        private KeyboardState _previousKeyboard;
        private RasterizerState _rasterizerState;
        private MeshCache _meshCache = new MeshCache();
        private readonly List<SpinningMeshInstance> _instances = new List<SpinningMeshInstance>();
        private bool _colorsOn = true;   // off = faces drawn in the background colour (wireframe look)

        private static readonly Color BackgroundColor = Color.CornflowerBlue;
        private const int IngotCount = 0;
        private const int PyramidCount = 0;
        private const int HouseCount = 0;
        private const int TrabantCount = 0;
        private const int CarCount = 0;
        private const int BarnCount = 0;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 0f, 3f);

        // (top, side, other) schemes for the coloured ingots.
        private static readonly (Color Top, Color Side, Color Other)[] ColorSchemes =
        {
            (Color.Gold, Color.DarkGoldenrod, Color.Silver),
            (Color.OrangeRed, Color.DarkRed, Color.SaddleBrown),
            (Color.LimeGreen, Color.DarkGreen, Color.Olive),
            (Color.DeepSkyBlue, Color.RoyalBlue, Color.Navy),
            (Color.Orchid, Color.DarkMagenta, Color.Indigo),
            (Color.Teal, Color.DarkSlateGray, Color.Aquamarine),
        };

        // (wall, roof) schemes for the houses; the first is the original's terracotta + purple.
        private static readonly (Color Wall, Color Roof)[] HouseSchemes =
        {
            (new Color(210, 140, 80), new Color(150, 80, 200)),
            (new Color(230, 220, 190), new Color(170, 50, 40)),
            (new Color(140, 170, 200), new Color(70, 70, 90)),
            (new Color(200, 190, 100), new Color(40, 110, 70)),
        };

        // Trabant body colours; the first is the original's dark green. Cabin and wheels stay as in the original.
        private static readonly Color[] TrabantBodyColors =
        {
            new Color(34, 85, 34),
            new Color(190, 40, 40),
            new Color(230, 190, 40),
            new Color(60, 90, 190),
        };

        private static readonly Color TrabantCabinColor = new Color(120, 200, 230);
        private static readonly Color TrabantWheelColor = new Color(40, 40, 40);

        // Sports car body colours; glass and wheels are shared.
        private static readonly Color[] CarBodyColors =
        {
            new Color(200, 30, 30),
            new Color(240, 200, 30),
            new Color(235, 235, 235),
            new Color(30, 100, 200),
        };
        private static readonly Color CarGlassColor = new Color(70, 110, 160);
        private static readonly Color CarWheelColor = new Color(30, 30, 30);

        // Barn: red walls, grey roof, cream doors.
        private static readonly Color BarnWallColor = new Color(170, 40, 35);
        private static readonly Color BarnRoofColor = new Color(110, 110, 120);
        private static readonly Color BarnDoorColor = new Color(235, 225, 200);

        // "Without colour": greys, so the faces are still distinguishable (there's no lighting).
        private static readonly (Color Top, Color Side, Color Other) GreyScheme = (Color.LightGray, Color.Gray, Color.DimGray);

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            // Exclusive fullscreen leaves the process running (window gone, exe alive) after exit; use borderless.
            _graphics.HardwareModeSwitch = false;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _basicEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                World = Matrix.Identity,
                View = Matrix.CreateLookAt(CameraPosition, Vector3.Zero, Vector3.Up),
                Projection = Matrix.CreatePerspectiveFieldOfView(
                    MathHelper.PiOver4,
                    GraphicsDevice.Viewport.AspectRatio,
                    0.1f,
                    100f)
            };

            _rasterizerState = new RasterizerState { CullMode = CullMode.None };
            // One shared mesh per shape; every instance only carries its own transform and colours.
            var ingot = _meshCache.GetOrAdd(GraphicsDevice, "ingot", IngotMesh.Build);
            BuildIngots(ingot);
            var pyramid = _meshCache.GetOrAdd(GraphicsDevice, "pyramid", PyramidMesh.Build);
            BuildPyramids(pyramid);
            var house = _meshCache.GetOrAdd(GraphicsDevice, "house", HouseMesh.Build);
            BuildHouses(house);
            var trabant = _meshCache.GetOrAdd(GraphicsDevice, "trabant", TrabantMesh.Build);
            BuildTrabants(trabant);
            var car = _meshCache.GetOrAdd(GraphicsDevice, "car", CarMesh.Build);
            BuildCars(car);
            var barn = _meshCache.GetOrAdd(GraphicsDevice, "barn", BarnMesh.Build);
            BuildBarns(barn);
            BuildStillLife();
        }

        // A living room: a coffee table with a fern on it, a sofa behind it, a settee beside it and a sideboard
        // with a 1950s television on it, with the garden (oaks and spiky bushes) behind. Everything stands
        // upright on the floor and does not rotate. Placed by hand, at fixed positions, so nothing overlaps.
        // Everything is at one scale, so how big each thing is next to the others is its true relative size.
        private void BuildStillLife()
        {
            const float ground = -0.7f;   // y of the floor they stand on (the camera is at y = 0)
            const float s = 1.4f;

            var table = _meshCache.GetOrAdd(GraphicsDevice, "coffeetable", CoffeeTableMesh.Build);
            var fern = _meshCache.GetOrAdd(GraphicsDevice, "fern", FernMesh.Build);
            var sofa = _meshCache.GetOrAdd(GraphicsDevice, "sofa", SofaMesh.Build);
            var settee = _meshCache.GetOrAdd(GraphicsDevice, "settee", SetteeMesh.Build);
            var sideboard = _meshCache.GetOrAdd(GraphicsDevice, "sideboard", SideboardMesh.Build);
            var television = _meshCache.GetOrAdd(GraphicsDevice, "television", TelevisionMesh.Build);
            var oak = _meshCache.GetOrAdd(GraphicsDevice, "oak", OakMesh.Build);
            var spikyBush = _meshCache.GetOrAdd(GraphicsDevice, "spikybush", SpikyBushMesh.Build);

            var tablePalette = CoffeeTableMesh.Palette(new Color(205, 155, 95), new Color(120, 80, 50));
            var fernPalette = FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60));
            var sofaPalette = SofaMesh.Palette(new Color(60, 125, 125), new Color(100, 170, 160), new Color(150, 100, 60));
            var setteePalette = SetteeMesh.Palette(new Color(195, 145, 45), new Color(225, 180, 85), new Color(150, 100, 60));
            var sideboardPalette = SideboardMesh.Palette(new Color(130, 80, 45), new Color(170, 115, 65), new Color(90, 55, 30), new Color(205, 175, 90));
            var televisionPalette = TelevisionMesh.Palette(new Color(110, 70, 40), new Color(120, 140, 130), new Color(235, 225, 200), new Color(60, 45, 35), new Color(90, 55, 30), new Color(190, 190, 195));
            var oakPalette = OakMesh.Palette(new Color(100, 70, 45), new Color(70, 145, 55));
            var spikyBushPalette = SpikyBushMesh.Palette(new Color(95, 65, 40), new Color(90, 130, 50));

            // lift: how far above the floor the thing stands (for the fern on the table, the television on the sideboard)
            void Place(MeshData mesh, Color[] palette, float x, float z, float scale, float yaw, float lift = 0f) =>
                _instances.Add(new SpinningMeshInstance(mesh, palette)
                {
                    Position = new Vector3(x, ground + lift, z),
                    Scale = scale,
                    Pitch = 0f,   // upright
                    Yaw = yaw,
                    YawSpeed = 0f,
                    PitchSpeed = 0f,
                });

            // Yaw turns a piece about the vertical; the seats and cabinets face +Z (towards the camera) at yaw 0,
            // so +90 degrees makes one face +X (to the right) and -90 degrees makes one face -X (to the left).
            const float faceRight = MathHelper.PiOver2, faceLeft = -MathHelper.PiOver2;

            // The living group, around a coffee table at (0, -3): the sofa behind it, the settee on its left,
            // and on its right the sideboard with the television, which the seats look across the table at.
            Place(table, tablePalette, 0.0f, -3.0f, s, 0.0f);
            Place(fern, fernPalette, 0.0f, -3.0f, 0.7f * s, 0.4f, lift: 0.28f * s);   // on the table top, a little smaller
            Place(sofa, sofaPalette, 0.0f, -4.3f, s, 0.0f);
            Place(settee, setteePalette, -1.7f, -2.75f, s, faceRight);
            Place(sideboard, sideboardPalette, 2.5f, -3.3f, s, faceLeft);
            Place(television, televisionPalette, 2.5f, -3.3f, s, faceLeft, lift: 0.64f * s);

            // The second coffee table and the second fern (on the floor), in front
            Place(table, tablePalette, -1.9f, -0.9f, s, 0.0f);
            Place(fern, fernPalette, 2.0f, -0.9f, s, 0.8f);

            // Garden behind: an oak on each side and two spiky bushes between them
            Place(oak, oakPalette, -5.0f, -8.5f, s, 0.3f);
            Place(oak, oakPalette, 5.0f, -8.5f, s, 1.1f);
            Place(spikyBush, spikyBushPalette, -1.8f, -9.2f, s, 0.0f);
            Place(spikyBush, spikyBushPalette, 1.9f, -9.6f, s, 0.5f);
        }

        private void BuildIngots(MeshData mesh)
        {
            var rng = new Random(1234);   // fixed seed: same scene every run
            var halfFovTan = MathF.Tan(MathHelper.PiOver4 / 2f);
            var aspect = GraphicsDevice.Viewport.AspectRatio;

            float Between(float min, float max) => min + (max - min) * rng.NextSingle();
            float RandomSign() => rng.Next(2) == 0 ? -1f : 1f;

            for (var i = 0; i < IngotCount; i++)
            {
                var colored = rng.NextDouble() < 0.6;
                var scheme = colored ? ColorSchemes[rng.Next(ColorSchemes.Length)] : GreyScheme;

                // Distance in front of the camera. Spread x/y across the visible area at that
                // depth (90% of it) so near and far ingots are all on screen.
                var distance = Between(3f, 20f);
                var halfHeight = halfFovTan * distance * 0.9f;
                var halfWidth = halfHeight * aspect;

                _instances.Add(new SpinningMeshInstance(mesh, IngotMesh.Palette(scheme.Top, scheme.Side, scheme.Other))
                {
                    Position = new Vector3(
                        Between(-halfWidth, halfWidth),
                        Between(-halfHeight, halfHeight),
                        CameraPosition.Z - distance),
                    Scale = Between(0.3f, 1.5f),
                    Yaw = Between(0f, MathHelper.TwoPi),
                    Pitch = Between(0f, MathHelper.TwoPi),
                    YawSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 120f)),
                    PitchSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 100f)),
                });
            }
        }

        // Separate Random so the ingot scene above is unchanged by the pyramids.
        private void BuildPyramids(MeshData mesh)
        {
            var rng = new Random(5678);
            float Between(float min, float max) => min + (max - min) * rng.NextSingle();

            for (var i = 0; i < PyramidCount; i++)
            {
                var scheme = ColorSchemes[rng.Next(ColorSchemes.Length)];
                _instances.Add(new SpinningMeshInstance(mesh, PyramidMesh.Palette(scheme.Other, scheme.Top))
                {
                    Position = new Vector3(Between(-4f, 4f), Between(-2.5f, 2.5f), CameraPosition.Z - Between(4f, 10f)),
                    Scale = Between(0.5f, 1.2f),
                    Yaw = Between(0f, MathHelper.TwoPi),
                    Pitch = Between(0f, MathHelper.TwoPi),
                    YawSpeed = MathHelper.ToRadians(Between(15f, 90f)),
                    PitchSpeed = MathHelper.ToRadians(Between(15f, 90f)),
                });
            }
        }

        // Own seed again, so the ingots and pyramids are unaffected by the houses.
        private void BuildHouses(MeshData mesh)
        {
            var rng = new Random(9012);
            float Between(float min, float max) => min + (max - min) * rng.NextSingle();
            float RandomSign() => rng.Next(2) == 0 ? -1f : 1f;

            for (var i = 0; i < HouseCount; i++)
            {
                var scheme = HouseSchemes[rng.Next(HouseSchemes.Length)];
                var palette = HouseMesh.Palette(scheme.Wall, scheme.Roof, Color.White, Color.Blue, new Color(80, 40, 20));
                _instances.Add(new SpinningMeshInstance(mesh, palette)
                {
                    Position = new Vector3(Between(-5f, 5f), Between(-3f, 3f), CameraPosition.Z - Between(4f, 12f)),
                    Scale = Between(0.5f, 1.0f),
                    Yaw = Between(0f, MathHelper.TwoPi),
                    Pitch = Between(0f, MathHelper.TwoPi),
                    YawSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 90f)),
                    PitchSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 70f)),
                });
            }
        }

        // Own seed again, so nothing already in the scene moves when cars are added.
        private void BuildTrabants(MeshData mesh)
        {
            var rng = new Random(3456);
            float Between(float min, float max) => min + (max - min) * rng.NextSingle();
            float RandomSign() => rng.Next(2) == 0 ? -1f : 1f;

            for (var i = 0; i < TrabantCount; i++)
            {
                var body = TrabantBodyColors[rng.Next(TrabantBodyColors.Length)];
                var palette = TrabantMesh.Palette(body, TrabantCabinColor, TrabantWheelColor);
                _instances.Add(new SpinningMeshInstance(mesh, palette)
                {
                    // One horizontal slot each (plus jitter) so the cars don't pile up on top of each other.
                    Position = new Vector3(
                        MathHelper.Lerp(-5f, 5f, (i + 0.5f) / TrabantCount) + Between(-0.5f, 0.5f),
                        Between(-2.5f, 2.5f),
                        CameraPosition.Z - Between(5f, 9f)),
                    Scale = Between(0.8f, 1.6f),
                    Yaw = Between(0f, MathHelper.TwoPi),
                    Pitch = Between(0f, MathHelper.TwoPi),
                    YawSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 90f)),
                    PitchSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 70f)),
                });
            }
        }

        // Own seed again, so nothing already in the scene moves when cars are added.
        private void BuildCars(MeshData mesh)
        {
            var rng = new Random(7788);
            float Between(float min, float max) => min + (max - min) * rng.NextSingle();
            float RandomSign() => rng.Next(2) == 0 ? -1f : 1f;

            for (var i = 0; i < CarCount; i++)
            {
                var body = CarBodyColors[rng.Next(CarBodyColors.Length)];
                var palette = CarMesh.Palette(body, CarGlassColor, CarWheelColor);
                _instances.Add(new SpinningMeshInstance(mesh, palette)
                {
                    // One horizontal slot each (plus jitter) so the cars don't pile up.
                    Position = new Vector3(
                        MathHelper.Lerp(-4.5f, 4.5f, (i + 0.5f) / CarCount) + Between(-0.5f, 0.5f),
                        Between(-2.5f, 2.5f),
                        CameraPosition.Z - Between(5f, 10f)),
                    Scale = Between(0.9f, 1.7f),
                    Yaw = Between(0f, MathHelper.TwoPi),
                    Pitch = Between(0f, MathHelper.TwoPi),
                    YawSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 90f)),
                    PitchSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 70f)),
                });
            }
        }

        // Own seed again.
        private void BuildBarns(MeshData mesh)
        {
            var rng = new Random(2468);
            float Between(float min, float max) => min + (max - min) * rng.NextSingle();
            float RandomSign() => rng.Next(2) == 0 ? -1f : 1f;

            var palette = BarnMesh.Palette(BarnWallColor, BarnRoofColor, BarnDoorColor);
            for (var i = 0; i < BarnCount; i++)
            {
                _instances.Add(new SpinningMeshInstance(mesh, palette)
                {
                    Position = new Vector3(
                        MathHelper.Lerp(-4f, 4f, (i + 0.5f) / BarnCount) + Between(-0.5f, 0.5f),
                        Between(-2f, 2f),
                        CameraPosition.Z - Between(6f, 11f)),
                    Scale = Between(0.6f, 1.0f),
                    Yaw = Between(0f, MathHelper.TwoPi),
                    Pitch = Between(0f, MathHelper.TwoPi),
                    YawSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 70f)),
                    PitchSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 50f)),
                });
            }
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (keyboard.IsKeyDown(Keys.F11) && _previousKeyboard.IsKeyUp(Keys.F11))
                _graphics.ToggleFullScreen();

            if (keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space))
                _colorsOn = !_colorsOn;

            _previousKeyboard = keyboard;
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (var instance in _instances)
            {
                instance.ColorsOn = _colorsOn;
                instance.Spin(dt);
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(BackgroundColor);
            GraphicsDevice.RasterizerState = _rasterizerState;

            foreach (var instance in _instances)
                instance.Draw(gameTime, GraphicsDevice, _basicEffect, BackgroundColor);

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _meshCache.Dispose();
                _basicEffect?.Dispose();
                _rasterizerState?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
