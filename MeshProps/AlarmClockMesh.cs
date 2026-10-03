using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A digital alarm clock, the wedge kind: a case Width wide, its face leaning back a little, a dark display window in
    // it, and two buttons on top. Its front towards +Z, centred on X and Z, on y = 0.
    //
    // Its digits are a mesh of their own (Digits), in the same frame, on the display: so they can be shown and hidden,
    // to flash, as an unset clock's do.
    public static class AlarmClockMesh
    {
        public const int Case = 0, Display = 3, Button = 4, Digit = 7;
        public const int PaletteSize = 8;

        public const float Width = 0.17f, Depth = 0.08f, Height = 0.09f;
        private const float Lean = 0.25f;   // radians the face leans back from upright

        // The display window: its middle, up the face, and its size
        private const float DisplayUp = 0.045f, DisplayWidth = 0.13f, DisplayHeight = 0.05f;

        public static Color[] Palette(Color body, Color button, Color digit)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Case, body);
            palette[Display] = new Color(25, 20, 20);
            MeshBuilder.SetBoxShades(palette, Button, button);
            palette[Digit] = digit;
            return palette;
        }

        // From the face's own frame (x across, y up the face, z out of it) to the clock's
        private static Vector3 OnFace(float x, float y, float z) =>
            new Vector3(x, 0f, Depth / 2f) + Vector3.Transform(new Vector3(0f, y, z), Matrix.CreateRotationX(-Lean));

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            var (hw, hd) = (Width / 2f, Depth / 2f);

            // The case: a wedge, the face's top leaning back over the top
            var faceTop = OnFace(0f, Height / MathF.Cos(Lean), 0f).Z;
            var back = -hd;
            Vector3[] Side(float x) => new[] { new Vector3(x, 0f, hd), new Vector3(x, 0f, back), new Vector3(x, Height, back), new Vector3(x, Height, faceTop) };
            var (left, right) = (Side(-hw), Side(hw));
            mesh.AddPolygon(Case + MeshBuilder.Dim, left);
            mesh.AddPolygon(Case + MeshBuilder.Dim, right);
            for (var k = 0; k < 4; k++)
            {
                var j = (k + 1) % 4;
                mesh.AddQuad(k == 3 ? Case + MeshBuilder.Side : k == 2 ? Case + MeshBuilder.Top : Case + MeshBuilder.Side, left[k], left[j], right[j], right[k]);
                mesh.AddLine(left[k], left[j]);
                mesh.AddLine(right[k], right[j]);
                mesh.AddLine(left[k], right[k]);
            }

            // The display, just proud of the face
            const float proud = 0.002f;
            var (dw, dh) = (DisplayWidth / 2f, DisplayHeight / 2f);
            var display = new[] { OnFace(-dw, DisplayUp - dh, proud), OnFace(dw, DisplayUp - dh, proud), OnFace(dw, DisplayUp + dh, proud), OnFace(-dw, DisplayUp + dh, proud) };
            mesh.AddQuad(Display, display[0], display[1], display[2], display[3]);
            mesh.AddLineLoop(display);

            // Two buttons on top: snooze, and the alarm
            foreach (var x in new[] { -0.035f, 0.035f })
                mesh.AddBox(Button, new Vector3(x, Height, (back + faceTop) / 2f), 0.025f, 0.04f, 0.008f);
            return mesh.Build(device);
        }

        // Which of a digit's seven segments are lit: top, top right, bottom right, bottom, bottom left, top left, middle
        private static readonly string[] Segments =
        {
            "1111110", "0110000", "1101101", "1111001", "0110011", "1011011", "1011111", "1110000", "1111111", "1111011",
        };

        // `time` ("12:00", say) in seven-segment digits on the display, a colon between: each lit segment a bar, outlined
        public static MeshData Digits(GraphicsDevice device, string time)
        {
            var mesh = new MeshBuilder();
            const float digitWidth = 0.02f, digitHeight = 0.034f, bar = 0.004f, gap = 0.007f, colon = 0.01f, z = 0.004f;
            var total = 0f;
            foreach (var c in time)
                total += c == ':' ? colon + gap : digitWidth + gap;
            var x = -(total - gap) / 2f;
            var bottom = DisplayUp - digitHeight / 2f;

            void Bar(float x0, float y0, float x1, float y1)
            {
                var corners = new[] { OnFace(x0, y0, z), OnFace(x1, y0, z), OnFace(x1, y1, z), OnFace(x0, y1, z) };
                mesh.AddQuad(Digit, corners[0], corners[1], corners[2], corners[3]);
                mesh.AddLineLoop(corners);
            }

            foreach (var c in time)
            {
                if (c == ':')
                {
                    var middle = x + colon / 2f;
                    Bar(middle - bar / 2f, bottom + digitHeight * 0.25f, middle + bar / 2f, bottom + digitHeight * 0.25f + bar);
                    Bar(middle - bar / 2f, bottom + digitHeight * 0.7f, middle + bar / 2f, bottom + digitHeight * 0.7f + bar);
                    x += colon + gap;
                    continue;
                }
                var lit = Segments[c - '0'];
                var (l, r, b, m, t) = (x, x + digitWidth, bottom, bottom + digitHeight / 2f, bottom + digitHeight);
                if (lit[0] == '1') Bar(l + bar, t - bar, r - bar, t);
                if (lit[1] == '1') Bar(r - bar, m, r, t - bar);
                if (lit[2] == '1') Bar(r - bar, b + bar, r, m);
                if (lit[3] == '1') Bar(l + bar, b, r - bar, b + bar);
                if (lit[4] == '1') Bar(l, b + bar, l + bar, m);
                if (lit[5] == '1') Bar(l, m, l + bar, t - bar);
                if (lit[6] == '1') Bar(l + bar, m - bar / 2f, r - bar, m + bar / 2f);
                x += digitWidth + gap;
            }
            return mesh.Build(device);
        }
    }
}
