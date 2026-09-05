using System.Text.Json.Serialization;

namespace OpenGL_lesson_CSharp
{
    /// <summary>
    /// JSON operation for the voxel scene DSL. Distances are expressed in millimetres;
    /// SceneBuilder converts them to voxel coordinates using scene.voxelsPerMm.
    /// Unused fields are ignored by each operation.
    /// </summary>
    public sealed class Operation
    {
        [JsonPropertyName("op")]
        public string Op { get; set; } = "addBox";

        // Generic bounds / box / TPMS / lattice region (mm).
        public double x0 { get; set; }
        public double y0 { get; set; }
        public double z0 { get; set; }
        public double x1 { get; set; }
        public double y1 { get; set; }
        public double z1 { get; set; }

        // Generic transform offset (mm).
        public double dx { get; set; }
        public double dy { get; set; }
        public double dz { get; set; }

        // Primitive centre (mm).
        public double cx { get; set; }
        public double cy { get; set; }
        public double cz { get; set; }

        // Sphere / cylinder / capsule.
        public double radius { get; set; }

        // Capsule segment endpoints (mm).
        public double ax { get; set; }
        public double ay { get; set; }
        public double az { get; set; }
        public double bx { get; set; }
        public double by { get; set; }
        public double bz { get; set; }

        // Torus (mm).
        public double majorRadius { get; set; }
        public double minorRadius { get; set; }

        // TPMS (mm): spatial period and approximate sheet thickness.
        public double period { get; set; }
        public double thickness { get; set; }

        // Truss lattice (mm).
        public double cell { get; set; }
        public double strut { get; set; }

        // Morphology / cleanup.
        public int iterations { get; set; } = 1;
        public int threshold { get; set; } = 14;
        public string neighborhood { get; set; } = "6";
    }
}
