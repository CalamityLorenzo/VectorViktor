using Hexa.NET.ImGui;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using Tools.DearImGui;
using World.Core.Animation;
using World.Core.Characters;
using World.Core.Movement;
using World.Maps;
using World.Maps.Files;
using World.Rendering;
using Num = System.Numerics;

namespace Droid.Playground
{
    public enum CameraMode { Head, Drone, Free }

    // The playtest harness: the home map with the droid dropped into it, an experiment deciding how it behaves (see
    // Experiment), and the tools to watch it by - time stopped, stepped or slowed; the droid's head camera, its drone,
    // or a camera flying free; overlays of what's usually unseen; and panels (Dear ImGui) with the experiment's live
    // values. The world runs in fixed ticks as the game's does, on the harness's own clock.
    //
    // Keys (when no panel has the keyboard): arrows (and W, A, S, D outside the free camera) drive; Shift goes faster;
    // P pauses, N steps a tick while paused, [ and ] slow and speed time; F1 head camera, F2 drone, F3 free camera;
    // Q/E run the head camera round its visor, R/F tilt it, Home puts it back; T drops the droid under the free camera;
    // Ctrl+click drops it on the ground clicked; Enter opens or shuts a door (or a drawer, a cupboard); K skips to the end
    // of a cut scene; C colours, L low resolution, Esc exits. G changes what the droid goes about on (see Locomotion): its
    // Segway wheels, tank tracks or tri-star wheels; none goes sideways, and on tracks A and D turn its turntable instead.
    //
    // On a controller: the left stick drives (outside the free camera), the right stick turns and tilts the head
    // camera, the right trigger goes faster; the shoulders run the head camera round its visor, a click of the right
    // stick puts it back; A opens or shuts a door, B changes camera (head, drone, free); Start pauses, the d-pad's
    // down steps a tick, left and right slow and speed time, up drops the droid under the free camera; Y colours,
    // Back exits. In the free camera the sticks fly it and look, the shoulders rise and sink (see FreeCamera).
    public sealed class Playground : RetroGame
    {
        private const int WindowWidth = 1440, WindowHeight = 810;
        private const int LowResWidth = 480, LowResHeight = 270;   // 16:9, three times over fills the window
        private const float StepTime = 1f / 60f;
        private const float MaxFrame = 0.25f;
        private static readonly (float scale, string name)[] Speeds = { (1f, "full speed"), (0.5f, "half"), (0.25f, "quarter"), (0.125f, "eighth") };

        private Map _map;
        private readonly Func<Map>? _reopen;
        private readonly (Vector2 at, float yaw)? _dropIn;
        private MapWatcher? _watcher;
        private string _mapStatus = "";
        private readonly string _startName;
        private string _experimentName;
        private Locomotion _locomotion;

        private BuiltWorld _built = null!;
        private Player _player = null!;
        private Rig _rig = null!;
        private DroidMotion _motion = null!;
        private Session _session = null!;
        private Experiment _experiment = null!;
        private RigView _rigView = null!;
        private WorldRenderer _renderer = null!;
        private MeshCache _worldMeshes = null!;
        private DebugLines _lines = null!;
        private ImGuiRenderer _imgui = null!;
        private string[] _starts = Array.Empty<string>();
        private readonly FreeCamera _free = new FreeCamera();
        private readonly (Vector3 feet, float radius, float height)[] _walkers = new (Vector3, float, float)[1];

        private CameraMode _camera = CameraMode.Head;
        private float _around, _up;              // the head camera round its visor, and tilted
        private bool _levelHorizon;
        private bool _paused, _stepOnce;
        private int _speed;                      // index into Speeds
        private float _pending;
        private bool _showWalls, _showBodies, _showJoints, _showCapsule;
        private MouseState _mouse;
        private float _frameSeconds;

