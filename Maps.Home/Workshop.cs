using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core;
using World.Core.Characters;
using World.Core.Physics;
using World.Maps;
using World.Rendering;

namespace Maps.Home
{
    // The droid's workshop yard, west of the start: somewhere to try out the ways it can get about (see Locomotion) and
    // to see the parts it can be fitted with. A concrete yard, levelled, with
    //  - the four droids on show at its south end, each going about its own way (see WorkshopDisplays): in the milk
    //    churn it starts in, going nowhere, teetering; on Segway wheels, rocking; on tank tracks, turning on the spot while its body turns the other way on its turntable to
    //    keep looking at you; and on tri-star wheels, climbing a little stair and backing down it
    //  - a row of kerbs, each higher than the last and painted for who gets up it: green (anything), yellow (tracks),
    //    orange (tri-star wheels), red (nothing on wheels: it needs legs)
    //  - a platform with three ways up: a ramp (anything), a stair (tri-star wheels) and a steep stair (legs)
    //  - two gaps between concrete blocks: the narrow one too narrow for tracks
    //  - three crates to push, each as heavy as one way of getting about can shift and the weaker can't (see
    //    Locomotions.GaitOf): a cardboard box (anything), a wooden crate (tri-star wheels) and a steel one (tracks)
    //  - a bench with spare parts on it: a wheel, a tri-star spider, a strip of track, the stick arm and a jointed arm
    // Everything solid is ledges (see Ledge), so it's stood on, stepped up and blocked by as any ground is.
    public sealed class Workshop : IDistrict
    {
        public static readonly Vector2 Centre = new Vector2(-50f, -9f);
        public const float Half = 10f;   // the yard, Half either way of its middle

        // The kerbs, west to east along the row, how high each is, and its paint
        public static readonly float[] KerbHeights = { 0.05f, 0.12f, 0.20f, 0.28f };

        // The platform, its stairs and its ramp
        public const float PlatformTop = 0.72f, StairRise = 0.18f, SteepRise = 0.24f, StairWidth = 1.6f;
        // How deep each step is. The step after next is more than a step up, so it's a wall (see BuildingGround.KeepOut), and
        // anything nearer it than its own radius is pushed back off the step it's on: under 0.3 m deep, the tri-star (and
        // the walker) never gets up.
        public const float StairRun = 0.35f;
        public const int RampStrips = 3;    // side by side up it, each sloping from the platform down to the ground (see Ledge)

        // The gaps between the blocks: too narrow for tracks, and wide enough for them
        public const float NarrowGap = 0.7f, WideGap = 1.0f;

        // The crates to push, west to east along CratesAt: how heavy, what they're made of, how big
        public static readonly (float mass, CrateKind kind, float size)[] Crates =
            { (15f, CrateKind.Cardboard, 0.5f), (70f, CrateKind.Wood, 0.8f), (300f, CrateKind.Steel, 1f) };
        public const float CratesAt = 6f;
        public static Vector2 CrateAt(int k) => new Vector2(-8.5f + 2f * k, CratesAt);

        public IEnumerable<TerrainGenerator.Pad> Pads => new[] { new TerrainGenerator.Pad(Centre, new Vector2(Half)) };

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["workshop"] = new(Centre + new Vector2(1f, 9.5f), 0f),             // at the yard's south end, looking north over it and the droids on show
            ["kerbs"] = new(Centre + new Vector2(-6.5f, 0.5f), 0f),             // facing the yellow kerb, in the row of them
            ["platform"] = new(Centre + new Vector2(3.5f, 0.5f), 0f),           // facing the stair up the platform
            ["gaps"] = new(Centre + new Vector2(-7f, -4.5f), 0f),               // facing the gaps between the blocks
            ["crates"] = new(Centre + new Vector2(-6.5f, 8.8f), 0f),            // facing the wooden crate, between the light one and the steel
        };

        // ---- Where everything is, across the yard (x east, z south, from its middle) and up from its ground

        private enum Paint { Concrete, Green, Yellow, Orange, Red, Wood }

        // A block standing on the ground: its middle, half its size each way across, and how high its top is
        private readonly record struct Block(Vector2 At, Vector2 Half, float Top, Paint Paint);

        private static readonly Paint[] KerbPaint = { Paint.Green, Paint.Yellow, Paint.Orange, Paint.Red };

        public static readonly Vector2 Platform = new Vector2(3.5f, -4.5f);
        public const float PlatformHalf = 1.5f;
        private static float RampFrom => Platform.X + PlatformHalf;
        private const float RampLength = 4.5f;

