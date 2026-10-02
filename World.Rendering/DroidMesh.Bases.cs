using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core.Characters;

namespace World.Rendering
{
    // What the droid can stand on instead of its wheels (see DroidBases): a tank's tracks - a hull, its turntable, and each
    // track's shoes and wheels - and tri-star wheels - a spider each side, a wheel on each of its arms. Each part about its
    // own joint, sizes the rig's, as the rest of the droid's are.
    public static partial class DroidMesh
    {
        // The tank's hull, about the middle of the ground under it: a box between the tracks, its nose sloped back to the
        // top, a mudguard over each track and two headlamps in front.
        public static MeshData BuildHull(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float w = DroidBases.HullWidth / 2f, l = DroidBases.HullLength / 2f, bottom = DroidBases.HullBottom, top = DroidBases.HullTop;

            // Its side (front to back is z, up is y): from the back along the bottom, up the nose and its slope, back along the top
            var side = new[] { new Vector2(-l, bottom), new Vector2(l, bottom), new Vector2(l, bottom + 0.1f), new Vector2(l - 0.12f, top), new Vector2(-l, top) };
            var slots = new[] { Hull + MeshBuilder.Dim, Hull + MeshBuilder.Dim, Hull + MeshBuilder.Top, Hull + MeshBuilder.Top, Hull + MeshBuilder.Dim };
            Vector3 At(float x, Vector2 p) => new Vector3(x, p.Y, p.X);
            var left = Array.ConvertAll(side, p => At(w, p));
            var right = Array.ConvertAll(side, p => At(-w, p));
            mesh.AddPolygon(Hull + MeshBuilder.Side, left);
            mesh.AddPolygon(Hull + MeshBuilder.Side, right);
            for (var k = 0; k < side.Length; k++)
            {
                var n = (k + 1) % side.Length;
                mesh.AddQuad(slots[k], left[k], left[n], right[n], right[k]);
                mesh.AddLine(left[k], right[k]);
            }
            mesh.AddLineLoop(left);
            mesh.AddLineLoop(right);

            foreach (var s in new[] { -1f, 1f })
            {
                mesh.AddBox(Hull, new Vector3(s * DroidBases.TrackGauge / 2f, top, 0f), DroidBases.TrackSpan + 2f * DroidBases.EndWheelRadius + 0.08f,
                    DroidBases.TrackWidth + 0.03f, 0.012f, sealBottom: true);
                mesh.AddBox(Lens, new Vector3(s * 0.14f, bottom + 0.03f, l + 0.005f), 0.01f, 0.06f, 0.04f);
            }
            return mesh.Build(device);
        }

        // The turntable on the hull, about its middle: a round plate, with a line on top pointing the way it faces, so
        // its turning shows.
        public static MeshData BuildTurntable(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float r = DroidBases.TurntableRadius, h = DroidBases.TurntableHeight;
            Lathe(mesh, Matrix.Identity, new Vector3(0f, h / 2f, 0f), 16, new (float, float, bool)[]
            {
                (0f, 0f, false),
                (r, 0f, true),
                (r, h, true),
                (r * 0.8f, h, true),
                (0f, h, false),
            }, new[] { Metal + MeshBuilder.Dim, Metal + MeshBuilder.Side, Metal + MeshBuilder.Top, Metal + MeshBuilder.Top });
            mesh.AddLine(new Vector3(0f, h, 0.05f), new Vector3(0f, h, r * 0.95f));
            return mesh.Build(device);
        }

        // The hub block the broom stands in, on the turntable: the axle's own, without the axle.
        public static MeshData BuildHub(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(Metal, new Vector3(0f, -DroidBases.HubDepth, 0f), 0.07f, 0.08f, 0.06f, sealBottom: true);
            return mesh.Build(device);
        }

        // One shoe of a track, about its middle, its grip facing -Y (a spare, on the workshop's bench).
        public static MeshData BuildShoe(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            AddShoe(mesh, Matrix.Identity);
            return mesh.Build(device);
        }

