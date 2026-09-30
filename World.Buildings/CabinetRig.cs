using Microsoft.Xna.Framework;
using World.Core.Animation;

namespace World.Buildings
{
    // A cabinet (see Cabinet) as a rig, to draw it by: its carcass, with a drawer or a door hung from it for each of its
    // own, in the cabinet's own space (its front facing +Z). Follow sets them from the simulation each frame: a drawer slid
    // out as far as it is, eased so it starts and stops softly (AnimationPlan.md 8), a door turned about its hinge.
    //
    //   carcass ─┬ drawer-0 (the top one), drawer-1, ...     slide out along +Z
    //            └ door-0, door-1                            hinged at the front's left and right edges
    //
    // The parts' names give their meshes' sizes (see World.Rendering's CabinetMesh), so cabinets alike share them.
    public static class CabinetRig
    {
        public const string Carcass = "carcass";

        public static string Drawer(int i) => "drawer-" + i;
        public static string Door(int i) => "door-" + i;

        // The mesh names for a cabinet's parts: what they are, and their sizes.
        public static string CarcassPart(CabinetSpec spec) => $"cabinet-carcass:{spec.Kind}:{spec.Width:F2}x{spec.Depth:F2}x{spec.Height:F2}:{spec.Count}";
        public static string DrawerPart(CabinetSpec spec) => $"cabinet-drawer:{spec.Width:F2}x{spec.Depth:F2}x{spec.Height:F2}:{spec.Count}";
        public static string DoorPart(CabinetSpec spec) => $"cabinet-door:{DoorWidth(spec):F2}x{spec.FrontTop - spec.FrontBottom:F2}";

        public static float DoorWidth(CabinetSpec spec) => spec.Count == 1 ? spec.Width : spec.Width / 2f - CabinetSpec.Gap / 2f;

        public static Rig Build(CabinetSpec spec)
        {
            var rig = new Rig();
            rig.Add(Carcass, null, Pose.Identity, CarcassPart(spec));
            var front = spec.Depth / 2f;
            if (spec.Kind == CabinetKind.Drawers)
                for (var i = 0; i < spec.Count; i++)
                    rig.Add(Drawer(i), Carcass, Pose.At(new Vector3(0f, spec.DrawerSpan(i).bottom, front)), DrawerPart(spec));
            else
            {
                // Each door's mesh runs along its +X from the hinge; the right-hand one's turned to run back along -X
                var half = spec.Width / 2f;
                rig.Add(Door(0), Carcass, Pose.At(new Vector3(-half, spec.FrontBottom, front)), DoorPart(spec));
                if (spec.Count == 2)
                    rig.Add(Door(1), Carcass, Pose.At(new Vector3(half, spec.FrontBottom, front), Pose.Turn(Vector3.Up, MathHelper.Pi)), DoorPart(spec));
            }
            return rig;
        }

        // Its drawers out and its doors open as far as the cabinet's are now (the rig at rest first: see Rig.Reset).
        public static void Follow(Rig rig, Cabinet cabinet)
        {
            for (var i = 0; i < cabinet.Drawers.Count; i++)
            {
                var drawer = cabinet.Drawers[i];
                var out1 = Ease.InOut.Apply(drawer.Fraction) * drawer.Travel;
                rig.Change(Drawer(i), p => p with { Translation = p.Translation + Vector3.UnitZ * out1 });
            }
            for (var i = 0; i < cabinet.Leaves.Count; i++)
            {
                // From across the front towards its facing (+Z), which is a turn one way for the left door, the other for the right
                var angle = cabinet.Leaves[i].Angle;
                rig.Change(Door(i), p => p with { Rotation = Pose.Turn(Vector3.Up, i == 0 ? -angle : MathHelper.Pi + angle) });
            }
        }
    }
}