        private static IEnumerable<Block> Blocks()
        {
            for (var k = 0; k < KerbHeights.Length; k++)
                yield return new Block(new Vector2(-8.5f + 2f * k, -2f), new Vector2(0.75f, 0.4f), KerbHeights[k], KerbPaint[k]);

            yield return new Block(Platform, new Vector2(PlatformHalf), PlatformTop, Paint.Concrete);
            var south = Platform.Y + PlatformHalf;
            for (var k = 1; PlatformTop - k * StairRise > 0.01f; k++)   // down from the platform's south edge
                yield return new Block(new Vector2(Platform.X, south + (k - 0.5f) * StairRun), new Vector2(StairWidth / 2f, StairRun / 2f),
                    PlatformTop - k * StairRise, Paint.Orange);
            var north = Platform.Y - PlatformHalf;
            for (var k = 1; PlatformTop - k * SteepRise > 0.01f; k++)   // down from its north edge
                yield return new Block(new Vector2(Platform.X, north - (k - 0.5f) * StairRun), new Vector2(StairWidth / 2f, StairRun / 2f),
                    PlatformTop - k * SteepRise, Paint.Red);

            foreach (var (at, half, top) in WorkshopDisplays.ShowStair())   // the tri-star's on show
                yield return new Block(at, half, top, Paint.Orange);
        }

        // The ramp, east off the platform down to the ground: strips running down it side by side, each sloping all the way
        // (a run of level strips, each a little higher than the last, made it a stair of tiny steps, jerked up one by one)
        private static IEnumerable<Ledge> RampLedges(float ground)
        {
            var half = StairWidth / RampStrips / 2f;
            for (var k = 0; k < RampStrips; k++)
            {
                var across = Platform.Y - StairWidth / 2f + (2 * k + 1) * half;
                yield return new Ledge(Centre + new Vector2(RampFrom, across), Centre + new Vector2(RampFrom + RampLength, across), half + Overlap,
                    ground - 0.2f, ground + PlatformTop, TopAtB: ground);
            }
        }

        // The concrete blocks the gaps are between: west to east, block, narrow gap, block, wide gap, block
        private const float GatesAt = -7f, GateThickness = 0.3f, GateHeight = 1f;
        private static IEnumerable<(float from, float to)> Gates()
        {
            var x = -9.5f;
            foreach (var (block, gap) in new[] { (1f, NarrowGap), (1.5f, WideGap), (1f, 0f) })
            {
                yield return (x, x + block);
                x += block + gap;
            }
        }

        // The bench the spare parts are on, too high to drive onto
        private static readonly Block Bench = new Block(new Vector2(-6.5f, -9.2f), new Vector2(1.6f, 0.4f), 0.75f, Paint.Wood);

        // ---- What's solid

        private static float Ground(Terrain terrain) => terrain.HeightAt(Centre.X, Centre.Y);

        public IEnumerable<Ledge> Ledges(Terrain terrain)
        {
            var ground = Ground(terrain);
            foreach (var block in Blocks().Append(Bench))
                foreach (var ledge in LedgesOf(block, ground))
                    yield return ledge;
            foreach (var ledge in RampLedges(ground))
                yield return ledge;
        }