        // A track's shoes, all the way round it, run on `frame` TrackFrames-ths of the way from one shoe's place to the
        // next's (see DroidBases.TrackPart), about the track's own joint.
        public static MeshData BuildTrack(GraphicsDevice device, int frame)
        {
            var mesh = new MeshBuilder();
            var rolled = frame * DroidBases.ShoePitch / DroidBases.TrackFrames;
            for (var k = 0; k < DroidBases.Shoes; k++)
                AddShoe(mesh, DroidBases.ShoeAt(k, rolled).Matrix);
            return mesh.Build(device);
        }

        // A shoe placed by `place`: a steel plate as wide as the track, a line across its grip for the bar on it. Two dozen
        // of them go round each track, so they're kept plain: any more edges, and at low resolution a track's all lines.
        private static void AddShoe(MeshBuilder mesh, Matrix place)
        {
            const float t = DroidBases.ShoeThickness, w = DroidBases.TrackWidth / 2f;
            AddBlock(mesh, Tread + MeshBuilder.Side, Tread + MeshBuilder.Dim, Tread + MeshBuilder.Top, place,
                new Vector3(DroidBases.TrackWidth, t, DroidBases.ShoeLength));
            mesh.AddLine(Vector3.Transform(new Vector3(-w, -t / 2f, 0f), place), Vector3.Transform(new Vector3(w, -t / 2f, 0f), place));
        }

        // Every part a rig's node with `part` might be changed to as it goes, `part` among them: a track's shoes, any of
        // their frames; anything else, only itself. For drawing a rig whose parts are fixed when it's made (see RigScene).
        public static IEnumerable<string> Variants(string part)
        {
            if (!part.StartsWith("tank-track:", StringComparison.Ordinal))
                return new[] { part };
            var frames = new string[DroidBases.TrackFrames];
            for (var f = 0; f < frames.Length; f++)
                frames[f] = DroidBases.TrackPart(f);
            return frames;
        }

        // The sprocket that drives a track, behind, about its axle (turning about X): a steel wheel with teeth round it.
        public static MeshData BuildSprocket(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float r = DroidBases.EndWheelRadius, half = 0.05f;
            AddDisc(mesh, r * 0.88f, half, Metal + MeshBuilder.Side, Hub, spokes: 5);
            const int teeth = 10;
            foreach (var x in new[] { -half, half })
            {
                var star = new Vector3[teeth * 2];
                for (var k = 0; k < star.Length; k++)
                {
                    var angle = k * MathHelper.Pi / teeth;
                    var radius = k % 2 == 0 ? r : r * 0.88f;
                    star[k] = new Vector3(x, radius * MathF.Cos(angle), radius * MathF.Sin(angle));
                }
                for (var k = 0; k < teeth; k++)
                    mesh.AddTri(Metal + MeshBuilder.Top, star[2 * k + 1], star[2 * k], star[(2 * k + star.Length - 1) % star.Length]);
                mesh.AddLineLoop(star);
            }
            return mesh.Build(device);
        }

        // The idler a track runs round in front, about its axle: a plain wheel, spoked.
        public static MeshData BuildIdler(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            AddDisc(mesh, DroidBases.EndWheelRadius, 0.05f, Metal + MeshBuilder.Side, Hub, spokes: 3);
            return mesh.Build(device);
        }

        // A road wheel, under a track's lower run, about its axle: a smaller wheel with a rubber rim.
        public static MeshData BuildRoadWheel(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            AddDisc(mesh, DroidBases.RoadWheelRadius, 0.05f, Tyre, Hub, spokes: 3);
            return mesh.Build(device);
        }

        // A tri-star's spider, about the axle: three flat arms from a round boss, the first straight up (see
        // DroidBases.SpiderArmAt), each holding a wheel at its end.
        public static MeshData BuildSpider(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            for (var k = 0; k < 3; k++)
                AddBlock(mesh, Frame + MeshBuilder.Side, Frame + MeshBuilder.Dim, Frame + MeshBuilder.Top,
                    Matrix.CreateRotationX(k * DroidBases.ClusterStep), new Vector3(0.016f, DroidBases.SpiderArm, 0.034f),
                    new Vector3(0f, DroidBases.SpiderArm / 2f + 0.01f, 0f));
            mesh.AddTube(new Vector3(-0.012f, 0f, 0f), new Vector3(0.012f, 0f, 0f), 0.032f, 0.032f, 8, Frame + MeshBuilder.Top, ringEdges: true);
            return mesh.Build(device);
        }

