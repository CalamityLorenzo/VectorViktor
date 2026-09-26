using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using static World.Buildings.Walls;

namespace Basic.World
{
    // The hangar the next cottage's window looks into (see Parts), and really there too, off the map, to walk round.
    // HangarMesh's roof stands in a plain room its size, whose floor and walls are what you walk on and into, coloured
    // as the hangar's are, so they don't show where they meet it. Its front wall, the one the cottage's window looks in
    // from, has a door in the middle, back into the corridor (see Lane), and a window of its own beside it, looking out
    // of the cottage's. The turntables are solid, to jump up onto.
    public static class Hangar
    {
        private static readonly Color FloorColour = new Color(120, 120, 125), RoofColour = new Color(150, 160, 170), WallColour = new Color(110, 115, 125);

        private const float Drop = 2f;          // its floor below the ground, seen from the cottage: you look down into it, as if from a gallery
        private const float TableSize = 6f;     // the turntables, across

        // The hangar's window, in its front wall beside the door (so far across from it): looking out of it is looking
        // out of the next cottage's front window, from inside it, onto the lane (see LookingOut)
        public const float WindowAcross = 6f;

        // The three trees, each on its turntable: where, across the hangar and back from its front, how big, how fast
        // it goes round (radians a second, the middle one the other way), and how thick its trunk is to walk into
        private readonly record struct Tree(MeshSource Mesh, float Scale, float X, float Z, float Turn, float Trunk);
        private static readonly Tree[] Trees =
        {
            new(new MeshSource("tree", TreeMesh.Build, TreeMesh.Palette(new Color(100, 70, 40), new Color(60, 150, 60))), 4.5f, -7f, 12f, 0.5f, 0.35f),
            new(new MeshSource("oak", OakMesh.Build, OakMesh.Palette(new Color(90, 65, 40), new Color(70, 130, 50))), 3f, 0f, 20f, -0.3f, 0.6f),
            new(new MeshSource("tree", TreeMesh.Build, TreeMesh.Palette(new Color(100, 70, 40), new Color(150, 170, 50))), 4.5f, 7f, 12f, 0.8f, 0.35f),
        };

        // A point in the hangar, so far across and back from its front, in the room's own (X, Z): the room's centred
        private static Vector2 InRoom(float x, float z) => new Vector2(x, z - HangarMesh.Depth / 2f);

        public static readonly RoomSpec Room = new RoomSpec
        {
            Id = "hangar", Name = "Hangar",
            Outline = RoomSpec.Rectangle(HangarMesh.Width, HangarMesh.Depth), Height = HangarMesh.Height,
            WorldOffset = new Vector3(300f, 0f, 1500f),
            GridSpacing = 4f,
            Floor = FloorColour, WallA = WallColour, WallB = WallColour, Ceiling = RoofColour,
            Doors = new[] { new DoorSpec("corridor", North, 0f, "", "") },
            Ramps = Array.ConvertAll(Trees, tree =>
            {
                var middle = InRoom(tree.X, tree.Z);
                var top = TurntableMesh.Height * TableSize;
                var half = TurntableMesh.Radius * TableSize * 0.9f;   // about the octagon's width across its flats
                return new RampSpec(new Vector3(middle.X, top, middle.Y - half), new Vector3(middle.X, top, middle.Y + half), half * 2f, Thickness: top);
            }),
        };

        public static Building Shell() => new Building(Room.Name, Room) { WallColor = WallColour, RoofColor = RoofColour };

        // Where to stand to look out of its window, from just in front of it
        public static Vector2 WindowStart => new Vector2(Room.WorldOffset.X + WindowAcross, Room.WorldOffset.Z - HangarMesh.Depth / 2f + 3f);

        // The trees' trunks, as a square round each, to walk into
        public static IEnumerable<WallSegment> Walls()
        {
            foreach (var tree in Trees)
            {
                var middle = InRoom(tree.X, tree.Z) + new Vector2(Room.WorldOffset.X, Room.WorldOffset.Z);
                var bottom = Room.WorldOffset.Y;
                var h = tree.Trunk;
                var corners = new[] { middle + new Vector2(-h, -h), middle + new Vector2(h, -h), middle + new Vector2(h, h), middle + new Vector2(-h, h) };
                for (var k = 0; k < 4; k++)
                    yield return new WallSegment(corners[k], corners[(k + 1) % 4], bottom, bottom + 4f);
            }
        }

        // The hangar you can walk round, in the world: in its room, the roof's front at the room's north wall, and
        // no floor of its own - the room's is walked on.
        public static ScenePart[] InTheWorld()
        {
            var front = InRoom(0f, 0f);
            return Parts(Matrix.CreateTranslation(Room.WorldOffset + new Vector3(front.X, 0f, front.Y)), withFloor: false);
        }