        // `reopen` opens the map again from its files when they're saved (see MapWatcher); `dropIn` puts the droid at that
        // point on the ground, facing that way, rather than at a start (the map studio's "Play here").
        public Playground(string experiment, Map map, string? start, Func<Map>? reopen = null, (Vector2 at, float yaw)? dropIn = null,
            Locomotion locomotion = Locomotion.Segway)
            : base(WindowWidth, WindowHeight, LowResWidth, LowResHeight, colorsKey: Keys.C)
        {
            _experimentName = experiment;
            _locomotion = locomotion;
            _map = map;
            _reopen = reopen;
            _dropIn = dropIn;
            _startName = start ?? map.DefaultStart;
        }

        protected override bool KeyboardCaptured => _imgui?.WantsKeyboard == true;
        protected override bool ShotOfWholeWindow => true;

        protected override void LoadWorld()
        {
            Build();
            var start = _built.Starts.TryGetValue(_startName, out var chosen) ? chosen : _built.Starts[_map.DefaultStart];
            if (_dropIn is { } dropIn)
                start = new Start(dropIn.at, dropIn.yaw, Above: DropInAbove);
            _player = new Player(Feet(start), start.Yaw, _built.Physics);
            FitBase(_locomotion);
            MakeRenderer();
            _lines = new DebugLines(GraphicsDevice);
            _imgui = new ImGuiRenderer(GraphicsDevice, Window);
            _free.LookFrom(_player.Body.Position + new Vector3(3f, 2f, 3f), _player.Body.Position + Vector3.Up);
            Pick(_experimentName);
            if (_reopen != null && _map.Files.Count > 0)
                _watcher = new MapWatcher(_map.Files);

            // A screenshot's keys (see RetroGame): d drone view, f free camera, o every overlay; w forward, r faster, t turning right
            if (Shot is { } shot)
            {
                _camera = shot.Keys.Contains('f') ? CameraMode.Free : shot.Keys.Contains('d') ? CameraMode.Drone : CameraMode.Head;
                _showWalls = _showBodies = _showJoints = _showCapsule = shot.Keys.Contains('o');
            }
        }

        // The droid on `locomotion`: its body going about that way, its rig built on it and drawn, and the experiment started
        // again with it.
        private void FitBase(Locomotion locomotion)
        {
            _locomotion = locomotion;
            _player.Body.Gait = Locomotions.GaitOf(locomotion);
            _rig = DroidRig.Build(locomotion);
            _motion = new DroidMotion(locomotion);
            var palette = DroidMesh.StartingPalette();
            _rigView = new RigView(_rig, DroidMesh.Sources(palette), DroidMesh.CableSource(palette), GraphicsDevice, MeshCache);
            _session = NewSession();
            if (_experiment != null)
                Pick(_experimentName);
        }

        // Dropped in at a point (see the constructor's `dropIn`), it's from this high, to land on whatever's there: the
        // ground, or the floor of a building standing on it
        private const float DropInAbove = 1.5f;

        private void Build()
        {
            _built = WorldBuilder.Build(_map);
            _starts = _built.Starts.Keys.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }

        private void MakeRenderer()
        {
            // A mesh cache of its own, thrown away with it: some meshes are built from the world they're in (a fence follows
            // the ground), so a world built again needs them built again too
            _worldMeshes = new MeshCache();
            _renderer = new WorldRenderer(_built, GraphicsDevice, _worldMeshes);
            _renderer.BuildTerrain(_player);
            // The drone's camera, for the televisions tuned to it (see ScreenSpec): the droid, from wherever it's following
            _renderer.Feed(WorldRenderer.DroneChannel, () => DroneView(), batch =>
            {
                _rigView.Add(batch);
                _experiment.Add(_session, batch);
            });
        }

