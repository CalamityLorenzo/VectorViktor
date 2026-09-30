using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps.Helpers
{
    // Where a robot face's features are (see RobotFaceBuilder), its right half (+X), the face looking along +Z. The
    // defaults are the Autobot's (RobotHeadMesh); other heads change what makes them theirs.
    public sealed record RobotFace
    {
        public float HalfWidth { get; init; } = 0.24f;         // out to the sockets' and cheekbones' outer ends
        public float ForeheadTop { get; init; } = 0.84f;       // how high the forehead goes, to meet the helmet's crown
        public float BrowOut { get; init; } = 0.675f;          // the brow's outer end
        public float BrowIn { get; init; } = 0.612f;           // its inner end, looking stern
        public float BrowPeak { get; init; } = 0.015f;         // how far below that the forehead's V comes, between the eyes
        public float SocketInBottom { get; init; } = 0.53f;
        public float SocketOutBottom { get; init; } = 0.57f;
        public Vector2 NoseTip { get; init; } = new(0.43f, 0.385f);   // (y, z)
        public float NostrilX { get; init; } = 0.05f;
        public Vector2 Cheekbone { get; init; } = new(0.44f, 0.235f);  // (y, z), at the face's outer edge
        public Vector3 Hollow { get; init; } = new(0.15f, 0.34f, 0.28f);   // the cheek's hollow, under the cheekbone
        public float MouthY { get; init; } = 0.35f;
        public Vector3 Jaw { get; init; } = new(0.20f, 0.24f, 0.22f);      // the jaw's corner
        public Vector3 ChinJut { get; init; } = new(0.08f, 0.24f, 0.345f); // the chin's top corner, out in front
        public Vector3 Chin { get; init; } = new(0.07f, 0.15f, 0.30f);     // its bottom corner
    }

    // Shared by the robot heads (RobotHeadMesh, MegatronHeadMesh): a face drawn like the 1980s Transformers cartoon's,
    // cut in flat planes like its cel shading, lit or shadowed by which way they face - a forehead (the helmet's) down
    // to a V over the eyes, glowing slanted eyes in sockets under the brow, a wedge of a nose, cheekbones over hollow
    // cheeks, an upper lip, a jaw cut back at its sides to under the head. Under the upper lip the lower lip and the
    // chin are a jaw of their own (BuildJaw, put where it goes by JawAt), hinged behind the mouth, to open and shut.
    // The expression sets the brow and the creases at the mouth's corners. The helmet round it is the head's own.
    //
    // Slots, for every robot head: the helmet, the face, the ears and the metal (the neck) have three shades each (see
    // MeshBuilder); the eyes, a gem and the inside of the mouth one each.
    public static class RobotFaceBuilder
    {
        public const int HelmetBase = 0, FaceBase = 3, EarBase = 6, MetalBase = 9, Eye = 12, Gem = 13, Mouth = 14;
        public const int PaletteSize = 15;

        public static Color[] Palette(Color helmet, Color face, Color ear, Color metal, Color eye, Color gem, Color mouth)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, HelmetBase, helmet);
            MeshBuilder.SetBoxShades(palette, FaceBase, face);
            MeshBuilder.SetBoxShades(palette, EarBase, ear);
            MeshBuilder.SetBoxShades(palette, MetalBase, metal);
            palette[Eye] = eye;
            palette[Gem] = gem;
            palette[Mouth] = mouth;
            return palette;
        }

        private const float Lift = 0.004f;   // how far what's drawn on a face stands off it
        private const float JawBack = -0.1f;

        public static void AddFace(MeshBuilder mesh, RobotFace face, RobotExpression expression)
        {
            const int lit = FaceBase + MeshBuilder.Top, plain = FaceBase + MeshBuilder.Side, shadow = FaceBase + MeshBuilder.Dim;

            // The other side's the same, mirrored
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
            void Across(Vector3 a) => mesh.AddLine(a, M(a));

            var w = face.HalfWidth;
            var browY = face.BrowIn + expression switch { RobotExpression.Grin => 0.023f, RobotExpression.Shout => -0.017f, _ => 0f };
            var browOut = new Vector3(w, face.BrowOut, 0.27f);
            var browIn = new Vector3(0.05f, browY, 0.34f);
            var browMid = new Vector3(0f, browY - face.BrowPeak, 0.35f);   // the V's point
            var foreheadTop = new Vector3(0f, face.ForeheadTop, 0.34f);
            var foreheadSide = new Vector3(w, face.ForeheadTop, 0.30f);

            var socketInTop = browIn with { Z = 0.315f };   // the eye's socket, set back under the brow
            var socketOutTop = browOut with { Z = 0.245f };
            var socketOutBottom = new Vector3(w, face.SocketOutBottom, 0.245f);
            var socketInBottom = new Vector3(0.05f, face.SocketInBottom, 0.315f);

            var noseTop = browMid with { Z = 0.335f };
            var noseTip = new Vector3(0f, face.NoseTip.X, face.NoseTip.Y);
            var nostril = new Vector3(face.NostrilX, face.NoseTip.X, 0.33f);

            var cheekbone = new Vector3(w, face.Cheekbone.X, face.Cheekbone.Y);
            var lipCorner = new Vector3(0.085f, face.MouthY, 0.337f);
            var lipFoot = new Vector3(0.065f, face.MouthY - 0.05f, 0.345f);
            var jaw = face.Jaw;
            var jawFoot = new Vector3(jaw.X - 0.04f, face.Chin.Y, jaw.Z - 0.01f);
            var chinJut = face.ChinJut;
            var chin = face.Chin;

            // The forehead, the helmet's, down to the V of the brow; the brow's underside, back to the sockets
            Both(HelmetBase + MeshBuilder.Side, foreheadTop, foreheadSide, browOut, browIn, browMid);
            Both(shadow, browIn, browOut, socketOutTop, socketInTop);
            Both(shadow, browMid, browIn, socketInTop, noseTop);
            // The sockets, in shadow
            Both(shadow, socketInTop, socketOutTop, socketOutBottom, socketInBottom);
            // The nose: its flank, down from between the eyes, and its underside
            Both(plain, noseTop, noseTip, nostril, socketInBottom, socketInTop);
            mesh.AddTri(shadow, noseTip, nostril, M(nostril));
            // The cheek over the cheekbone, lit; under it, round the hollow, the cheek falls away into shadow
            Both(lit, socketInBottom, socketOutBottom, cheekbone, nostril);
            var hollow = face.Hollow;
            BothTri(lit, nostril, cheekbone, hollow);
            BothTri(shadow, cheekbone, jaw, hollow);
            BothTri(shadow, jaw, chinJut, hollow);
            BothTri(plain, chinJut, lipFoot, hollow);
            BothTri(plain, lipFoot, lipCorner, hollow);
            BothTri(plain, lipCorner, nostril, hollow);
            // Over the mouth, the face; under it, the jaw (see AddJaw), and behind that, dark, the inside of the mouth
            mesh.AddPolygon(plain, M(nostril), nostril, lipCorner, M(lipCorner));
            mesh.AddPolygon(Mouth, new Vector3(-lipCorner.X, chin.Y + 0.005f, 0.295f), new Vector3(lipCorner.X, chin.Y + 0.005f, 0.295f),
                            new Vector3(lipCorner.X, face.MouthY + 0.01f, 0.295f), new Vector3(-lipCorner.X, face.MouthY + 0.01f, 0.295f));
            // The chin's sides; the jaw's sides, back under the helmet, and underneath
            Both(plain, jaw, jawFoot, chin, chinJut);
            Both(shadow, cheekbone, jaw, jaw with { Z = JawBack }, cheekbone with { Z = JawBack });
            Both(shadow, jaw, jawFoot, jawFoot with { Z = JawBack }, jaw with { Z = JawBack });
            mesh.AddPolygon(shadow, M(jawFoot), M(chin), chin, jawFoot, jawFoot with { Z = JawBack }, M(jawFoot) with { Z = JawBack });

            // Its lines: the brow and the forehead's top; the sockets; the nose; the cheekbones, and the crease from
            // each down into the hollow; the lines from the nose to the mouth; the jaw and the chin's sides
            BothLine(browOut, browIn);
            BothLine(browIn, browMid);
            BothLine(foreheadTop, foreheadSide);
            BothLine(socketInBottom, socketOutBottom);
            BothLine(socketInTop, socketInBottom);
            BothLine(noseTip, nostril);
            BothLine(nostril, socketInBottom);
            BothLine(nostril, cheekbone);
            BothLine(cheekbone, hollow);
            BothLine(nostril, lipCorner);
            BothLine(lipCorner, lipFoot);
            BothLine(lipFoot, chinJut);
            BothLine(cheekbone, jaw);
            BothLine(jaw, chinJut);
            BothLine(chinJut, chin);
            BothLine(jaw, jawFoot);
            BothLine(jawFoot, chin);

            // ---- The eyes, glowing, filling their sockets but for a rim: slanted down to the nose
            Vector3 OnSocket(float x, float t)   // t from the socket's bottom (0) to its top (1)
            {
                var along = (x - 0.05f) / (w - 0.05f);
                var bottom = MathHelper.Lerp(socketInBottom.Y, socketOutBottom.Y, along);
                var top = MathHelper.Lerp(socketInTop.Y, socketOutTop.Y, along);
                return new Vector3(x, MathHelper.Lerp(bottom, top, t), MathHelper.Lerp(0.315f, 0.245f, along) + Lift);
            }
            var eye = new[] { OnSocket(0.065f, 0.18f), OnSocket(w - 0.025f, 0.18f), OnSocket(w - 0.025f, 0.82f), OnSocket(0.065f, 0.82f) };
            Both(Eye, eye);
            mesh.AddLineLoop(eye);
            mesh.AddLineLoop(Array.ConvertAll(eye, M));

            // ---- The mouth: the lips and the jaw are meshes of their own (see BuildLip, BuildJaw, MouthAt); behind them
            // the top teeth, glimpsed between the lips, and shown as they part
            var mouth = face.MouthY;

            var teeth = new[] { new Vector3(-0.06f, mouth, 0.3f), new Vector3(0.06f, mouth, 0.3f), new Vector3(0.06f, mouth - 0.02f, 0.3f), new Vector3(-0.06f, mouth - 0.02f, 0.3f) };
            mesh.AddPolygon(lit, teeth);
            mesh.AddLineLoop(teeth);
            foreach (var x in new[] { -0.03f, 0f, 0.03f })
                mesh.AddLine(new Vector3(x, mouth, 0.3f), new Vector3(x, mouth - 0.02f, 0.3f));

            // The creases at the mouth's corners: down, stern; up, grinning; down and out, shouting
            var crease = expression switch { RobotExpression.Grin => new Vector2(0.022f, 0.02f), RobotExpression.Shout => new Vector2(0.014f, -0.045f), _ => new Vector2(0.018f, -0.02f) };
            var corner = new Vector3(0.086f + 0.004f, mouth, lipCorner.Z + Lift);
            BothLine(corner, corner + new Vector3(crease.X, crease.Y, 0f));
        }

        // The corners of the jaw's front, its right half: the mouth's corner, the lower lip's foot, the chin's top
        // corner, out in front, and its bottom
        private static (Vector3 lipCorner, Vector3 lipFoot, Vector3 chinJut, Vector3 chin) JawPoints(RobotFace face) =>
            (new Vector3(0.085f, face.MouthY, 0.337f), new Vector3(0.065f, face.MouthY - 0.05f, 0.345f), face.ChinJut, face.Chin);

        // The jaw, a mesh of its own so it can move (see MouthAt): under the lower lip (see BuildLip), tipped up to
        // the light, and the chin under it, jutting out, flush with the face; its sides back into the head; the bottom teeth
        // standing on its top, behind the face, hidden but when it opens
        public static MeshData BuildJaw(GraphicsDevice device, RobotFace face)
        {
            const int lit = FaceBase + MeshBuilder.Top, plain = FaceBase + MeshBuilder.Side, shadow = FaceBase + MeshBuilder.Dim;
            const float depth = 0.03f;
            static Vector3 M(Vector3 p) => new Vector3(-p.X, p.Y, p.Z);
            var mesh = new MeshBuilder();
            var (lipCorner, lipFoot, chinJut, chin) = JawPoints(face);
            var mouth = face.MouthY;

            mesh.AddPolygon(lit, M(lipCorner), lipCorner, lipFoot, M(lipFoot));
            mesh.AddPolygon(shadow, M(lipFoot), lipFoot, chinJut, M(chinJut));
            mesh.AddPolygon(plain, M(chinJut), chinJut, chin, M(chin));
            var outline = new[] { M(lipCorner), lipCorner, lipFoot, chinJut, chin, M(chin), M(chinJut), M(lipFoot) };
            for (var i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                mesh.AddQuad(shadow, a, b, b - new Vector3(0f, 0f, depth), a - new Vector3(0f, 0f, depth));
            }
            mesh.AddLineLoop(outline);
            mesh.AddLine(lipFoot, M(lipFoot));
            mesh.AddLine(chinJut, M(chinJut));

            var teeth = new[] { new Vector3(-0.05f, mouth + 0.016f, 0.305f), new Vector3(0.05f, mouth + 0.016f, 0.305f), new Vector3(0.05f, mouth, 0.305f), new Vector3(-0.05f, mouth, 0.305f) };
            mesh.AddPolygon(lit, teeth);
            mesh.AddLineLoop(teeth);
            return mesh.Build(device);
        }

        // A lip, the upper or the lower, a mesh of its own so it can move (see MouthAt), in front of the face under
        // the nose or the jaw's front
        public static MeshData BuildLip(GraphicsDevice device, RobotFace face, bool upper)
        {
            var mesh = new MeshBuilder();
            var mouth = face.MouthY;
            var (lipCorner, lipFoot, _, _) = JawPoints(face);
            var nostril = new Vector2(face.NoseTip.X, 0.33f);   // (y, z), as AddFace has it
            if (upper)
                AddLip(mesh, mouth, true, y => MathHelper.Lerp(nostril.Y, lipCorner.Z, (nostril.X - y) / (nostril.X - mouth)));
            else
                AddLip(mesh, mouth, false, y => y >= lipFoot.Y ? MathHelper.Lerp(lipCorner.Z, lipFoot.Z, (mouth - y) / (mouth - lipFoot.Y)) : lipFoot.Z);
            return mesh.Build(device);
        }

        // Where the mouth's moving parts go, `open` of the way to wide open (0 to 1): the lips part first, the upper
        // lifting and the lower dropping, and only for the wide shapes, past LipsAlone, does the jaw drop too, taking
        // the lower lip with it
        public readonly record struct MouthPose(Matrix UpperLip, Matrix LowerLip, Matrix Jaw);
        private const float LipsAlone = 0.55f;
        public static MouthPose MouthAt(RobotFace face, float open)
        {
            open = MathHelper.Clamp(open, 0f, 1f);
            var lips = MathF.Min(open / LipsAlone, 1f);
            var jaw = JawAt(face, MathF.Max(0f, (open - LipsAlone) / (1f - LipsAlone)));
            return new MouthPose(Matrix.CreateTranslation(0f, 0.012f * lips, 0.002f * lips),
                                 Matrix.CreateTranslation(0f, -0.018f * lips, 0.002f * lips) * jaw,
                                 jaw);
        }

        // A lip, the upper or the lower, low poly, sculpted like a face's: round the mouth, from corner to corner, a
        // ring flush with the face; in from that, standing out, the lip's crest, fullest in the middle (the upper's
        // dipping there, a cupid's bow); and in from that the lip's edge at the mouth, rolled in, the lips just parted
        // in the middle and meeting at the corners; and from the edge, the inside of the lip, back into the mouth.
        // Each ring's a half ellipse round the mouth's middle, at `mouth` high; `surface` is how far forward the face
        // is at a height, there.
        private static readonly float[] LipAngles = { 0f, 35f, 70f, 90f, 110f, 145f, 180f };   // degrees, from the right corner round
        private static void AddLip(MeshBuilder mesh, float mouth, bool upper, Func<float, float> surface)
        {
            const int lit = FaceBase + MeshBuilder.Top, plain = FaceBase + MeshBuilder.Side, shadow = FaceBase + MeshBuilder.Dim;
            var n = LipAngles.Length;
            Vector3[] outer = new Vector3[n], crest = new Vector3[n], edge = new Vector3[n], inside = new Vector3[n];
            var sign = upper ? 1f : -1f;
            for (var k = 0; k < n; k++)
            {
                var angle = MathHelper.ToRadians(LipAngles[k]);
                var (cos, sin) = (MathF.Cos(angle), MathF.Max(0f, MathF.Sin(angle)));   // sin(180) comes out a hair below 0
                var full = MathF.Sqrt(sin);   // 0 at the corners, 1 in the middle
                Vector3 At(float halfWidth, float height, float forward)
                {
                    var y = mouth + sign * height * sin;
                    return new Vector3(halfWidth * cos, y, surface(y) + forward);
                }
                outer[k] = At(0.085f, upper ? 0.06f : 0.065f, 0.002f);
                crest[k] = At(0.07f, upper ? 0.028f : 0.03f, 0.004f + 0.018f * full);
                if (upper && LipAngles[k] == 90f)
                    crest[k].Y -= 0.007f;
                edge[k] = At(0.06f, 0.005f, 0.002f + 0.01f * full);
                inside[k] = edge[k] with { Z = 0.312f };
            }
            void Band(int slot, Vector3[] a, Vector3[] b)
            {
                for (var k = 0; k < n - 1; k++)
                {
                    mesh.AddTri(slot, a[k], a[k + 1], b[k + 1]);
                    mesh.AddTri(slot, a[k], b[k + 1], b[k]);
                }
            }
            Band(plain, outer, crest);
            Band(lit, crest, edge);
            Band(shadow, edge, inside);
            for (var k = 0; k < n - 1; k++)
            {
                mesh.AddLine(outer[k], outer[k + 1]);
                mesh.AddLine(crest[k], crest[k + 1]);
                mesh.AddLine(edge[k], edge[k + 1]);
            }
            // The ridges down to the upper lip's bow
            if (upper)
                foreach (var k in new[] { 2, 4 })
                    mesh.AddLine(outer[k], crest[k]);
        }

        // Where a jaw goes in its head, `open` of the way to wide open (0 to 1): swung down and back on a hinge behind
        // the mouth, as far as `swing` (radians)
        public static Matrix Hinged(Vector3 hinge, float swing, float open) =>
            Matrix.CreateTranslation(-hinge) * Matrix.CreateRotationX(swing * MathHelper.Clamp(open, 0f, 1f)) * Matrix.CreateTranslation(hinge);

        // Where this face's jaw (see BuildJaw) goes, `open` of the way
        public static Matrix JawAt(RobotFace face, float open) => Hinged(new Vector3(0f, face.MouthY, 0.03f), 0.25f, open);

        // A plate standing `depth` proud of what's behind it: its face (`front`, convex, in order round), in `slot`, and
        // its edges back from that, in `edgeSlot`; the face's outline drawn
        public static void AddPlate(MeshBuilder mesh, int slot, int edgeSlot, float depth, params Vector3[] front)
        {
            var back = Array.ConvertAll(front, p => p - new Vector3(0f, 0f, depth));
            mesh.AddPolygon(slot, front);
            for (var i = 0; i < front.Length; i++)
            {
                var j = (i + 1) % front.Length;
                mesh.AddQuad(edgeSlot, front[i], front[j], back[j], back[i]);
            }
            mesh.AddLineLoop(front);
        }

        // The neck's stump, eight-sided, and the back of the skull, hidden in the helmet but from underneath
        public static void AddNeck(MeshBuilder mesh)
        {
            mesh.AddFrustum(new Vector3(0f, 0f, -0.12f), 0.14f, 0.13f, 0.16f, 8, MetalBase + MeshBuilder.Side, MetalBase + MeshBuilder.Dim, MetalBase + MeshBuilder.Top);
            mesh.AddBox(MetalBase, new Vector3(0f, 0.15f, -0.2f), 0.4f, 0.32f, 0.47f, sealBottom: true);
        }
    }
}
