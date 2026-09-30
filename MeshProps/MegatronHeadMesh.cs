using MeshCore.Library;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A giant robot's head after the 1980s cartoon's Megatron (without the flare at the bottom of his helmet), its
    // proportions taken off a drawing of him. A big faceted bucket of a helmet, as wide as the head: a front panel
    // rising from the brow, narrowing to the top, facets either side of it falling back to its sides, its brow
    // overhanging a dark gap over the face. Its sides come down either side of the face, a guard block standing out
    // beside each cheek, and under those, big boxes for ears, vented on their fronts, standing out past the helmet.
    //
    // The face is a broad plate set into the helmet, bevelled round its sides and chin: straight down the cheeks, then
    // tapering in, from about the nose's foot, to the chin. A tab rises from the middle of its top into the dark over
    // it, the top edge either side slanting down to it - a scowl - over red eyes set in the dark. A long straight
    // nose, widening to its foot, creases from there down to the mouth's corners, and the mouth, a frown.
    //
    // The jaw under the mouth, down to the chin, is a mesh of its own (BuildJaw), set flush in the face, hinged behind
    // the mouth, so it can move: put it where it goes with JawAt, open as far as you like, and it shows the dark inside
    // of the mouth and the teeth.
    //
    // It sits on the stump of its neck. About 1.29 high, 1.13 across the ears; scale it to taste. The face looks along
    // +Z, centred on X, standing on y = 0. Its slots are RobotFaceBuilder's (the gem's unused).
    public static class MegatronHeadMesh
    {
        public static readonly Vector2 HalfFootprint = new Vector2(0.565f, 0.46f);
        public const float Height = 1.29f;

        public static Color[] Palette(Color helmet, Color face, Color ear, Color metal, Color eye, Color mouth) =>
            RobotFaceBuilder.Palette(helmet, face, ear, metal, eye, helmet, mouth);

        public static MeshSource Source(Color[] palette) => new MeshSource("megatron-head", Build, palette);
        public static MeshSource JawSource(Color[] palette) => new MeshSource("megatron-jaw", BuildJaw, palette);

        // The mouth, and the jaw hinged behind it
        private const float MouthY = 0.372f, MouthCornerY = 0.35f, MouthHalf = 0.084f, ChinY = 0.17f;
        private static readonly Vector3 Hinge = new Vector3(0f, MouthY, 0.02f);
        private const float JawSwing = 0.25f;   // radians, wide open

        // Where the jaw goes in the head, `open` of the way to wide open (0 to 1)
        public static Matrix JawAt(float open) => RobotFaceBuilder.Hinged(Hinge, JawSwing, open);

        // The face plate's front: two planes meeting down its middle, each falling back towards its side
        private static float FaceZ(float x) => 0.31f - 0.12f * MathF.Abs(x);
        private static Vector3 P(float x, float y, float proud = 0f) => new Vector3(x, y, FaceZ(x) + proud);

        public static MeshData Build(GraphicsDevice device)
        {
            const int helmet = RobotFaceBuilder.HelmetBase, face = RobotFaceBuilder.FaceBase, ear = RobotFaceBuilder.EarBase;
            const int lit = face + MeshBuilder.Top, plain = face + MeshBuilder.Side, shadow = face + MeshBuilder.Dim;
            const int dark = RobotFaceBuilder.Mouth;
            const float lift = 0.004f;
            var mesh = new MeshBuilder();

            static Vector3 M(Vector3 p) => new Vector3(-p.X, p.Y, p.Z);
            void Both(int slot, params Vector3[] polygon)
            {
                mesh.AddPolygon(slot, polygon);
                mesh.AddPolygon(slot, Array.ConvertAll(polygon, M));
            }
            void BothTri(int slot, Vector3 a, Vector3 b, Vector3 c)
            {
                mesh.AddTri(slot, a, b, c);
                mesh.AddTri(slot, M(a), M(b), M(c));
            }
            void BothLine(Vector3 a, Vector3 b)
            {
                mesh.AddLine(a, b);
                mesh.AddLine(M(a), M(b));
            }
            void BothLoop(params Vector3[] loop)
            {
                mesh.AddLineLoop(loop);
                mesh.AddLineLoop(Array.ConvertAll(loop, M));
            }

            RobotFaceBuilder.AddNeck(mesh);

            // ---- The face plate, its right half (+X) and mirrored
            const float edge = 0.20f, bevel = 0.242f, back = 0.23f, gapZ = 0.25f;
            const float tab = 0.034f, tabTop = 0.93f, eyeFoot = 0.715f, taper = 0.55f;
            var browIn = P(tab, 0.78f);            // where the scowl meets the tab
            var browOut = P(edge, 0.833f);
            var cheekFoot = P(edge, taper);        // where the cheek's side starts to taper in
            var chinCorner = P(0.115f, ChinY);
            var sideAtMouth = P(MathHelper.Lerp(chinCorner.X, edge, (MouthCornerY - ChinY) / (taper - ChinY)), MouthCornerY);
            var noseFoot = P(0.055f, 0.483f);
            var mouthCorner = P(MouthHalf, MouthCornerY);
            var mouthTop = P(0.04f, MouthY);       // the frown's top, either side of the middle
            var jawFoot = P(MouthHalf, ChinY);
            var outTop = new Vector3(bevel, 0.846f, back);
            var outFoot = new Vector3(bevel, taper - 0.01f, back);
            var outChin = new Vector3(0.14f, ChinY - 0.03f, back);

            // The dark gap over it, under the helmet's brow, that the eyes are set in
            mesh.AddPolygon(dark, new Vector3(-0.3f, 0.7f, gapZ), new Vector3(0.3f, 0.7f, gapZ), new Vector3(0.3f, 0.95f, gapZ), new Vector3(-0.3f, 0.95f, gapZ));

            // The tab up the middle; the scowl's top, back to the gap
            Both(plain, P(0f, tabTop), P(tab, tabTop), P(tab, eyeFoot), P(0f, eyeFoot));
            Both(shadow, P(tab, tabTop), browIn, browIn with { Z = gapZ }, P(tab, tabTop) with { Z = gapZ });
            Both(shadow, browIn, browOut, browOut with { Z = gapZ }, browIn with { Z = gapZ });
            // The cheeks, lit, from under the eyes to where the face tapers
            Both(lit, P(0f, eyeFoot), P(edge, eyeFoot), cheekFoot, P(0f, taper));
            // Under them, round the mouth, and either side of the jaw down to the chin
            Both(plain, P(0f, taper), cheekFoot, sideAtMouth, mouthCorner);
            BothTri(plain, P(0f, taper), mouthCorner, mouthTop);
            BothTri(plain, P(0f, taper), mouthTop, P(0f, MouthY));
            Both(plain, mouthCorner, sideAtMouth, chinCorner, jawFoot);
            // The bevel round its sides and chin, and under it, back into the helmet
            Both(shadow, browOut, cheekFoot, outFoot, outTop);
            Both(shadow, cheekFoot, chinCorner, outChin, outFoot);
            Both(shadow, jawFoot, chinCorner, outChin, outChin with { X = MouthHalf });
            Both(shadow, outFoot, outChin, outChin with { Z = 0f }, outFoot with { Z = 0f });
            // Behind the jaw, the inside of the mouth, dark
            mesh.AddPolygon(dark, new Vector3(-MouthHalf, ChinY, 0.26f), new Vector3(MouthHalf, ChinY, 0.26f),
                            new Vector3(MouthHalf, MouthY + 0.01f, 0.26f), new Vector3(-MouthHalf, MouthY + 0.01f, 0.26f));

            BothLine(P(tab, tabTop), browIn);
            BothLine(browIn, browOut);
            BothLine(browOut, cheekFoot);
            BothLine(cheekFoot, chinCorner);
            BothLine(chinCorner, jawFoot);
            BothLine(outTop, outFoot);
            BothLine(outFoot, outChin);
            BothLine(noseFoot, mouthCorner);

            // ---- The eyes: red, set in the dark under the scowl, slanting up to it, two glints across each
            var eye = new[] { P(0.045f, 0.768f, lift), P(0.195f, 0.818f, lift), P(0.195f, 0.722f, lift), P(0.05f, 0.727f, lift) };
            Both(dark, P(tab, 0.78f, 0.002f), P(edge, 0.833f, 0.002f), P(edge, eyeFoot, 0.002f), P(tab, eyeFoot, 0.002f));
            Both(RobotFaceBuilder.Eye, eye);
            BothLoop(eye);
            BothLine(P(0.12f, 0.73f, 2f * lift), P(0.135f, 0.79f, 2f * lift));
            BothLine(P(0.145f, 0.73f, 2f * lift), P(0.16f, 0.80f, 2f * lift));
            BothLine(P(tab, eyeFoot, lift), P(edge, eyeFoot, lift));

            // ---- The nose: long and straight, proud of the face, down from the tab, a facet at its foot
            const float noseBack = 0.295f;
            var bridge = new Vector3(tab, 0.78f, 0.33f);
            var tip = new Vector3(0.045f, 0.51f, 0.36f);
            var foot = new Vector3(0.055f, 0.483f, 0.345f);
            mesh.AddPolygon(lit, M(bridge), bridge, tip, M(tip));
            mesh.AddPolygon(shadow, M(tip), tip, foot, M(foot));
            mesh.AddPolygon(shadow, M(foot), foot, foot with { Z = noseBack }, M(foot) with { Z = noseBack });
            Both(plain, bridge, bridge with { Z = noseBack }, foot with { Z = noseBack }, foot, tip);
            mesh.AddLineLoop(M(bridge), bridge, tip, foot, M(foot), M(tip));
            mesh.AddLine(tip, M(tip));

            // ---- The top teeth, behind the mouth, hidden but when the jaw opens
            var teeth = new[] { new Vector3(-0.07f, MouthY, 0.27f), new Vector3(0.07f, MouthY, 0.27f), new Vector3(0.07f, MouthY - 0.022f, 0.27f), new Vector3(-0.07f, MouthY - 0.022f, 0.27f) };
            mesh.AddPolygon(lit, teeth);
            mesh.AddLineLoop(teeth);
            foreach (var x in new[] { -0.035f, 0f, 0.035f })
                mesh.AddLine(new Vector3(x, MouthY, 0.27f), new Vector3(x, MouthY - 0.022f, 0.27f));

            // ---- The helmet's top: a faceted bucket from the brow up, as wide as the head, its front a panel
            // narrowing to the top, facets either side falling back to its sides
            const float brow = 0.913f, helmetHalf = 0.45f, helmetBack = -0.45f;
            var bottom = Ring(brow, helmetHalf, 0.36f, helmetBack, 0.16f);
            var top = Ring(Height, 0.32f, 0.30f, helmetBack + 0.08f, 0.10f);
            for (var k = 0; k < 8; k++)
            {
                var n = (k + 1) % 8;
                mesh.AddQuad(helmet + (k == 0 ? MeshBuilder.Top : MeshBuilder.Side), bottom[k], bottom[n], top[n], top[k]);
                mesh.AddLine(bottom[k], top[k]);
            }
            mesh.AddPolygon(helmet + MeshBuilder.Top, top);
            mesh.AddPolygon(helmet + MeshBuilder.Dim, bottom);
            mesh.AddLineLoop(bottom);
            mesh.AddLineLoop(top);
            mesh.AddLine(new Vector3(0f, brow, 0.36f + lift), new Vector3(0f, Height, 0.30f + lift));   // the front panel's middle

            // Its sides, down either side of the face, set back from it; its back, filled in behind the face
            var side = new[] { new Vector2(0.22f, brow), new Vector2(0.22f, 0.30f), new Vector2(helmetBack, 0.30f), new Vector2(helmetBack, brow) };
            mesh.AddBox(helmet, new Vector3(0f, 0.30f, helmetBack / 2f), -helmetBack, 2f * helmetHalf, brow - 0.30f, sealBottom: true);
            foreach (var s in new[] { -1f, 1f })
            {
                SlabBuilder.AddSlab(mesh, helmet, s * 0.235f, s * helmetHalf, side);

                // The guard block standing out beside the cheek
                mesh.AddBox(helmet, new Vector3(s * 0.30f, 0.375f, 0.16f), 0.32f, 0.12f, 0.285f, sealBottom: true);

                // The ear, a big box low on the side, standing out past the helmet, vented on its front: a dark
                // recess, bars down it
                mesh.AddBox(ear, new Vector3(s * 0.46f, 0.19f, 0.05f), 0.40f, 0.21f, 0.22f, sealBottom: true);
                var vent = new[] { new Vector3(s * 0.385f, 0.21f, 0.25f + lift), new Vector3(s * 0.535f, 0.21f, 0.25f + lift),
                                   new Vector3(s * 0.535f, 0.39f, 0.25f + lift), new Vector3(s * 0.385f, 0.39f, 0.25f + lift) };
                mesh.AddPolygon(dark, vent);
                mesh.AddLineLoop(vent);
                foreach (var x in new[] { 0.415f, 0.445f, 0.475f, 0.505f })
                    mesh.AddBox(ear, new Vector3(s * x, 0.21f, 0.256f), 0.01f, 0.012f, 0.18f);
            }

            return mesh.Build(device);
        }

        // The jaw: flush with the face, from the frowning mouth down to the chin, its sides the seams down from the
        // mouth's corners; the lower lip's shadow across it; the bottom teeth standing on its top, behind the face, hidden
        // but when it opens (see JawAt)
        public static MeshData BuildJaw(GraphicsDevice device)
        {
            const int face = RobotFaceBuilder.FaceBase;
            const float lift = 0.004f;
            var mesh = new MeshBuilder();
            RobotFaceBuilder.AddPlate(mesh, face + MeshBuilder.Side, face + MeshBuilder.Dim, 0.03f,
                P(-MouthHalf, MouthCornerY), P(-0.04f, MouthY), P(0.04f, MouthY), P(MouthHalf, MouthCornerY), P(MouthHalf, ChinY), P(-MouthHalf, ChinY));
            mesh.AddLine(P(-0.045f, 0.31f, lift), P(0.045f, 0.31f, lift));
            var teeth = new[] { new Vector3(-0.05f, MouthY + 0.018f, 0.28f), new Vector3(0.05f, MouthY + 0.018f, 0.28f),
                                new Vector3(0.05f, MouthY, 0.28f), new Vector3(-0.05f, MouthY, 0.28f) };
            mesh.AddPolygon(face + MeshBuilder.Top, teeth);
            mesh.AddLineLoop(teeth);
            return mesh.Build(device);
        }

        // A level octagon at y, `halfWidth` either side, from `front` back to `back` in z, its corners cut `chamfer`
        // across: starting with the front's left end, going round by the front
        private static Vector3[] Ring(float y, float halfWidth, float front, float back, float chamfer) => new[]
        {
            new Vector3(-halfWidth + chamfer, y, front), new Vector3(halfWidth - chamfer, y, front),
            new Vector3(halfWidth, y, front - chamfer), new Vector3(halfWidth, y, back + chamfer),
            new Vector3(halfWidth - chamfer, y, back), new Vector3(-halfWidth + chamfer, y, back),
            new Vector3(-halfWidth, y, back + chamfer), new Vector3(-halfWidth, y, front - chamfer),
        };
    }
}
