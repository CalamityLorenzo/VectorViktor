// Draws and steps Basic.World at every one of its starts, with no window on screen, and says what that costs:
//
//     dotnet run -c Release --project Basic.World.Benchmark [-- frames [output.md]]
//
// Run it in Release for timings (a Debug build is slower, and also runs the debug-only checks, which is what to run
// it in to see whether a mesh is malformed). Draws go to an off-screen target the size of the game's low-res one, so
// they cost what the game's do; the numbers are this machine's, for comparing before and after a change.
using Basic.World;
using MeshRendering;
using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using World.Core;
using World.Core.Characters;
using World.Core.Movement;

const int LowResWidth = 640, LowResHeight = 256;   // as the game's
const float StepTime = 1f / 60f;
const int WarmUpFrames = 30, SimTicks = 600;

var frames = args.Length > 0 && int.TryParse(args[0], out var f) ? f : 300;
var outputFile = args.Length > 1 ? args[1] : null;

// A device with no game window: the form is never shown, it's only somewhere to attach the swap chain to
using var form = new System.Windows.Forms.Form();
var parameters = new PresentationParameters
{
    BackBufferWidth = LowResWidth, BackBufferHeight = LowResHeight, BackBufferFormat = SurfaceFormat.Color,
    DepthStencilFormat = DepthFormat.Depth24Stencil8, DeviceWindowHandle = form.Handle, IsFullScreen = false,
};
using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, parameters);
using var target = new RenderTarget2D(device, LowResWidth, LowResHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
using var cache = new MeshCache();

var loadTime = Stopwatch.StartNew();
var built = WorldBuilder.Build(WorldBuilder.Standard());
using var renderer = new WorldRenderer(built, device, cache);
var loaded = loadTime.Elapsed;

var isDebug = false;
#if DEBUG
isDebug = true;
#endif

var report = new StringBuilder();
void Say(string line)
{
    Console.WriteLine(line);
    report.AppendLine(line);
}
string N(double value, string format = "F2") => value.ToString(format, CultureInfo.InvariantCulture);

Say($"# Basic.World benchmark: {frames} frames per start, {LowResWidth} x {LowResHeight}, {device.Adapter.Description}");
Say($"{(isDebug ? "DEBUG build: timings are not representative" : "Release build")}; world built in {N(loaded.TotalMilliseconds, "F0")} ms");
Say("");
Say("| start | meshes drawn | culled | draw calls | chunks drawn / built | draw CPU ms | frame ms incl. GPU | garbage B / frame | sim us / tick | garbage B / tick |");
Say("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

var totals = (calls: 0, cpu: 0.0, frame: 0.0, frameGarbage: 0L, sim: 0.0, simGarbage: 0L, count: 0);
foreach (var (name, start) in built.Starts.OrderBy(s => s.Key, StringComparer.Ordinal))
{
    var player = NewPlayer(start);
    for (var tick = 0; tick < 120; tick++)   // settle: land, and let the bodies come to rest
        Tick(player, MoveInput.None);
    renderer.BuildTerrain(player);

    // Drawing: what the frame costs the CPU to submit, then all of them through to the GPU finishing
    Frame(player, 0);
    for (var i = 0; i < WarmUpFrames; i++)
        Frame(player, i);
    var cpu = 0.0;
    var garbage = GC.GetAllocatedBytesForCurrentThread();
    var total = Stopwatch.StartNew();
    for (var i = 0; i < frames; i++)
        cpu += Frame(player, i);
    var pixel = new Color[1];
    target.GetData(0, new Rectangle(0, 0, 1, 1), pixel, 0, 1);   // waits for the GPU
    total.Stop();
    garbage = GC.GetAllocatedBytesForCurrentThread() - garbage;
    var (drawn, culled, calls) = (renderer.Batch.Drawn, renderer.Batch.Culled, renderer.Batch.DrawCalls);
    var terrain = renderer.View.Terrain;

    // Stepping: walking straight ahead, which is the collision code's worst case (a walker moving among the walls)
    var walker = NewPlayer(start);
    for (var tick = 0; tick < 120; tick++)
        Tick(walker, MoveInput.None);
    var simGarbage = GC.GetAllocatedBytesForCurrentThread();
    var sim = Stopwatch.StartNew();
    for (var tick = 0; tick < SimTicks; tick++)
        Tick(walker, new MoveInput(new Vector2(0f, 1f)));
    sim.Stop();
    simGarbage = GC.GetAllocatedBytesForCurrentThread() - simGarbage;

    var cpuMs = cpu / frames;
    var frameMs = total.Elapsed.TotalMilliseconds / frames;
    var simUs = sim.Elapsed.TotalMilliseconds * 1000.0 / SimTicks;
    Say($"| {name} | {drawn} | {culled} | {calls} | {terrain.Drawn} / {terrain.Built} | {N(cpuMs)} | {N(frameMs)} | {garbage / frames} | {N(simUs, "F0")} | {simGarbage / SimTicks} |");
    totals = (totals.calls + calls, totals.cpu + cpuMs, totals.frame + frameMs, totals.frameGarbage + garbage / frames, totals.sim + simUs, totals.simGarbage + simGarbage / SimTicks, totals.count + 1);
}
Say($"| **average of {totals.count}** | | | {totals.calls / totals.count} | | {N(totals.cpu / totals.count)} | {N(totals.frame / totals.count)} | {totals.frameGarbage / totals.count} | {N(totals.sim / totals.count, "F0")} | {totals.simGarbage / totals.count} |");
if (outputFile != null)
    File.WriteAllText(outputFile, report.ToString());
return 0;

Player NewPlayer(Start start)
{
    var dropFrom = start.Above > 0f ? built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above : 0f;
    return new Player(new Vector3(start.At.X, dropFrom, start.At.Y), start.Yaw, built.Physics);
}

// One tick of the world, as the game steps it
void Tick(Player player, MoveInput input)
{
    built.Ground.StepDoors(StepTime, built.Physics.Bodies, new[] { (player.Body.Position, CharacterController.Radius, Player.Height) });
    player.Step(input, StepTime, built.Physics);
    built.Physics.Step(StepTime);
}

// One frame, drawn as the game draws it; how long the CPU took, in ms
double Frame(Player player, int index)
{
    device.SetRenderTarget(target);
    device.Clear(RetroStyle.Background);
    device.BlendState = BlendState.Opaque;
    device.DepthStencilState = DepthStencilState.Default;
    device.RasterizerState = RasterizerState.CullNone;
    var time = Stopwatch.GetTimestamp();
    renderer.Draw(player, index * StepTime, colorsOn: true);
    return Stopwatch.GetElapsedTime(time).TotalMilliseconds;
}
