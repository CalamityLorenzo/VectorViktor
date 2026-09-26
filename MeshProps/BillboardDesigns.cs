using Microsoft.Xna.Framework;
using System;

namespace MeshProps
{
    // What a billboard advertises: a Key to tell its mesh from the others', and the art it paints on the face.
    // Add another design here and stand it anywhere with BillboardMesh.Source.
    public sealed record BillboardDesign(string Key, Action<BillboardFace> Paint)
    {
        // The Commodore logo on the left - the thick C, open to the right, and in its mouth the two flags,
        // blue over red, their ends cut away to a notch - and on the right a big C64 in blue block capitals
        public static readonly BillboardDesign Commodore64 = new BillboardDesign("commodore64", face =>
        {
            const float outer = 1.15f, inner = 0.62f, mouth = 40f;
            const int segments = 28;
            var centre = new Vector2(-BillboardMesh.Width / 2f + 1.6f, BillboardMesh.Height / 2f);
            var from = MathHelper.ToRadians(mouth);
            var to = MathHelper.ToRadians(360f - mouth);
            var ring = new Vector2[2][] { new Vector2[segments + 1], new Vector2[segments + 1] };
            for (var k = 0; k <= segments; k++)
            {
                var angle = MathHelper.Lerp(from, to, k / (float)segments);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                ring[0][k] = centre + inner * direction;
                ring[1][k] = centre + outer * direction;
            }
            face.Band(BillboardMesh.Blue, ring[0], ring[1]);

            const float flag = 0.36f, gap = 0.05f, reach = 1.3f, notch = 0.32f;
            face.Shape(BillboardMesh.Blue,
                centre + new Vector2(0f, gap), centre + new Vector2(reach - notch, gap),
                centre + new Vector2(reach, gap + flag), centre + new Vector2(0f, gap + flag));
            face.Shape(BillboardMesh.Red,
                centre + new Vector2(0f, -gap - flag), centre + new Vector2(reach, -gap - flag),
                centre + new Vector2(reach - notch, -gap), centre + new Vector2(0f, -gap));

            face.Write("C64", 0f, BillboardMesh.Height / 2f - 0.7f, 1.4f, BillboardMesh.Blue);
        });

        // The 1980s Atari symbol, the Fuji, over ATARI, all in red: a thin straight bar standing tall in the middle,
        // and on each side of it a thick arm, straight down from beside its top, then sweeping out to end level
        public static readonly BillboardDesign Atari = new BillboardDesign("atari", face =>
        {
            const float height = 2f, bar = 0.12f, gap = 0.13f, half = 0.12f, reach = 1.2f, size = 0.63f, between = 0.2f;
            const int segments = 14;
            var text = BillboardMesh.Height / 2f - (height + between + size) / 2f;   // the two together, centred on the board
            var bottom = text + size + between;
            var top = bottom + height;
            face.Shape(BillboardMesh.Red,
                new Vector2(-bar, bottom), new Vector2(bar, bottom), new Vector2(bar, top), new Vector2(-bar, top));

            // Each arm's middle line is a curve leaving its top straight down and reaching its end level: a thick
            // stroke of it, its two edges a constant `half` either side
            var tip = bottom + half;
            var p0 = new Vector2(bar + gap + half, top);
            var p1 = new Vector2(p0.X, top - 0.6f * (top - tip));
            var p2 = new Vector2(p0.X + 0.55f * (reach - p0.X), tip);
            var p3 = new Vector2(reach, tip);
            foreach (var side in new[] { -1f, 1f })
            {
                var inner = new Vector2[segments + 1];
                var outer = new Vector2[segments + 1];
                for (var k = 0; k <= segments; k++)
                {
                    var t = k / (float)segments;
                    var u = 1f - t;
                    var at = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                    var along = Vector2.Normalize(3f * u * u * (p1 - p0) + 6f * u * t * (p2 - p1) + 3f * t * t * (p3 - p2));
                    var across = half * new Vector2(-along.Y, along.X);   // out from the bar
                    inner[k] = new Vector2(side * (at - across).X, (at - across).Y);
                    outer[k] = new Vector2(side * (at + across).X, (at + across).Y);
                }
                face.Band(BillboardMesh.Red, inner, outer);
            }

            face.Write("ATARI", -1.8f * size, text, size, BillboardMesh.Red);
        });
    }
}
