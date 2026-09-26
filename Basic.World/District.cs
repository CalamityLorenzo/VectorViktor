using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Core.Physics;

namespace Basic.World
{
    // A part of the world - the country round the middle, the town, the street - and everything it puts in
    // it. WorldBuilder asks each district in turn, in the order they're listed, so adding another one is a
    // matter of writing it and adding it to the list, not of threading its pieces through the game.
    //
    // Everything has a default of nothing, so a district only says what it has.
    public interface IDistrict
    {
        // Ground to level for it (see TerrainGenerator.Pad), before the terrain's made. Later pads, this
        // district's or a later one's, are levelled over earlier ones.
        IEnumerable<TerrainGenerator.Pad> Pads => Array.Empty<TerrainGenerator.Pad>();

        // Water of its own, on top of the terrain's lakes and ponds.
        IEnumerable<Pool> Pools(Terrain terrain) => Array.Empty<Pool>();

        IEnumerable<Building> Buildings(Terrain terrain) => Array.Empty<Building>();

        // Free-standing walls to walk into: fences, posts.
        IEnumerable<WallSegment> Walls(Terrain terrain) => Array.Empty<WallSegment>();

        // Doors that take you somewhere else when you walk into them (see Portal).
        IEnumerable<Portal> Portals(Terrain terrain) => Array.Empty<Portal>();

        // What's built into it and never moves: roads, fences, paving, signs.
        IEnumerable<Fixture> Fixtures(Terrain terrain) => Array.Empty<Fixture>();

        // Windows that look onto somewhere else (see Window).
        IEnumerable<Window> Windows(Terrain terrain) => Array.Empty<Window>();

        // What's drawn that moves by itself, never touched (something going round), placed in the world.
        IEnumerable<ScenePart> Moving(Terrain terrain) => Array.Empty<ScenePart>();

        // Things lying about, to push, stack and knock over: each a body added to the world.
        IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain) => Array.Empty<Thing>();

        // Named places to start (see Program), each unique across all the districts.
        IReadOnlyDictionary<string, Start> Starts => new Dictionary<string, Start>();

        // Where the terrain gets no grid lines, because something's laid over it (see TerrainMesh).
        bool Bare(float x, float z) => false;
    }

    // Where to start, and which way to face. With Above, you're dropped from that far above the ground and land
    // on the highest floor below that: to start up in a building rather than on the ground under it.
    public readonly record struct Start(Vector2 At, float Yaw, float Above = 0f);

    // A door that leads elsewhere, the way Basic.Levels' doors do: walk into the wall between A and B, your feet
    // within a step of Floor, and you're taken To, facing Yaw. It needs a wall there to walk into.
    public readonly record struct Portal(Vector2 A, Vector2 B, float Floor, Vector3 To, float Yaw)
    {
        public const float Reach = 0.05f;   // how much further off than a walker's radius still counts as walking into it

        public bool WalkedInto(Vector3 feet, float radius)
        {
            var q = new Vector2(feet.X, feet.Z);
            var along = B - A;
            var t = Vector2.Dot(q - A, along) / along.LengthSquared();
            return t > 0f && t < 1f && MathF.Abs(feet.Y - Floor) <= WorldConstants.MaxStepUp &&
                Vector2.Distance(q, A + along * t) <= radius + Reach;
        }
    }

    // Something built into the world: its mesh, and where it goes. With ShownTo, it's only drawn while that's true
    // of where you are (your feet): to swap one thing for another as you come up to it.
    public readonly record struct Fixture(MeshSource Mesh, Matrix Transform, Func<Vector3, bool> ShownTo = null);

    // A window onto somewhere else, drawn as if it were behind it (see WindowPortals), whatever's really there:
    // Width by Height, its bottom Sill above Frame's origin. Frame places the window and what's seen through it in the
    // world - the window upright in its x-y plane, centred on x, facing its -Z, and what's beyond it, Beyond, in its
    // own space, running back along its +Z. Beyond Reach of it (where you stand, across the ground) it's shut, its
    // Pane solid; coming in, the pane thins, and it's gone at Clear. However close you are, the glass tints what's
    // seen through it: TintStrength of the way to Tint, by default a lighter shade of the pane.
    //
    // With Onto, it looks out onto the world itself instead, from somewhere else in it: Onto is a frame like Frame,
    // there, and what's seen through the window is the world beyond it, as if you stood as far in front of it as you
    // do of this window (see Through). Beyond's left empty.
    //
    // With Onto, it looks out onto the world itself instead, from somewhere else in it: Onto is a frame like Frame,
    // there, and what's seen through the window is the world beyond it, as if you stood as far in front of it as you
    // do of this window (see Through). Beyond's left empty.
    public sealed record Window(Matrix Frame, float Width, float Sill, float Height, Color Pane, IReadOnlyList<ScenePart> Beyond)
    {
        public float Reach { get; init; } = 5f;
        public float Clear { get; init; } = 1.5f;
        public Color Tint { get; init; } = Color.Lerp(Pane, new Color(170, 210, 255), 0.6f);
        public float TintStrength { get; init; } = 0.35f;
        public Matrix? Onto { get; init; }

        // What's seen through it, placed in the world: Beyond's space, or the world beyond Onto, brought round to it
        public Matrix Scene => Onto is { } onto ? Matrix.Invert(onto) * Frame : Frame;

        // Where a point in front of it would be in front of Onto: where you'd be standing, and where the eye'd be,
        // to see out of it there what you see through this one.
        public Vector3 Through(Vector3 p) => Onto is { } onto ? Vector3.Transform(p, Matrix.Invert(Frame) * onto) : p;

        public Vector3 Centre => Vector3.Transform(new Vector3(0f, Sill + Height / 2f, 0f), Frame);

        // Round the window, in the world
        public Vector3[] Corners()
        {
            float half = Width / 2f, top = Sill + Height;
            return Array.ConvertAll(new[] { new Vector3(-half, Sill, 0f), new Vector3(half, Sill, 0f), new Vector3(half, top, 0f), new Vector3(-half, top, 0f) },
                p => Vector3.Transform(p, Frame));
        }

        // Whether the eye's out in front of it, where it can be seen from
        public bool Faces(Vector3 eye) => Vector3.Dot(eye - Centre, Vector3.TransformNormal(Vector3.UnitZ, Frame)) < 0f;

        public bool Open(Vector3 you) => Across(you) <= Reach;

        public float Opacity(Vector3 you) => MathHelper.Clamp((Across(you) - Clear) / (Reach - Clear), 0f, 1f);

        private float Across(Vector3 you)
        {
            var centre = Centre;
            return Vector2.Distance(new Vector2(you.X, you.Z), new Vector2(centre.X, centre.Z));
        }
    }

    // Something drawn that can move by itself: its mesh, and where it is so many seconds in - in the world (see
    // IDistrict.Moving), or in its own space, for what's seen through a window.
    public readonly record struct ScenePart(MeshSource Mesh, Func<float, Matrix> At);

    // A body and how it's drawn: its mesh, and how far it's turned about the vertical inside its box - only by a
    // half turn, so the box it fills is the same.
    public record Thing(Body Body, MeshSource Mesh, float Turn = 0f);
}
