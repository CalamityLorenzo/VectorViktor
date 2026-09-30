using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MeshProps
{
    // A monorail's pieces, all from one palette (see Palette): its beam, straight or curved, that a train sits astride;
    // a platform; a through-truss bridge; a gate; and a buffer stop.
    //
    // Nothing here is drawn longer than about 10 m in one piece (a platform's boxes, a bridge's panels, a curve's beam
    // a metre at a time): fog is worked out at the corners of what's drawn and blended between them, so one long face
    // with its corners far off in the fog would hardly be fogged at all.
    public static class RailwayMesh
    {
        public const float BeamWidth = 0.7f, BeamHeight = 0.9f;

        public const int BeamBase = 0, PlatformBase = 3, Edge = 6, SteelBase = 7, GateBase = 10, BufferBase = 13, PaletteSize = 16;

        public static Color[] Palette(Color beam, Color platform, Color edge, Color steel, Color gate, Color buffer)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, BeamBase, beam);
            MeshBuilder.SetBoxShades(palette, PlatformBase, platform);
            palette[Edge] = edge;
            MeshBuilder.SetBoxShades(palette, SteelBase, steel);
            MeshBuilder.SetBoxShades(palette, GateBase, gate);
            MeshBuilder.SetBoxShades(palette, BufferBase, buffer);
            return palette;
        }

        // Where a piece of beam `length` long, turning through `bend` radians towards +X over its length (0 for a
        // straight), is `s` along it: from its start at the origin, heading +Z. And which way it's heading there, and
        // which way is across it, towards +X.
        public static (Vector2 at, Vector2 heading, Vector2 across) Along(float length, float bend, float s)
        {
            if (bend == 0f)
                return (new Vector2(0f, s), Vector2.UnitY, Vector2.UnitX);
            var radius = length / bend;   // negative, bending towards -X
            var angle = bend * s / length;
            var (sin, cos) = MathF.SinCos(angle);
            return (new Vector2(radius * (1f - cos), radius * sin), new Vector2(sin, cos), new Vector2(cos, -sin));
        }

        // A piece of beam, `length` along its middle line, turning through `bend` radians towards +X (see Along),
        // standing on the ground at y = 0: its top and two sides, outlined along their edges, and a joint across it
        // where it starts. Its ends are left open: the next piece carries on from each.
        public static MeshData Beam(GraphicsDevice device, float length = 10f, float bend = 0f)
        {
            var mesh = new MeshBuilder();
            // Curved, in pieces a metre or so long; straight, in one
            var pieces = bend == 0f ? 1 : Math.Max(1, (int)MathF.Ceiling(length));
            const float half = BeamWidth / 2f, top = BeamHeight;

            Vector3 At(float s, float across, float y)
            {
                var (at, _, side) = Along(length, bend, s);
                var p = at + side * across;
                return new Vector3(p.X, y, p.Y);
            }

            for (var k = 0; k < pieces; k++)
            {
                var s0 = length * k / pieces;
                var s1 = length * (k + 1) / pieces;
                mesh.AddQuad(BeamBase + MeshBuilder.Top, At(s0, -half, top), At(s1, -half, top), At(s1, half, top), At(s0, half, top));
                mesh.AddQuad(BeamBase + MeshBuilder.Side, At(s0, -half, 0f), At(s1, -half, 0f), At(s1, -half, top), At(s0, -half, top));
                mesh.AddQuad(BeamBase + MeshBuilder.Side, At(s0, half, 0f), At(s1, half, 0f), At(s1, half, top), At(s0, half, top));
                foreach (var across in new[] { -half, half })
                {
                    mesh.AddLine(At(s0, across, top), At(s1, across, top));
                    mesh.AddLine(At(s0, across, 0f), At(s1, across, 0f));
                }
            }
            // The joint with the piece before
            mesh.AddLine(At(0f, -half, 0f), At(0f, -half, top));
            mesh.AddLine(At(0f, -half, top), At(0f, half, top));
            mesh.AddLine(At(0f, half, top), At(0f, half, 0f));
            return mesh.Build(device);
        }

        // A platform `length` along Z, `width` across X and `height` tall, its foot's middle at the origin, in boxes no
        // more than 10 m long; a painted strip along its +X edge, the side the line's on.
        public static MeshData Platform(GraphicsDevice device, float length = 80f, float width = 11f, float height = 1.1f)
        {
            var mesh = new MeshBuilder();
            var boxes = Math.Max(1, (int)MathF.Ceiling(length / 10f));
            for (var k = 0; k < boxes; k++)
            {
                var z = -length / 2f + length * (k + 0.5f) / boxes;
                mesh.AddBox(PlatformBase, new Vector3(0f, 0f, z), length / boxes, width, height);
                float z0 = z - length / boxes / 2f, z1 = z + length / boxes / 2f, x0 = width / 2f - 0.5f, x1 = width / 2f, y = height + 0.01f;
                mesh.AddQuad(Edge, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0));
                mesh.AddLine(new Vector3(x0, y, z0), new Vector3(x0, y, z1));
            }
            return mesh.Build(device);
        }

        // A through-truss bridge `length` along Z, `width` across X between its two trusses and `height` from its deck to
        // the tops of them, in panels no more than 5 m long: its deck's top at y = 0, `deck` deep below that; along each
        // side a bottom and top chord, a post at every panel point and a diagonal across every panel, leaning in from
        // the ends to the middle; across the top, a beam at every panel point and a cross of bracing in every panel;
        // and a portal beam across each end, above the way in.
        public static MeshData Bridge(GraphicsDevice device, float length = 32f, float width = 6f, float height = 5.5f, float deck = 0.8f)
        {
            var mesh = new MeshBuilder();
            var panels = Math.Max(2, (int)MathF.Ceiling(length / 5f));
            var panel = length / panels;
            const float chord = 0.45f, member = 0.12f;
            float Z(int k) => -length / 2f + k * panel;

            for (var k = 0; k < panels; k++)
            {
                var middle = (Z(k) + Z(k + 1)) / 2f;
                mesh.AddBox(SteelBase, new Vector3(0f, -deck, middle), panel, width, deck, sealBottom: true);
                foreach (var side in new[] { -width / 2f, width / 2f })
                {
                    mesh.AddBox(SteelBase, new Vector3(side, 0f, middle), panel, chord, chord);
                    mesh.AddBox(SteelBase, new Vector3(side, height - chord, middle), panel, chord, chord);
                    // Leaning in towards the middle: from the bottom at the end nearer it, up to the top at the far one
                    var towardsMiddle = k < panels / 2;
                    var bottom = new Vector3(side, chord, towardsMiddle ? Z(k + 1) : Z(k));
                    var top = new Vector3(side, height - chord, towardsMiddle ? Z(k) : Z(k + 1));
                    mesh.AddTube(bottom, top, member, member, 4, SteelBase + MeshBuilder.Side);
                }
                // A cross of bracing under the top
                var y = height - chord / 2f;
                mesh.AddTube(new Vector3(-width / 2f, y, Z(k)), new Vector3(width / 2f, y, Z(k + 1)), member / 2f, member / 2f, 4, SteelBase + MeshBuilder.Side);
                mesh.AddTube(new Vector3(width / 2f, y, Z(k)), new Vector3(-width / 2f, y, Z(k + 1)), member / 2f, member / 2f, 4, SteelBase + MeshBuilder.Side);
            }
            for (var k = 0; k <= panels; k++)
            {
                foreach (var side in new[] { -width / 2f, width / 2f })
                    mesh.AddTube(new Vector3(side, chord, Z(k)), new Vector3(side, height - chord, Z(k)), member, member, 4, SteelBase + MeshBuilder.Side);
                mesh.AddTube(new Vector3(-width / 2f, height - chord / 2f, Z(k)), new Vector3(width / 2f, height - chord / 2f, Z(k)), member, member, 4,
                             SteelBase + MeshBuilder.Side);
            }
            // The portals: a deeper beam across each end, a little below the top
            foreach (var end in new[] { Z(0), Z(panels) })
                mesh.AddBox(SteelBase, new Vector3(0f, height - chord - 0.9f, end), chord, width, 0.6f);
            return mesh.Build(device);
        }

        // A gate across a way `width` wide and `height` tall, shut, over the beam: two leaves meeting in the middle,
        // each a frame of rails and stiles filled with upright bars, braced corner to corner, and cut away at the
        // bottom of the middle to clear the beam. Across X, in the plane z = 0, standing on y = 0.
        public static MeshData Gate(GraphicsDevice device, float width = 5.6f, float height = 2.4f)
        {
            var mesh = new MeshBuilder();
            const float frame = 0.1f, bar = 0.025f, gap = 0.14f;
            const float notch = BeamWidth / 2f + 0.05f, sill = BeamHeight + 0.02f;   // the cut-away over the beam: half as wide, and as high
            foreach (var side in new[] { -1f, 1f })
            {
                float inner = side * 0.01f, outer = side * width / 2f, cut = side * notch;
                var middle = (inner + outer) / 2f;
                var leaf = MathF.Abs(outer - inner);
                mesh.AddBox(GateBase, new Vector3((cut + outer) / 2f, 0.05f, 0f), frame, MathF.Abs(outer - cut), frame);   // bottom rail, up to the beam
                mesh.AddBox(GateBase, new Vector3(middle, sill, 0f), frame, leaf, frame);                                  // rail over the beam, and on across
                mesh.AddBox(GateBase, new Vector3(middle, (sill + height) / 2f, 0f), frame, leaf, frame);                  // middle rail
                mesh.AddBox(GateBase, new Vector3(middle, height - frame, 0f), frame, leaf, frame);                        // top rail
                mesh.AddBox(GateBase, new Vector3(outer - side * frame / 2f, 0.05f, 0f), frame, frame, height - 0.05f);   // hinge stile
                mesh.AddBox(GateBase, new Vector3(cut + side * frame / 2f, 0.05f, 0f), frame, frame, sill - 0.05f);       // stile beside the beam
                mesh.AddBox(GateBase, new Vector3(inner + side * frame / 2f, sill, 0f), frame, frame, height - sill);     // closing stile
                var bars = (int)(leaf / gap);
                for (var k = 1; k < bars; k++)
                {
                    var x = inner + side * k * leaf / bars;
                    var foot = MathF.Abs(x) < notch + frame ? sill + frame : 0.15f;   // over the beam, from the rail across it
                    mesh.AddTube(new Vector3(x, foot, 0f), new Vector3(x, height - frame, 0f), bar, bar, 4, GateBase + MeshBuilder.Side);
                }
                mesh.AddTube(new Vector3(outer, 0.15f, 0f), new Vector3(inner, height - frame, 0f), bar * 1.5f, bar * 1.5f, 4, GateBase + MeshBuilder.Side);
            }
            return mesh.Build(device);
        }

        // A buffer stop at the end of the line, facing +Z, the way a train comes at it: a block standing over the beam's
        // end, from BufferStopDepth behind it to its face at z = 0, BufferStopWidth across and BufferStopHeight tall,
        // with one buffer out from its face, head out, at the height of a train's.
        public const float BufferStopDepth = 0.8f, BufferStopWidth = 1.2f, BufferStopHeight = BeamHeight + 1.1f;

        public static MeshData BufferStop(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(SteelBase, new Vector3(0f, 0f, -BufferStopDepth / 2f), BufferStopDepth, BufferStopWidth, BufferStopHeight);
            const float buffer = BeamHeight + 0.55f;
            mesh.AddBox(BufferBase, new Vector3(0f, buffer - 0.3f, 0.02f), 0.04f, BufferStopWidth - 0.2f, 0.6f);        // the plate it's on
            mesh.AddTube(new Vector3(0f, buffer, 0.04f), new Vector3(0f, buffer, 0.5f), 0.14f, 0.24f, 8, BufferBase + MeshBuilder.Side,
                         ringEdges: true);                                                                              // the buffer, head out
            return mesh.Build(device);
        }
    }
}
