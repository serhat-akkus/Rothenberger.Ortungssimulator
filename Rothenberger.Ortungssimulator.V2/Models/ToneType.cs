using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Rothenberger.Ortungssimulator.V2.Models
{
    public sealed class ToneType
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        // Sinus
        public double? MinFrequency { get; set; }
        public double? MaxFrequency { get; set; }
        public string? LogBase { get; set; }
        public List<double>? Frequencies { get; set; }

        public double? CenterFrequency { get; set; }
        public double? Sigma { get; set; }
        public int? OctaveHalfSpan { get; set; }
        public double? MasterLevel { get; set; }
        public bool? Normalize { get; set; }
        public double? AudibleMin { get; set; }
        public double? AudibleMax { get; set; }

        // ShepardProvider2-spezifisch (bereits im Projekt)
        public double? SemitonesPerMeter { get; set; }
        public double? OctaveShiftFraction { get; set; }
    }
}