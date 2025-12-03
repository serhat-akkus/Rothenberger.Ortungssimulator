using System.Collections.Generic;

namespace Rothenberger.Ortungssimulator.V2.Models
{
    public sealed class ToneType
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        // Bereich für kontinuierliche Sinus-Typen
        public double? MinFrequency { get; set; }
        public double? MaxFrequency { get; set; }
        public string? LogBase { get; set; }

        // Diskrete Tonleiter
        public List<double>? Frequencies { get; set; }

        // Shepard-spezifische Konfiguration
        public int? MMax { get; set; }                         // z.B. 12
        public int? NMax { get; set; }                         // z.B. 10
        public double? FMin { get; set; }                      // z.B. 55.0
        public double? Sigma { get; set; }                     // z.B. 0.8
        public double? SensitivitySemitonesPerSecond { get; set; } // z.B. 24.0
        public double? OctaveOffset { get; set; }
        public double? ShepardCyclesFromEdgeToProbe { get; set; }
    }
}