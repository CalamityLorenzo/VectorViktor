using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Core.Physics;
using World.Maps;
using static World.Buildings.Walls;

namespace Maps.Coast
{
    // The station, on the plateau at the line's west end (see Railway): a platform along the north side of the beam,
    // PlatformHeight up from the ground with steps up at its east end, and on its back half a brick ticket hall, its
    // door onto the platform, facing the track.
    public sealed class Station : IDistrict
    {
        // The platform: PlatformLength along the line, from PlatformEast back west, and from its edge, PlatformEdge
        // north of the line's middle, PlatformWidth back
        public const float PlatformEast = -375f, PlatformLength = 80f, PlatformWidth = 11f, PlatformHeight = 1.1f;
        public const float PlatformEdge = 1.6f;
        public const float PlatformTop = Railway.Formation + PlatformHeight;
        public static readonly Vector2 PlatformCentre = new Vector2(PlatformEast - PlatformLength / 2f, -PlatformEdge - PlatformWidth / 2f);

        private const int Steps = 5;       // up to the platform, each the same rise
        private const float Tread = 0.4f;

        // The ticket hall: HallLength along the platform, HallDepth back from the front it shares with the platform's
        // back edge, less a margin
        public const float HallLength = 14f, HallDepth = 6f, HallHeight = 3.2f;
        public static readonly Vector2 HallCentre = new Vector2(PlatformCentre.X, -PlatformEdge - PlatformWidth + 0.4f + HallDepth / 2f);

        public IEnumerable<Building> Buildings(Terrain terrain)
        {
            var hall = new RoomSpec
            {
                Id = "ticket hall", Name = "Ticket hall",
                Outline = RoomSpec.Rectangle(HallLength, HallDepth), Height = HallHeight,
                WorldOffset = new Vector3(HallCentre.X, PlatformTop, HallCentre.Y),
                Floor = new Color(150, 60, 50), WallA = new Color(225, 215, 185), WallB = new Color(195, 185, 155), Ceiling = new Color(240, 235, 220),
                // Its door, onto the platform, shut to begin with (E opens it)
                Openings = new[] { new OpeningSpec(South, 0f, 1.4f, 2.4f, null!, Door: true) },
                Props = new[]
                {
                    // Two benches against the back wall, facing the door
                    new PropSpec(new MeshSource("station bench", SetteeMesh.Build,
                        SetteeMesh.Palette(new Color(100, 70, 45), new Color(130, 95, 60), new Color(60, 45, 30))),
                        new Vector3(-4.5f, 0f, -HallDepth / 2f + 0.45f), 0f, new Vector2(0.65f, 0.4f)),
                    new PropSpec(new MeshSource("station bench", SetteeMesh.Build,
                        SetteeMesh.Palette(new Color(100, 70, 45), new Color(130, 95, 60), new Color(60, 45, 30))),
                        new Vector3(-1.5f, 0f, -HallDepth / 2f + 0.45f), 0f, new Vector2(0.65f, 0.4f)),
                    // The ticket counter, against the east wall, facing into the hall
                    new PropSpec(new MeshSource("station counter", SideboardMesh.Build,
                        SideboardMesh.Palette(new Color(110, 70, 40), new Color(140, 95, 55), new Color(70, 45, 25), new Color(220, 200, 90))),
                        new Vector3(HallLength / 2f - 0.35f, 0f, 0f), 270f, new Vector2(0.25f, 0.75f)),
                },
            };
            yield return new Building("station", hall)
            {
                Roof = Gable.Pitched(30f, alongX: true),
                WallColor = new Color(165, 75, 55),     // brick
                RoofColor = new Color(70, 75, 85),      // slate
                PlinthColor = new Color(120, 115, 105),
                DoorColor = new Color(40, 80, 60),
            };
        }

        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            // Along X, so turned a quarter from along Z, its painted edge (the mesh's +X) to the south, the line's side
            yield return new Fixture(new MeshSource("station platform", d => RailwayMesh.Platform(d, PlatformLength, PlatformWidth, PlatformHeight),
                    Railway.Palette()),
                Matrix.CreateRotationY(-MathHelper.PiOver2) * Matrix.CreateTranslation(PlatformCentre.X, Railway.Formation, PlatformCentre.Y));
        }

        // The platform, to stand on, drawn as a fixture (above); and the steps up to it, each a solid block
        public IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain)
        {
            world.Add(Body.Fixed("platform", new Vector3(PlatformLength, PlatformHeight, PlatformWidth),
                                 new Vector3(PlatformCentre.X, Railway.Formation, PlatformCentre.Y)));
            var palette = PlatformMesh.Palette(new Color(175, 170, 160));
            for (var k = 1; k < Steps; k++)
            {
                var rise = PlatformHeight * k / Steps;
                var x = PlatformEast + Tread * (Steps - k - 0.5f);
                var step = world.Add(Body.Fixed($"platform step {k}", new Vector3(Tread, rise, PlatformWidth), new Vector3(x, Railway.Formation, PlatformCentre.Y)));
                yield return new Thing(step, PlatformMesh.Source(PlatformWidth, Tread, rise, palette));
            }
        }

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            // On the platform, in front of the ticket hall's door, looking east down the line
            ["station"] = new(new Vector2(HallCentre.X + 3f, -PlatformEdge - 2f), MathHelper.PiOver2, Above: 3f),
            // In the ticket hall, facing its door
            ["hall"] = new(HallCentre, MathHelper.Pi, Above: 3f),
        };
    }
}
