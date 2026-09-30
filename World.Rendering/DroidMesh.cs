using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core.Characters;

namespace World.Rendering
{
    // The starting droid's parts (see DroidRig), a mesh each, each built about its own joint, so the rig moves
    // them and a part is swapped by swapping its mesh. They share one palette. Sizes are the rig's.
    //
    // The rounded parts - the head, the visor, the tyres - are outlined afresh for each view (see
    // MeshBuilder.AddOutlineTri), so they show as round in wireframe rather than as a cage of lines.
    //
    // Its cables (see Cable) are all one mesh, a unit length of cable that each stretch is drawn with (see CableSource,
    // Rig.Span). It has faces and no edges, so it's a fine strand close to and next to nothing far off.
    public static class DroidMesh
    {
        // Three shades each (see MeshBuilder.SetBoxShades) but the tyre, hub and cable
        public const int Head = 0, Ruby = 3, Wood = 6, Metal = 9, Lens = 12, Servo = 15, Spork = 18, Tyre = 21, Hub = 22, CableSlot = 23;
        public const int PaletteSize = 24;

        public const string CablePart = "droid-cable";

        private const int Round = 16;   // sides to the head and visor

        public static Color[] Palette(Color head, Color ruby, Color wood, Color metal, Color lens, Color servo, Color spork,
            Color tyre, Color hub, Color cable)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Head, head);
            MeshBuilder.SetBoxShades(palette, Ruby, ruby);
            MeshBuilder.SetBoxShades(palette, Wood, wood);
            MeshBuilder.SetBoxShades(palette, Metal, metal);
            MeshBuilder.SetBoxShades(palette, Lens, lens);
            MeshBuilder.SetBoxShades(palette, Servo, servo);
            MeshBuilder.SetBoxShades(palette, Spork, spork);
            palette[Tyre] = tyre;
            palette[Hub] = hub;
            palette[CableSlot] = cable;
            return palette;
        }

        // Its colours as it starts: a pale head, the ruby visor, a wooden broom and arm, dull metal, a blue hobby servo,
        // orange camping sporks, and yellow cable.
        public static Color[] StartingPalette() => Palette(new Color(205, 205, 198), new Color(175, 20, 45), new Color(150, 105, 60),
            new Color(120, 122, 132), new Color(235, 240, 255), new Color(45, 70, 150), new Color(235, 130, 35),
            new Color(40, 40, 45), new Color(150, 150, 160), new Color(230, 200, 40));

        // Every part's mesh, by the part name the rig gives it (see RigNode.Part).
        public static IReadOnlyDictionary<string, MeshSource> Sources(Color[] palette) => new Dictionary<string, MeshSource>
        {
            [DroidRig.AxlePart] = new MeshSource(DroidRig.AxlePart, BuildAxle, palette),
            [DroidRig.WheelPart] = new MeshSource(DroidRig.WheelPart, BuildWheel, palette),
            [DroidRig.SpinePart] = new MeshSource(DroidRig.SpinePart, BuildBroom, palette),
            [DroidRig.ArmPart] = new MeshSource(DroidRig.ArmPart, BuildStickArm, palette),
            [DroidRig.HandPart] = new MeshSource(DroidRig.HandPart, BuildSporks, palette),
            [DroidRig.HeadPart] = new MeshSource(DroidRig.HeadPart, BuildHead, palette),
            [DroidRig.EarPart] = new MeshSource(DroidRig.EarPart, BuildEar, palette),
            [DroidRig.DishPart] = new MeshSource(DroidRig.DishPart, BuildDish, palette),
            [DroidRig.CameraPart] = new MeshSource(DroidRig.CameraPart, BuildCamera, palette),
            [DroidRig.ServoMountPart] = new MeshSource(DroidRig.ServoMountPart, BuildServoMount, palette),
            [DroidRig.LimbPart] = new MeshSource(DroidRig.LimbPart, BuildLimb, palette),
        };

        // What each stretch of its cables is drawn with.
        public static MeshSource CableSource(Color[] palette) => new MeshSource(CablePart, BuildCable, palette);

        // The urn head, about its joint at the top of the broom: a short metal neck, then the head's body with its
        // top and bottom edges rounded off, and the ruby visor all the way round its upper part, standing proud.
        public static MeshData BuildHead(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float n = DroidRig.NeckHeight, r = DroidRig.HeadRadius, h = DroidRig.HeadHeight, b = DroidRig.Bevel;
            var inside = new Vector3(0f, n + h / 2f, 0f);
            Lathe(mesh, Matrix.Identity, inside, Round, new (float, float, bool)[]
            {
                (0f, 0f, false),
                (DroidRig.NeckRadius, 0f, true),
                (DroidRig.NeckRadius, n, true),
                (r - b, n, true),
                (r, n + b, false),        // the rounded edges: outlined as seen, no fixed ring where they meet the side
                (r, n + h - b, false),
                (r - b, n + h, true),
                (0f, n + h, false),
            }, new[] { Metal + MeshBuilder.Dim, Metal + MeshBuilder.Side, Head + MeshBuilder.Dim, Head + MeshBuilder.Side,
                       Head + MeshBuilder.Side, Head + MeshBuilder.Top, Head + MeshBuilder.Top });

            // The visor: a band standing proud of the head's side (its inside is the head)
            var (low, high, proud) = (n + DroidRig.VisorBottom, n + DroidRig.VisorTop, DroidRig.VisorRadius);
            Lathe(mesh, Matrix.Identity, new Vector3(0f, (low + high) / 2f, 0f), Round, new (float, float, bool)[]
            {
                (r, low, false),
                (proud, low, true),
                (proud, high, true),
                (r, high, false),
            }, new[] { Ruby + MeshBuilder.Dim, Ruby + MeshBuilder.Side, Ruby + MeshBuilder.Top });
            return mesh.Build(device);
        }

        // An ear: a hoop, face on from the front, with an upright pin across it for the dish to turn on.
        public static MeshData BuildEar(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const int segments = 10;
            const float thickness = 0.007f;
            var ring = new Vector3[segments];
            for (var k = 0; k < segments; k++)
            {
                var angle = k * MathHelper.TwoPi / segments;
                ring[k] = new Vector3(DroidRig.EarRadius * MathF.Sin(angle), DroidRig.EarRadius * MathF.Cos(angle), 0f);
            }
            for (var k = 0; k < segments; k++)
                mesh.AddTube(ring[k], ring[(k + 1) % segments], thickness, thickness, 5, Metal + MeshBuilder.Side);
            mesh.AddTube(ring[0], ring[segments / 2], 0.003f, 0.003f, 4, Metal + MeshBuilder.Dim);
            return mesh.Build(device);
        }

        // A radar dish, about its pivot on the ear's pin, facing +X (out from the head): a shallow bowl on a short
        // stalk, with a feed horn standing out of the middle so which way it's listening shows in wireframe.
        public static MeshData BuildDish(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float rim = DroidRig.EarRadius * 0.8f;
            var turn = Matrix.CreateRotationZ(-MathHelper.PiOver2);   // the lathe's axis, y, onto x
            Lathe(mesh, turn, new Vector3(0.004f, 0f, 0f), 10, new (float, float, bool)[]
            {
                (0f, -0.008f, false),
                (0.006f, -0.008f, true),     // the stalk's end
                (rim, 0.012f, true),         // the rim
                (0f, 0.004f, false),         // the bowl's hollow
            }, new[] { Metal + MeshBuilder.Dim, Metal + MeshBuilder.Side, Metal + MeshBuilder.Top }, outlined: false);
            mesh.AddTube(new Vector3(0.004f, 0f, 0f), new Vector3(0.03f, 0f, 0f), 0.003f, 0.002f, 4, Metal + MeshBuilder.Dim, ringEdges: true);
            return mesh.Build(device);
        }

        // The head camera, about its place on the visor's face, looking along +Z: a small bright box with a round lens.
        public static MeshData BuildCamera(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float wide = 0.045f, tall = 0.03f, deep = 0.016f;
            mesh.AddBox(Lens, new Vector3(0f, -tall / 2f, 0f), deep, wide, tall, sealBottom: true);
            var lens = new Vector3[8];
            for (var k = 0; k < lens.Length; k++)
            {
                var angle = k * MathHelper.TwoPi / lens.Length;
                lens[k] = new Vector3(0.01f * MathF.Cos(angle), 0.01f * MathF.Sin(angle), deep / 2f);
            }
            mesh.AddLineLoop(lens);
            return mesh.Build(device);
        }

        // The broom handle, from the axle up to the neck, with a metal collar at the top, and the arm's servo clamped to
        // its right at the shoulder: a strap round the handle and the servo's box beside it, its shaft boss facing out.
        public static MeshData BuildBroom(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float length = DroidRig.SpineLength, radius = DroidRig.SpineRadius;
            // Outlined as seen, not with a line down every side, which at low resolution would hide the wood between them
            Lathe(mesh, Matrix.Identity, new Vector3(0f, length / 2f, 0f), 8, new (float, float, bool)[]
            {
                (0f, 0f, false),
                (radius, 0f, true),
                (radius, length, true),
                (0f, length, false),
            }, new[] { Wood + MeshBuilder.Dim, Wood + MeshBuilder.Side, Wood + MeshBuilder.Top });
            mesh.AddFrustum(new Vector3(0f, length - 0.04f, 0f), radius * 1.6f, radius * 1.6f, 0.04f, 6, Metal + MeshBuilder.Side,
                bottomSlot: Metal + MeshBuilder.Dim, topSlot: Metal + MeshBuilder.Top);

            const float shoulder = DroidRig.ShoulderHeight, w = DroidRig.ServoWidth, h = DroidRig.ServoHeight, d = DroidRig.ServoDepth;
            mesh.AddFrustum(new Vector3(0f, shoulder - 0.012f, 0f), radius + 0.004f, radius + 0.004f, 0.024f, 8, Metal + MeshBuilder.Side);
            mesh.AddBox(Servo, new Vector3(-(radius + w / 2f), shoulder - h / 2f, 0f), d, w, h, sealBottom: true);
            mesh.AddTube(new Vector3(-(radius + w), shoulder, 0f), new Vector3(-(radius + w + 0.006f), shoulder, 0f), 0.01f, 0.01f, 6,
                Metal + MeshBuilder.Top, ringEdges: true);
            return mesh.Build(device);
        }

        // The arm: a metal boss on the servo's shaft (which is the shoulder joint), and the stick hanging down from it along -Y.
        public static MeshData BuildStickArm(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float r = DroidRig.ArmRadius;
            mesh.AddTube(new Vector3(r, 0f, 0f), new Vector3(-r, 0f, 0f), r * 1.1f, r * 1.1f, 6, Metal + MeshBuilder.Side, ringEdges: true);
            // Outlined as seen, like the broom, so the wood shows
            Lathe(mesh, Matrix.Identity, new Vector3(0f, -DroidRig.ArmLength / 2f, 0f), 8, new (float, float, bool)[]
            {
                (0f, -DroidRig.ArmLength, false),
                (r * 0.85f, -DroidRig.ArmLength, true),
                (r, 0f, true),
                (0f, 0f, false),
            }, new[] { Wood + MeshBuilder.Dim, Wood + MeshBuilder.Side, Wood + MeshBuilder.Top });
            return mesh.Build(device);
        }

        // A jointed arm's servo (see DroidRig.JointedArm), about its fitting on the broom's axis: its box clamped against
        // the broom's left, its shaft boss facing out, where the upper arm hangs.
        public static MeshData BuildServoMount(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float radius = DroidRig.SpineRadius, w = DroidRig.ServoWidth, h = DroidRig.ServoHeight, d = DroidRig.ServoDepth;
            mesh.AddBox(Servo, new Vector3(radius + w / 2f, -h / 2f, 0f), d, w, h, sealBottom: true);
            mesh.AddBox(Metal, new Vector3(radius - 0.004f, -h / 2f - 0.004f, 0f), d + 0.008f, 0.008f, h + 0.008f);   // the bracket against the broom
            mesh.AddTube(new Vector3(radius + w, 0f, 0f), new Vector3(radius + w + 0.006f, 0f, 0f), 0.01f, 0.01f, 6,
                Metal + MeshBuilder.Top, ringEdges: true);
            return mesh.Build(device);
        }

        // One stick of a jointed arm, upper arm or forearm: a boss at its joint, and LimbLength of wood hanging down -Y.
        public static MeshData BuildLimb(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float r = DroidRig.ArmRadius;
            mesh.AddTube(new Vector3(r, 0f, 0f), new Vector3(-r, 0f, 0f), r * 1.1f, r * 1.1f, 6, Metal + MeshBuilder.Side, ringEdges: true);
            Lathe(mesh, Matrix.Identity, new Vector3(0f, -DroidRig.LimbLength / 2f, 0f), 8, new (float, float, bool)[]
            {
                (0f, -DroidRig.LimbLength, false),
                (r * 0.9f, -DroidRig.LimbLength, true),
                (r, 0f, true),
                (0f, 0f, false),
            }, new[] { Wood + MeshBuilder.Dim, Wood + MeshBuilder.Side, Wood + MeshBuilder.Top });
            return mesh.Build(device);
        }

        // The hand: two plastic sporks, one in front of the other, bowls facing each other like a pincer, a bolt through the tops of their handles
        // and a couple of turns of wire round them, a little askew: crude, but it holds.
        public static MeshData BuildSporks(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddTube(Vector3.Zero, new Vector3(0f, -DroidRig.SporkTop - 0.006f, 0f), 0.016f, 0.012f, 5, Metal + MeshBuilder.Side, ringEdges: true);
            foreach (var side in new[] { -1f, 1f })
                AddSpork(mesh, DroidRig.SporkPlace(side), side);

            var bolt = -DroidRig.SporkTop - 0.012f;
            var facing = DroidRig.SporkFacing;
            mesh.AddTube(Vector3.Transform(new Vector3(-0.026f, bolt, 0f), facing), Vector3.Transform(new Vector3(0.026f, bolt, 0f), facing),
                0.0035f, 0.0035f, 5, Metal + MeshBuilder.Top, ringEdges: true);
            foreach (var (down, askew) in new[] { (0.028f, 0.09f), (0.04f, -0.06f) })
                AddBlock(mesh, CableSlot, CableSlot, CableSlot,
                    Matrix.CreateRotationZ(askew) * Matrix.CreateTranslation(0f, -DroidRig.SporkTop - down, 0f) * facing, new Vector3(0.042f, 0.005f, 0.017f));
            return mesh.Build(device);
        }

        // One spork, placed by `place` (see DroidRig.SporkPlace), its handle down its -Y and its bowl's hollow facing
        // towards -`side` X: a flat handle, a shallow oval bowl, and three short tines off the bowl's end.
        private static void AddSpork(MeshBuilder mesh, Matrix place, float side)
        {
            Vector3 At(float x, float y, float z) => Vector3.Transform(new Vector3(x, y, z), place);
            const float handle = DroidRig.SporkHandle, bowl = DroidRig.SporkBowl, half = DroidRig.SporkBowlWidth / 2f;
            AddBlock(mesh, Spork + MeshBuilder.Side, Spork + MeshBuilder.Dim, Spork + MeshBuilder.Top,
                place, new Vector3(0.004f, handle, 0.012f), new Vector3(0f, -handle / 2f, 0f));

            // The bowl: an oval rim, and a fan to a middle sunk away from the other spork, so its hollow faces it
            const int around = 12;
            var middle = At(side * 0.006f, -handle - bowl / 2f, 0f);
            var rim = new Vector3[around];
            for (var k = 0; k < around; k++)
            {
                var angle = k * MathHelper.TwoPi / around;
                rim[k] = At(0f, -handle - bowl / 2f + MathF.Cos(angle) * bowl / 2f, MathF.Sin(angle) * half);
            }
            for (var k = 0; k < around; k++)
                mesh.AddTri(Spork + MeshBuilder.Side, middle, rim[k], rim[(k + 1) % around]);
            mesh.AddLineLoop(rim);

            // The tines, off the bottom of the bowl
            var end = -handle - bowl;
            foreach (var z in new[] { -0.009f, 0f, 0.009f })
            {
                var (a, b, tip) = (At(0f, end + 0.002f, z - 0.003f), At(0f, end + 0.002f, z + 0.003f), At(0f, end - DroidRig.SporkTines, z));
                mesh.AddTri(Spork + MeshBuilder.Top, a, b, tip);
                mesh.AddLine(a, tip);
                mesh.AddLine(b, tip);
            }
        }

        // A box `size` big, centred on `centre` in the space `place` puts it in (which can turn it any way): its faces,
        // with sides, ends and top in the slots given, and its edges.
        private static void AddBlock(MeshBuilder mesh, int sides, int ends, int top, Matrix place, Vector3 size, Vector3 centre = default)
        {
            var h = size / 2f;
            Vector3 C(float x, float y, float z) => Vector3.Transform(centre + new Vector3(x * h.X, y * h.Y, z * h.Z), place);
            var (a, b, c, d) = (C(-1, -1, -1), C(1, -1, -1), C(1, -1, 1), C(-1, -1, 1));
            var (e, f, g, k) = (C(-1, 1, -1), C(1, 1, -1), C(1, 1, 1), C(-1, 1, 1));
            mesh.AddQuad(sides, a, d, k, e);   // -X
            mesh.AddQuad(sides, b, f, g, c);   // +X
            mesh.AddQuad(ends, a, e, f, b);    // -Z
            mesh.AddQuad(ends, d, c, g, k);    // +Z
            mesh.AddQuad(top, e, k, g, f);     // top
            mesh.AddQuad(ends, a, b, c, d);    // bottom
            mesh.AddLineLoop(a, b, c, d);
            mesh.AddLineLoop(e, f, g, k);
            mesh.AddLine(a, e); mesh.AddLine(b, f); mesh.AddLine(c, g); mesh.AddLine(d, k);
        }

        // A unit length of cable: radius 1 round the z axis from z = 0 to 1, four-sided, faces only (see Rig.Span).
        public static MeshData BuildCable(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const int sides = 4;
            for (var k = 0; k < sides; k++)
            {
                var (a0, a1) = (k * MathHelper.TwoPi / sides, (k + 1) * MathHelper.TwoPi / sides);
                var (p, q) = (new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0f), new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0f));
                mesh.AddQuad(CableSlot, p, q, q + Vector3.UnitZ, p + Vector3.UnitZ);
            }
            return mesh.Build(device);
        }

        // The axle between the wheels, about its middle, with the hub block the broom stands in and a motor drum just
        // inside each wheel (on the axle, so it doesn't turn with the wheel).
        public static MeshData BuildAxle(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            var half = DroidRig.Track / 2f;
            mesh.AddTube(new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), 0.016f, 0.016f, 6, Metal + MeshBuilder.Dim, ringEdges: true);
            mesh.AddBox(Metal, new Vector3(0f, -0.035f, 0f), 0.07f, 0.08f, 0.06f, sealBottom: true);
            var inside = half - DroidRig.WheelWidth / 2f - DroidRig.MotorLength;
            foreach (var side in new[] { -1f, 1f })
                Lathe(mesh, Matrix.CreateRotationZ(-MathHelper.PiOver2), new Vector3(side * (inside + DroidRig.MotorLength / 2f), 0f, 0f), 10,
                    new (float, float, bool)[]
                    {
                        (0f, side * inside, false),
                        (DroidRig.MotorRadius, side * inside, true),
                        (DroidRig.MotorRadius, side * (inside + DroidRig.MotorLength), true),
                        (0f, side * (inside + DroidRig.MotorLength), false),
                    }, new[] { Hub, Metal + MeshBuilder.Dim, Hub });
            return mesh.Build(device);
        }

        // A hoverboard wheel, about its middle, turning about X: a fat tyre with a hub each side and three spokes
        // across each hub, so it's seen to turn even in wireframe.
        public static MeshData BuildWheel(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float r = DroidRig.WheelRadius, w = DroidRig.WheelWidth / 2f;
            var turn = Matrix.CreateRotationZ(-MathHelper.PiOver2);   // the lathe's axis, y, onto x
            Lathe(mesh, turn, Vector3.Zero, 14, new (float, float, bool)[]
            {
                (0f, -w, false),
                (r, -w, true),
                (r, w, true),
                (0f, w, false),
            }, new[] { Hub, Tyre, Hub });
            foreach (var side in new[] { -w, w })
            {
                var hub = new Vector3(side, 0f, 0f);
                for (var k = 0; k < 3; k++)
                {
                    var angle = k * MathHelper.TwoPi / 3f;
                    mesh.AddLine(hub, hub + new Vector3(0f, MathF.Cos(angle), MathF.Sin(angle)) * (r * 0.85f));
                }
            }
            return mesh.Build(device);
        }

        // A surface turned about the y axis (then placed by `place`): `profile` is (radius, height) from its bottom to
        // its top, and each band between two points is a ring of faces in that band's slot; a point of radius 0 closes
        // it there. A ring of edges goes round each point marked so. `inside` is a point inside it, for its outline.
        private static void Lathe(MeshBuilder mesh, Matrix place, Vector3 inside, int sides, (float radius, float y, bool edge)[] profile, int[] slots, bool outlined = true)
        {
            var rings = new Vector3[profile.Length][];
            for (var p = 0; p < profile.Length; p++)
            {
                rings[p] = new Vector3[sides];
                for (var k = 0; k < sides; k++)
                {
                    var angle = (k + 0.5f) * MathHelper.TwoPi / sides;
                    var (radius, y, _) = profile[p];
                    rings[p][k] = Vector3.Transform(new Vector3(radius * MathF.Sin(angle), y, radius * MathF.Cos(angle)), place);
                }
                if (profile[p].edge)
                    mesh.AddLineLoop(rings[p]);
            }

            void Face(int slot, Vector3 a, Vector3 b, Vector3 c)
            {
                if (Vector3.Cross(b - a, c - a).LengthSquared() < 1e-14f)
                    return;   // where a band closes to a point, half its quad has no area
                mesh.AddTri(slot, a, b, c);
                if (outlined)
                    mesh.AddOutlineTri(a, b, c, inside);
            }
            for (var p = 0; p < profile.Length - 1; p++)
                for (var k = 0; k < sides; k++)
                {
                    var n = (k + 1) % sides;
                    Face(slots[p], rings[p][k], rings[p][n], rings[p + 1][n]);
                    Face(slots[p], rings[p][k], rings[p + 1][n], rings[p + 1][k]);
                }
        }
    }
}