        // One of a tri-star's wheels, about its axle: a fat tyre with a small spoked hub cap on each side, on a stub of
        // axle back to the spider.
        //
        // The tyre's a balloon: its sides bulge out to the hub and round over into the tread, so it looks soft and
        // blown up. Its rounded parts are outlined as seen, as the head's are; it and the caps are each convex, which
        // outlining is far quicker for (see OutlineData).
        public static MeshData BuildSpiderWheel(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float r = DroidBases.SpiderWheelRadius, h = DroidBases.SpiderWheelWidth / 2f;
            const float hub = r * 0.4f, cap = 0.004f;   // the hub caps' radius, and how far they stand out of the tyre
            var axle = Matrix.CreateRotationZ(-MathHelper.PiOver2);   // the lathe's axis, y, onto x
            Lathe(mesh, axle, Vector3.Zero, 16, new (float, float, bool)[]
            {
                (0f, -h, false),
                (hub, -h, false),
                (r * 0.62f, -h * 0.94f, false),   // the side bulging, then rounding over
                (r * 0.86f, -h * 0.76f, false),
                (r, -h * 0.32f, false),           // into the tread
                (r, h * 0.32f, false),
                (r * 0.86f, h * 0.76f, false),
                (r * 0.62f, h * 0.94f, false),
                (hub, h, false),
                (0f, h, false),
            }, new[] { Tyre, Tyre, Tyre, Tyre, Tyre, Tyre, Tyre, Tyre, Tyre });
            foreach (var side in new[] { -1f, 1f })
            {
                Lathe(mesh, axle, new Vector3(side * (h + cap / 2f), 0f, 0f), 10, new (float, float, bool)[]
                {
                    (0f, side * (h - 0.002f), false),
                    (hub, side * (h - 0.002f), false),
                    (hub, side * (h + cap), true),
                    (0f, side * (h + cap), false),
                }, new[] { Hub, Hub, Hub }, outlined: false);
                for (var k = 0; k < 3; k++)
                {
                    var angle = k * MathHelper.TwoPi / 3f;
                    var face = side * (h + cap);
                    mesh.AddLine(new Vector3(face, 0f, 0f), new Vector3(face, MathF.Cos(angle) * hub * 0.85f, MathF.Sin(angle) * hub * 0.85f));   // spokes across the hub, to see it turn
                }
            }
            var stub = DroidBases.SpiderWheelOut + 0.004f;
            mesh.AddTube(new Vector3(-stub, 0f, 0f), new Vector3(stub, 0f, 0f), 0.008f, 0.008f, 6, Metal + MeshBuilder.Dim, ringEdges: true);
            return mesh.Build(device);
        }

        // A wheel `radius` round turning about X, `half` its width either side: its rim in `rim`, its faces in `face`,
        // outlined as seen, and `spokes` spokes across each face, so it's seen to turn even in wireframe.
        private static void AddDisc(MeshBuilder mesh, float radius, float half, int rim, int face, int spokes)
        {
            Lathe(mesh, Matrix.CreateRotationZ(-MathHelper.PiOver2), Vector3.Zero, 14, new (float, float, bool)[]
            {
                (0f, -half, false),
                (radius, -half, true),
                (radius, half, true),
                (0f, half, false),
            }, new[] { face, rim, face });
            foreach (var x in new[] { -half, half })
            {
                var hub = new Vector3(x, 0f, 0f);
                for (var k = 0; k < spokes; k++)
                {
                    var angle = k * MathHelper.TwoPi / spokes;
                    mesh.AddLine(hub, hub + new Vector3(0f, MathF.Cos(angle), MathF.Sin(angle)) * (radius * 0.85f));
                }
            }
        }
    }
}
