using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Buildings;
using World.Core;

namespace Basic.World
{
    // A billboard standing in the world: a design (see BillboardDesign) on a board at At, turned by Yaw - its face
    // (the mesh's +Z) looking that way. View is a good place to stand to see it, looking along ViewHeading. The board
    // is one mesh, whatever it says; the posts are solid.
    public sealed record Billboard(BillboardDesign Design, Vector2 At, float Yaw, Vector2 View, float ViewHeading)
    {
        private static readonly Color PostColour = new Color(110, 110, 115), BoardColour = new Color(80, 80, 85);

        // A point in its own (X, Z), in the world
        public Vector2 InWorld(float x, float z)
        {
            var turned = Vector3.Transform(new Vector3(x, 0f, z), Matrix.CreateRotationY(Yaw));
            return At + new Vector2(turned.X, turned.Z);
        }

        // Level ground under it, so both its posts stand in it alike: level with `levelWith`, a road say, whose
        // slope reaches it and would tip one end of it otherwise
        public TerrainGenerator.Pad Pad(Vector2 levelWith) =>
            new TerrainGenerator.Pad(At, new Vector2(4f, 2.5f), Apron: 1f, Blend: 4f, LevelWith: levelWith);

        public Fixture Fixture(Terrain terrain) =>
            new Fixture(BillboardMesh.Source(Design, PostColour, BoardColour),
                Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(At.X, terrain.HeightAt(At.X, At.Y), At.Y));

        // Its posts, to walk into, each as its four sides
        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            var foot = terrain.HeightAt(At.X, At.Y);
            foreach (var x in BillboardMesh.PostsAt)
            {
                var half = BillboardMesh.PostSize / 2f;
                var corners = new[]
                {
                    InWorld(x - half, BillboardMesh.PostZ - half), InWorld(x + half, BillboardMesh.PostZ - half),
                    InWorld(x + half, BillboardMesh.PostZ + half), InWorld(x - half, BillboardMesh.PostZ + half),
                };
                for (var k = 0; k < 4; k++)
                    yield return new WallSegment(corners[k], corners[(k + 1) % 4], foot - 1f, foot + BillboardMesh.Clearance + BillboardMesh.Height);
            }
        }
    }
}
