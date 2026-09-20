
using Microsoft.Xna.Framework;

namespace BasicTests.Meshes
{
    internal class RawData
    {        
        // IMagine we imported this from a 3D model, but we can also just define it manually. The ingot is a frustum (truncated pyramid) shape.
        // 0-3: bottom (larger) rectangle. 4-7: top (smaller) rectangle, inset on both
        // axes so the sides slope inward.
        public static Vector3[] Basic_Ingot_Frustrum =  {
            new Vector3(-0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, 0.3f),
            new Vector3(-0.6f, -0.3f, 0.3f),
            new Vector3(-0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, 0.2f),
            new Vector3(-0.4f, 0.3f, 0.2f),
        };
    }
}
