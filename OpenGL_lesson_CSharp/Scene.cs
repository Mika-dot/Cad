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
    public sealed class Scene
    {
        [JsonPropertyName("voxelsPerMm")]
        public double VoxelsPerMm { get; set; } = 1.0;

        [JsonPropertyName("operations")]
        public List<Operation> Operations { get; set; } = new List<Operation>();
    }
}