        // A block as ledges: strips along its longer side, none wider than StripWidth (a ledge's ends are round, half its
        // width out from its line, so a wide one would round off the block's corners). Side by side, the strips' round ends
        // leave notches between them along the block's ends, as deep as a strip is half wide, right down to the ground,
        // where whatever drives on over the end (onto the platform off the ramp) would drop in and stick: so across each
        // end lies one more strip, filling them in. Only the block's outer corners are left round. And they overlap by
        // Overlap: just meeting, a droid on the line between two would be exactly half a strip from either, and as often as
        // not just outside both, and drop through.
        private const float StripWidth = 0.6f, Overlap = 0.01f;
        private static IEnumerable<Ledge> LedgesOf(Block block, float ground)
        {
            var alongX = block.Half.X >= block.Half.Y;
            var (length, across) = alongX ? (block.Half.X, block.Half.Y) : (block.Half.Y, block.Half.X);
            var strips = Math.Max(1, (int)MathF.Ceiling(across * 2f / StripWidth));
            var half = across / strips;
            var end = MathF.Max(0f, length - half);
            var wide = strips > 1 ? half + Overlap : half;
            Ledge Strip(Vector2 a, Vector2 b) =>
                alongX ? new Ledge(Centre + block.At + a, Centre + block.At + b, wide, ground - 0.2f, ground + block.Top)
                       : new Ledge(Centre + block.At + new Vector2(a.Y, a.X), Centre + block.At + new Vector2(b.Y, b.X), wide, ground - 0.2f, ground + block.Top);

            for (var k = 0; k < strips; k++)
            {
                var offset = -across + (2 * k + 1) * half;
                yield return Strip(new Vector2(-end, offset), new Vector2(end, offset));
            }
            if (strips > 1)
                foreach (var x in new[] { -end, end })
                    yield return Strip(new Vector2(x, -(across - half)), new Vector2(x, across - half));
        }

        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            var ground = Ground(terrain);
            foreach (var (from, to) in Gates())
                yield return new WallSegment(Centre + new Vector2(from, GatesAt), Centre + new Vector2(to, GatesAt), ground - 0.2f, ground + GateHeight);
        }

        public IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain)
        {
            for (var k = 0; k < Crates.Length; k++)
            {
                var (mass, kind, size) = Crates[k];
                var at = Centre + CrateAt(k);
                yield return Scenery.Crate(world, terrain, $"workshop crate {k + 1}", kind, new Vector3(size), mass, at.X, at.Y);
            }
        }

        // ---- How it looks

        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var at = Matrix.CreateTranslation(Centre.X, Ground(terrain), Centre.Y);
            yield return new Fixture(new MeshSource("workshop-yard", BuildYard, YardPalette), at);
            foreach (var part in WorkshopDisplays.SpareParts(Bench.At + new Vector2(0f, 0f), Bench.Top))
                yield return part with { Transform = part.Transform * at };
        }

        public IEnumerable<ScenePart> Moving(Terrain terrain) =>
            WorkshopDisplays.Parts(Matrix.CreateTranslation(Centre.X, Ground(terrain), Centre.Y));

        // The concrete's laid over the yard: no grid lines through it
        public bool Bare(float x, float z) => MathF.Abs(x - Centre.X) < Half && MathF.Abs(z - Centre.Y) < Half;

        private static readonly Color[] YardPalette = MakePalette();

        private static Color[] MakePalette()
        {
            var palette = new Color[18];
            MeshBuilder.SetBoxShades(palette, (int)Paint.Concrete * 3, new Color(175, 172, 165));
            MeshBuilder.SetBoxShades(palette, (int)Paint.Green * 3, new Color(70, 160, 70));
            MeshBuilder.SetBoxShades(palette, (int)Paint.Yellow * 3, new Color(225, 200, 50));
            MeshBuilder.SetBoxShades(palette, (int)Paint.Orange * 3, new Color(230, 130, 40));
            MeshBuilder.SetBoxShades(palette, (int)Paint.Red * 3, new Color(200, 50, 45));
            MeshBuilder.SetBoxShades(palette, (int)Paint.Wood * 3, new Color(150, 105, 60));
            return palette;
        }

        // Everything built, about the yard's middle on its ground: the concrete, the blocks, the ramp as a slope, the
        // gaps' blocks, and the bench on its legs.
        private static MeshData BuildYard(Microsoft.Xna.Framework.Graphics.GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float floor = 0.02f;
            var corners = new[] { new Vector3(-Half, floor, -Half), new Vector3(Half, floor, -Half), new Vector3(Half, floor, Half), new Vector3(-Half, floor, Half) };
            mesh.AddPolygon((int)Paint.Concrete * 3 + MeshBuilder.Top, corners);
            mesh.AddLineLoop(corners);

            foreach (var block in Blocks())
                AddBlock(mesh, block.At, block.Half, 0f, block.Top, block.Paint);

            // The ramp: a wedge from the ground up to the platform's edge
            var (w, x0, x1, top) = (StairWidth / 2f, RampFrom, RampFrom + RampLength, PlatformTop);
            var z = Platform.Y;
            var (a, b, c, d) = (new Vector3(x0, 0f, z - w), new Vector3(x0, 0f, z + w), new Vector3(x1, 0f, z + w), new Vector3(x1, 0f, z - w));
            var (e, f) = (new Vector3(x0, top, z - w), new Vector3(x0, top, z + w));
            var slot = (int)Paint.Green * 3;
            mesh.AddQuad(slot + MeshBuilder.Top, e, f, c, d);
            mesh.AddTri(slot + MeshBuilder.Side, a, e, d);
            mesh.AddTri(slot + MeshBuilder.Side, b, c, f);
            mesh.AddLineLoop(e, f, c, d);
            mesh.AddLine(a, e);
            mesh.AddLine(b, f);

            foreach (var (from, to) in Gates())
                AddBlock(mesh, new Vector2((from + to) / 2f, GatesAt), new Vector2((to - from) / 2f, GateThickness / 2f), 0f, GateHeight, Paint.Concrete);

            // The bench: a top on four legs
            var bench = Bench;
            AddBlock(mesh, bench.At, bench.Half, bench.Top - 0.05f, bench.Top, Paint.Wood);
            foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                AddBlock(mesh, bench.At + new Vector2(sx * (bench.Half.X - 0.06f), sz * (bench.Half.Y - 0.06f)), new Vector2(0.03f), 0f, bench.Top - 0.05f, Paint.Wood);
            return mesh.Build(device);
        }

        private static void AddBlock(MeshBuilder mesh, Vector2 at, Vector2 half, float bottom, float top, Paint paint) =>
            mesh.AddBox((int)paint * 3, new Vector3(at.X, bottom, at.Y), half.Y * 2f, half.X * 2f, top - bottom);
    }
}
