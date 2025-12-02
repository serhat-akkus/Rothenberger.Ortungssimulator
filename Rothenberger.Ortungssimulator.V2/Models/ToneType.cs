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
    }
}