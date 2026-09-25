using MeshCore.Library;
using MeshLoader;
using Microsoft.Xna.Framework;

namespace Basic.Models
{
    // A mesh on show that turns on its own, about the vertical (YawSpeed) and across (PitchSpeed).
    public class SpinningMeshInstance : MeshInstance
    {
        public float YawSpeed { get; set; }     // radians per second
        public float PitchSpeed { get; set; }   // radians per second

        public SpinningMeshInstance(MeshData meshData, Color[] palette) : base(meshData, palette)
        {
        }

        public void Spin(float dt)
        {
            Yaw += YawSpeed * dt;
            Pitch += PitchSpeed * dt;
        }
    }
}