        // The map's files have been saved (by the map studio, say): the map built again from them, with the droid where it
        // was, and the experiment started again in the new world. If they don't make a map now, the world stays as it was
        // and the panel says what's wrong.
        private void Reload()
        {
            try
            {
                _map = _reopen!();
                Build();
                _watcher!.Dispose();
                _watcher = new MapWatcher(_map.Files);   // it may have gained a district file, or lost one
            }
            catch (Exception e) when (e is System.IO.InvalidDataException or System.IO.IOException or InvalidOperationException)
            {
                _mapStatus = "The map didn't load: " + e.Message;
                return;
            }
            var (feet, yaw) = (_player.Body.Position, _player.Body.Yaw);
            _player = new Player(feet + Vector3.Up * 0.1f, yaw, _built.Physics);
            _player.Body.Gait = Locomotions.GaitOf(_locomotion);
            _session = NewSession();
            _renderer.Dispose();
            _worldMeshes.Dispose();
            MakeRenderer();
            Pick(_experimentName);
            _mapStatus = $"Built again from its files at {DateTime.Now:HH:mm:ss}.";
        }

        // The droid in the world as it is now, on the same clock
        private Session NewSession() =>
            new Session(_built, _player, _rig, _motion, _rigView, GraphicsDevice, MeshCache) { Clock = _session?.Clock ?? 0f };

        // Where a start puts your feet: on the ground, or dropped from above it (to land on a floor up in a building)
        private Vector3 Feet(Start start) =>
            new Vector3(start.At.X, start.Above > 0f ? _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above : 0f, start.At.Y);

        private void Pick(string name)
        {
            _experiment?.Stop(_session);
            _experimentName = name;
            _experiment = Experiments.Make(name);
            _experiment.Start(_session);
            Window.Title = $"Droid Playground - {name} on {Locomotions.NameOf(_locomotion)}";
        }

        private void DropAt(Vector3 feet, float yaw) => _player.Teleport(feet, yaw, _built.Physics);

        // ---- Each frame's input, and the world moving on

        protected override void UpdateWorld(GameTime gameTime, KeyboardState keyboard)
        {
            if (_watcher?.Changed() == true)
                Reload();
            _frameSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = Mouse.GetState(Window);
            var keys = IsActive && !_imgui.WantsKeyboard;
            var pointer = IsActive && !_imgui.WantsMouse;
            var pad = IsActive ? Pad : default;

            if (keys)
            {
                if (Pressed(keyboard, Keys.P)) _paused = !_paused;
                if (Pressed(keyboard, Keys.N)) _stepOnce = true;
                if (Pressed(keyboard, Keys.OemOpenBrackets)) _speed = Math.Min(_speed + 1, Speeds.Length - 1);
                if (Pressed(keyboard, Keys.OemCloseBrackets)) _speed = Math.Max(_speed - 1, 0);
                if (Pressed(keyboard, Keys.F1)) _camera = CameraMode.Head;
                if (Pressed(keyboard, Keys.F2)) _camera = CameraMode.Drone;
                if (Pressed(keyboard, Keys.F3)) _camera = CameraMode.Free;
                if (Pressed(keyboard, Keys.Home)) _around = _up = 0f;
                if (Pressed(keyboard, Keys.T)) DropUnder(_free.Position);
                if (Pressed(keyboard, Keys.Enter)) _built.Ground.Interact(_player.Body.Position, _player.Body.Heading);
                if (Pressed(keyboard, Keys.K)) _experiment.Skip(_session);
                if (Pressed(keyboard, Keys.G)) FitBase(Locomotions.All[(Locomotions.IndexOf(_locomotion) + 1) % Locomotions.All.Count]);
                _around = MathHelper.WrapAngle(_around + Axis(keyboard, Keys.Q, Keys.E) * 2f * _frameSeconds);
                _up = Math.Clamp(_up + Axis(keyboard, Keys.R, Keys.F) * 1f * _frameSeconds, -DroidRig.MaxLookDown, DroidRig.MaxLookUp);
            }
            if (IsActive)
            {
                if (PadPressed(Buttons.Start)) _paused = !_paused;
                if (PadPressed(Buttons.DPadDown)) _stepOnce = true;
                if (PadPressed(Buttons.DPadLeft)) _speed = Math.Min(_speed + 1, Speeds.Length - 1);
                if (PadPressed(Buttons.DPadRight)) _speed = Math.Max(_speed - 1, 0);
                if (PadPressed(Buttons.B)) _camera = (CameraMode)(((int)_camera + 1) % 3);
                if (PadPressed(Buttons.RightStick)) _around = _up = 0f;
                if (PadPressed(Buttons.DPadUp)) DropUnder(_free.Position);
                if (PadPressed(Buttons.A)) _built.Ground.Interact(_player.Body.Position, _player.Body.Heading);
                if (_camera != CameraMode.Free)   // there the shoulders and the right stick are the camera's
                {
                    _around = MathHelper.WrapAngle(_around + Axis(pad, Buttons.LeftShoulder, Buttons.RightShoulder) * 2f * _frameSeconds);
                    _up = Math.Clamp(_up + pad.ThumbSticks.Right.Y * 1f * _frameSeconds, -DroidRig.MaxLookDown, DroidRig.MaxLookUp);
                }
            }
            if (_camera == CameraMode.Free)
            {
                var turn = pointer && mouse.RightButton == ButtonState.Pressed
                    ? new Vector2(mouse.X - _mouse.X, mouse.Y - _mouse.Y) : Vector2.Zero;
                _free.Step(keys ? keyboard : default, pad, turn, _frameSeconds);
            }
            if (pointer && mouse.LeftButton == ButtonState.Pressed && _mouse.LeftButton == ButtonState.Released &&
                keyboard.IsKeyDown(Keys.LeftControl) && GroundUnder(mouse) is { } clicked)
                DropAt(clicked, _player.Body.Yaw);
            _mouse = mouse;

            var asked = keys ? ReadInput(keyboard, wasd: _camera != CameraMode.Free) : MoveInput.None;
            if (_camera != CameraMode.Free)
                asked = WithPad(asked, pad);
            if (Shot is { } shot && shot.Keys.Contains('w'))
                asked = new MoveInput(new Vector2(0f, 1f), Run: shot.Keys.Contains('r'));
            if (Shot is { } turning && turning.Keys.Contains('t'))
                asked = asked with { Turn = 1f };

            if (_paused)
            {
                _pending = 0f;
                if (_stepOnce)
                    Tick(asked);
            }
            else
            {
                _pending += MathF.Min(_frameSeconds, MaxFrame) * Speeds[_speed].scale;
                while (_pending >= StepTime)
                {
                    Tick(asked);
                    _pending -= StepTime;
                }
            }
            _stepOnce = false;
        }

