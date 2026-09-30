using MeshCore.Library;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // How a robot head looks (see RobotFaceBuilder): its brow and its mouth
    public enum RobotExpression { Stern, Grin, Shout }

    // A giant robot's head, drawn like an Autobot's in the 1980s Transformers cartoon: a helmet that hoods the face,
    // with a panelled crest up its middle (a gem on its front) and panel lines along its crown; cheek guards framing
    // the face, standing proud of it and cut back under the jaw; on each side an ear, a vented panel on a plate, with
    // an antenna rising behind it. The face is RobotFaceBuilder's, with its expression; its lips and its jaw are
    // meshes of their own (BuildUpperLip, BuildLowerLip, BuildJaw), put where they go, open or shut, by MouthAt.
    //
    // It sits on the stump of its neck. About 1.16 high to the crest's top, 0.9 across the ears; scale it to taste. The
    // face looks along +Z, centred on X, standing on y = 0. Its slots are RobotFaceBuilder's.
    public static class RobotHeadMesh
    {
        // Its footprint, (x, z) either way of its middle: out to the ears, and from the back of the helmet to the nose
        public static readonly Vector2 HalfFootprint = new Vector2(0.46f, 0.46f);
        public const float Height = 1.16f;

        public static Color[] Palette(Color helmet, Color face, Color ear, Color metal, Color eye, Color gem, Color mouth) =>
            RobotFaceBuilder.Palette(helmet, face, ear, metal, eye, gem, mouth);

        public static MeshSource Source(RobotExpression expression, Color[] palette) =>
            new MeshSource($"robot-head:{expression}", d => Build(d, expression), palette);
        public static MeshSource JawSource(Color[] palette) => new MeshSource("robot-jaw", BuildJaw, palette);
        public static MeshSource UpperLipSource(Color[] palette) => new MeshSource("robot-upper-lip", BuildUpperLip, palette);
        public static MeshSource LowerLipSource(Color[] palette) => new MeshSource("robot-lower-lip", BuildLowerLip, palette);

        private static readonly RobotFace Face = new RobotFace();

        // Its mouth's moving parts, each a mesh of its own (see RobotFaceBuilder): the lips and the jaw; and where they
        // go in the head, `open` of the way to wide open (0 to 1)
        public static MeshData BuildJaw(GraphicsDevice device) => RobotFaceBuilder.BuildJaw(device, Face);
        public static MeshData BuildUpperLip(GraphicsDevice device) => RobotFaceBuilder.BuildLip(device, Face, upper: true);
        public static MeshData BuildLowerLip(GraphicsDevice device) => RobotFaceBuilder.BuildLip(device, Face, upper: false);
        public static RobotFaceBuilder.MouthPose MouthAt(float open) => RobotFaceBuilder.MouthAt(Face, open);

        public static MeshData Build(GraphicsDevice device, RobotExpression expression = RobotExpression.Stern)
        {
            const int helmet = RobotFaceBuilder.HelmetBase, ear = RobotFaceBuilder.EarBase, metal = RobotFaceBuilder.MetalBase;
            const float lift = 0.004f;
            var mesh = new MeshBuilder();
            var face = Face;

            RobotFaceBuilder.AddNeck(mesh);
            RobotFaceBuilder.AddFace(mesh, face, expression);

            // ---- The crown, over the top and down the back, meeting the forehead's top; a panel line either side
            // of the crest, from its front over to the back
            var crown = new[]
            {
                new Vector2(-0.46f, 0.58f), new Vector2(0.08f, 0.58f), new Vector2(0.30f, 0.84f), new Vector2(0.24f, 0.98f),
                new Vector2(-0.26f, 1.02f), new Vector2(-0.46f, 0.90f),
            };
            SlabBuilder.AddSlab(mesh, helmet, -0.33f, 0.33f, crown);
            foreach (var x in new[] { -0.17f, 0.17f })
            {
                var centre = new Vector2(-0.1f, 0.8f);
                Vector3 On(Vector2 p)
                {
                    var off = p + Vector2.Normalize(p - centre) * lift;
                    return new Vector3(x, off.Y, off.X);
                }
                mesh.AddLine(On(crown[2]), On(crown[3]));
                mesh.AddLine(On(crown[3]), On(crown[4]));
                mesh.AddLine(On(crown[4]), On(crown[5]));
            }

            // ---- The cheek guards, framing the face, standing proud of it, cut back under the jaw; a panel on each
            var guard = new[] { new Vector2(-0.44f, 0.22f), new Vector2(0.12f, 0.22f), new Vector2(0.30f, 0.34f), new Vector2(0.29f, 0.86f), new Vector2(-0.44f, 0.86f) };
            foreach (var side in new[] { -1f, 1f })
            {
                SlabBuilder.AddSlab(mesh, helmet, side * (face.HalfWidth - 0.005f), side * 0.36f, guard);
                SlabBuilder.AddPanel(mesh, side * (0.36f + lift), 0.8f, guard);
            }

            // ---- The crest up the middle, from the forehead over the crown, panelled on its sides, and the gem on its front
            var crest = new[]
            {
                new Vector2(0.33f, 0.64f), new Vector2(0.375f, 0.70f), new Vector2(0.37f, 0.92f), new Vector2(0.28f, 1.14f),
                new Vector2(0.02f, Height), new Vector2(-0.34f, 1.05f), new Vector2(-0.34f, 0.95f),
            };
            SlabBuilder.AddSlab(mesh, helmet, -0.045f, 0.045f, crest);
            foreach (var side in new[] { -1f, 1f })
                mesh.AddLineLoop(new Vector3(side * (0.045f + lift), 1.06f, 0.26f), new Vector3(side * (0.045f + lift), 1.09f, 0.02f),
                                 new Vector3(side * (0.045f + lift), 1.02f, -0.26f), new Vector3(side * (0.045f + lift), 0.99f, 0.20f));
            var gem = new[] { new Vector3(-0.03f, 0.74f, 0.376f), new Vector3(0.03f, 0.74f, 0.376f), new Vector3(0.03f, 0.87f, 0.374f), new Vector3(-0.03f, 0.87f, 0.374f) };
            mesh.AddPolygon(RobotFaceBuilder.Gem, gem);
            mesh.AddLineLoop(gem);

            // ---- The ears: a plate on each side, a vented panel standing out from it, and an antenna rising behind
            foreach (var side in new[] { -1f, 1f })
            {
                mesh.AddBox(ear, new Vector3(side * 0.39f, 0.40f, -0.08f), 0.36f, 0.06f, 0.32f, sealBottom: true);
                mesh.AddBox(ear, new Vector3(side * 0.435f, 0.46f, -0.08f), 0.24f, 0.03f, 0.2f, sealBottom: true);
                var outside = side * (0.45f + lift);
                foreach (var y in new[] { 0.50f, 0.56f, 0.62f })
                    mesh.AddLine(new Vector3(outside, y, -0.18f), new Vector3(outside, y, 0.02f));
                mesh.AddBox(metal, new Vector3(side * 0.39f, 0.72f, -0.2f), 0.05f, 0.04f, 0.3f);
            }

            return mesh.Build(device);
        }
    }
}