        // The hangar as seen through the next cottage's window: its floor well down below the window, as if you're
        // looking in from a gallery
        public static ScenePart[] SeenFromCottage() => Parts(Matrix.CreateTranslation(0f, -Drop, 0f), withFloor: true);

        // The hangar - 24 m across, 32 m back and 12 m high, where the cottage is 9 by 4.5 - placed by `floor`, the
        // middle of the front of its floor. Three trees stand in it on turntables, each going round at its own pace,
        // the middle one the other way.
        private static ScenePart[] Parts(Matrix floor, bool withFloor)
        {
            var tablePalette = TurntableMesh.Palette(new Color(90, 90, 100), new Color(200, 170, 40));
            var parts = new List<ScenePart>
            {
                ScenePart.Still(new MeshSource(withFloor ? "hangar" : "hangar-roof", d => HangarMesh.Build(d, withFloor),
                    HangarMesh.Palette(FloorColour, RoofColour, WallColour, new Color(160, 60, 40))),
                    floor),
            };
            foreach (var tree in Trees)
            {
                var spot = Matrix.CreateTranslation(tree.X, 0f, tree.Z) * floor;
                var turn = tree.Turn;
                parts.Add(new ScenePart(new MeshSource("turntable", TurntableMesh.Build, tablePalette),
                    t => Matrix.CreateScale(TableSize) * Matrix.CreateRotationY(t * turn) * spot));
                parts.Add(new ScenePart(tree.Mesh,
                    t => Matrix.CreateScale(tree.Scale) * Matrix.CreateRotationY(t * turn) * Matrix.CreateTranslation(0f, TurntableMesh.Height * TableSize, 0f) * spot));
            }
            return parts.ToArray();
        }

        // The hangar's window, the size the cottage's is and as high up its front wall (a couple of centimetres in
        // front of it, so the wall's behind it), looking out onto the world from `cottage`'s front window, as if you
        // were standing in the cottage. Seen from further in than the cottage is deep, you'd be out through the back
        // of it, so it's shut sooner than the cottage's own.
        public static Window LookingOut(Terrain terrain, LaneCottage cottage)
        {
            var wall = Room.WorldOffset + new Vector3(WindowAcross, 0f, -HangarMesh.Depth / 2f + 0.02f);
            var outOf = cottage.FrontWindow(terrain);
            return new Window(Matrix.CreateRotationY(MathHelper.Pi) * Matrix.CreateTranslation(wall),   // looking north, out of the front wall
                outOf.Width, outOf.Sill, outOf.Height, outOf.Pane, Array.Empty<ScenePart>(),
                Onto: Matrix.CreateRotationY(MathHelper.Pi) * outOf.Frame)   // the cottage's window, turned to look out of it
            {
                Reach = LaneCottage.Half.X * 2f - 0.3f,
            };
        }

        // The hangar window's frame, always, and its pane, cross and all, while it's shut
        public static IEnumerable<Fixture> WindowFixtures(Terrain terrain, LaneCottage cottage)
        {
            var window = LookingOut(terrain, cottage);
            var (width, sill, height) = (window.Width, window.Sill, window.Height);
            var palette = new Color[3];
            MeshBuilder.SetBoxShades(palette, 0, new Color(90, 130, 190));   // as the cottages' windows: its face, the dim shade, is the pane
            Vector3[] Rim(float z) => new[] { new Vector3(-width / 2f, sill, z), new Vector3(width / 2f, sill, z), new Vector3(width / 2f, sill + height, z), new Vector3(-width / 2f, sill + height, z) };
            yield return new Fixture(new MeshSource("hangar-window-frame", d =>
            {
                var mesh = new MeshBuilder();
                mesh.AddLineLoop(Rim(-0.04f));
                return mesh.Build(d);
            }, Array.Empty<Color>()), window.Frame);
            yield return new Fixture(new MeshSource("hangar-window-pane", d =>
            {
                var mesh = new MeshBuilder();
                mesh.AddBox(0, new Vector3(0f, sill, -0.015f), 0.03f, width, height);
                var middle = sill + height / 2f;
                mesh.AddLine(new Vector3(-width / 2f, middle, -0.04f), new Vector3(width / 2f, middle, -0.04f));
                mesh.AddLine(new Vector3(0f, sill, -0.04f), new Vector3(0f, sill + height, -0.04f));
                return mesh.Build(d);
            }, palette), window.Frame, you => !window.Open(you));
        }
    }
}