        private static MoveInput ReadInput(KeyboardState keyboard, bool wasd)
        {
            var forward = Axis(keyboard, Keys.Up, Keys.Down) + (wasd ? Axis(keyboard, Keys.W, Keys.S) : 0f);
            var sideways = wasd ? Axis(keyboard, Keys.D, Keys.A) : 0f;
            return new MoveInput(new Vector2(sideways, MathHelper.Clamp(forward, -1f, 1f)), Axis(keyboard, Keys.Right, Keys.Left),
                keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
        }

        // The controller's asking added to the keyboard's: the left stick to go, the right stick's sideways to turn.
        private static MoveInput WithPad(MoveInput asked, GamePadState pad) => asked with
        {
            Move = Vector2.Clamp(asked.Move + pad.ThumbSticks.Left, -Vector2.One, Vector2.One),
            Turn = MathHelper.Clamp(asked.Turn + pad.ThumbSticks.Right.X, -1f, 1f),
            Run = asked.Run || pad.Triggers.Right > 0.5f,
        };

        // One tick of the world: the experiment says what the droid's told to do, the world moves on, the rig follows.
        private void Tick(MoveInput asked)
        {
            var told = _experiment.Drive(_session, asked, StepTime);
            var gait = _player.Body.Gait;
            _walkers[0] = (_player.Body.Position, gait.Radius, gait.Height);
            _built.Ground.StepDoors(StepTime, _built.Physics.Bodies, _walkers);
            _player.Step(told, StepTime, _built.Physics);
            foreach (var portal in _built.Portals)
                if (!portal.LeadsOffMap && portal.WalkedInto(_player.Body.Position, gait.Radius))
                {
                    _player.Teleport(portal.To, portal.Yaw, _built.Physics);
                    break;
                }
            _built.Physics.Step(StepTime);
            // Going sideways is what turns a tank's turntable (it can't go sideways itself): right turns it right
            _motion.Follow(_player.Body, StepTime, _built.Physics, gait.Strafes ? 0f : -told.Move.X);
            _session.Clock += StepTime;
            _experiment.AfterTick(_session, StepTime);
        }

        // ---- Picking the ground

        // The terrain under the mouse (buildings aren't looked at), or nothing: along the ray from the camera through the
        // mouse's point on the picture, in steps, then narrowed down to where it crosses the ground.
        private Vector3? GroundUnder(MouseState mouse)
        {
            var area = PictureArea;
            if (!area.Contains(mouse.X, mouse.Y))
                return null;
            var viewport = new Viewport(0, 0, area.Width, area.Height);   // the same shape as the picture, so the projection fits it
            var (x, y) = (mouse.X - area.X, mouse.Y - area.Y);
            var near = viewport.Unproject(new Vector3(x, y, 0f), _renderer.Projection, _renderer.ViewMatrix, Matrix.Identity);
            var far = viewport.Unproject(new Vector3(x, y, 1f), _renderer.Projection, _renderer.ViewMatrix, Matrix.Identity);
            var along = Vector3.Normalize(far - near);
            bool Below(float t) { var p = near + along * t; return p.Y <= _built.Terrain.HeightAt(p.X, p.Z); }

            for (var t = 0.25f; t < WorldRenderer.FogEnd; t += 0.25f)
                if (Below(t))
                {
                    var (above, below) = (t - 0.25f, t);
                    for (var i = 0; i < 12; i++)
                    {
                        var middle = (above + below) / 2f;
                        if (Below(middle)) below = middle; else above = middle;
                    }
                    return near + along * below;
                }
            return null;
        }

        private void DropUnder(Vector3 point) =>
            DropAt(new Vector3(point.X, _built.Terrain.HeightAt(point.X, point.Z), point.Z), _player.Body.Yaw);

        // ---- Drawing

        protected override void DrawWorld(GameTime gameTime)
        {
            // The rig: back at rest, then its movement, where its head camera's looking, and whatever the experiment adds
            var body = _player.Body;
            _rig.Reset();
            _motion.Pose(_rig, _session.Clock);
            DroidRig.Look(_rig, _around, _up);
            _experiment.Pose(_session, _rig);
            _rig.Solve(_session.Placement);

            // A cut scene's camera, if one's playing; else the one picked
            Vector3 eye, forward, up;
            var camera = _camera;
            if (_experiment.Camera(_session) is { } scene)
                ((eye, forward, up), camera) = ((scene.view.Eye, scene.view.Forward, scene.view.Up), scene.mode);
            else
                switch (_camera)
                {
                    case CameraMode.Head:
                        (eye, forward, up) = DroidRig.CameraView(_rig);
                        if (_levelHorizon)
                            up = Vector3.Up;
                        break;
                    case CameraMode.Drone:
                        var drone = DroneView();
                        (eye, forward, up) = (drone.Eye, drone.Forward, drone.Up);
                        break;
                    default:
                        (eye, forward, up) = (_free.Position, _free.Forward, Vector3.Up);
                        break;
                }
            // The televisions' feeds first; then, from its own head camera, the camera's own mesh would be all round the eye:
            // leave it out
            _renderer.DrawFeeds(body.Position, _session.Clock, ColorsOn);
            _renderer.DrawFrom(eye, eye + forward, up, body.Position, _session.Clock, ColorsOn,
                batch =>
                {
                    _rigView.Add(batch, node => camera != CameraMode.Head || node.Part != DroidRig.CameraPart);
                    _experiment.Add(_session, batch);
                });

            AddOverlays(eye);
            _lines.Draw(GraphicsDevice, _renderer.ViewMatrix, _renderer.Projection);
        }

        // The droid's drone looking at it, from wherever it's following.
        private CameraView DroneView()
        {
            var eye = _player.Drone.Position;
            return new CameraView(eye, Vector3.Normalize(_player.Body.Position + Vector3.Up * 1.1f - eye), Vector3.Up);
        }

        private void AddOverlays(Vector3 eye)
        {
            if (_showWalls)
                foreach (var wall in _built.Ground.Walls)
                {
                    var (a, b) = (new Vector3(wall.A.X, 0f, wall.A.Y), new Vector3(wall.B.X, 0f, wall.B.Y));
                    if (Vector3.Distance(eye, ((a + b) / 2f) with { Y = eye.Y }) > 40f)
                        continue;
                    var (bottom, top) = (Vector3.Up * wall.Bottom, Vector3.Up * wall.Top);
                    _lines.Line(a + bottom, b + bottom, Color.Yellow);
                    _lines.Line(a + top, b + top, Color.Yellow);
                    _lines.Line(a + bottom, a + top, Color.Yellow);
                    _lines.Line(b + bottom, b + top, Color.Yellow);
                }
            if (_showBodies)
                foreach (var thing in _built.Physics.Bodies)
                    if (Vector3.Distance(eye, thing.Position) < 60f)
                        _lines.Box(thing.Pose, thing.OriginalSize, Color.Cyan);
            if (_showJoints)
                for (var i = 0; i < _rig.Count; i++)
                    _lines.Axes(_rig.World(i), 0.08f);
            if (_showCapsule)
            {
                var feet = _player.Body.Position;
                var gait = _player.Body.Gait;
                _lines.Circle(feet, gait.Radius, Color.Lime);
                _lines.Circle(feet + Vector3.Up * gait.Height, gait.Radius, Color.Lime);
                for (var k = 0; k < 4; k++)
                {
                    var side = new Vector3(MathF.Cos(k * MathHelper.PiOver2), 0f, MathF.Sin(k * MathHelper.PiOver2)) * gait.Radius;
                    _lines.Line(feet + side, feet + side + Vector3.Up * gait.Height, Color.Lime);
                }
            }
        }

        // ---- Panels

        protected override void DrawOverlay(GameTime gameTime)
        {
            _imgui.BeginFrame(_frameSeconds);
            PlaygroundPanel();
            ImGui.SetNextWindowPos(new Num.Vector2(WindowWidth - 380, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Num.Vector2(370, 0), ImGuiCond.FirstUseEver);
            ImGui.Begin($"Experiment: {_experiment.Name}");
            ImGui.TextWrapped(_experiment.About);
            ImGui.Separator();
            _experiment.Panel(_session);
            ImGui.End();
            _imgui.EndFrame();
        }

        private void PlaygroundPanel()
        {
            ImGui.SetNextWindowPos(new Num.Vector2(10, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Num.Vector2(330, 0), ImGuiCond.FirstUseEver);
            ImGui.Begin("Playground");
            ImGui.Text($"map: {_map.Name} ({(_map.Files.Count > 0 ? "from its files" : "built in code")})");
            if (_mapStatus.Length > 0)
                ImGui.TextWrapped(_mapStatus);

            var names = Experiments.All.Select(e => e.name).ToArray();
            var chosen = Array.IndexOf(names, _experimentName);
            if (ImGui.Combo("experiment", ref chosen, names, names.Length))
                Pick(names[chosen]);
            if (ImGui.Button("restart it"))
                Pick(_experimentName);

            if (ImGui.CollapsingHeader("Time", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("paused (P)", ref _paused);
                ImGui.SameLine();
                if (ImGui.Button("step a tick (N)"))
                    _stepOnce = true;
                var speeds = Speeds.Select(s => s.name).ToArray();
                ImGui.Combo("speed ([ ])", ref _speed, speeds, speeds.Length);
                ImGui.Text($"world time {_session.Clock:F2} s");
            }

            if (ImGui.CollapsingHeader("Camera", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var camera = (int)_camera;
                ImGui.RadioButton("head (F1)", ref camera, (int)CameraMode.Head);
                ImGui.SameLine();
                ImGui.RadioButton("drone (F2)", ref camera, (int)CameraMode.Drone);
                ImGui.SameLine();
                ImGui.RadioButton("free (F3)", ref camera, (int)CameraMode.Free);
                _camera = (CameraMode)camera;
                ImGui.SliderAngle("round visor (Q/E)", ref _around, -180f, 180f);
                ImGui.SliderAngle("tilt (R/F)", ref _up, -90f, 60f);
                ImGui.Checkbox("level horizon", ref _levelHorizon);
            }

            if (ImGui.CollapsingHeader("Drop in"))
            {
                foreach (var name in _starts)
                    if (ImGui.Selectable(name, name == _startName))
                        DropAt(Feet(_built.Starts[name]), _built.Starts[name].Yaw);
                if (ImGui.Button("under the free camera (T)"))
                    DropUnder(_free.Position);
                ImGui.TextDisabled("or Ctrl+click the ground");
            }

            if (ImGui.CollapsingHeader("Overlays", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Checkbox("walls", ref _showWalls);
                ImGui.SameLine();
                ImGui.Checkbox("bodies", ref _showBodies);
                ImGui.SameLine();
                ImGui.Checkbox("joints", ref _showJoints);
                ImGui.SameLine();
                ImGui.Checkbox("capsule", ref _showCapsule);
            }

            if (ImGui.CollapsingHeader("Droid", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var bases = Locomotions.All.Select(Locomotions.NameOf).ToArray();
                var fitted = Locomotions.IndexOf(_locomotion);
                if (ImGui.Combo("goes on (G)", ref fitted, bases, bases.Length))
                    FitBase(Locomotions.All[fitted]);
                ImGui.TextWrapped(Locomotions.About(_locomotion));
                var gait = _player.Body.Gait;
                ImGui.TextDisabled($"steps up {gait.StepUp:F2} m, {gait.Radius * 2f:F2} m across, {gait.Height:F2} m tall, {gait.Speed:F1} m/s");
                var body = _player.Body;
                ImGui.Text($"at {body.Position.X:F1}, {body.Position.Y:F2}, {body.Position.Z:F1}   facing {MathHelper.ToDegrees(body.Yaw):F0} deg");
                ImGui.Text($"speed {_motion.Speed:F2} m/s   lean {MathHelper.ToDegrees(_motion.Lean):F1} deg");
                ImGui.Text($"{(body.Grounded ? "on the ground" : "in the air")}{(body.Swimming ? ", swimming" : "")}   wheels {_motion.LeftRolled:F1} / {_motion.RightRolled:F1} m");
                if (_locomotion == Locomotion.Tracks)
                    ImGui.Text($"turntable {MathHelper.ToDegrees(_motion.Turret):F0} deg   pitch {MathHelper.ToDegrees(_motion.Pitch):F1} deg");
                if (_locomotion == Locomotion.TriStar)
                    ImGui.Text($"spiders turned {MathHelper.ToDegrees(_motion.ClusterTurn):F0} deg   heave {_motion.Heave:F2} m");
            }
            ImGui.TextDisabled($"{1f / MathF.Max(_frameSeconds, 1e-4f):F0} fps, {_renderer.Batch.Drawn} meshes, {_renderer.Batch.DrawCalls} draw calls");
            ImGui.End();
        }

        protected override void WriteShotReport(string path) =>
            System.IO.File.WriteAllText(path, $"experiment {_experimentName}\nbase {Locomotions.NameOf(_locomotion)}\ncamera {_camera}\nfeet {_player.Body.Position}\n" +
                $"speed {_motion.Speed:F2}\nlean {MathHelper.ToDegrees(_motion.Lean):F1} deg\nclock {_session.Clock:F2}\n" +
                $"frame {_frameSeconds * 1000f:F1} ms, {_renderer.Batch.Drawn} meshes, {_renderer.Batch.DrawCalls} draw calls\n");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _experiment?.Stop(_session);
                _imgui?.Dispose();
                _watcher?.Dispose();
                _lines?.Dispose();
                _renderer?.Dispose();
                _worldMeshes?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
