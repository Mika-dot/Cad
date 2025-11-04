using BriefFiniteElementNet;
using BriefFiniteElementNet.Elements;
using BriefFiniteElementNet.Materials;
using BriefFiniteElementNet.Sections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGL_lesson_CSharp
{
    public sealed class Operation
    {
        // "addBox" | "subtractBox"
        [JsonPropertyName("op")]
        public string Op { get; set; } = "addBox";

        // Границы в мм: [x0,x1)×[y0,y1)×[z0,z1)
        public double x0 { get; set; }
        public double y0 { get; set; }
        public double z0 { get; set; }
        public double x1 { get; set; }
        public double y1 { get; set; }
        public double z1 { get; set; }

        // Необязательный перенос (мм), если захотите
        public double dx { get; set; } = 0;
        public double dy { get; set; } = 0;
        public double dz { get; set; } = 0;
    }
}
