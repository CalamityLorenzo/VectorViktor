using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using World.Buildings;
using World.Core.Animation;

namespace World.Rendering
{
    // A chest of drawers or a cupboard (see CabinetSpec), as the meshes of its rig's parts (see CabinetRig): the carcass,
    // open at the front, with a shelf across a cupboard; a drawer, a tray behind a front with a handle; and a door, from
    // its hinge along +X, with a handle up its far edge. Fronts stand just proud of the carcass, so they don't z-fight it.
    public static class CabinetMesh
    {
        public const int Wood = 0, Front = 3, Handle = 6;
        public const int PaletteSize = 9;

        private const float Proud = 0.02f;   // a front's thickness, all of it in front of the carcass

        public static Color[] Palette(Color wood)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Wood, wood);
            MeshBuilder.SetBoxShades(palette, Front, Color.Lerp(wood, Color.White, 0.15f));
            MeshBuilder.SetBoxShades(palette, Handle, new Color(210, 180, 60));
            return palette;
        }

        // Every part's mesh, by the part name its rig gives it.
        public static IReadOnlyDictionary<string, MeshSource> Sources(CabinetSpec spec)
        {
            var palette = Palette(spec.Color);
            spec = spec with { Position = Vector3.Zero, YawDegrees = 0f, Color = default };   // only its shape, as the keys hold (see MeshCache)
            var sources = new Dictionary<string, MeshSource>
            {
                [CabinetRig.CarcassPart(spec)] = new MeshSource(CabinetRig.CarcassPart(spec), d => BuildCarcass(d, spec), palette),
            };
            if (spec.Kind == CabinetKind.Drawers)
                sources[CabinetRig.DrawerPart(spec)] = new MeshSource(CabinetRig.DrawerPart(spec), d => BuildDrawer(d, spec), palette);
            else
                sources[CabinetRig.DoorPart(spec)] = new MeshSource(CabinetRig.DoorPart(spec), d => BuildDoor(d, spec), palette);
            return sources;
        }

        // Keyed by the part's name, which holds its sizes: two cabinets alike share one (see CabinetRig), but a colour of
        // its own is the palette's business, not the mesh's.
        public static MeshData BuildCarcass(GraphicsDevice device, CabinetSpec spec)
        {
            const float b = CabinetSpec.Board;
            var (w, d, h) = (spec.Width, spec.Depth, spec.Height);
            var mesh = new MeshBuilder();
            foreach (var side in new[] { -1f, 1f })
                mesh.AddBox(Wood, new Vector3(side * (w - b) / 2f, 0f, 0f), d, b, h);
            mesh.AddBox(Wood, new Vector3(0f, h - b, 0f), d, w - 2f * b, b);
            mesh.AddBox(Wood, new Vector3(0f, 0f, 0f), d, w - 2f * b, CabinetSpec.Plinth);
            mesh.AddBox(Wood, new Vector3(0f, CabinetSpec.Plinth, -(d - b) / 2f), b, w - 2f * b, h - b - CabinetSpec.Plinth);
            if (spec.Kind == CabinetKind.Cupboard)
                mesh.AddBox(Wood, new Vector3(0f, (spec.FrontBottom + spec.FrontTop) / 2f, b / 2f), d - b, w - 2f * b, b);
            return mesh.Build(device);
        }

        // From the middle of its front's bottom edge, which is flush with the carcass's front: a tray back into the
        // carcass behind a front, and a handle across it.
        public static MeshData BuildDrawer(GraphicsDevice device, CabinetSpec spec)
        {
            var (_, height) = spec.DrawerSpan(0);
            var width = spec.Width - 2f * CabinetSpec.Board - CabinetSpec.Gap;
            var inner = width - 0.02f;
            var depth = spec.Depth - CabinetSpec.Board - 0.03f;
            var sides = height * 0.7f;
            var mesh = new MeshBuilder();
            mesh.AddBox(Front, new Vector3(0f, 0f, Proud / 2f), Proud, width, height);
            mesh.AddBox(Handle, new Vector3(0f, height * 0.5f - 0.015f, Proud + 0.012f), 0.024f, MathHelper.Min(0.14f, width * 0.3f), 0.03f);
            mesh.AddBox(Wood, new Vector3(0f, 0.01f, -depth / 2f), depth, inner, 0.01f);   // its bottom
            foreach (var side in new[] { -1f, 1f })
                mesh.AddBox(Wood, new Vector3(side * (inner - 0.012f) / 2f, 0.02f, -depth / 2f), depth, 0.012f, sides);
            mesh.AddBox(Wood, new Vector3(0f, 0.02f, -depth + 0.006f), 0.012f, inner - 0.024f, sides);   // its back
            return mesh.Build(device);
        }

        // From its hinge, along +X and up.
        public static MeshData BuildDoor(GraphicsDevice device, CabinetSpec spec)
        {
            var width = CabinetRig.DoorWidth(spec);
            var height = spec.FrontTop - spec.FrontBottom;
            var mesh = new MeshBuilder();
            mesh.AddBox(Front, new Vector3(width / 2f, 0f, Proud / 2f), Proud, width, height);
            mesh.AddBox(Handle, new Vector3(width - 0.05f, height * 0.5f, Proud + 0.012f), 0.024f, 0.03f, 0.16f);
            return mesh.Build(device);
        }
    }

    // A cabinet as drawn: its rig (see CabinetRig), posed each frame from the drawers and doors as they are.
    public sealed class CabinetView
    {
        private readonly Cabinet _cabinet;
        private readonly Rig _rig;
        private readonly RigView _view;

        public CabinetView(Cabinet cabinet, GraphicsDevice device, MeshCache cache)
        {
            _cabinet = cabinet;
            _rig = CabinetRig.Build(cabinet.Spec);
            _view = new RigView(_rig, CabinetMesh.Sources(cabinet.Spec), null, device, cache);
        }

        public void Collect(MeshBatch batch)
        {
            _rig.Reset();
            CabinetRig.Follow(_rig, _cabinet);
            _rig.Solve(_cabinet.Placement);
            _view.Add(batch);
        }
    }
}
